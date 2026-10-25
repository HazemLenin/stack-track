using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StackTrack.Api.Tests.TestSupport;

public record ValidateCommand(string Name) : IRequest<string>;

public class ValidateCommandValidator : AbstractValidator<ValidateCommand>
{
    public ValidateCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MinimumLength(3);
        RuleFor(c => c).Custom((c, context) =>
        {
            if (c.Name == "forbidden") context.AddFailure("Name is forbidden.");
        });
    }
}

public class ValidateCommandHandler : IRequestHandler<ValidateCommand, string>
{
    public Task<string> Handle(ValidateCommand request, CancellationToken ct) => Task.FromResult(request.Name);
}

[ApiController]
[AllowAnonymous]
[Route("test")]
public class TestController(ISender sender) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<ActionResult<string>> Validate(ValidateCommand command) => await sender.Send(command);

    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("secret detail");
}
