namespace OneClick.Domain.Security;

/// <summary>
/// Result of path guard validation.
/// </summary>
public sealed class GuardResult
{
    private GuardResult(bool allowed, string? reason = null)
    {
        Allowed = allowed;
        Reason = reason;
    }

    /// <summary>
    /// Whether the path operation is allowed.
    /// </summary>
    public bool Allowed { get; }

    /// <summary>
    /// Reason why the path was rejected (if not allowed).
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates an allowed result.
    /// </summary>
    public static GuardResult Allow() => new(true);

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    public static GuardResult Deny(string reason) => new(false, reason);

    /// <summary>
    /// Implicit conversion to boolean for easy checking.
    /// </summary>
    public static implicit operator bool(GuardResult result) => result.Allowed;
}