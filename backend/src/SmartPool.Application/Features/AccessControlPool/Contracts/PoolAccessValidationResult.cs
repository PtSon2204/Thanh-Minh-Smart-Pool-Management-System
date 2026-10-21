namespace SmartPool.Application.Features.AccessControlPool.Contracts
{
    public sealed class PoolAccessValidationResult<T>
    {
        public T? Value { get; private set; }
        public IReadOnlyDictionary<string, string[]>? Errors { get; private set; }
        public bool IsValid => Errors is null;

        private PoolAccessValidationResult(T? value, IReadOnlyDictionary<string, string[]>? errors)
        {
            Value = value;
            Errors = errors;
        }

        public static PoolAccessValidationResult<T> Success(T value)
        {
            return new PoolAccessValidationResult<T>(value, null);
        }

        public static PoolAccessValidationResult<T> Failure(string property, string error)
        {
            return new PoolAccessValidationResult<T>(default, new Dictionary<string, string[]> { [property] = [error] });
        }
    }
}
