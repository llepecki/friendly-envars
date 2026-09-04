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
        IBindingPlanObserver planObserver)
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

            // Validate before using the name in diagnostics.
            if (!EnvarAttribute.IsValidName(environmentVariableName))
            {
                throw EnvarsException.InvalidAttributeName(optionsType, optionsName, property.Name, targetType);
            }

            if (!IsSupportedBindTarget(property))
            {
                throw EnvarsException.InvalidPropertyShape(environmentVariableName, optionsType, optionsName, property.Name, targetType);
            }

            DefaultEnvarPropertyBinder.PrecomputedConversion conversion;

            try
            {
                // Precompute before any environment read or service registration.
                conversion = DefaultEnvarPropertyBinder.PrecomputedConversion.Create(targetType, enumMetadataCache);
            }
            catch (Exception ex)
            {
                throw EnvarsException.PropertyMetadataFailure(
                    optionsType, optionsName, property.Name, targetType, EnvarsException.DescribeCause(ex));
            }

            descriptors.Add(new PropertyDescriptor(property, environmentVariableName, targetType, conversion));
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

    [StackTraceHidden]
    internal void Apply(object instance, IEnvarPropertyBinder binder, CultureInfo culture)
    {
        // Only the library's sealed binder may use its precomputed path.
        bool isDefaultBinder = binder is DefaultEnvarPropertyBinder;

        foreach (var entry in _entries)
        {
            string? capturedValue = entry.CapturedValue;

            if (capturedValue is null)
            {
                continue;
            }

            object? convertedValue;

            // Keep the failure stage exact and sanitize all non-cancellation exceptions.
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
