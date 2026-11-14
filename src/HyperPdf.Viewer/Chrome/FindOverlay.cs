// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Finding;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The search bar: a box to type what to find, how many matches there are and buttons to step through them.</summary>
/// <param name="search">The search of the tab shown.</param>
[DebuggerDisplay("FindOverlay: '{search.Query}'")]
internal sealed class FindOverlay(SearchViewModel search) : IChromeOverlay
{
    /// <summary>The width of the typing box, as a share of the dialog width.</summary>
    private const int FieldShare = 2;

    /// <inheritdoc/>
    public bool IsModal => false;

    /// <inheritdoc/>
    public bool TakesAllKeys => false;

    /// <inheritdoc/>
    public bool ClosesOnOutsidePress => false;

    /// <inheritdoc/>
    public int Version => HashCode.Combine(RuntimeHelpers.GetHashCode(search.Query), RuntimeHelpers.GetHashCode(search.Status));

    /// <inheritdoc/>
    public void Build(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, SurfaceSize size)
    {
        var style = ChromeBars.Style(metrics);
        var fieldWidth = metrics.DialogWidth / FieldShare;
        var height = metrics.ToolbarHeight;
        var top = metrics.TabHeight + metrics.ToolbarHeight;
        var panelIndex = items.Count;
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, default, true, false));
        var row = new ChromeRow(items, runs, metrics, 0, top + metrics.Gap, height - metrics.Gap - metrics.Gap);
        row.Button(UiText.Get("Previous match"), ChromeCommand.FindPrevious, search.Results.Count > 0);
        row.Button(UiText.Get("Next match"), ChromeCommand.FindNext, search.Results.Count > 0);
        row.Label(search.Status.Length > 0 ? search.Status : UiText.Get("Type to find"));
        row.Button(UiText.Get("Close"), ChromeCommand.Cancel, true);

        // The bar sits against the right edge, so the controls laid out from zero are moved across once their width is known.
        var width = fieldWidth + metrics.Padding + row.Next;
        var left = Math.Max(0, size.Width - width);
        items[panelIndex] = items[panelIndex] with { Bounds = new(left, top, width, height) };
        for (var i = panelIndex + 1; i < items.Count; i++)
        {
            var bounds = items[i].Bounds;
            items[i] = items[i] with { Bounds = new(bounds.X + left + fieldWidth + metrics.Padding, bounds.Y, bounds.Width, bounds.Height) };
        }

        var shown = ChromeText.Fit(search.Query, style, fieldWidth - metrics.Padding - metrics.Padding - metrics.Gap, runs);
        items.Add(new(ChromeItemKind.Field, ChromeCommand.None, 0, shown, new(left + metrics.Gap, top + metrics.Gap, fieldWidth, height - metrics.Gap - metrics.Gap), true, true));
    }

    /// <inheritdoc/>
    public bool Press(ChromeItem item)
    {
        switch (item.Command)
        {
            case ChromeCommand.FindNext:
                {
                    search.Next();
                    return true;
                }

            case ChromeCommand.FindPrevious:
                {
                    search.Previous();
                    return true;
                }

            case ChromeCommand.Cancel:
                {
                    search.Close();
                    return false;
                }

            default:
                {
                    return true;
                }
        }
    }

    /// <inheritdoc/>
    public bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.Enter when (modifiers & ViewerModifiers.Shift) != 0:
                {
                    search.Previous();
                    return true;
                }

            case ViewerKey.Enter:
                {
                    search.Next();
                    return true;
                }

            case ViewerKey.Escape:
                {
                    search.Close();
                    return false;
                }

            case ViewerKey.Back when search.Query.Length > 0:
                {
                    var query = search.Query;
                    var cut = char.IsLowSurrogate(query[^1]) && query.Length > 1 ? query.Length - 1 - 1 : query.Length - 1;
                    search.Query = query[..cut];
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
        if (Rune.TryCreate(scalar, out var rune) && !Rune.IsControl(rune))
        {
            search.Query = string.Concat(search.Query, rune.ToString());
        }
    }

    /// <inheritdoc/>
    public void Scroll(int rows)
    {
        // The search bar has no list.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dismiss() => search.Close();
}
