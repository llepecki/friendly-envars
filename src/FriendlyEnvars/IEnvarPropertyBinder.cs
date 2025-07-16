using System;
using System.Globalization;

namespace FriendlyEnvars;

public interface IEnvarPropertyBinder
{
    object? Convert(string value, Type targetType, CultureInfo culture);
}
