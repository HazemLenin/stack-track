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

    [Fact]
    public void Requests_with_a_board_scope_marker_are_MediatR_requests()
    {
        var markers = new[]
        {
            typeof(IBoardScoped), typeof(IListScoped), typeof(ICardScoped), typeof(ILabelScoped), typeof(ICommentScoped),
        };

        var scoped = typeof(BoardAccessBehavior<,>).Assembly.GetTypes()
            .Where(t => t is { IsClass: true } && markers.Any(m => m.IsAssignableFrom(t)));

        scoped.Where(t => !typeof(IBaseRequest).IsAssignableFrom(t)).Should().BeEmpty();
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
