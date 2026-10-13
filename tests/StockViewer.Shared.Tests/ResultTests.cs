namespace StockViewer.Shared.Tests;

public class ResultTests
{
    [Fact]
    public void Success_carries_the_value_and_no_errors()
    {
        var result = Result.Success(42);

        Assert.True(result.IsValid);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_keeps_the_errors_in_order()
    {
        var result = Result.Failure<int>([new FieldError("a", "first"), new FieldError("b", "second")]);

        Assert.False(result.IsValid);
        Assert.Equal(["first", "second"], result.Errors.Select(error => error.Message));
    }

    [Fact]
    public void Failure_requires_at_least_one_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Result.Failure<int>([]));
    }
}
