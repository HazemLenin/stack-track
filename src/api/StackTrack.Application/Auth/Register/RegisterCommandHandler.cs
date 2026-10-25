using MediatR;
using StackTrack.Application.Errors;
using StackTrack.Domain.Entities;

namespace StackTrack.Application.Auth.Register;

public class RegisterCommandHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens)
    : IRequestHandler<RegisterCommand, IssuedTokens>
{
    const string EmailTaken = "An account with this email already exists";

    public async Task<IssuedTokens> Handle(RegisterCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.EmailExistsAsync(email, ct)) throw new ConflictException(EmailTaken);

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Role = UserRole.Member,
        };
        // A concurrent registration can win between the check above and this insert.
        if (!await users.TryAddAsync(user, ct)) throw new ConflictException(EmailTaken);

        return await tokens.IssueAsync(user, ct);
    }
}
