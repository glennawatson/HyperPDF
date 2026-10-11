// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;

namespace HyperPdfLibrary.Navigation;

/// <summary>
/// Runs actions that need no scripting: reset-form, hide and set-layer-state change the document; named, go-to, URI,
/// launch, submit-form and import-data are handed to the <see cref="IPdfActionHost"/>. JavaScript, media, 3D and the
/// other types are skipped, so a document cannot run code. A <c>/Next</c> chain runs in order. Safe to call from any thread
/// that may edit the document.
/// </summary>
[DebuggerDisplay("PdfActionRunner")]
public sealed class PdfActionRunner
{
    /// <summary>The mode that shows layers.</summary>
    private const string ModeOn = "ON";

    /// <summary>The mode that hides layers.</summary>
    private const string ModeOff = "OFF";

    /// <summary>The additional action key of a page that was opened.</summary>
    private const string PageOpened = "O";

    /// <summary>The additional action key of a page that was closed.</summary>
    private const string PageClosed = "C";

    /// <summary>The document being acted on.</summary>
    private readonly PdfDocument _document;

    /// <summary>The application's part of running actions.</summary>
    private readonly IPdfActionHost _host;

    /// <summary>Initializes a new instance of the <see cref="PdfActionRunner"/> class.</summary>
    /// <param name="document">The document the actions belong to.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="host"/> is <see langword="null"/>.</exception>
    public PdfActionRunner(PdfDocument document, IPdfActionHost host)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(host);
        _document = document;
        _host = host;
    }

    /// <summary>Runs an action and the actions that follow it.</summary>
    /// <param name="node">The action with its chain.</param>
    /// <param name="sourcePage">The page the action came from, or -1.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public async ValueTask<PdfActionResult> RunAsync(PdfActionNode node, int sourcePage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        var result = PdfActionResult.None;
        var pending = new Stack<PdfActionNode>();
        pending.Push(node);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            result |= await RunOneAsync(current.Action, sourcePage, cancellationToken).ConfigureAwait(false);
            for (var i = current.Next.Length - 1; i >= 0; i--)
            {
                pending.Push(current.Next[i]);
            }
        }

        return result;
    }

    /// <summary>Runs an action and the actions that follow it.</summary>
    /// <param name="node">The action with its chain.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PdfActionResult> RunAsync(PdfActionNode node, CancellationToken cancellationToken) => RunAsync(node, -1, cancellationToken);

    /// <summary>Runs the actions a page does when it is opened (<c>/AA /O</c>).</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PdfActionResult> RunPageOpenedAsync(int pageIndex, CancellationToken cancellationToken) => RunPageAsync(pageIndex, PageOpened, cancellationToken);

    /// <summary>Runs the actions a page does when it is closed (<c>/AA /C</c>).</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PdfActionResult> RunPageClosedAsync(int pageIndex, CancellationToken cancellationToken) => RunPageAsync(pageIndex, PageClosed, cancellationToken);

    /// <summary>Runs the action the document does when it is opened (the catalog's <c>/OpenAction</c>).</summary>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    public async ValueTask<PdfActionResult> RunDocumentOpenedAsync(CancellationToken cancellationToken) =>
        PdfDocumentActions.GetOpenAction(_document) is { } open ? await RunAsync(open, -1, cancellationToken).ConfigureAwait(false) : PdfActionResult.None;

    /// <summary>Runs the document's additional actions for one event: WC (will close), WS (will save), DS (did save), WP (will print) or DP (did print).</summary>
    /// <param name="eventKey">The event's key.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventKey"/> is <see langword="null"/>.</exception>
    public ValueTask<PdfActionResult> RunDocumentEventAsync(string eventKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventKey);
        return RunTriggersAsync(PdfDocumentActions.GetTriggers(_document), eventKey, -1, cancellationToken);
    }

    /// <summary>Describes a local change.</summary>
    /// <param name="changes">How many things changed.</param>
    /// <returns>The result.</returns>
    private static PdfActionResult Local(int changes) => changes > 0 ? PdfActionResult.Ran | PdfActionResult.Changed : PdfActionResult.Ran;

    /// <summary>Describes a host call.</summary>
    /// <param name="accepted">Whether the host did what was asked.</param>
    /// <returns>The result.</returns>
    private static PdfActionResult Asked(bool accepted) => accepted ? PdfActionResult.Ran : PdfActionResult.Declined;

    /// <summary>Applies one mode to one layer.</summary>
    /// <param name="content">The document's layers.</param>
    /// <param name="mode">ON, OFF or Toggle.</param>
    /// <param name="id">The layer's object number.</param>
    /// <returns><see langword="true"/> when the layer's visibility changed.</returns>
    private static bool Apply(PdfOptionalContent content, string mode, int id)
    {
        var visible = IsVisible(content, id);
        if (visible is null)
        {
            return false;
        }

        var wanted = mode switch
        {
            ModeOn => true,
            ModeOff => false,
            _ => !visible.Value,
        };
        return wanted != visible.Value && content.SetVisible(id, wanted);
    }

    /// <summary>Finds whether a layer is shown now.</summary>
    /// <param name="content">The document's layers.</param>
    /// <param name="id">The layer's object number.</param>
    /// <returns>Whether it is shown; <see langword="null"/> when the document has no such layer.</returns>
    private static bool? IsVisible(PdfOptionalContent content, int id)
    {
        foreach (var layer in content.Layers)
        {
            if (layer.Id == id)
            {
                return layer.IsVisible;
            }
        }

        return null;
    }

    /// <summary>Runs the triggers whose event matches.</summary>
    /// <param name="triggers">The triggers.</param>
    /// <param name="eventKey">The event key to run.</param>
    /// <param name="sourcePage">The page the triggers came from, or -1.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    private async ValueTask<PdfActionResult> RunTriggersAsync(PdfTrigger[] triggers, string eventKey, int sourcePage, CancellationToken cancellationToken)
    {
        var result = PdfActionResult.None;
        foreach (var trigger in triggers)
        {
            if (string.Equals(trigger.Event, eventKey, StringComparison.Ordinal))
            {
                result |= await RunAsync(trigger.Action, sourcePage, cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }

    /// <summary>Runs one event of a page's additional actions.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="eventKey">The event key.</param>
    /// <param name="cancellationToken">A token that cancels the run between actions.</param>
    /// <returns>What happened.</returns>
    private ValueTask<PdfActionResult> RunPageAsync(int pageIndex, string eventKey, CancellationToken cancellationToken) =>
        (uint)pageIndex < (uint)_document.PageCount
            ? RunTriggersAsync(PdfDocumentActions.GetTriggers(_document, PdfDocumentPages.GetPage(_document, pageIndex)), eventKey, pageIndex, cancellationToken)
            : ValueTask.FromResult(PdfActionResult.None);

    /// <summary>Runs one action, without its chain.</summary>
    /// <param name="action">The action.</param>
    /// <param name="sourcePage">The page the action came from, or -1.</param>
    /// <param name="cancellationToken">A token that cancels host calls.</param>
    /// <returns>What happened.</returns>
    private async ValueTask<PdfActionResult> RunOneAsync(PdfAction action, int sourcePage, CancellationToken cancellationToken)
    {
        switch (action.Value)
        {
            case ResetFormAction reset:
                {
                    return Local(HyperPdfLibrary.Forms.PdfFormActions.Reset(PdfDocumentForms.GetForm(_document), reset, cancellationToken));
                }

            case HideAction hide:
                {
                    return Local(HyperPdfLibrary.Forms.PdfFormActions.SetHidden(PdfDocumentForms.GetForm(_document), hide, cancellationToken));
                }

            case SetOcgStateAction layers:
                {
                    return ApplyLayers(layers) > 0 ? PdfActionResult.Ran | PdfActionResult.LayersChanged : PdfActionResult.Ran;
                }

            case NamedAction named:
                {
                    return Asked(await _host.RunNamedAsync(named.Name, sourcePage, cancellationToken).ConfigureAwait(false));
                }

            case SubmitFormAction submit:
                {
                    var submission = HyperPdfLibrary.Forms.PdfFormActions.CreateSubmission(PdfDocumentForms.GetForm(_document), submit, cancellationToken);
                    return Asked(await _host.SubmitFormAsync(submission, cancellationToken).ConfigureAwait(false));
                }

            case ImportDataAction import:
                {
                    return Asked(await _host.ImportDataAsync(import, cancellationToken).ConfigureAwait(false));
                }

            case GoToAction or UriAction or RemoteGoToAction or LaunchAction or EmbeddedGoToAction:
                {
                    return Asked(await _host.NavigateAsync(action, sourcePage, cancellationToken).ConfigureAwait(false));
                }

            default:
                {
                    return PdfActionResult.Skipped;
                }
        }
    }

    /// <summary>Applies the steps of a set-layer-state action in order.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The number of layers whose visibility changed.</returns>
    private int ApplyLayers(SetOcgStateAction action)
    {
        var content = PdfDocumentLayers.GetOptionalContent(_document);
        var changed = 0;
        foreach (var step in action.Changes)
        {
            foreach (var group in step.Groups)
            {
                changed += Apply(content, step.Mode, group.Number) ? 1 : 0;
            }
        }

        return changed;
    }
}
