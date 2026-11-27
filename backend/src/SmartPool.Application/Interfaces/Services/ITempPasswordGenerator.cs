namespace SmartPool.Application.Interfaces.Services;

public interface ITempPasswordGenerator
{
    // 10 characters, letters + digits, at least one of each,
    // ambiguous characters (0 O 1 l I o) excluded.
    string Generate();
}
