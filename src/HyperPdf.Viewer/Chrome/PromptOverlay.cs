// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Presentation.Input;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>A dialog that asks for one line of text: a note, a name or a password.</summary>
/// <param name="request">What is asked.</param>
[DebuggerDisplay("PromptOverlay: {request.Title}")]
internal sealed class PromptOverlay(PromptRequest request) : IChromeOverlay
{
    /// <summary>The rows of the dialog: the heading, the label, the typing box and the buttons.</summary>
    private const int Rows = 3;

    /// <summary>The character shown in place of each character of a password.</summary>
    private const char MaskCharacter = '•';

    /// <summary>The answer, set when the dialog closes.</summary>
    private readonly TaskCompletionSource<string?> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The text typed so far.</summary>
    private string _entry = request.Text;

    /// <inheritdoc/>
    public bool IsModal => true;

    /// <inheritdoc/>
    public bool TakesAllKeys => true;

    /// <inheritdoc/>
    public bool ClosesOnOutsidePress => false;

    /// <inheritdoc/>
    public int Version { get; private set; }

    /// <summary>Gets the answer: the text typed, or <see langword="null"/> when the dialog was cancelled.</summary>
    internal Task<string?> Answer => _answer.Task;

    /// <inheritdoc/>
    public void Build(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, SurfaceSize size)
    {
        var style = ChromeBars.Style(metrics);
        var width = Math.Min(metrics.DialogWidth, size.Width);
        var inner = width - metrics.Padding - metrics.Padding;
        var height = (metrics.RowHeight * Rows) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, ChromeText.Fit(request.Title, style, inner, runs), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, ChromeText.Fit(request.Label, style, inner, runs), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;

        // A password is never drawn or handed to a screen reader: each character shows as a dot.
        var shown = request.Masked ? new string(MaskCharacter, _entry.Length) : _entry;
        var fitted = FitEnd(shown, style, inner - metrics.Padding - metrics.Padding, runs);
        items.Add(new(ChromeItemKind.Field, ChromeCommand.None, 0, fitted, new(left + metrics.Padding, y, inner, metrics.RowHeight), true, true));
        y += metrics.RowHeight;
        var buttons = new ChromeRow(items, runs, metrics, left + metrics.Padding, y + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        buttons.Button(request.AcceptText, ChromeCommand.Accept, true);
        buttons.Button(UiText.Get("Cancel"), ChromeCommand.Cancel, true);
    }

    /// <inheritdoc/>
    public bool Press(ChromeItem item)
    {
        if (item.Command is not (ChromeCommand.Accept or ChromeCommand.Cancel))
        {
            return true;
        }

        _ = _answer.TrySetResult(item.Command == ChromeCommand.Accept ? _entry : null);
        return false;
    }

    /// <inheritdoc/>
    public bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.Enter:
                {
                    _ = _answer.TrySetResult(_entry);
                    return false;
                }

            case ViewerKey.Escape:
                {
                    _ = _answer.TrySetResult(null);
                    return false;
                }

            case ViewerKey.Back when _entry.Length > 0:
                {
                    var cut = char.IsLowSurrogate(_entry[^1]) && _entry.Length > 1 ? _entry.Length - 1 - 1 : _entry.Length - 1;
                    _entry = _entry[..cut];
                    Version++;
                    return true;
                }

            default:
                {
                    return true;
                }
        }
    }

    /// <inheritdoc/>
    public void Type(int scalar)
    {
        if (!Rune.TryCreate(scalar, out var rune) || Rune.IsControl(rune))
        {
            return;
        }

        _entry = string.Concat(_entry, rune.ToString());
        Version++;
    }

    /// <inheritdoc/>
    public void Scroll(int rows)
    {
        // The dialog has no list.
    }

    /// <inheritdoc/>
    public void Dismiss() => _ = _answer.TrySetResult(null);

    /// <summary>Cuts text short at its start so its end, where typing goes on, fits a width.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width it must fit.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The text, whole when it fits.</returns>
    private static string FitEnd(string text, TextStyle style, int width, TextRuns runs)
    {
        var start = 0;
        while (start < text.Length && runs.Measure(text.AsSpan(start), style) > width)
        {
            start++;
        }

        if (start < text.Length && char.IsLowSurrogate(text[start]))
        {
            start++;
        }

        return start == 0 ? text : text[start..];
    }
}
