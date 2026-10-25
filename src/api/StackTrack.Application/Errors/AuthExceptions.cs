namespace StackTrack.Application.Errors;

// One message for a wrong password and an unknown email, so the response never reveals which accounts exist.
public class InvalidCredentialsException() : Exception("Email or password is incorrect");

public class AccountDeactivatedException()
    : Exception("Your account has been deactivated. Please contact an administrator.");

public class AccountLockedException(DateTimeOffset until)
    : Exception("This account is temporarily locked because of too many failed sign-in attempts.")
{
    public DateTimeOffset LockedUntil { get; } = until;
}
