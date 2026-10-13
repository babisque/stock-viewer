using StockViewer.Shared;

namespace StockViewer.Api.Errors;

internal static class ValidationResults
{
    /// <summary>Returns the validated value, or throws 400 VALIDATION_ERROR with the first error as "field: message", like the Fastify handler.</summary>
    public static T OrThrowValidation<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsValid)
        {
            return result.Value!;
        }
        var first = result.Errors[0];
        throw ApiErrors.Validation($"{first.Field}: {first.Message}");
    }
}
