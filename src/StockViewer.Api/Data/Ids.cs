namespace StockViewer.Api.Data;

internal static class Ids
{
    /// <summary>New row ids are time-ordered GUIDs. Existing cuid ids (from the M4 import) are kept as they are.</summary>
    public static string New() => Guid.CreateVersion7().ToString("N");
}
