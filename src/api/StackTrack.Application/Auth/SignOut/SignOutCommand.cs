using FluentValidation;
using MediatR;

namespace StackTrack.Application.Auth.SignOut;

public record SignOutCommand(string? RefreshToken) : IRequest;

public class SignOutCommandValidator : AbstractValidator<SignOutCommand>
{
    public SignOutCommandValidator() => RuleFor(c => c.RefreshToken).MaximumLength(256);
}

public class SignOutCommandHandler(ITokenService tokens) : IRequestHandler<SignOutCommand>
{
    public async Task Handle(SignOutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
            await tokens.RevokeAsync(request.RefreshToken, ct);
    }
}
