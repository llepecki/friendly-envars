using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace FriendlyEnvars.RepositoryVerifier;

internal readonly record struct PublicMember(string DeclaringType, string Kind, string Name)
{
    public override string ToString()
    {
        return Kind == "type" ? $"type {DeclaringType}" : $"{Kind} {DeclaringType}.{Name}";
    }
}

internal sealed class PublicApiReader : IDisposable
{
    private readonly PEReader _peReader;
    private readonly MetadataReader _metadata;

    private PublicApiReader(PEReader peReader, MetadataReader metadata)
    {
        _peReader = peReader;
        _metadata = metadata;
    }

    public static PublicApiReader Open(Stream stream, string description)
    {
        PEReader peReader;

        try
        {
            peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                peReader.Dispose();
                throw new VerificationException($"'{description}' is not a managed assembly.");
            }

            return new PublicApiReader(peReader, peReader.GetMetadataReader());
        }
        catch (BadImageFormatException ex)
        {
            throw new VerificationException($"'{description}' is not a readable PE image: {ex.GetType().FullName}.");
        }
    }

    public static PublicApiReader OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new VerificationException($"Assembly '{path}' does not exist.");
        }

        return Open(File.OpenRead(path), path);
    }

    public IEnumerable<PublicMember> EnumeratePublicMembers()
    {
        foreach (var handle in _metadata.TypeDefinitions)
        {
            var type = _metadata.GetTypeDefinition(handle);

            if (!IsVisibleType(type))
            {
                continue;
            }

            string typeName = GetFullTypeName(type);

            yield return new PublicMember(typeName, "type", typeName);

            foreach (var methodHandle in type.GetMethods())
            {
                var method = _metadata.GetMethodDefinition(methodHandle);

                if (IsVisibleMember(method.Attributes))
                {
                    yield return new PublicMember(typeName, "method", _metadata.GetString(method.Name));
                }
            }

            foreach (var propertyHandle in type.GetProperties())
            {
                var property = _metadata.GetPropertyDefinition(propertyHandle);
                var accessors = property.GetAccessors();

                if (IsVisibleAccessor(accessors.Getter) || IsVisibleAccessor(accessors.Setter))
                {
                    yield return new PublicMember(typeName, "property", _metadata.GetString(property.Name));
                }
            }

            foreach (var fieldHandle in type.GetFields())
            {
                var field = _metadata.GetFieldDefinition(fieldHandle);
                var visibility = field.Attributes & FieldAttributes.FieldAccessMask;

                if (visibility is FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem)
                {
                    yield return new PublicMember(typeName, "field", _metadata.GetString(field.Name));
                }
            }

            foreach (var eventHandle in type.GetEvents())
            {
                var declared = _metadata.GetEventDefinition(eventHandle);
                var accessors = declared.GetAccessors();

                if (IsVisibleAccessor(accessors.Adder) || IsVisibleAccessor(accessors.Remover))
                {
                    yield return new PublicMember(typeName, "event", _metadata.GetString(declared.Name));
                }
            }
        }
    }

    private bool IsVisibleAccessor(MethodDefinitionHandle handle)
    {
        return !handle.IsNil && IsVisibleMember(_metadata.GetMethodDefinition(handle).Attributes);
    }

    private static bool IsVisibleMember(MethodAttributes attributes)
    {
        var visibility = attributes & MethodAttributes.MemberAccessMask;
        return visibility is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem;
    }

    private bool IsVisibleType(TypeDefinition type)
    {
        var visibility = type.Attributes & TypeAttributes.VisibilityMask;

        if (visibility == TypeAttributes.Public)
        {
            return true;
        }

        if (visibility is not (TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem))
        {
            return false;
        }

        var declaringHandle = type.GetDeclaringType();
        return !declaringHandle.IsNil && IsVisibleType(_metadata.GetTypeDefinition(declaringHandle));
    }

    private string GetFullTypeName(TypeDefinition type)
    {
        string name = _metadata.GetString(type.Name);
        var declaringHandle = type.GetDeclaringType();

        if (!declaringHandle.IsNil)
        {
            return GetFullTypeName(_metadata.GetTypeDefinition(declaringHandle)) + "+" + name;
        }

        string ns = _metadata.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    public void Dispose()
    {
        _peReader.Dispose();
    }
}
