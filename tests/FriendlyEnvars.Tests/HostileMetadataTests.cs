using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace FriendlyEnvars.Tests;

// A malformed attribute blob must surface as the sanitized metadata failure, never as a raw
// reflection exception. The blob is emitted as raw bytes, which is the only way to produce
// metadata the decoder rejects from inside C#.
public class HostileMetadataTests
{
    private static Type EmitTypeWithGarbageEnvarBlob()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("HostileMetadata"), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("HostileMetadata");
        var type = module.DefineType(
            "HostileOptions", TypeAttributes.Public | TypeAttributes.Class);

        var field = type.DefineField("_value", typeof(string), FieldAttributes.Private);
        var property = type.DefineProperty("Value", PropertyAttributes.None, typeof(string), null);

        var getter = type.DefineMethod(
            "get_Value",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string), Type.EmptyTypes);
        var getterBody = getter.GetILGenerator();
        getterBody.Emit(OpCodes.Ldarg_0);
        getterBody.Emit(OpCodes.Ldfld, field);
        getterBody.Emit(OpCodes.Ret);
        property.SetGetMethod(getter);

        var setter = type.DefineMethod(
            "set_Value",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [typeof(string)]);
        var setterBody = setter.GetILGenerator();
        setterBody.Emit(OpCodes.Ldarg_0);
        setterBody.Emit(OpCodes.Ldarg_1);
        setterBody.Emit(OpCodes.Stfld, field);
        setterBody.Emit(OpCodes.Ret);
        property.SetSetMethod(setter);

        var envarConstructor = typeof(EnvarAttribute).GetConstructor([typeof(string)])!;

        // A valid encoded attribute starts with the 0x0001 prolog followed by a serialized string.
        // This truncated blob passes emit-time checks but fails custom-attribute decoding.
        property.SetCustomAttribute(envarConstructor, [0x01, 0x00, 0xFF]);

        return type.CreateType();
    }

    [Fact]
    public void AMalformedAttributeBlobSurfacesAsTheSanitizedMetadataFailure()
    {
        var hostileType = EmitTypeWithGarbageEnvarBlob();

        var services = new ServiceCollection();
        var builder = services.AddOptions<object>();

        // The public generic surface cannot name an emitted type, so the internal plan builder is
        // exercised directly, exactly as BindEnvars would drive it.
        var exception = Assert.Throws<EnvarsException>(() => BindingPlan.Build(
            hostileType, string.Empty, ProcessEnvironmentVariableReader.Instance, NullBindingPlanObserver.Instance));

        // The runtimes take different, equally sanitized doors: net10's decoder throws (metadata
        // failure with a recorded cause), net8's decodes the truncated blob to an invalid name
        // (invalid-attribute-name failure with no cause). The contract is the same either way.
        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal("Value", exception.PropertyName);
        Assert.Null(exception.InnerException);

        // The raw decoder message must not leak through any failure surface.
        var decoderException = Record.Exception(
            () => hostileType.GetProperty("Value")!.GetCustomAttributesData().ToList()
                .ForEach(static data => _ = data.ConstructorArguments));

        if (decoderException is not null)
        {
            foreach (string fragment in decoderException.Message.Split(' ').Where(static part => part.Length >= 8))
            {
                Assert.DoesNotContain(fragment, exception.Message, StringComparison.Ordinal);
            }
        }

        _ = builder;
    }

    [Fact]
    public void AFailureDuringMetadataInspectionIsSanitizedNotPropagatedRaw()
    {
        // Differential coverage for the wrapper itself: the blob test above happens to throw inside
        // the attribute walk, which was already protected. This failure originates between that walk
        // and the shape checks - the region the wrapper newly covers - so it fails against the
        // unwrapped implementation.
        var exception = Assert.Throws<EnvarsException>(() => BindingPlan.Build(
            typeof(PlainOptions), string.Empty, ProcessEnvironmentVariableReader.Instance, new ThrowingObserver()));

        Assert.Equal(EnvarFailureKind.InvalidProperty, exception.FailureKind);
        Assert.Equal(nameof(PlainOptions.Value), exception.PropertyName);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(ThrowingObserver.SecretMessage, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ThrowingObserver.SecretMessage, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(typeof(InvalidOperationException).FullName, exception.CauseType);
    }

    public sealed class PlainOptions
    {
        [Envar("HOSTILE_METADATA_VALUE")]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ThrowingObserver : IBindingPlanObserver
    {
        internal const string SecretMessage = "QQWWEEXX-observer-detail-that-must-not-leak";

        public void PlanBuildStarted()
        {
        }

        public void MetadataInspected(PropertyInfo property) => throw new InvalidOperationException(SecretMessage);
    }
}
