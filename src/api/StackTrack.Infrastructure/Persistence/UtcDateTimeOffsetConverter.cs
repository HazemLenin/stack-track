using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StackTrack.Infrastructure.Persistence;

public class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);
