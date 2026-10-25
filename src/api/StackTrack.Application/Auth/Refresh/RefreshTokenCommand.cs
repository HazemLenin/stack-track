using FluentValidation;
using MediatR;
using StackTrack.Application.Errors;

namespace StackTrack.Application.Auth.Refresh;

public record RefreshTokenCommand(string RefreshToken) : IRequest<IssuedTokens>;

// An empty token is valid input here: it is answered with 401, not 400.
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(c => c.RefreshToken).MaximumLength(256);
}

public class RefreshTokenCommandHandler(ITokenService tokens) : IRequestHandler<RefreshTokenCommand, IssuedTokens>
{
    public async Task<IssuedTokens> Handle(RefreshTokenCommand request, CancellationToken ct) =>
        await tokens.RotateAsync(request.RefreshToken, ct) ?? throw new InvalidRefreshTokenException();
}
