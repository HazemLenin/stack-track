using MediatR;
using StackTrack.Application.Errors;

namespace StackTrack.Application.Auth.Refresh;

public record RefreshTokenCommand(string RefreshToken) : IRequest<IssuedTokens>;

public class RefreshTokenCommandHandler(ITokenService tokens) : IRequestHandler<RefreshTokenCommand, IssuedTokens>
{
    public async Task<IssuedTokens> Handle(RefreshTokenCommand request, CancellationToken ct) =>
        await tokens.RotateAsync(request.RefreshToken, ct) ?? throw new InvalidRefreshTokenException();
}
