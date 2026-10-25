using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StackTrack.Application.Behaviors;

namespace StackTrack.Application.Tests;

public class ValidationBehaviorTests
{
    record TestCommand(string Name) : IRequest<string>;

    class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(c => c.Name).NotEmpty().MinimumLength(3);
        }
    }

    class TestHandler(Counter counter) : IRequestHandler<TestCommand, string>
    {
        public Task<string> Handle(TestCommand request, CancellationToken ct)
        {
            counter.Calls++;
            return Task.FromResult(request.Name);
        }
    }

    class Counter { public int Calls; }

    record NoValidatorCommand : IRequest<int>;

    class NoValidatorHandler : IRequestHandler<NoValidatorCommand, int>
    {
        public Task<int> Handle(NoValidatorCommand request, CancellationToken ct) => Task.FromResult(42);
    }

    static (IMediator Mediator, Counter Counter) Build()
    {
        var counter = new Counter();
        var services = new ServiceCollection();
        services.AddSingleton(counter);
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<ValidationBehaviorTests>());
        services.AddTransient<IValidator<TestCommand>, TestCommandValidator>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return (services.BuildServiceProvider().GetRequiredService<IMediator>(), counter);
    }

    [Fact]
    public async Task Invalid_request_throws_ValidationException_with_all_failures_and_handler_not_called()
    {
        var (mediator, counter) = Build();

        var act = () => mediator.Send(new TestCommand(""));

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Select(e => e.PropertyName).Should().OnlyContain(p => p == "Name");
        ex.Errors.Should().HaveCount(2);
        counter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Valid_request_reaches_handler()
    {
        var (mediator, counter) = Build();

        var result = await mediator.Send(new TestCommand("abc"));

        result.Should().Be("abc");
        counter.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Request_without_validators_passes_through()
    {
        var (mediator, _) = Build();

        (await mediator.Send(new NoValidatorCommand())).Should().Be(42);
    }
}
