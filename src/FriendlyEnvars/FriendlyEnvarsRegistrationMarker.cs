using Microsoft.Extensions.DependencyInjection;
using System;

namespace FriendlyEnvars;

internal sealed class FriendlyEnvarsRegistrationMarker
{
    internal FriendlyEnvarsRegistrationMarker(Type optionsType, string optionsName)
    {
        OptionsType = optionsType;
        OptionsName = optionsName;
    }

    internal Type OptionsType { get; }

    internal string OptionsName { get; }

    internal static bool IsRegistered(IServiceCollection services, Type optionsType, string optionsName)
    {
        for (int i = 0; i < services.Count; i++)
        {
            if (services[i].ImplementationInstance is FriendlyEnvarsRegistrationMarker marker &&
                marker.OptionsType == optionsType &&
                string.Equals(marker.OptionsName, optionsName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
