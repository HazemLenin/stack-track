using FluentValidation;
using MediatR;
using StackTrack.Application.Errors;

namespace StackTrack.Application.Auth.SignIn;

public record SignInCommand(string Email, string Password) : IRequest<IssuedTokens>;

public class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
    }
}

public class SignInCommandHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, TimeProvider clock)
    : IRequestHandler<SignInCommand, IssuedTokens>
{
    const int MaxFailedAttempts = 5;
    static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public async Task<IssuedTokens> Handle(SignInCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null)
        {
            hasher.VerifyDummy(request.Password);
            throw new InvalidCredentialsException();
        }

        var now = clock.GetUtcNow();
        if (user.LockedUntil > now) throw new AccountLockedException(user.LockedUntil.Value);

        if (!hasher.Verify(user.PasswordHash, request.Password))
        {
            // An expired lock starts a fresh count.
            if (user.LockedUntil is not null) user.FailedLoginCount = 0;
            user.LockedUntil = null;

            // ponytail: read-modify-write, two parallel wrong guesses can undercount by one. Use ExecuteUpdate if it matters.
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts) user.LockedUntil = now + LockDuration;
            await users.SaveAsync(ct);

            if (user.LockedUntil is { } until) throw new AccountLockedException(until);
            throw new InvalidCredentialsException();
        }

        if (!user.IsActive) throw new AccountDeactivatedException();

        if (user.FailedLoginCount != 0 || user.LockedUntil is not null)
        {
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            await users.SaveAsync(ct);
        }

        return await tokens.IssueAsync(user, ct);
    }
}
