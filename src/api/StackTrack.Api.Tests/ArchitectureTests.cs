using System.Xml.Linq;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StackTrack.Application;
using StackTrack.Application.Boards;

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

    [Fact]
    public void Board_access_behavior_is_registered_for_every_request()
    {
        var services = new ServiceCollection().AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(IPipelineBehavior<,>)
            && d.ImplementationType == typeof(BoardAccessBehavior<,>));
    }

    static readonly Type[] ScopeMarkers =
        [typeof(IBoardScoped), typeof(IListScoped), typeof(ICardScoped), typeof(ILabelScoped), typeof(ICommentScoped)];

    // The Application assembly holds the real requests; this assembly holds the probe requests used to exercise the behavior.
    static Type[] ScopedRequests() => new[] { typeof(BoardAccessBehavior<,>).Assembly, typeof(ArchitectureTests).Assembly }
        .SelectMany(a => a.GetTypes())
        .Where(t => t is { IsClass: true } && ScopeMarkers.Any(m => m.IsAssignableFrom(t)))
        .ToArray();

    [Fact]
    public void Requests_with_a_board_scope_marker_are_MediatR_requests()
    {
        var scoped = ScopedRequests();

        scoped.Should().NotBeEmpty("an empty scan would make this rule pass without checking anything");
        scoped.Where(t => !typeof(IBaseRequest).IsAssignableFrom(t)).Should().BeEmpty();
    }

    // Admin read-only is enforced through IWriteRequest, so a forgotten marker would let an Admin write.
    [Fact]
    public void Scoped_commands_are_write_requests_and_scoped_queries_are_not()
    {
        var scoped = ScopedRequests();

        scoped.Where(t => t.Name.EndsWith("Command") && !typeof(IWriteRequest).IsAssignableFrom(t)).Should().BeEmpty();
        scoped.Where(t => t.Name.EndsWith("Query") && typeof(IWriteRequest).IsAssignableFrom(t)).Should().BeEmpty();
        scoped.Where(t => !t.Name.EndsWith("Command") && !t.Name.EndsWith("Query")).Should()
            .BeEmpty("scoped requests must be named ...Command or ...Query so the write rule can be checked");
    }

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
