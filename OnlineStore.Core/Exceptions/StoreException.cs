namespace OnlineStore.Core.Exceptions;

public class StoreException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class NotFoundException(string resource, int id)
    : StoreException($"{resource} with ID {id} was not found.", 404);

public class ConflictException(string message) : StoreException(message, 409);
