using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Xunit;

namespace FriendlyEnvars.Tests;

public class AssemblyUnloadTests
{
    public class UnloadableOptions
    {
        [Envar("M07_VALUE")]
        public string Value { get; set; } = "default";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Context, WeakReference OptionsType, string BoundValue) BindInCollectibleContext(int generation)
    {
        var context = new AssemblyLoadContext($"friendly-envars-unload-{generation}", isCollectible: true);

        string boundValue;
        WeakReference optionsTypeReference;

        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(AssemblyUnloadTests).Assembly.Location);
            var optionsType = assembly.GetType(typeof(UnloadableOptions).FullName!, throwOnError: true)!;

            // The loaded type is a different Type instance from the one this assembly compiled against.
            Assert.NotSame(typeof(UnloadableOptions), optionsType);

            var services = new ServiceCollection();

            var addOptions = typeof(OptionsServiceCollectionExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(OptionsServiceCollectionExtensions.AddOptions)
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1)
                .MakeGenericMethod(optionsType);

            object builder = addOptions.Invoke(null, [services])!;

            typeof(OptionsBuilderExtensions)
                .GetMethod(nameof(OptionsBuilderExtensions.BindEnvars), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(optionsType)
                .Invoke(null, [builder, null]);

            using (var provider = services.BuildServiceProvider())
            {
                object options = provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(optionsType));
                object value = options.GetType().GetProperty("Value")!.GetValue(options)!;

                boundValue = (string)value.GetType().GetProperty(nameof(UnloadableOptions.Value))!.GetValue(value)!;
            }

            optionsTypeReference = new WeakReference(optionsType);
        }
        finally
        {
            context.Unload();
        }

        return (new WeakReference(context), optionsTypeReference, boundValue);
    }

    private static bool CollectUntilDead(params WeakReference[] references)
    {
        // Unloading completes after references and the finalizer queue are cleared.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (references.All(static reference => !reference.IsAlive))
            {
                return true;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return references.All(static reference => !reference.IsAlive);
    }

    [Fact]
    public void ACollectibleContextIsReleasedAfterBinding()
    {
        Environment.SetEnvironmentVariable("M07_VALUE", "bound-in-context");

        try
        {
            var (context, optionsType, boundValue) = BindInCollectibleContext(0);

            // The registration really did bind, so the context was genuinely exercised.
            Assert.Equal("bound-in-context", boundValue);

            Assert.True(CollectUntilDead(context, optionsType),
                "the collectible load context was still alive after binding; something in the library is rooting it");
        }
        finally
        {
            Environment.SetEnvironmentVariable("M07_VALUE", null);
        }
    }

    [Fact]
    public void TenLoadAndUnloadGenerationsRetainNothing()
    {
        Environment.SetEnvironmentVariable("M07_VALUE", "bound-in-context");

        try
        {
            var contexts = new List<WeakReference>();
            var optionsTypes = new List<WeakReference>();

            for (int generation = 0; generation < 12; generation++)
            {
                var (context, optionsType, boundValue) = BindInCollectibleContext(generation);

                Assert.Equal("bound-in-context", boundValue);

                contexts.Add(context);
                optionsTypes.Add(optionsType);
            }

            var everything = contexts.Concat(optionsTypes).ToArray();

            Assert.True(CollectUntilDead(everything),
                $"{everything.Count(static reference => reference.IsAlive)} of {everything.Length} references " +
                "survived; a prior generation is being retained");
        }
        finally
        {
            Environment.SetEnvironmentVariable("M07_VALUE", null);
        }
    }

    [Fact]
    public void TheLibraryHoldsNoStaticCollectionOrReflectionHandle()
    {
        // Inspect binaries to catch static caches in generated or nested types.
        var offenders = new List<string>();

        foreach (var type in typeof(EnvarsException).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsLiteral)
                {
                    continue;
                }

                if (MentionsForbiddenType(field.FieldType))
                {
                    offenders.Add($"{type.FullName}.{field.Name} : {field.FieldType.FullName} (forbidden field type)");
                    continue;
                }

                object? value = field.GetValue(null);

                switch (value)
                {
                    case MemberInfo:
                        offenders.Add($"{type.FullName}.{field.Name} holds a {value.GetType().Name}");
                        break;

                    // A global collection is forbidden whether or not it currently has anything in it.
                    case IEnumerable and not string:
                        offenders.Add($"{type.FullName}.{field.Name} holds a collection ({value.GetType().Name})");
                        break;

                    default:
                        break;
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static bool MentionsForbiddenType(Type type)
    {
        if (type == typeof(ConditionalWeakTable<,>) || type == typeof(RuntimeTypeHandle) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ConditionalWeakTable<,>)))
        {
            return true;
        }

        // Delegate signatures may mention Type; only stored state can root it.
        return type.IsGenericType
            && !typeof(Delegate).IsAssignableFrom(type)
            && type.GetGenericArguments().Any(MentionsForbiddenType);
    }
}
