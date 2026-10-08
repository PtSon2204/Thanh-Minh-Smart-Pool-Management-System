namespace SmartPool.Application.Common.Exceptions;

public sealed class UniqueUserFieldException : Exception
{
    public string Field { get; }

    public UniqueUserFieldException(string field)
        : base($"The user field '{field}' already exists.")
    {
        Field = field;
    }

    public UniqueUserFieldException(string field, Exception innerException)
        : base($"The user field '{field}' already exists.", innerException)
    {
        Field = field;
    }
}
