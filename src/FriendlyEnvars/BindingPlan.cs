using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace FriendlyEnvars;

/// <summary>
/// Observes plan construction so tests can prove that discovery happens exactly once per registration.
/// Internal, and never retained by the registered configurator.
/// </summary>
internal interface IBindingPlanObserver
{
    /// <summary>Called once, before any property is inspected.</summary>
    void PlanBuildStarted();

    /// <summary>Called once for each property selected for binding.</summary>
    void MetadataInspected(PropertyInfo property);
}

/// <summary>
/// The production observer. Does nothing, so the production path carries no instrumentation cost.
/// </summary>
internal sealed class NullBindingPlanObserver : IBindingPlanObserver
{
    internal static readonly NullBindingPlanObserver Instance = new();

    private NullBindingPlanObserver()
    {
    }

    public void PlanBuildStarted()
    {
    }

    public void MetadataInspected(PropertyInfo property)
    {
    }
}

/// <summary>
/// One decorated property, together with the environment value captured for it at registration time.
/// </summary>
internal sealed class BindingPlanEntry
{
    internal BindingPlanEntry(PropertyInfo property, string environmentVariableName, Type targetType, string? capturedValue)
    {
        Property = property;
        EnvironmentVariableName = environmentVariableName;
        TargetType = targetType;
        CapturedValue = capturedValue;
    }

    internal PropertyInfo Property { get; }

    internal string EnvironmentVariableName { get; }

    /// <summary>The property's declared type, including <see cref="Nullable{T}"/> rather than its underlying type.</summary>
    internal Type TargetType { get; }

    /// <summary>
    /// The raw string captured when the plan was built, or <see langword="null"/> when the variable was
    /// not set. An empty string is a captured value, not an absent one.
    /// </summary>
    internal string? CapturedValue { get; }
}

/// <summary>
/// An immutable snapshot of everything one FriendlyEnvars registration needs in order to produce options
/// instances: which properties to bind, and the exact environment strings that were present when
/// <c>BindEnvars</c> ran.
/// </summary>
/// <remarks>
/// The plan is built once per <c>(options type, options name)</c> registration and captured by that
/// registration's configurator. Nothing here is stored in a process-wide cache, and creating an options
/// instance never touches the process environment, so options are immune to environment mutation that
/// happens after registration.
/// </remarks>
internal sealed class BindingPlan
{
    private readonly BindingPlanEntry[] _entries;

    private BindingPlan(Type optionsType, string optionsName, BindingPlanEntry[] entries)
    {
        OptionsType = optionsType;
        OptionsName = optionsName;
        _entries = entries;
    }

    internal Type OptionsType { get; }

    internal string OptionsName { get; }

    /// <summary>
    /// Discovers the decorated properties of <paramref name="optionsType"/>, validates them, and captures
    /// one environment string per selected property.
    /// </summary>
    /// <remarks>
    /// Every property is validated before any environment variable is read, so a malformed options type
    /// fails without having touched the environment at all. Reads are then performed in discovery order and
    /// are fail-fast: a failing read leaves the remaining variables unread and the registration incomplete.
    /// </remarks>
    [StackTraceHidden]
    internal static BindingPlan Build(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type optionsType,
        string optionsName,
        IEnvironmentVariableReader environmentVariableReader,
        IBindingPlanObserver planObserver)
    {
        planObserver.PlanBuildStarted();

        PropertyInfo[] properties;

        try
        {
            // Static properties are deliberately included so that they can be rejected as bind targets
            // rather than silently mutating shared state. Non-public properties are outside the binding
            // surface and are not considered at all.
            properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        }
        catch (Exception ex)
        {
            throw EnvarsException.TypeDiscoveryFailure(optionsType, optionsName, EnvarsException.DescribeCause(ex));
        }

        var descriptors = new List<PropertyDescriptor>(properties.Length);

        foreach (var property in properties)
        {
            CustomAttributeData? attributeData;

            try
            {
                attributeData = FindEnvarAttributeData(property);
            }
            catch (Exception ex)
            {
                throw EnvarsException.PropertyMetadataFailure(
                    optionsType, optionsName, property.Name, SafePropertyType(property), EnvarsException.DescribeCause(ex));
            }

            if (attributeData is null)
            {
                continue;
            }

            planObserver.MetadataInspected(property);

            var targetType = property.PropertyType;
            string? environmentVariableName = DecodeEnvironmentVariableName(attributeData);

            // The name is validated first, so that the unsupported-shape failure below can carry a name
            // that is known to be usable rather than echoing malformed metadata.
            if (!EnvarAttribute.IsValidName(environmentVariableName))
            {
                throw EnvarsException.InvalidAttributeName(optionsType, optionsName, property.Name, targetType);
            }

            if (!IsSupportedBindTarget(property))
            {
                throw EnvarsException.InvalidPropertyShape(environmentVariableName, optionsType, optionsName, property.Name, targetType);
            }

            descriptors.Add(new PropertyDescriptor(property, environmentVariableName, targetType));
        }

        var entries = new BindingPlanEntry[descriptors.Count];

        for (int i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            string? capturedValue;

            try
            {
                capturedValue = environmentVariableReader.GetEnvironmentVariable(descriptor.EnvironmentVariableName);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the caller's control flow, not a binding failure.
                throw;
            }
            catch (Exception ex)
            {
                throw EnvarsException.EnvironmentReadFailure(
                    descriptor.EnvironmentVariableName,
                    optionsType,
                    optionsName,
                    descriptor.Property.Name,
                    descriptor.TargetType,
                    EnvarsException.DescribeCause(ex));
            }

            entries[i] = new BindingPlanEntry(descriptor.Property, descriptor.EnvironmentVariableName, descriptor.TargetType, capturedValue);
        }

        return new BindingPlan(optionsType, optionsName, entries);
    }

    /// <summary>
    /// Converts and assigns every captured value onto a freshly created options instance.
    /// </summary>
    /// <remarks>
    /// Runs entirely from the captured plan. The process environment is never consulted here, so every
    /// options instance produced by this registration sees the values that were present at registration.
    /// </remarks>
    [StackTraceHidden]
    internal void Apply(object instance, IEnvarPropertyBinder binder, CultureInfo culture)
    {
        foreach (var entry in _entries)
        {
            string? capturedValue = entry.CapturedValue;

            if (capturedValue is null)
            {
                continue;
            }

            object? convertedValue;

            // Conversion and assignment are caught separately so the reported failure kind says which of
            // the two went wrong. Every exception is sanitised, including EnvarsException raised by a
            // custom binder, because an unsanitised one would carry the value straight through. The sole
            // exception is cancellation, which is the caller's control flow rather than a binding failure
            // and is propagated as the very same object.
            try
            {
                convertedValue = binder.Convert(capturedValue, entry.TargetType, culture);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw EnvarsException.ConversionFailure(
                    entry.EnvironmentVariableName,
                    OptionsType,
                    OptionsName,
                    entry.Property.Name,
                    entry.TargetType,
                    culture.Name,
                    binder.GetType(),
                    EnvarsException.DescribeCause(ex));
            }

            try
            {
                entry.Property.SetValue(instance, convertedValue);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is OperationCanceledException cancellation)
            {
                // A setter's exception arrives wrapped by reflection, so cancellation has to be unwrapped
                // to be propagated unchanged. Rethrowing through ExceptionDispatchInfo keeps both the
                // original instance and its stack trace.
                ExceptionDispatchInfo.Capture(cancellation).Throw();
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw EnvarsException.AssignmentFailure(
                    entry.EnvironmentVariableName,
                    OptionsType,
                    OptionsName,
                    entry.Property.Name,
                    entry.TargetType,
                    EnvarsException.DescribeCause(ex));
            }
        }
    }

    /// <summary>
    /// Finds the <see cref="EnvarAttribute"/> on a property without constructing it.
    /// </summary>
    /// <remarks>
    /// Reading the attribute through <see cref="CustomAttributeData"/> means a malformed name reaches the
    /// binder as data to be reported, instead of blowing up inside the attribute constructor with an
    /// exception that says nothing about which property is at fault.
    /// <para>
    /// <see cref="MemberInfo.GetCustomAttributesData"/> does not walk the inheritance chain, so an
    /// override of a decorated virtual property is followed explicitly. Without this, decorating a base
    /// property and overriding it in a derived type would silently stop binding.
    /// </para>
    /// </remarks>
    private static CustomAttributeData? FindEnvarAttributeData(PropertyInfo property)
    {
        var current = property;

        while (current is not null)
        {
            foreach (var data in current.GetCustomAttributesData())
            {
                if (data.AttributeType == typeof(EnvarAttribute))
                {
                    return data;
                }
            }

            current = GetOverriddenProperty(current);
        }

        return null;
    }

    /// <summary>
    /// Returns the property this one overrides, one level up, or null when there is none.
    /// </summary>
    /// <remarks>
    /// The base chain is walked a level at a time rather than jumping to
    /// <see cref="MethodInfo.GetBaseDefinition"/>'s declaring type, because that returns the ROOT
    /// declaration and would skip every intermediate one. An attribute declared on a middle override
    /// would then be lost silently, which for a configuration library means the property quietly stops
    /// binding instead of failing.
    /// <para>
    /// The exact-signature lookup also disambiguates overloaded indexers, which the name-only overload
    /// rejects with <see cref="AmbiguousMatchException"/>.
    /// </para>
    /// </remarks>
    private static PropertyInfo? GetOverriddenProperty(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;

        if (accessor is null || !accessor.IsVirtual || accessor.GetBaseDefinition() == accessor)
        {
            return null;
        }

        var indexParameterTypes = Array.ConvertAll(property.GetIndexParameters(), static parameter => parameter.ParameterType);
        var baseType = property.DeclaringType?.BaseType;

        while (baseType is not null)
        {
            var candidate = baseType.GetProperty(
                property.Name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly,
                binder: null,
                property.PropertyType,
                indexParameterTypes,
                modifiers: null);

            if (candidate is not null)
            {
                return candidate;
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Decodes the attribute's single string constructor argument, or returns null when the metadata is
    /// not the shape this library wrote.
    /// </summary>
    private static string? DecodeEnvironmentVariableName(CustomAttributeData attributeData)
    {
        var arguments = attributeData.ConstructorArguments;

        if (arguments.Count != 1 || arguments[0].ArgumentType != typeof(string))
        {
            return null;
        }

        return arguments[0].Value as string;
    }

    /// <summary>
    /// A bind target must be a non-indexed property with a public instance set or init accessor.
    /// </summary>
    /// <remarks>
    /// Static properties are rejected rather than bound: assigning one would mutate process-wide state
    /// shared by every options instance, which is never what a per-registration snapshot means. An
    /// indexer has no single value to assign. A setter that is private, protected or internal is not part
    /// of the type's configuration surface, so binding through it would defeat the author's intent.
    /// An init accessor is a setter carrying a modreq, so it satisfies this rule exactly like an ordinary
    /// one.
    /// </remarks>
    private static bool IsSupportedBindTarget(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        return property.SetMethod is { IsPublic: true, IsStatic: false };
    }

    /// <summary>
    /// Reads a property's declared type without letting a reflection failure mask the failure being
    /// reported.
    /// </summary>
    private static Type? SafePropertyType(PropertyInfo property)
    {
        try
        {
            return property.PropertyType;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private readonly record struct PropertyDescriptor(PropertyInfo Property, string EnvironmentVariableName, Type TargetType);
}
