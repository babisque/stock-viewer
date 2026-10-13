using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;

namespace StockViewer.Web.Tests.Support;

/// <summary>The few Testing Library queries the React tests used, on top of bUnit's CSS selectors.</summary>
public static partial class Dom
{
    /// <summary>Text content with non-breaking spaces as spaces and runs of whitespace collapsed (markup indentation).</summary>
    public static string Normalized(this INode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Whitespace().Replace(Fixtures.NormalizeSpaces(node.TextContent), " ").Trim();
    }

    /// <summary>The control a &lt;label&gt; with this text points to (getByLabelText).</summary>
    public static IElement ByLabel<T>(this IRenderedComponent<T> component, string label)
        where T : IComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        var target = component.FindAll("label").Single(candidate => candidate.Normalized() == label).GetAttribute("for");
        return component.Find($"#{target}");
    }

    /// <summary>A button by its accessible name: aria-label, or else its text (getByRole("button", { name })).</summary>
    public static IElement Button<T>(this IRenderedComponent<T> component, string name)
        where T : IComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        return Single(component.FindAll("button"), name);
    }

    public static IElement Button(this IElement scope, string name)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return Single(scope.QuerySelectorAll("button"), name);
    }

    public static bool HasButton<T>(this IRenderedComponent<T> component, string name)
        where T : IComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        return component.FindAll("button").Any(button => NameOf(button) == name);
    }

    private static IElement Single(IEnumerable<IElement> buttons, string name)
    {
        var matches = buttons.Where(button => NameOf(button) == name).ToList();
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException($"Expected one button named \"{name}\", found {matches.Count}");
    }

    private static string NameOf(IElement button) => button.GetAttribute("aria-label") ?? button.Normalized();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
