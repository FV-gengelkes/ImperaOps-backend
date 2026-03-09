namespace ImperaOps.Application.Abstractions;

/// <summary>
/// Provides the identity of the currently authenticated user.
/// Implemented in the API layer via IHttpContextAccessor.
/// </summary>
public interface ICurrentUser
{
    long    Id            { get; }
    string  DisplayName   { get; }
    bool    IsSuperAdmin  { get; }

    /// <summary>Returns true if the user has access to the given client.</summary>
    bool HasClientAccess(long clientId);

    /// <summary>Returns the set of client IDs the user is authorized for.</summary>
    HashSet<long> AuthorizedClientIds();
}
