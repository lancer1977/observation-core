using System.Reflection;
using System.Xml.Linq;
using Observation.Core;
using Xunit;

namespace Observation.Core.Tests.Architecture;

public sealed class ArchitectureGuardrailTests
{
    private static readonly string[] ForbiddenTerms =
    [
        "game", "emulator", "ocr", "vlm", "opencv", "ffmpeg", "hdhomerun", "device", "stream"
    ];

    private static readonly string[] AllowedCoreAssemblyReferences =
    [
        "System.Collections",
        "System.Linq",
        "System.Runtime",
        "System.Text.Json",
        "System.Threading"
    ];

    private static readonly string[] AllowedPackageReferences = [];
    private static readonly string[] AllowedProjectReferences = [];

    [Fact]
    public void CoreAssemblyReferencesOnlyTheDocumentedBclAllowlist()
    {
        var references = typeof(ObservationScope).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!).Order().ToArray();

        Assert.Equal(AllowedCoreAssemblyReferences.Order(), references);
    }

    [Fact]
    public void CoreProjectReferencesOnlyTheDocumentedDependencyAllowlist()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Observation.Core", "Observation.Core.csproj"));
        var packageReferences = project.Descendants().Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? "")
            .Order().ToArray();
        var projectReferences = project.Descendants().Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? "")
            .Order().ToArray();

        Assert.Equal(AllowedPackageReferences.Order(), packageReferences);
        Assert.Equal(AllowedProjectReferences.Order(), projectReferences);
    }

    [Fact]
    public void CoreSourceContainsNoForbiddenDomainTerms()
    {
        var findings = SourceFiles()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, lineNumber: index + 1)))
            .Where(item => ForbiddenTerms.Any(term => item.line.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(item => $"{Path.GetRelativePath(RepositoryRoot(), item.file)}:{item.lineNumber}: {item.line.Trim()}")
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void PublicApiNamesContainNoForbiddenDomainTerms()
    {
        var names = typeof(ObservationScope).Assembly.GetExportedTypes()
            .SelectMany(type => new[] { type.Name }.Concat(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(member => member.Name)))
            .ToArray();

        Assert.DoesNotContain(names, name => ForbiddenTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void PublicApiDoesNotReferenceNetworkOrFileIoNamespaces()
    {
        var assembly = typeof(ObservationScope).Assembly;
        var forbiddenReferences = assembly.GetReferencedAssemblies()
            .Where(reference => IsForbiddenNamespace(reference.Name))
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.Empty(forbiddenReferences);

        var publicApiTypes = assembly.GetExportedTypes().SelectMany(PublicApiTypes).ToArray();
        Assert.DoesNotContain(publicApiTypes, type => IsForbiddenNamespace(type.Namespace) || IsForbiddenNamespace(type.FullName));
    }

    private static IEnumerable<Type> PublicApiTypes(Type type)
    {
        yield return type;

        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            switch (member)
            {
                case FieldInfo field:
                    foreach (var referencedType in ReferencedTypes(field.FieldType)) yield return referencedType;
                    break;
                case PropertyInfo property:
                    foreach (var referencedType in ReferencedTypes(property.PropertyType)) yield return referencedType;
                    break;
                case EventInfo @event when @event.EventHandlerType is not null:
                    foreach (var referencedType in ReferencedTypes(@event.EventHandlerType)) yield return referencedType;
                    break;
                case MethodBase method:
                    foreach (var referencedType in ReferencedTypes(method is MethodInfo info ? info.ReturnType : typeof(void))) yield return referencedType;
                    foreach (var parameter in method.GetParameters())
                    {
                        foreach (var referencedType in ReferencedTypes(parameter.ParameterType)) yield return referencedType;
                    }
                    break;
            }
        }
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        yield return type;
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            foreach (var referencedType in ReferencedTypes(type.GetElementType()!)) yield return referencedType;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var referencedType in ReferencedTypes(argument)) yield return referencedType;
            }
        }
    }

    private static bool IsForbiddenNamespace(string? name) =>
        name is not null && (name.StartsWith("System.Net", StringComparison.Ordinal) || name.StartsWith("System.IO", StringComparison.Ordinal));

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Observation.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(file => file.Split(Path.DirectorySeparatorChar).All(part => part is not "bin" and not "obj"));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ObservationCore.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
