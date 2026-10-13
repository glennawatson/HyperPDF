// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Lists the fields a form design edits and changes their attributes (ISO 32000-2 12.7.3 and 12.7.4): flags, value, default
/// value, character limit, alignment, default appearance, options, export values, colours, border, caption, tooltip and actions.
/// A change is one undoable edit that draws the appearances again. A change the field or the document cannot take is refused
/// with a plain reason and nothing is written.
/// </summary>
public static class PdfFormFieldEditing
{
    /// <summary>Reads every field that has widgets and is a kind the form design edits.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The fields, in the order of the form's field tree.</returns>
    /// <exception cref="ArgumentNullException">The document is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldDetails[] GetFields(PdfDocument document) => GetFields(document, CancellationToken.None);

    /// <summary>Reads every field that has widgets and is a kind the form design edits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The fields, in the order of the form's field tree.</returns>
    /// <exception cref="ArgumentNullException">The document is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldDetails[] GetFields(PdfDocument document, CancellationToken cancellationToken) => ReadAll(document, cancellationToken);

    /// <summary>Reads every field after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels loading and the walk.</param>
    /// <returns>The fields.</returns>
    /// <exception cref="ArgumentNullException">The document is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldDetails[]> GetFieldsAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        await FormFieldPrefetch.LoadAllPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return ReadAll(document, cancellationToken);
    }

    /// <summary>Reads one field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <returns>The field; <see langword="null"/> when no editable field has the name.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldDetails? GetField(PdfDocument document, string name) => GetField(document, name, CancellationToken.None);

    /// <summary>Reads one field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The field; <see langword="null"/> when no editable field has the name.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfFieldDetails? GetField(PdfDocument document, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        var index = FormFieldIndex.Build(document, cancellationToken);
        return index.Find(name) is { } node ? FormFieldReader.Read(document, node, FormFieldReader.LocateWidgets(document, cancellationToken)) : null;
    }

    /// <summary>Changes a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="changes">The changes; a property left <see langword="null"/> keeps what the field has.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldChangeResult ChangeField(PdfDocument document, string name, PdfFieldChanges changes) =>
        ChangeField(document, name, changes, CancellationToken.None);

    /// <summary>Changes a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="changes">The changes; a property left <see langword="null"/> keeps what the field has.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfFieldChangeResult ChangeField(PdfDocument document, string name, PdfFieldChanges changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(changes);
        return Run(document, new(name, changes, -1, default), cancellationToken);
    }

    /// <summary>Changes a field after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldChangeResult> ChangeFieldAsync(PdfDocument document, string name, PdfFieldChanges changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(changes);
        await FormFieldPrefetch.LoadAllPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return Run(document, new(name, changes, -1, default), cancellationToken);
    }

    /// <summary>Moves or resizes one widget of a field on its page, and draws it again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="widgetIndex">The widget's position in the field.</param>
    /// <param name="bounds">The new rectangle in page space.</param>
    /// <returns>Whether the widget was moved, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldChangeResult SetWidgetBounds(PdfDocument document, string name, int widgetIndex, PdfRectangle bounds) =>
        SetWidgetBounds(document, name, widgetIndex, bounds, CancellationToken.None);

    /// <summary>Moves or resizes one widget of a field on its page, and draws it again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="widgetIndex">The widget's position in the field.</param>
    /// <param name="bounds">The new rectangle in page space.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>Whether the widget was moved, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfFieldChangeResult SetWidgetBounds(PdfDocument document, string name, int widgetIndex, PdfRectangle bounds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        return Run(document, new(name, null, widgetIndex, bounds), cancellationToken);
    }

    /// <summary>Moves or resizes a widget after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="widgetIndex">The widget's position in the field.</param>
    /// <param name="bounds">The new rectangle in page space.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>Whether the widget was moved, or why it was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldChangeResult> SetWidgetBoundsAsync(PdfDocument document, string name, int widgetIndex, PdfRectangle bounds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        await FormFieldPrefetch.LoadAllPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return Run(document, new(name, null, widgetIndex, bounds), cancellationToken);
    }

    /// <summary>Reads every field that has widgets and is a kind the form design edits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The fields.</returns>
    private static PdfFieldDetails[] ReadAll(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var index = FormFieldIndex.Build(document, cancellationToken);
        var locations = FormFieldReader.LocateWidgets(document, cancellationToken);
        var fields = new List<PdfFieldDetails>(index.All.Count);
        foreach (var node in index.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FormFieldReader.Read(document, node, locations) is { } details)
            {
                fields.Add(details);
            }
        }

        return [.. fields];
    }

    /// <summary>Plans a change and, if it is allowed, makes it in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="request">What to change.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <returns>Whether the change was made, or why it was refused.</returns>
    private static PdfFieldChangeResult Run(PdfDocument document, ChangeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (PdfDocumentForms.GetForm(document).Gate)
        {
            var index = FormFieldIndex.Build(document, cancellationToken);
            var locations = FormFieldReader.LocateWidgets(document, cancellationToken);
            var refusal = Prepare(document, request, index, locations, cancellationToken, out var run);
            if (refusal is not null || run is null)
            {
                return PdfFieldChangeResult.Refused(refusal ?? "The field was not changed.");
            }

            PdfDocumentEditing.RunEdit(document, run.Label, PdfChangeKinds.Other, PdfCacheScopes.None, run, Apply);
            return PdfFieldChangeResult.Done;
        }
    }

    /// <summary>Finds the field and plans the change without writing anything.</summary>
    /// <param name="document">The document.</param>
    /// <param name="request">What to change.</param>
    /// <param name="index">The form's fields.</param>
    /// <param name="locations">Where the widgets sit.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <param name="run">Receives the edit to make.</param>
    /// <returns>A plain reason the change is refused; <see langword="null"/> when it can be made.</returns>
    private static string? Prepare(
        PdfDocument document,
        ChangeRequest request,
        FormFieldIndex index,
        Dictionary<int, PdfWidgetLocation> locations,
        CancellationToken cancellationToken,
        out FormFieldEdit? run)
    {
        run = null;
        if (index.Find(request.Name) is not { } node)
        {
            return $"No field is named '{request.Name}'.";
        }

        if (!node.Id.IsValid || FormFieldReader.Read(document, node, locations) is not { } details)
        {
            return $"The field '{request.Name}' is not a kind of field the form design edits, or is written inside its parent.";
        }

        if (request.WidgetIndex >= 0)
        {
            return PrepareBounds(document, node, details, request, cancellationToken, out run);
        }

        try
        {
            var change = FormFieldMerge.Plan(document, details, request.Changes!);
            run = new(document, node, details, "Change form field", cancellationToken) { Change = change };
            return CheckWidgetObjects(node);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return exception.Message;
        }
    }

    /// <summary>Plans the move of a widget.</summary>
    /// <param name="document">The document.</param>
    /// <param name="node">The field.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="request">What to change.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <param name="run">Receives the edit to make.</param>
    /// <returns>A plain reason the change is refused; <see langword="null"/> when it can be made.</returns>
    private static string? PrepareBounds(
        PdfDocument document,
        FormFieldNode node,
        PdfFieldDetails details,
        ChangeRequest request,
        CancellationToken cancellationToken,
        out FormFieldEdit? run)
    {
        run = null;
        if ((uint)request.WidgetIndex >= (uint)details.Widgets.Length)
        {
            return $"The field '{request.Name}' has {details.Widgets.Length} widgets, so widget {request.WidgetIndex} does not exist.";
        }

        if (request.Bounds.IsEmpty || !float.IsFinite(request.Bounds.Left + request.Bounds.Bottom + request.Bounds.Right + request.Bounds.Top))
        {
            return "A widget needs a rectangle with a width and a height.";
        }

        var refusal = CheckWidgetObjects(node);
        run = refusal is null
            ? new(document, node, details, "Move form field", cancellationToken) { WidgetIndex = request.WidgetIndex, Bounds = request.Bounds }
            : null;
        return refusal;
    }

    /// <summary>Checks that the widgets can be rewritten: each is an object of its own, or the field itself.</summary>
    /// <param name="node">The field.</param>
    /// <returns>A plain reason the field cannot be rewritten; <see langword="null"/> when it can.</returns>
    private static string? CheckWidgetObjects(FormFieldNode node)
    {
        foreach (var widget in node.Widgets)
        {
            if (!widget.Id.IsValid)
            {
                return $"A widget of the field '{node.Name}' is written inside its field, not as an object of its own, so it cannot be changed.";
            }
        }

        return null;
    }

    /// <summary>Writes the change inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="run">The edit.</param>
    private static void Apply(PdfEditTransaction transaction, FormFieldEdit run)
    {
        var document = run.Document;
        if (run.Change is { } change)
        {
            FormFieldChangeWriter.Write(document, transaction, run.Node, run.Details, change, run.CancellationToken);
            return;
        }

        FormFieldChangeWriter.WriteBounds(document, transaction, run.Node, run.Details, run.WidgetIndex, run.Bounds, run.CancellationToken);
    }

    /// <summary>What to change, as asked.</summary>
    /// <param name="Name">The field's fully qualified name.</param>
    /// <param name="Changes">The attribute changes; <see langword="null"/> for a move.</param>
    /// <param name="WidgetIndex">The widget to move; -1 for an attribute change.</param>
    /// <param name="Bounds">The new rectangle of a move.</param>
    [DebuggerDisplay("ChangeRequest: {Name}")]
    private sealed record ChangeRequest(string Name, PdfFieldChanges? Changes, int WidgetIndex, PdfRectangle Bounds);
}
