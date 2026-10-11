// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests the form runtime: calculation order, tab order, and the actions that reset, hide and switch layers.</summary>
public sealed class FormRuntimeTests
{
    /// <summary>The Hidden annotation flag.</summary>
    private const int HiddenFlag = 2;

    /// <summary>The submit flag that includes fields with no value.</summary>
    private const int IncludeNoValue = 2;

    /// <summary>The reset flag that resets every field but the listed ones.</summary>
    private const int ExcludeListed = 1;

    /// <summary>The number of widgets on the form.</summary>
    private const int AnnotationCount = 8;

    /// <summary>The text fields on the form, which a reset changes.</summary>
    private const int ResettableFields = 3;

    /// <summary>The name of the first field in calculation order.</summary>
    private const string PriceName = "Price";

    /// <summary>The name of the calculated field.</summary>
    private const string TotalName = "Total";

    /// <summary>The name of the field with no default.</summary>
    private const string QtyName = "Qty";

    /// <summary>The address the Send button names.</summary>
    private const string SendAddress = "https://example.com/form";

    /// <summary>The index of "Price".</summary>
    private static readonly int PriceIndex = FormRuntimeSamples.PriceIndex;

    /// <summary>The index of "Total".</summary>
    private static readonly int TotalIndex = FormRuntimeSamples.TotalIndex;

    /// <summary>The index of "Qty".</summary>
    private static readonly int QtyIndex = FormRuntimeSamples.QtyIndex;

    /// <summary>The index of "Clear".</summary>
    private static readonly int ClearIndex = FormRuntimeSamples.ClearIndex;

    /// <summary>The index of "Hider".</summary>
    private static readonly int HiderIndex = FormRuntimeSamples.HiderIndex;

    /// <summary>The index of "Layer".</summary>
    private static readonly int LayerIndex = FormRuntimeSamples.LayerIndex;

    /// <summary>The index of "Send".</summary>
    private static readonly int SendIndex = FormRuntimeSamples.SendIndex;

    /// <summary>The index of "Script".</summary>
    private static readonly int ScriptIndex = FormRuntimeSamples.ScriptIndex;

    /// <summary>The widget indexes in row order: left to right along each row, rows from the top.</summary>
    private static readonly int[] RowOrder = [PriceIndex, TotalIndex, QtyIndex, ClearIndex, HiderIndex, LayerIndex, SendIndex, ScriptIndex];

    /// <summary>The widget indexes in column order: down each column, columns from the left.</summary>
    private static readonly int[] ColumnOrder = [PriceIndex, QtyIndex, ClearIndex, LayerIndex, ScriptIndex, TotalIndex, HiderIndex, SendIndex];

    /// <summary>The calculation order lists the fields of the AcroForm's /CO array by name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalculationOrderFollowsCo()
    {
        using var document = Open(string.Empty);
        List<string> names = [];

        HyperPdfLibrary.Forms.PdfFormOrder.GetCalculationOrder(PdfDocumentForms.GetForm(document), names);

        await Assert.That(names.ToArray()).IsEquivalentTo([PriceName, TotalName]);
        await Assert.That(names[0]).IsEqualTo(PriceName);
    }

    /// <summary>A page without /Tabs keeps the annotation order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabOrderDefaultsToAnnotationOrder()
    {
        using var document = Open(string.Empty);
        List<int> order = [];

        HyperPdfLibrary.Forms.PdfFormOrder.GetTabSequence(PdfDocumentForms.GetForm(document), 0, order);

        await Assert.That(HyperPdfLibrary.Forms.PdfFormOrder.GetTabOrder(PdfDocumentForms.GetForm(document), 0)).IsEqualTo(PdfTabOrder.Unspecified);
        await Assert.That(order.ToArray()).IsEquivalentTo(Enumerable.Range(0, AnnotationCount).ToArray());
        await Assert.That(order[0]).IsEqualTo(0);
        await Assert.That(order[^1]).IsEqualTo(AnnotationCount - 1);
    }

    /// <summary>Row order reads left to right along each row, rows from the top.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabOrderFollowsRows()
    {
        using var document = Open("/Tabs /R");
        List<int> order = [];

        HyperPdfLibrary.Forms.PdfFormOrder.GetTabSequence(PdfDocumentForms.GetForm(document), 0, order);

        await Assert.That(HyperPdfLibrary.Forms.PdfFormOrder.GetTabOrder(PdfDocumentForms.GetForm(document), 0)).IsEqualTo(PdfTabOrder.Row);
        await Assert.That(order.SequenceEqual(RowOrder)).IsTrue();
    }

    /// <summary>Column order reads down each column, columns from the left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabOrderFollowsColumns()
    {
        using var document = Open("/Tabs /C");
        List<int> order = [];

        HyperPdfLibrary.Forms.PdfFormOrder.GetTabSequence(PdfDocumentForms.GetForm(document), 0, order);

        await Assert.That(HyperPdfLibrary.Forms.PdfFormOrder.GetTabOrder(PdfDocumentForms.GetForm(document), 0)).IsEqualTo(PdfTabOrder.Column);
        await Assert.That(order.SequenceEqual(ColumnOrder)).IsTrue();
    }

    /// <summary>Structure order without a structure tree falls back to annotation order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureOrderNeedsATree()
    {
        using var document = Open("/Tabs /S");
        List<int> order = [];

        HyperPdfLibrary.Forms.PdfFormOrder.GetTabSequence(PdfDocumentForms.GetForm(document), 0, order);

        await Assert.That(HyperPdfLibrary.Forms.PdfFormOrder.GetTabOrder(PdfDocumentForms.GetForm(document), 0)).IsEqualTo(PdfTabOrder.Structure);
        await Assert.That(order.SequenceEqual(Enumerable.Range(0, AnnotationCount))).IsTrue();
    }

    /// <summary>Resetting puts fields back to their default values, or to nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetRestoresDefaults()
    {
        using var document = Open(string.Empty);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, PriceIndex, "9");

        var count = HyperPdfLibrary.Forms.PdfFormActions.Reset(PdfDocumentForms.GetForm(document), new([], 0));

        await Assert.That(count).IsEqualTo(ResettableFields);
        await Assert.That(ValueOf(document, PriceIndex)).IsEqualTo("1");
        await Assert.That(ValueOf(document, QtyIndex)).IsEmpty();
        await Assert.That(ValueOf(document, TotalIndex)).IsEqualTo("0");
    }

    /// <summary>A cancelled form action leaves fields unchanged and does not collect a partial submission or order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledAsyncFormActionsLeaveTheFormUnchanged()
    {
        using var document = Open(string.Empty);
        var form = PdfDocumentForms.GetForm(document);
        _ = PdfFormEditing.SetText(form, 0, PriceIndex, "9");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var token = cancellation.Token;

        await Assert.That(await Cancelled(() => PdfFormAsync.ResetAsync(form, new([], 0), token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfFormAsync.SetHiddenAsync(form, new([QtyName], true), token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfFormAsync.CreateSubmissionAsync(form, new(SendAddress, [], 0), token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfFormAsync.GetCalculationOrderAsync(form, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfFormAsync.GetTabSequenceAsync(form, 0, token).AsTask())).IsTrue();
        await Assert.That(ValueOf(document, PriceIndex)).IsEqualTo("9");
        await Assert.That(Annotation(document, QtyIndex).GetInt32(KnownName.F) & HiddenFlag).IsEqualTo(0);
    }

    /// <summary>A reset with the exclude flag leaves the listed fields alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetCanExcludeFields()
    {
        using var document = Open(string.Empty);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, PriceIndex, "9");

        _ = HyperPdfLibrary.Forms.PdfFormActions.Reset(PdfDocumentForms.GetForm(document), new([PriceName], ExcludeListed));

        await Assert.That(ValueOf(document, PriceIndex)).IsEqualTo("9");
        await Assert.That(ValueOf(document, QtyIndex)).IsEmpty();
    }

    /// <summary>A hide action sets and clears the Hidden flag of the named field.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HideSetsAndClearsTheFlag()
    {
        using var document = Open(string.Empty);

        var hidden = HyperPdfLibrary.Forms.PdfFormActions.SetHidden(PdfDocumentForms.GetForm(document), new([QtyName], true));
        var flagWhileHidden = Annotation(document, QtyIndex).GetInt32(KnownName.F);
        var shown = HyperPdfLibrary.Forms.PdfFormActions.SetHidden(PdfDocumentForms.GetForm(document), new([QtyName], false));

        await Assert.That(hidden).IsEqualTo(1);
        await Assert.That(flagWhileHidden & HiddenFlag).IsEqualTo(HiddenFlag);
        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(Annotation(document, QtyIndex).GetInt32(KnownName.F) & HiddenFlag).IsEqualTo(0);
    }

    /// <summary>A submission lists the fields that have values and never includes buttons; the no-value flag adds the empty ones.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubmissionCollectsFieldValues()
    {
        using var document = Open(string.Empty);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, QtyIndex, string.Empty);

        var withValues = HyperPdfLibrary.Forms.PdfFormActions.CreateSubmission(PdfDocumentForms.GetForm(document), new(SendAddress, [], 0));
        var withEmpty = HyperPdfLibrary.Forms.PdfFormActions.CreateSubmission(PdfDocumentForms.GetForm(document), new(SendAddress, [], IncludeNoValue));

        await Assert.That(withValues.Url).IsEqualTo(SendAddress);
        await Assert.That(withValues.Fields.Select(static field => field.Name).ToArray()).IsEquivalentTo([TotalName, PriceName]);
        await Assert.That(withEmpty.Fields.Select(static field => field.Name).ToArray()).IsEquivalentTo([TotalName, PriceName, QtyName]);
    }

    /// <summary>A button's reset and hide actions run, and the chain after the hide reaches the host.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunnerRunsLocalActionsAndHandsOnNamedOnes()
    {
        using var document = Open(string.Empty);
        var host = new RecordingActionHost();
        var runner = new PdfActionRunner(document, host);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, PriceIndex, "9");

        var reset = await runner.RunAsync(ButtonAction(document, ClearIndex), 0, CancellationToken.None);
        var hide = await runner.RunAsync(ButtonAction(document, HiderIndex), 0, CancellationToken.None);

        await Assert.That(reset).IsEqualTo(PdfActionResult.Ran | PdfActionResult.Changed);
        await Assert.That(ValueOf(document, PriceIndex)).IsEqualTo("1");
        await Assert.That(hide).IsEqualTo(PdfActionResult.Ran | PdfActionResult.Changed);
        await Assert.That(Annotation(document, QtyIndex).GetInt32(KnownName.F) & HiddenFlag).IsEqualTo(HiddenFlag);
        await Assert.That(host.Named.ToArray()).IsEquivalentTo(["NextPage"]);
    }

    /// <summary>A set-layer-state action toggles the layer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunnerTogglesLayers()
    {
        using var document = Open(string.Empty);
        var runner = new PdfActionRunner(document, new RecordingActionHost());
        var before = PdfDocumentLayers.GetOptionalContent(document).Layers[0].IsVisible;

        var first = await runner.RunAsync(ButtonAction(document, LayerIndex), CancellationToken.None);
        var afterFirst = PdfDocumentLayers.GetOptionalContent(document).Layers[0].IsVisible;
        _ = await runner.RunAsync(ButtonAction(document, LayerIndex), CancellationToken.None);

        await Assert.That(first).IsEqualTo(PdfActionResult.Ran | PdfActionResult.LayersChanged);
        await Assert.That(before).IsTrue();
        await Assert.That(afterFirst).IsFalse();
        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Layers[0].IsVisible).IsTrue();
    }

    /// <summary>A submit action is only offered to the host; a host that declines sends nothing and the result says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunnerOffersSubmissionsToTheHost()
    {
        using var document = Open(string.Empty);
        var host = new RecordingActionHost { Accepts = false };
        var runner = new PdfActionRunner(document, host);

        var result = await runner.RunAsync(ButtonAction(document, SendIndex), CancellationToken.None);

        await Assert.That(result).IsEqualTo(PdfActionResult.Declined);
        await Assert.That(host.Submissions.Count).IsEqualTo(1);
        await Assert.That(host.Submissions[0].Url).IsEqualTo(SendAddress);
        await Assert.That(host.Submissions[0].Fields.Length).IsGreaterThan(0);
    }

    /// <summary>JavaScript is never run; the runner skips it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunnerSkipsJavaScript()
    {
        using var document = Open(string.Empty);
        var host = new RecordingActionHost();
        var runner = new PdfActionRunner(document, host);

        var result = await runner.RunAsync(ButtonAction(document, ScriptIndex), CancellationToken.None);

        await Assert.That(result).IsEqualTo(PdfActionResult.Skipped);
        await Assert.That(host.Named.Count).IsEqualTo(0);
    }

    /// <summary>Page and document additional actions go through the same runner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunnerRunsPageAndDocumentTriggers()
    {
        using var document = Open(string.Empty);
        var host = new RecordingActionHost();
        var runner = new PdfActionRunner(document, host);

        var opened = await runner.RunPageOpenedAsync(0, CancellationToken.None);
        var closing = await runner.RunDocumentEventAsync("WC", CancellationToken.None);
        var closed = await runner.RunPageClosedAsync(0, CancellationToken.None);

        await Assert.That(opened).IsEqualTo(PdfActionResult.Ran);
        await Assert.That(closing).IsEqualTo(PdfActionResult.Ran);
        await Assert.That(closed).IsEqualTo(PdfActionResult.None);
        await Assert.That(host.Named.ToArray()).IsEquivalentTo(["FirstPage", "Close"]);
    }

    /// <summary>Opens the form with a page entry.</summary>
    /// <param name="pageEntries">The page dictionary's extra entries.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string pageEntries) => PdfDocumentReader.Open(FormRuntimeSamples.Create(pageEntries), null);

    /// <summary>Reports whether a task stopped on cancellation.</summary>
    /// <param name="action">The operation.</param>
    /// <returns>Whether it was cancelled.</returns>
    private static async Task<bool> Cancelled(Func<Task> action)
    {
        try
        {
            await action();
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Reads a widget annotation dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Annotation(PdfDocument document, int index) =>
        PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(index)!;

    /// <summary>Reads the action of a button.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The action with its chain.</returns>
    private static PdfActionNode ButtonAction(PdfDocument document, int index) =>
        PdfDocumentActions.ReadActionNode(document, Annotation(document, index).GetDictionary(KnownName.A)!);

    /// <summary>Reads a field's value by annotation index.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The value.</returns>
    private static string ValueOf(PdfDocument document, int index)
    {
        List<PdfFormWidget> widgets = [];
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), 0, widgets);
        return widgets.Find(widget => widget.Index == index)!.Value;
    }
}
