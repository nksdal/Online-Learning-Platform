using System.Xml.Linq;

namespace OnlineLearningPlatform.UnitTests.Architecture;

/// <summary>
/// Guards the "dependencies point inward" rule by reading each project's .csproj. If someone adds
/// a forbidden ProjectReference (say Application -> Infrastructure), this fails before it spreads.
/// </summary>
public class LayerDependencyTests
{
    private const string Prefix = "OnlineLearningPlatform.";

    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { Prefix + "Domain", [] },
        { Prefix + "Application", [Prefix + "Domain"] },
        { Prefix + "Infrastructure", [Prefix + "Application", Prefix + "Domain"] },
        { Prefix + "Api", [Prefix + "Application", Prefix + "Infrastructure"] },
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void Project_references_only_inward_layers(string project, string[] allowed)
    {
        var actual = ReadProjectReferences(project);

        var forbidden = actual.Except(allowed).ToList();

        Assert.True(forbidden.Count == 0, $"{project} must not reference: {string.Join(", ", forbidden)}");
    }

    private static List<string> ReadProjectReferences(string project)
    {
        var csproj = Path.Combine(FindRepositoryRoot(), "src", project, project + ".csproj");

        return XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(reference => ((string?)reference.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (no global.json found).");
    }
}
