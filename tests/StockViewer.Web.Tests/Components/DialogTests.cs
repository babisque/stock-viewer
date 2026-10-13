using StockViewer.Web.Components.Ui;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Components;

/// <summary>The native-&lt;dialog&gt; wrapper: content only while open, showModal()/close() through dialog.js, Esc reported.</summary>
public class DialogTests : WebTestContext
{
    private int closes;

    private IRenderedComponent<Dialog> RenderDialog(bool open) =>
        Render<Dialog>(parameters => parameters
            .Add(dialog => dialog.Open, open)
            .Add(dialog => dialog.Title, "Add position")
            .Add(dialog => dialog.Description, "Details")
            .Add(dialog => dialog.OnClose, () => closes++)
            .AddChildContent("<p>body</p>"));

    [Fact]
    public void Renders_its_content_only_while_open_and_opens_it_as_a_modal()
    {
        var module = JSInterop.SetupModule("./js/dialog.js");

        Assert.Empty(RenderDialog(open: false).FindAll("p"));

        var cut = RenderDialog(open: true);
        Assert.Equal("Add position", cut.Find("h2").Normalized());
        Assert.Equal(cut.Find("h2").Id, cut.Find("dialog").GetAttribute("aria-labelledby"));
        Assert.Single(module.Invocations, invocation => invocation.Identifier == "show");
    }

    [Fact]
    public void Reports_a_close_by_the_user_Esc_or_the_close_button()
    {
        var cut = RenderDialog(open: true);

        cut.Find("dialog").TriggerEvent("onclose", EventArgs.Empty);
        cut.Button("Close").Click();

        Assert.Equal(2, closes);
    }

    [Fact]
    public void Ignores_the_close_event_of_a_dialog_the_parent_already_closed()
    {
        var cut = RenderDialog(open: false);

        cut.Find("dialog").TriggerEvent("onclose", EventArgs.Empty);

        Assert.Equal(0, closes);
    }
}
