using System;

namespace FriendlyEnvars;

public sealed class EnvarsException : Exception
{
    public EnvarsException()
    {
    }

    public EnvarsException(string message) : base(message)
    {
    }

    public EnvarsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
