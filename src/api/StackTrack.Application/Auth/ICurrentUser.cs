namespace StackTrack.Application.Auth;

public interface ICurrentUser
{
    // Throws when the request is anonymous; callers sit behind the authenticated fallback policy.
    Guid Id { get; }

    bool IsAdmin { get; }
}
