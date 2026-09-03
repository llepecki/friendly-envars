using Microsoft.Extensions.DependencyInjection;
using System;

namespace FriendlyEnvars;

/// <summary>
/// Records that FriendlyEnvars has already been registered for one
/// <c>(options type, options name)</c> pair.
/// </summary>
/// <remarks>
/// <para>
/// The marker lives in the service collection rather than in a process-wide table, so it is scoped to
/// exactly the container being built and cannot leak between containers, tests or assembly load
/// contexts.
/// </para>
/// <para>
/// It exists because a second <c>BindEnvars</c> for the same pair is always a mistake: each call takes
/// its own snapshot of the environment, and the resulting configurators would then run in registration
/// order with the later one silently overwriting the earlier. Reporting that at registration is far more
/// useful than debugging why one of two identical-looking registrations wins.
/// </para>
/// </remarks>
internal sealed class FriendlyEnvarsRegistrationMarker
{
    internal FriendlyEnvarsRegistrationMarker(Type optionsType, string optionsName)
    {
        OptionsType = optionsType;
        OptionsName = optionsName;
    }

    internal Type OptionsType { get; }

    /// <summary>The exact registration name, which is <see cref="string.Empty"/> for default options.</summary>
    internal string OptionsName { get; }

    /// <summary>
    /// True when the collection already carries a marker for this exact pair.
    /// </summary>
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
