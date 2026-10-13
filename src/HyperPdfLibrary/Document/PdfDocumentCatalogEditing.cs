// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Edits how a viewer opens the document: the viewer preferences (ISO 32000-2 12.2), the page layout and page mode
/// (7.7.2), the action run on opening (<c>/OpenAction</c>) and the scripts on document events (the catalog's
/// <c>/AA</c>, 12.6.3). Each edit joins the open transaction or makes its own, so it can be undone. A document certified
/// with a signature refuses the edit; a document that claims PDF/A refuses a script; a document that claims PDF/UA
/// refuses to stop showing its title.
/// </summary>
public static class PdfDocumentCatalogEditing
{
    /// <summary>What an edit of the opening settings is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change how the document opens";

    /// <summary>Changes the viewer preferences. A property left <see langword="null"/> keeps its value.</summary>
    /// <param name="document">The document.</param>
    /// <param name="change">The changes.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value is not one the standard allows.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or the change would break a PDF/UA claim.</exception>
    public static void SetViewerPreferences(PdfDocument document, PdfViewerPreferencesEdit change)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(change);

        // The text pages read the reading direction from the viewer preferences.
        Run(
            document,
            "Change viewer preferences",
            PdfCacheScopes.PageContent,
            new Step<PdfViewerPreferencesEdit>(document, change),
            static (edits, step) => PdfCatalogEditor.SetViewerPreferences(edits, step.Document, step.Value));
    }

    /// <summary>Changes the viewer preferences.</summary>
    /// <param name="document">The document.</param>
    /// <param name="change">The changes.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the preferences are changed.</returns>
    public static async ValueTask SetViewerPreferencesAsync(PdfDocument document, PdfViewerPreferencesEdit change, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.ViewerPreferences, cancellationToken).ConfigureAwait(false);
        SetViewerPreferences(document, change);
    }

    /// <summary>Sets how a viewer lays out the pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layout">The layout; <see cref="PdfPageLayout.SinglePage"/> is the default and removes the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The layout is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetPageLayout(PdfDocument document, PdfPageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(document, "Change page layout", layout, PdfCatalogEditor.SetPageLayout);
    }

    /// <summary>Sets how a viewer lays out the pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="cancellationToken">Cancels the work; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the layout is set.</returns>
    public static ValueTask SetPageLayoutAsync(PdfDocument document, PdfPageLayout layout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetPageLayout(document, layout);
        return ValueTask.CompletedTask;
    }

    /// <summary>Sets which panel a viewer shows when the document opens.</summary>
    /// <param name="document">The document.</param>
    /// <param name="mode">The mode; <see cref="PdfPageMode.UseNone"/> is the default and removes the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The mode is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetPageMode(PdfDocument document, PdfPageMode mode)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(document, "Change page mode", mode, PdfCatalogEditor.SetPageMode);
    }

    /// <summary>Sets which panel a viewer shows when the document opens.</summary>
    /// <param name="document">The document.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">Cancels the work; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the mode is set.</returns>
    public static ValueTask SetPageModeAsync(PdfDocument document, PdfPageMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetPageMode(document, mode);
        return ValueTask.CompletedTask;
    }

    /// <summary>Sets the destination or action a viewer runs when the document opens.</summary>
    /// <param name="document">The document.</param>
    /// <param name="target">The target; <see langword="null"/> or <see cref="PdfBookmarkTarget.Nowhere"/> removes it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The target is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetOpenAction(PdfDocument document, PdfBookmarkTarget? target)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(document, "Change opening action", new Step<PdfBookmarkTarget?>(document, target), static (edits, step) => PdfCatalogEditor.SetOpenAction(edits, step.Document, step.Value));
    }

    /// <summary>Sets the destination or action a viewer runs when the document opens.</summary>
    /// <param name="document">The document.</param>
    /// <param name="target">The target, or <see langword="null"/> to remove it.</param>
    /// <param name="cancellationToken">Cancels the work; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the action is set.</returns>
    public static ValueTask SetOpenActionAsync(PdfDocument document, PdfBookmarkTarget? target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetOpenAction(document, target);
        return ValueTask.CompletedTask;
    }

    /// <summary>Sets the script that runs on a document event, or removes it. The script is stored as text; nothing runs it here.</summary>
    /// <param name="document">The document.</param>
    /// <param name="documentEvent">The event.</param>
    /// <param name="script">The JavaScript, or <see langword="null"/> or empty to remove the action.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The event is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or claims PDF/A, which forbids scripts.</exception>
    public static void SetDocumentAction(PdfDocument document, PdfDocumentActionEvent documentEvent, string? script)
    {
        ArgumentNullException.ThrowIfNull(document);
        Run(
            document,
            "Change document script",
            new ScriptStep(document, documentEvent, script),
            static (edits, step) => PdfCatalogEditor.SetDocumentAction(edits, step.Document, step.Event, step.Script));
    }

    /// <summary>Sets the script that runs on a document event, or removes it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="documentEvent">The event.</param>
    /// <param name="script">The JavaScript, or <see langword="null"/> to remove the action.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the script is set.</returns>
    public static async ValueTask SetDocumentActionAsync(PdfDocument document, PdfDocumentActionEvent documentEvent, string? script, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.AA, cancellationToken).ConfigureAwait(false);
        SetDocumentAction(document, documentEvent, script);
    }

    /// <summary>Runs a catalog edit in a transaction.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Run<TState>(PdfDocument document, string label, TState state, Action<PdfObjectEdits, TState> edit) =>
        PdfDocumentCatalogEditing.Run(document, label, PdfCacheScopes.None, state, edit);

    /// <summary>Runs a catalog edit in a transaction.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="scopes">The parts of the document the edit can change.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Run<TState>(PdfDocument document, string label, PdfCacheScopes scopes, TState state, Action<PdfObjectEdits, TState> edit) =>
        PdfDocumentEditing.RunObjectEdit(document, label, PdfChangeKinds.Other, scopes, EditDescription, state, edit);

    /// <summary>The document and one argument of an edit.</summary>
    /// <typeparam name="TValue">The type of the argument.</typeparam>
    /// <param name="Document">The document.</param>
    /// <param name="Value">The argument.</param>
    [DebuggerDisplay("Step: {Value}")]
    private readonly record struct Step<TValue>(PdfDocument Document, TValue Value);

    /// <summary>The arguments of a script change.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Event">The event.</param>
    /// <param name="Script">The script.</param>
    [DebuggerDisplay("ScriptStep: {Event}")]
    private readonly record struct ScriptStep(PdfDocument Document, PdfDocumentActionEvent Event, string? Script);
}
