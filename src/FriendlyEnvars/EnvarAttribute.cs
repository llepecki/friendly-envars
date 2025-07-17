using System;

namespace FriendlyEnvars;

[AttributeUsage(AttributeTargets.Property)]
public sealed class EnvarAttribute : Attribute
{
    public string Name { get; }

    public EnvarAttribute(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name can't be null or empty", nameof(name));
        }

        Name = name;
    }
}
