namespace StackTrack.Application.Auth;

public interface ICurrentUser
{
    // Id throws when the request is anonymous (IsAdmin is just false); callers sit behind the authenticated fallback policy.
    Guid Id { get; }

    bool IsAdmin { get; }
}
