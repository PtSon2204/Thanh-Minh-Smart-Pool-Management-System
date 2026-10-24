namespace SmartPool.Application.Features.ManageSwimClasses;

public sealed class SwimClassStateConflictException(string message) : Exception(message);
