using Microsoft.AspNetCore.Identity;
using StackTrack.Application.Auth;

namespace StackTrack.Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    readonly PasswordHasher<object> _inner = new();
    readonly object _user = new();

    public string Hash(string password) => _inner.HashPassword(_user, password);

    readonly Lazy<string> _dummyHash;

    public PasswordHasher() => _dummyHash = new(() => _inner.HashPassword(_user, Guid.NewGuid().ToString("N")));

    public void VerifyDummy(string password) => Verify(_dummyHash.Value, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(_user, hash, password) != PasswordVerificationResult.Failed;
}
