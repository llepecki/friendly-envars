using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text;

namespace FriendlyEnvars.RepositoryVerifier;

// Formats metadata signatures as C# XML documentation IDs.
internal sealed class DocumentationIdProvider : ISignatureTypeProvider<string, object?>
{
    private readonly MetadataReader _reader;

    public DocumentationIdProvider(MetadataReader reader)
    {
        _reader = reader;
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode switch
        {
            PrimitiveTypeCode.Void => "System.Void",
            PrimitiveTypeCode.Boolean => "System.Boolean",
            PrimitiveTypeCode.Char => "System.Char",
            PrimitiveTypeCode.SByte => "System.SByte",
            PrimitiveTypeCode.Byte => "System.Byte",
            PrimitiveTypeCode.Int16 => "System.Int16",
            PrimitiveTypeCode.UInt16 => "System.UInt16",
            PrimitiveTypeCode.Int32 => "System.Int32",
            PrimitiveTypeCode.UInt32 => "System.UInt32",
            PrimitiveTypeCode.Int64 => "System.Int64",
            PrimitiveTypeCode.UInt64 => "System.UInt64",
            PrimitiveTypeCode.Single => "System.Single",
            PrimitiveTypeCode.Double => "System.Double",
            PrimitiveTypeCode.String => "System.String",
            PrimitiveTypeCode.IntPtr => "System.IntPtr",
            PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
            PrimitiveTypeCode.Object => "System.Object",
            PrimitiveTypeCode.TypedReference => "System.TypedReference",
            _ => throw new VerificationException($"Unsupported primitive type code '{typeCode}' in a signature.")
        };
    }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var definition = reader.GetTypeDefinition(handle);
        string name = StripArity(reader.GetString(definition.Name));

        var declaringHandle = definition.GetDeclaringType();

        if (!declaringHandle.IsNil)
        {
            return GetTypeFromDefinition(reader, declaringHandle, rawTypeKind) + "." + name;
        }

        string ns = reader.GetString(definition.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var reference = reader.GetTypeReference(handle);
        string name = StripArity(reader.GetString(reference.Name));

        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var declaring = GetTypeFromReference(reader, (TypeReferenceHandle)reference.ResolutionScope, rawTypeKind);
            return declaring + "." + name;
        }

        string ns = reader.GetString(reference.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape)
    {
        if (shape.Rank == 1)
        {
            return elementType + "[]";
        }

        // Documentation IDs spell a multidimensional array as [0:,0:] with one entry per rank.
        return elementType + "[" + string.Join(",", Repeat("0:", shape.Rank)) + "]";
    }

    public string GetByReferenceType(string elementType) => elementType + "@";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        genericType + "{" + string.Join(",", typeArguments) + "}";

    public string GetGenericMethodParameter(object? genericContext, int index) =>
        "``" + index.ToString(CultureInfo.InvariantCulture);

    public string GetGenericTypeParameter(object? genericContext, int index) =>
        "`" + index.ToString(CultureInfo.InvariantCulture);

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;

    public string GetFunctionPointerType(MethodSignature<string> signature) => "System.IntPtr";

    public string GetTypeFromHandle(MetadataReader reader, EntityHandle handle) =>
        handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
            HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
            HandleKind.TypeSpecification => GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
            _ => throw new VerificationException($"Unsupported type handle kind '{handle.Kind}'.")
        };

    private static string StripArity(string name)
    {
        int tick = name.LastIndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static IEnumerable<string> Repeat(string value, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return value;
        }
    }

    public static string FormatParameters(IReadOnlyList<string> parameterTypes)
    {
        if (parameterTypes.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("(");

        for (int i = 0; i < parameterTypes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(parameterTypes[i]);
        }

        return builder.Append(')').ToString();
    }
}
