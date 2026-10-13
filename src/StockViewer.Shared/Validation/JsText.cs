namespace StockViewer.Shared.Validation;

internal static class JsText
{
    /// <summary>Same as JavaScript's <c>String.prototype.trim()</c>, which also removes the BOM.</summary>
    public static string Trim(string value) =>
        value.Trim().Trim('\uFEFF').Trim();
}
