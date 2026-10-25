using System.Xml.Linq;
using FluentAssertions;

namespace StackTrack.Api.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_has_no_package_and_no_project_references()
    {
        var domain = Load("StackTrack.Domain");

        domain.Descendants("PackageReference").Should().BeEmpty();
        domain.Descendants("ProjectReference").Should().BeEmpty();
    }

    [Fact]
    public void Application_references_only_Domain() =>
        ProjectRefs("StackTrack.Application").Should().BeEquivalentTo(["StackTrack.Domain"]);

    [Fact]
    public void Infrastructure_does_not_reference_Api() =>
        ProjectRefs("StackTrack.Infrastructure").Should().NotContain("StackTrack.Api");

    static string[] ProjectRefs(string project) =>
        Load(project).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension((string)r.Attribute("Include")!))
            .ToArray();

    static XDocument Load(string project)
    {
        var relative = Path.Combine(project, project + ".csproj");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
            dir = dir.Parent;
        return dir is null
            ? throw new FileNotFoundException(relative)
            : XDocument.Load(Path.Combine(dir.FullName, relative));
    }
}
