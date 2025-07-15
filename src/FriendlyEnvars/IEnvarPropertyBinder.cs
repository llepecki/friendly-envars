using System;

namespace FriendlyEnvars;

public interface IEnvarPropertyBinder
{
    object? Convert(string value, Type targetType);
}
