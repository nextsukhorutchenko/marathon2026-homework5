namespace APIHandler.Exceptions;

public class GroupOrderConflictException(string message) : ValidationException(message);
