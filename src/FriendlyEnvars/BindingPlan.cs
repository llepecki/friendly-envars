using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace FriendlyEnvars;

internal interface IBindingPlanObserver
{
    void PlanBuildStarted();

    void MetadataInspected(PropertyInfo property);
}

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

internal sealed class BindingPlanEntry
{
    internal BindingPlanEntry(
        PropertyInfo property,
        string environmentVariableName,
        Type targetType,
        DefaultEnvarPropertyBinder.PrecomputedConversion conversion,
        string? capturedValue)
    {
        Property = property;
        EnvironmentVariableName = environmentVariableName;
        TargetType = targetType;
        Conversion = conversion;
        CapturedValue = capturedValue;
    }

    internal PropertyInfo Property { get; }

    internal string EnvironmentVariableName { get; }

    internal Type TargetType { get; }

    internal DefaultEnvarPropertyBinder.PrecomputedConversion Conversion { get; }

    internal string? CapturedValue { get; }
}

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

    [StackTraceHidden]
    internal static BindingPlan Build(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type optionsType,
        string optionsName,
        IEnvironmentVariableReader environmentVariableReader,
        IBindingPlanObserver planObserver,
        string? namePrefix = null)
    {
        planObserver.PlanBuildStarted();

        PropertyInfo[] properties;

        try
        {
            // Include public statics so invalid mappings fail instead of changing shared state.
            properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        }
        catch (Exception ex)
        {
            throw EnvarsException.TypeDiscoveryFailure(optionsType, optionsName, EnvarsException.DescribeCause(ex));
        }

        var descriptors = new List<PropertyDescriptor>(properties.Length);
        var enumMetadataCache = new Dictionary<Type, DefaultEnvarPropertyBinder.EnumText.Metadata>();

        foreach (var property in properties)
        {
            // Malformed or hostile metadata can throw anywhere in the inspection; nothing raw may
            // escape the sanitized failure contract. Deliberate failures pass through unchanged.
            try
            {
                CustomAttributeData? attributeData = FindEnvarAttributeData(property);

                if (attributeData is null)
                {
                    continue;
                }

                planObserver.MetadataInspected(property);

                var targetType = property.PropertyType;
                string? environmentVariableName = DecodeEnvironmentVariableName(attributeData);

                if (environmentVariableName is not null && namePrefix is not null)
                {
                    environmentVariableName = namePrefix + environmentVariableName;
                }

                // Validate before using the name in diagnostics.
                if (!EnvarAttribute.IsValidName(environmentVariableName))
                {
                    throw EnvarsException.InvalidAttributeName(optionsType, optionsName, property.Name, targetType);
                }

                if (!IsSupportedBindTarget(property))
                {
                    throw EnvarsException.InvalidPropertyShape(environmentVariableName, optionsType, optionsName, property.Name, targetType);
                }

                // Precompute before any environment read or service registration.
                var conversion = DefaultEnvarPropertyBinder.PrecomputedConversion.Create(targetType, enumMetadataCache);

                descriptors.Add(new PropertyDescriptor(property, environmentVariableName, targetType, conversion));
            }
            catch (EnvarsException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw EnvarsException.PropertyMetadataFailure(
                    optionsType, optionsName, property.Name, SafePropertyType(property), EnvarsException.DescribeCause(ex));
            }
        }

        RejectUnreachableAttributes(optionsType, optionsName, properties, descriptors);

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

            entries[i] = new BindingPlanEntry(
                descriptor.Property, descriptor.EnvironmentVariableName, descriptor.TargetType, descriptor.Conversion, capturedValue);
        }

        return new BindingPlan(optionsType, optionsName, entries);
    }

    /// <summary>
    /// Converts every captured value once and discards the results, so an unconvertible value fails
    /// at registration instead of at the first options resolution. Assignment still runs per creation,
    /// so setter failures surface there.
    /// </summary>
    [StackTraceHidden]
    internal void DryRunConversions(IEnvarPropertyBinder binder, CultureInfo culture)
    {
        bool isDefaultBinder = binder is DefaultEnvarPropertyBinder;

        foreach (var entry in _entries)
        {
            if (entry.CapturedValue is not { } capturedValue)
            {
                continue;
            }

            try
            {
                _ = isDefaultBinder
                    ? DefaultEnvarPropertyBinder.ConvertPrecomputed(capturedValue, entry.Conversion, culture)
                    : binder.Convert(capturedValue, entry.TargetType, culture);
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
                    EnvarsException.DescribeCulture(culture),
                    binder.GetType(),
                    EnvarsException.DescribeCause(ex));
            }
        }
    }

    [StackTraceHidden]
    internal void Apply(object instance, IEnvarPropertyBinder binder, CultureInfo culture)
    {
        bool isDefaultBinder = binder is DefaultEnvarPropertyBinder;

        foreach (var entry in _entries)
        {
            string? capturedValue = entry.CapturedValue;

            if (capturedValue is null)
            {
                continue;
            }

            object? convertedValue;

            try
            {
                convertedValue = isDefaultBinder
                    ? DefaultEnvarPropertyBinder.ConvertPrecomputed(capturedValue, entry.Conversion, culture)
                    : binder.Convert(capturedValue, entry.TargetType, culture);
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
                    EnvarsException.DescribeCulture(culture),
                    binder.GetType(),
                    EnvarsException.DescribeCause(ex));
            }

            try
            {
                entry.Property.SetValue(instance, convertedValue);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is OperationCanceledException cancellation)
            {
                // Reflection wraps setter failures; preserve the original cancellation and stack.
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

    // Read metadata without invoking the attribute constructor, and follow overridden properties.
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

    // Walk one level at a time so attributes on intermediate overrides are not skipped.
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The walk revisits declarations of a property the DynamicallyAccessedMembers annotation on the options type already preserves; covered by the trim smoke test's inheritance case.")]
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
    /// Fails when a base type declares [Envar] on a property the discovered surface cannot reach: one
    /// hidden by a non-override redeclaration, or a static on a base type. Both would otherwise bind
    /// nothing, silently, in a library that rejects every other suspect shape loudly.
    /// </summary>
    private static void RejectUnreachableAttributes(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type optionsType,
        string optionsName,
        PropertyInfo[] discoveredProperties,
        List<PropertyDescriptor> descriptors)
    {
        // PropertyInfo identity varies with ReflectedType, so declarations are keyed by their
        // metadata token: an inherited property discovered through the derived type and the same
        // declaration seen on its base compare equal.
        var reachable = new HashSet<(Module Module, int MetadataToken)>();

        foreach (var property in discoveredProperties)
        {
            reachable.Add((property.Module, property.MetadataToken));
        }

        foreach (var descriptor in descriptors)
        {
            try
            {
                var current = GetOverriddenProperty(descriptor.Property);

                while (current is not null)
                {
                    reachable.Add((current.Module, current.MetadataToken));
                    current = GetOverriddenProperty(current);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw EnvarsException.PropertyMetadataFailure(
                    optionsType, optionsName, descriptor.Property.Name, descriptor.TargetType, EnvarsException.DescribeCause(ex));
            }
        }

        for (var baseType = optionsType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            PropertyInfo[] baseProperties;

            try
            {
                baseProperties = GetDeclaredPublicProperties(baseType);
            }
            catch (Exception ex)
            {
                throw EnvarsException.TypeDiscoveryFailure(optionsType, optionsName, EnvarsException.DescribeCause(ex));
            }

            foreach (var property in baseProperties)
            {
                // Hostile metadata on a base type must stay behind the sanitized contract exactly as
                // it does in the discovery loop above.
                bool unreachable;

                try
                {
                    unreachable = !reachable.Contains((property.Module, property.MetadataToken))
                        && FindEnvarAttributeData(property) is not null;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw EnvarsException.PropertyMetadataFailure(
                        optionsType, optionsName, property.Name, SafePropertyType(property), EnvarsException.DescribeCause(ex));
                }

                if (unreachable)
                {
                    throw EnvarsException.UnreachableProperty(
                        optionsType,
                        optionsName,
                        $"{baseType.Name}.{property.Name}",
                        SafePropertyType(property));
                }
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Base declarations of properties the DynamicallyAccessedMembers annotation on the options type preserves; covered by the trim smoke test's inheritance case.")]
    private static PropertyInfo[] GetDeclaredPublicProperties(Type type)
    {
        return type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
    }

    private static string? DecodeEnvironmentVariableName(CustomAttributeData attributeData)
    {
        var arguments = attributeData.ConstructorArguments;

        if (arguments.Count != 1 || arguments[0].ArgumentType != typeof(string))
        {
            return null;
        }

        return arguments[0].Value as string;
    }

    private static bool IsSupportedBindTarget(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        return property.SetMethod is { IsPublic: true, IsStatic: false };
    }

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

    private readonly record struct PropertyDescriptor(
        PropertyInfo Property,
        string EnvironmentVariableName,
        Type TargetType,
        DefaultEnvarPropertyBinder.PrecomputedConversion Conversion);
}
