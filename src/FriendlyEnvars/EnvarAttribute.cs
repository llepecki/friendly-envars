using System;

namespace FriendlyEnvars;

[AttributeUsage(AttributeTargets.Property)]
public class EnvarAttribute : Attribute
{
    public string Name { get; }

    public EnvarAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }
}
