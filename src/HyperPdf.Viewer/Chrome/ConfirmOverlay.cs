// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>A dialog that asks a yes or no question before something that cannot be undone.</summary>
/// <param name="request">What is asked.</param>
[DebuggerDisplay("ConfirmOverlay: {request.Title}")]
internal sealed class ConfirmOverlay(ConfirmRequest request) : IChromeOverlay
{
    /// <summary>The lines of the message, kept between layouts.</summary>
    private readonly List<string> _lines = [];

    /// <summary>The answer, set when the dialog closes.</summary>
    private readonly TaskCompletionSource<bool> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public bool IsModal => true;

    /// <inheritdoc/>
    public bool TakesAllKeys => true;

    /// <inheritdoc/>
    public bool ClosesOnOutsidePress => false;

    /// <inheritdoc/>
    public int Version => 0;

    /// <summary>Gets the answer: <see langword="true"/> when the user agreed.</summary>
    internal Task<bool> Answer => _answer.Task;

    /// <inheritdoc/>
    public void Build(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, SurfaceSize size)
    {
        var width = Math.Min(metrics.DialogWidth, size.Width);
        var inner = width - metrics.Padding - metrics.Padding;
        _lines.Clear();
        ChromeText.Wrap(request.Message, ChromeBars.Style(metrics), inner, runs, _lines);
        var height = (metrics.RowHeight * (_lines.Count + 1)) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, ChromeText.Fit(request.Title, ChromeBars.Style(metrics), inner, runs), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        for (var i = 0; i < _lines.Count; i++)
        {
            items.Add(new(ChromeItemKind.Label, ChromeCommand.None, i, _lines[i], new(left, y, width, metrics.RowHeight), true, false));
            y += metrics.RowHeight;
        }

        var row = new ChromeRow(items, runs, metrics, left + metrics.Padding, y + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        row.Button(request.ConfirmText, ChromeCommand.Accept, true);
        row.Button(UiText.Get("Cancel"), ChromeCommand.Cancel, true);
    }

    /// <inheritdoc/>
    public bool Press(ChromeItem item)
    {
        if (item.Command is not (ChromeCommand.Accept or ChromeCommand.Cancel))
        {
            return true;
        }

        _ = _answer.TrySetResult(item.Command == ChromeCommand.Accept);
        return false;
    }

    /// <inheritdoc/>
    public bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        if (key is not (ViewerKey.Enter or ViewerKey.Escape))
        {
            return true;
        }

        _ = _answer.TrySetResult(key == ViewerKey.Enter);
        return false;
    }

    /// <inheritdoc/>
    public void Type(int scalar)
    {
        // The dialog has nothing to type into.
    }

    /// <inheritdoc/>
    public void Scroll(int rows)
    {
        // The dialog has no list.
    }

    /// <inheritdoc/>
    public void Dismiss() => _ = _answer.TrySetResult(false);
}
