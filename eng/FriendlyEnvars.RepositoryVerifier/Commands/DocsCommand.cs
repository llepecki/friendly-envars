using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace FriendlyEnvars.RepositoryVerifier.Commands;

internal static class DocsCommand
{
    public static void Run(CommandLine commandLine)
    {
        string packagePath = commandLine.GetRequired("package");
        string assemblyEntry = NuGetPackage.NormalisePackagePath(commandLine.GetRequired("assembly-path"));
        string documentationEntry = NuGetPackage.NormalisePackagePath(commandLine.GetRequired("documentation-path"));
        commandLine.EnsureAllConsumed();

        using var package = NuGetPackage.Open(packagePath);

        byte[] assemblyBytes = package.ReadEntry(assemblyEntry);
        byte[] documentationBytes = package.ReadEntry(documentationEntry);

        var documented = ReadDocumentedMembers(documentationBytes, documentationEntry, out var emptySummaries);
        var required = ReadDocumentableMembers(assemblyBytes, assemblyEntry);

        var failures = new List<string>();

        foreach (var member in required)
        {
            if (!documented.Contains(member.DocumentationId))
            {
                failures.Add($"undocumented {member.Description}: no <member name=\"{member.DocumentationId}\"> entry");
            }
        }

        foreach (string id in emptySummaries.OrderBy(static value => value, StringComparer.Ordinal))
        {
            failures.Add($"<member name=\"{id}\"> has no usable summary");
        }

        if (failures.Count > 0)
        {
            throw new VerificationException(
                $"Documentation verification failed for '{packagePath}':{Environment.NewLine}  - " +
                string.Join($"{Environment.NewLine}  - ", failures));
        }

        if (required.Count == 0)
        {
            throw new VerificationException($"'{assemblyEntry}' exposes no public members; the assembly is wrong or empty.");
        }

        Console.WriteLine(
            $"docs OK: all {required.Count.ToString(CultureInfo.InvariantCulture)} externally visible member(s) of " +
            $"'{assemblyEntry}' have a documented entry in '{documentationEntry}'.");
    }

    private static HashSet<string> ReadDocumentedMembers(byte[] documentationBytes, string entryName, out List<string> emptySummaries)
    {
        XDocument document;

        using (var stream = new MemoryStream(documentationBytes))
        {
            try
            {
                document = XDocument.Load(stream);
            }
            catch (System.Xml.XmlException exception)
            {
                throw new VerificationException($"'{entryName}' is not well-formed XML: {exception.GetType().FullName}.");
            }
        }

        var members = document.Root?.Element("members");

        if (members is null)
        {
            throw new VerificationException($"'{entryName}' has no <members> element.");
        }

        var documented = new HashSet<string>(StringComparer.Ordinal);
        emptySummaries = [];

        foreach (var member in members.Elements("member"))
        {
            string? id = member.Attribute("name")?.Value;

            if (string.IsNullOrEmpty(id))
            {
                throw new VerificationException($"'{entryName}' contains a <member> with no name attribute.");
            }

            documented.Add(id);

            var summary = member.Element("summary");

            // An unresolved <inheritdoc/> provides no packaged text.
            bool hasUsableSummary = summary is not null && !string.IsNullOrWhiteSpace(summary.Value);

            if (!hasUsableSummary && member.Element("inheritdoc") is null)
            {
                emptySummaries.Add(id);
            }
            else if (member.Element("inheritdoc") is not null && !hasUsableSummary)
            {
                emptySummaries.Add(id);
            }
        }

        return documented;
    }

    private readonly record struct DocumentableMember(string DocumentationId, string Description);

    private static List<DocumentableMember> ReadDocumentableMembers(byte[] assemblyBytes, string entryName)
    {
        using var peReader = new PEReader(System.Collections.Immutable.ImmutableArray.Create(assemblyBytes));

        if (!peReader.HasMetadata)
        {
            throw new VerificationException($"'{entryName}' is not a managed assembly.");
        }

        var reader = peReader.GetMetadataReader();
        var provider = new DocumentationIdProvider(reader);
        var members = new List<DocumentableMember>();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);

            if (!IsVisible(reader, type) || IsCompilerGenerated(reader, type.GetCustomAttributes()))
            {
                continue;
            }

            string typeId = provider.GetTypeFromDefinition(reader, typeHandle, 0);
            int typeArity = type.GetGenericParameters().Count;
            string typeIdWithArity = typeArity == 0 ? typeId : typeId + "`" + typeArity.ToString(CultureInfo.InvariantCulture);

            members.Add(new DocumentableMember("T:" + typeIdWithArity, $"type {typeId}"));

            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);

                if (!IsVisible(method.Attributes) || IsCompilerGenerated(reader, method.GetCustomAttributes()))
                {
                    continue;
                }

                string name = reader.GetString(method.Name);

                if (IsAccessor(reader, type, methodHandle))
                {
                    continue;
                }

                var signature = method.DecodeSignature(provider, genericContext: null);
                string methodName = name == ".ctor" ? "#ctor" : name == ".cctor" ? "#cctor" : name;
                int methodArity = method.GetGenericParameters().Count;

                if (methodArity > 0)
                {
                    methodName += "``" + methodArity.ToString(CultureInfo.InvariantCulture);
                }

                string id = "M:" + typeIdWithArity + "." + methodName +
                    DocumentationIdProvider.FormatParameters(signature.ParameterTypes);

                members.Add(new DocumentableMember(id, $"method {typeId}.{name}"));
            }

            foreach (var propertyHandle in type.GetProperties())
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                var accessors = property.GetAccessors();

                if (!IsVisibleAccessor(reader, accessors.Getter) && !IsVisibleAccessor(reader, accessors.Setter))
                {
                    continue;
                }

                string name = reader.GetString(property.Name);
                members.Add(new DocumentableMember($"P:{typeIdWithArity}.{name}", $"property {typeId}.{name}"));
            }

            foreach (var fieldHandle in type.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                var visibility = field.Attributes & FieldAttributes.FieldAccessMask;

                if (visibility is not (FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem))
                {
                    continue;
                }

                if (IsCompilerGenerated(reader, field.GetCustomAttributes()))
                {
                    continue;
                }

                string name = reader.GetString(field.Name);

                if (name == "value__")
                {
                    continue;
                }

                members.Add(new DocumentableMember($"F:{typeIdWithArity}.{name}", $"field {typeId}.{name}"));
            }

            foreach (var eventHandle in type.GetEvents())
            {
                var declared = reader.GetEventDefinition(eventHandle);
                var accessors = declared.GetAccessors();

                if (!IsVisibleAccessor(reader, accessors.Adder) && !IsVisibleAccessor(reader, accessors.Remover))
                {
                    continue;
                }

                string name = reader.GetString(declared.Name);
                members.Add(new DocumentableMember($"E:{typeIdWithArity}.{name}", $"event {typeId}.{name}"));
            }
        }

        return members;
    }

    private static bool IsAccessor(MetadataReader reader, TypeDefinition type, MethodDefinitionHandle methodHandle)
    {
        foreach (var propertyHandle in type.GetProperties())
        {
            var accessors = reader.GetPropertyDefinition(propertyHandle).GetAccessors();

            if (accessors.Getter == methodHandle || accessors.Setter == methodHandle)
            {
                return true;
            }
        }

        foreach (var eventHandle in type.GetEvents())
        {
            var accessors = reader.GetEventDefinition(eventHandle).GetAccessors();

            if (accessors.Adder == methodHandle || accessors.Remover == methodHandle || accessors.Raiser == methodHandle)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompilerGenerated(MetadataReader reader, CustomAttributeHandleCollection attributes)
    {
        foreach (var handle in attributes)
        {
            var attribute = reader.GetCustomAttribute(handle);

            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

            if (constructor.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var attributeType = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);

            if (reader.GetString(attributeType.Name) == "CompilerGeneratedAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisibleAccessor(MetadataReader reader, MethodDefinitionHandle handle) =>
        !handle.IsNil && IsVisible(reader.GetMethodDefinition(handle).Attributes);

    private static bool IsVisible(MethodAttributes attributes)
    {
        var visibility = attributes & MethodAttributes.MemberAccessMask;
        return visibility is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem;
    }

    private static bool IsVisible(MetadataReader reader, TypeDefinition type)
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
        return !declaringHandle.IsNil && IsVisible(reader, reader.GetTypeDefinition(declaringHandle));
    }
}
