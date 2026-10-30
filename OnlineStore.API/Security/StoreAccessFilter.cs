using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OnlineStore.API.Security;

public class StoreAccessFilter(IWebHostEnvironment environment) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Existing isolated university API tests run in Testing; production never bypasses access checks.
        if (environment.IsEnvironment("Testing")) return Task.CompletedTask;
        var request = context.HttpContext.Request;
        var controller = ((ControllerActionDescriptor)context.ActionDescriptor).ControllerName;
        if (controller == "CatalogImport" && environment.IsProduction())
        { context.Result = new NotFoundResult(); return Task.CompletedTask; }
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any()) return Task.CompletedTask;
        if (HttpMethods.IsGet(request.Method) && controller is "Products" or "Categories" or "ExchangeRates") return Task.CompletedTask;
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            context.Result = new ObjectResult(new ProblemDetails { Status = 401, Title = "Sign in required", Detail = "Administrator access is required for this operation." }) { StatusCode = 401 };
        else if (!HttpMethods.IsGet(request.Method) && request.Headers["X-Store-Request"] != "1")
            context.Result = new ObjectResult(new ProblemDetails { Status = 403, Title = "Request rejected", Detail = "The request verification header is missing." }) { StatusCode = 403 };
        return Task.CompletedTask;
    }
}
