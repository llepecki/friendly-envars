using System;

namespace FriendlyEnvars.RepositoryVerifier;

/// <summary>
/// Signals a failed repository assertion or a missing prerequisite. The dispatcher turns this into a
/// nonzero exit code and a single-line diagnostic, so every gate failure is actionable.
/// </summary>
internal sealed class VerificationException : Exception
{
    public VerificationException(string message) : base(message)
    {
    }
}
