namespace ImperaOps.Domain.Exceptions;

/// <summary>Base class for all domain-layer exceptions.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>Requested entity was not found or the caller lacks access.</summary>
public sealed class NotFoundException(string message = "Not found.")
    : DomainException(message);

/// <summary>Caller is authenticated but not authorized for this action.</summary>
public sealed class ForbiddenException(string message = "Forbidden.")
    : DomainException(message);

/// <summary>Action conflicts with the current state (e.g. duplicate, in-use).</summary>
public sealed class ConflictException(string message)
    : DomainException(message);

/// <summary>One or more input values are invalid.</summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}
