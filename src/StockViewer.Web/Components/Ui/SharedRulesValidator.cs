using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Api;

namespace StockViewer.Web.Components.Ui;

/// <summary>
/// Runs the same <c>Shared/Validation</c> rules the Api uses and puts each <see cref="FieldError"/> on its field
/// ("averagePrice" → the model's <c>AveragePrice</c>). Like React Hook Form: validates on submit, then again on every
/// change once a submit was attempted.
/// </summary>
public sealed class SharedRulesValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? store;
    private EditContext? subscribedTo;
    private bool submitted;

    [CascadingParameter] private EditContext? EditContext { get; set; }

    [Parameter, EditorRequired] public required Func<IReadOnlyList<FieldError>> Validate { get; set; }

    protected override void OnParametersSet()
    {
        if (EditContext is null)
        {
            throw new InvalidOperationException($"{nameof(SharedRulesValidator)} must be inside an EditForm.");
        }
        if (ReferenceEquals(EditContext, subscribedTo))
        {
            return;
        }
        Unsubscribe();
        store = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += OnValidationRequested;
        EditContext.OnFieldChanged += OnFieldChanged;
        subscribedTo = EditContext;
    }

    /// <summary>Shows a server error on its field; returns false when <paramref name="field"/> isn't on the model.</summary>
    public bool AddError(string field, string message)
    {
        var editContext = EditContext!;
        var property = PropertyName(field);
        if (editContext.Model.GetType().GetProperty(property) is null)
        {
            return false;
        }
        store!.Add(new FieldIdentifier(editContext.Model, property), message);
        editContext.NotifyValidationStateChanged();
        return true;
    }

    /// <summary>
    /// Maps an Api error onto a field: <c>DUPLICATE_POSITION</c> → ticker and <c>INSUFFICIENT_QUANTITY</c> → quantity
    /// (as the React forms did), or a "field: message" validation error to that field. False means it belongs at
    /// the top of the form.
    /// </summary>
    public bool AddApiError(ApiException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        switch (error.Code)
        {
            case ErrorCode.DuplicatePosition:
                return AddError("ticker", error.Message);
            case ErrorCode.InsufficientQuantity:
                return AddError("quantity", error.Message);
        }
        var separator = error.Message.IndexOf(": ", StringComparison.Ordinal);
        return separator > 0 && AddError(error.Message[..separator], error.Message[(separator + 2)..]);
    }

    /// <summary>Forgets earlier errors and the submit attempt (after a successful submit that keeps the form open).</summary>
    public void Reset()
    {
        submitted = false;
        store?.Clear();
        EditContext?.NotifyValidationStateChanged();
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        submitted = true;
        Run();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        if (submitted)
        {
            Run();
        }
    }

    private void Run()
    {
        var editContext = EditContext!;
        store!.Clear();
        foreach (var error in Validate())
        {
            store.Add(new FieldIdentifier(editContext.Model, PropertyName(error.Field)), error.Message);
        }
        editContext.NotifyValidationStateChanged();
    }

    private static string PropertyName(string field) =>
        field.Length == 0 ? field : string.Concat(char.ToUpperInvariant(field[0]).ToString(), field.AsSpan(1));

    private void Unsubscribe()
    {
        if (subscribedTo is null)
        {
            return;
        }
        subscribedTo.OnValidationRequested -= OnValidationRequested;
        subscribedTo.OnFieldChanged -= OnFieldChanged;
        subscribedTo = null;
    }

    public void Dispose() => Unsubscribe();
}
