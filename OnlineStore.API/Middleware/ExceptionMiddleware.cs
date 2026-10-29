using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Exceptions;

namespace OnlineStore.API.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) throw;
            var sql = exception as SqlException ?? exception.InnerException as SqlException;
            var (status, detail) = exception switch
            {
                StoreException store => (store.StatusCode, store.Message),
                DbUpdateConcurrencyException => (409, "The record changed. Reload it and retry."),
                _ when sql?.Number is 2601 or 2627 => (409, "A unique value already exists."),
                _ when sql?.Number == 547 => (409, "The operation conflicts with related records or database constraints."),
                _ when sql?.Number is 1205 or 50011 => (409, "The store is busy. Retry the request."),
                _ => (500, "An unexpected error occurred. Please try again later.")
            };
            if (status >= 500) logger.LogError(exception, "Request {TraceId} failed", context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = status;
            var problem = new ProblemDetails
            {
                Status = status,
                Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
                Detail = detail,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
