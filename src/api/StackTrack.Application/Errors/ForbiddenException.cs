namespace StackTrack.Application.Errors;

public class ForbiddenException(string message) : Exception(message);
