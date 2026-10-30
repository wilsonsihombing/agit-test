namespace AccessRequestHub.Api.Exceptions;

public abstract class DomainException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    protected DomainException(string message, int statusCode, string code) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

public class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message, 400, "VALIDATION_ERROR") { }
}

public class DomainUnauthorizedException : DomainException
{
    public DomainUnauthorizedException(string message = "User not authenticated") : base(message, 401, "UNAUTHORIZED") { }
}

public class DomainForbiddenException : DomainException
{
    public DomainForbiddenException(string message = "Permission denied to perform this action") : base(message, 403, "FORBIDDEN") { }
}

public class DomainNotFoundException : DomainException
{
    public DomainNotFoundException(string message = "Resource not found") : base(message, 404, "NOT_FOUND") { }
}

public class DomainConflictException : DomainException
{
    public DomainConflictException(string message = "Resource conflict or concurrent modification detected") : base(message, 409, "CONFLICT") { }
}
