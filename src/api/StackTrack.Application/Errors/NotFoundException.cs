namespace StackTrack.Application.Errors;

public class NotFoundException(string message) : Exception(message);
