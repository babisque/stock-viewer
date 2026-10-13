namespace StockViewer.Web.Components.Ui;

public sealed record SegmentedOption<TValue>(TValue Value, string Label, IconName? Icon = null);
