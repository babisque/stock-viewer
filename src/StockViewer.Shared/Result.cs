namespace StockViewer.Shared;

public sealed record FieldError(string Field, string Message);

public sealed class Result<T>
{
    internal Result(T? value, IReadOnlyList<FieldError> errors)
    {
        Value = value;
        Errors = errors;
    }

    public T? Value { get; }

    public IReadOnlyList<FieldError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(value, []);

    public static Result<T> Failure<T>(IReadOnlyList<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentOutOfRangeException.ThrowIfZero(errors.Count, nameof(errors));
        return new(default, errors);
    }
}
