// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms.Detection;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// Places to write on forms without fillable fields. While the Text tool is on, each shown page is read for lines,
/// boxes and rows of character boxes, off the UI thread and once per page. Clicking inside one starts typing fitted
/// to it: on the line, in the middle of the box, or one character to a box; holding Alt places text freely instead.
/// </summary>
public sealed partial class AnnotationsViewModel
{
    /// <summary>The space kept between a box's edge and its text, in points.</summary>
    private const float BoxPadding = 2;

    /// <summary>The padding on both sides of a box's text, in points.</summary>
    private const float BothPaddings = BoxPadding * 2;

    /// <summary>How far above a line text sits, as a share of the text size, so letters rest on it.</summary>
    private const float AboveLine = 0.15F;

    /// <summary>The share of a box's height its text may take.</summary>
    private const float BoxFill = 0.8F;

    /// <summary>The share of a comb box's width a character may take, as a text size.</summary>
    private const float CellFill = 1.1F;

    /// <summary>Reads the places to write on pages.</summary>
    private readonly FlatFormFinder _finder = new();

    /// <summary>The places found on each page.</summary>
    private readonly Dictionary<int, FormRegion[]> _regions = [];

    /// <summary>The pages being read.</summary>
    private readonly HashSet<int> _readingRegions = [];

    /// <summary>The document the places were found in.</summary>
    private IDocument? _regionsDocument;

    /// <summary>Gets a number that changes when places to write are found, so the pages can be drawn again.</summary>
    [Reactive]
    public partial int RegionsVersion { get; private set; }

    /// <summary>
    /// Gets the places to write found on a page, starting to read the page when it has not been read. Forms with
    /// fillable fields are filled through their fields, so they are not read.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The places, or an empty list while the page is being read.</returns>
    public FormRegion[] RegionsOn(int page)
    {
        if (_owner.TryGetDocument() is not { } document || _owner.Forms.HasForm)
        {
            return [];
        }

        if (!ReferenceEquals(document, _regionsDocument))
        {
            _regions.Clear();
            _readingRegions.Clear();
            _regionsDocument = document;
        }

        if (_regions.TryGetValue(page, out var found))
        {
            return found;
        }

        if (_readingRegions.Add(page))
        {
            _ = ReadRegionsAsync(document, page);
        }

        return [];
    }

    /// <summary>Finds the place to write under a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point.</param>
    /// <returns>The place, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FormRegion? RegionAt(int page, PagePoint point) => FormRegion.At(RegionsOn(page), point);

    /// <summary>Starts typing fitted to a place to write: the text size fits it, and a comb takes one character per box.</summary>
    /// <param name="page">The page.</param>
    /// <param name="region">The place.</param>
    /// <returns><see langword="true"/> when typing started.</returns>
    public bool BeginInRegion(int page, FormRegion region)
    {
        var bounds = region.Bounds;
        var fit = region.Kind switch
        {
            FormRegionKind.Comb => Math.Min(bounds.Height * BoxFill / LineSpacing, bounds.Width / region.Cells * CellFill),
            FormRegionKind.Box => (bounds.Height - BothPaddings) / LineSpacing,
            _ => bounds.Height / LineSpacing,
        };
        var size = Math.Clamp(Math.Min(FontSize, fit), TextFormat.MinFontSize, TextFormat.MaxFontSize);
        var lineHeight = size * LineSpacing;
        _loadingFormat = true;
        try
        {
            FontSize = size;
            CombCells = region.Kind == FormRegionKind.Comb ? region.Cells : 0;
        }
        finally
        {
            _loadingFormat = false;
        }

        return region.Kind switch
        {
            FormRegionKind.Comb => BeginTextAt(page, new(bounds.Left, bounds.Top + ((bounds.Height - lineHeight) * Half)), bounds.Width),
            FormRegionKind.Box => BeginTextAt(page, new(bounds.Left + BoxPadding, bounds.Top + ((bounds.Height - lineHeight) * Half)), bounds.Width - BothPaddings),
            _ => BeginTextAt(page, new(bounds.Left, bounds.Bottom - lineHeight - (size * AboveLine)), bounds.Width),
        };
    }

    /// <summary>Reads a page's places to write off the UI thread, then offers them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>A task.</returns>
    private async Task ReadRegionsAsync(IDocument document, int page)
    {
        await document.PreparePageAsync(page, CancellationToken.None).ConfigureAwait(true);
        var found = await Task.Run(() =>
        {
            // Captures the finder and the page: the read runs once per page, off the UI thread.
            var output = new List<FormRegion>();
            _finder.Find(document, page, output);
            return output.ToArray();
        }).ConfigureAwait(true);
        if (!ReferenceEquals(document, _regionsDocument))
        {
            return;
        }

        _regions[page] = found;
        RegionsVersion++;
    }
}
