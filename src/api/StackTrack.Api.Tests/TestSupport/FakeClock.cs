namespace StackTrack.Api.Tests.TestSupport;

public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
