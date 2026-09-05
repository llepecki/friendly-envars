using System;

namespace FriendlyEnvars.RepositoryVerifier;

internal sealed class VerificationException : Exception
{
    public VerificationException(string message) : base(message)
    {
    }
}
