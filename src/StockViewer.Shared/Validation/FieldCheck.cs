namespace StockViewer.Shared.Validation;

/// <summary>One field's validation outcome, mirroring Zod: errors in Zod's order, and <see cref="Aborted"/> when an error stopped the field's later checks.</summary>
public sealed class FieldCheck<T>
{
    internal FieldCheck(T? value, string? text, IReadOnlyList<string> errors, bool aborted)
    {
        Value = value;
        Text = text;
        Errors = errors;
        Aborted = aborted;
    }

    public T? Value { get; }

    public string? Text { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool Aborted { get; }

    public bool IsValid => Errors.Count == 0;
}

public static class FieldCheck
{
    public static FieldCheck<T> Valid<T>(T value, string? text = null) => new(value, text, [], aborted: false);

    public static FieldCheck<T> Abort<T>(string error) => new(default, null, [error], aborted: true);

    public static FieldCheck<T> Invalid<T>(string? text, IReadOnlyList<string> errors) =>
        new(default, text, errors, aborted: false);
}
