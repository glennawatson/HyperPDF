// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A grid placed row group by row group across pages, with its header rows repeated as artifacts on every page it continues on.</summary>
[DebuggerDisplay("GridFlow: next group {_next}")]
internal sealed class GridFlow : Flowable
{
    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The document and its styles.</summary>
    private readonly LayoutEnv _env;

    /// <summary>The grid.</summary>
    private readonly PdfGrid _grid;

    /// <summary>Where the grid is: its indent and structure parent.</summary>
    private readonly FlattenScope _scope;

    /// <summary>The grid laid out, once its width is known.</summary>
    private GridLayout? _layout;

    /// <summary>The header rows laid out as artifacts for repeating, or <see langword="null"/> when there are none or they are not needed yet.</summary>
    private GridLayout? _repeat;

    /// <summary>The widths of the columns.</summary>
    private float[] _widths = [];

    /// <summary>The number of groups of rows that are headers.</summary>
    private int _headerGroups;

    /// <summary>The next group of rows to place.</summary>
    private int _next;

    /// <summary>The page the last group was placed on.</summary>
    private PagePlan? _page;

    /// <summary>The number of groups placed on the current page, counting header groups.</summary>
    private int _placedOnPage;

    /// <summary>Whether a repeated header was drawn on the current page.</summary>
    private bool _headerDrawn;

    /// <summary>Initializes a new instance of the <see cref="GridFlow"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="grid">The grid.</param>
    /// <param name="scope">Where the grid is: its indent and structure parent.</param>
    internal GridFlow(LayoutEnv env, PdfGrid grid, FlattenScope scope)
    {
        _env = env;
        _grid = grid;
        _scope = scope;
    }

    /// <inheritdoc/>
    internal override float SpaceBefore => _grid.SpaceBefore;

    /// <inheritdoc/>
    internal override float SpaceAfter => _grid.SpaceAfter;

    /// <inheritdoc/>
    internal override float MeasureStart(FlowCursor cursor)
    {
        var layout = EnsureLayout(cursor);
        var height = 0F;
        for (var i = 0; i <= Math.Min(_headerGroups, layout.Groups.Count - 1); i++)
        {
            height += layout.Groups[i].Height;
        }

        return height;
    }

    /// <inheritdoc/>
    internal override PlaceResult Place(FlowCursor cursor)
    {
        var layout = EnsureLayout(cursor);
        if (!ReferenceEquals(_page, cursor.Page))
        {
            StartPage(cursor, layout);
        }

        while (_next < layout.Groups.Count)
        {
            var group = layout.Groups[_next];
            var canForce = _placedOnPage == 0 && (cursor.AtTop || _headerDrawn);
            if (group.Height > cursor.Remaining && !canForce)
            {
                return PlaceResult.NeedsPage;
            }

            GridPainter.Draw(cursor.Page.Commands, layout, group, GetLeft(cursor, layout), cursor.Y, cursor.Artifact);
            cursor.Y -= group.Height;
            _next++;
            _placedOnPage++;
        }

        return PlaceResult.Finished;
    }

    /// <summary>Lays the grid out the first time it is needed.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <returns>The layout.</returns>
    private GridLayout EnsureLayout(FlowCursor cursor)
    {
        if (_layout is { } known)
        {
            return known;
        }

        var available = MathF.Min(_grid.Width ?? cursor.Width, cursor.Width - _scope.Indent);
        _widths = GridColumns.Resolve(_grid.Columns, available);
        var options = PdfTagOptions.Default with { AlternativeText = _grid.Summary };
        var tableNode = _env.CreateNode("Table", _scope.Parent, options);
        var layout = GridLayout.Build(_env, _grid, _widths, _grid.Rows.Count, tableNode, cursor.Artifact);
        _headerGroups = CountHeaderGroups(layout);
        _layout = layout;
        return layout;
    }

    /// <summary>Counts the groups of rows that make up the header.</summary>
    /// <param name="layout">The laid out grid.</param>
    /// <returns>The number of groups.</returns>
    private int CountHeaderGroups(GridLayout layout)
    {
        var count = 0;
        while (count < layout.Groups.Count && layout.Groups[count].Start < _grid.HeaderRows)
        {
            count++;
        }

        return count;
    }

    /// <summary>Gets the x of the table's left edge, after the indent and alignment.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="layout">The laid out grid.</param>
    /// <returns>The x.</returns>
    private float GetLeft(FlowCursor cursor, GridLayout layout)
    {
        var spare = cursor.Width - _scope.Indent - layout.Width;
        var shift = _grid.Alignment switch
        {
            PdfTextAlignment.Center => spare * Half,
            PdfTextAlignment.Right => spare,
            _ => 0F,
        };
        return cursor.Left + _scope.Indent + MathF.Max(0F, shift);
    }

    /// <summary>Starts a page: repeats the header rows when the table continues from an earlier page.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="layout">The laid out grid.</param>
    private void StartPage(FlowCursor cursor, GridLayout layout)
    {
        _page = cursor.Page;
        _placedOnPage = 0;
        _headerDrawn = false;
        if (_next == 0 || _headerGroups == 0 || _next < _headerGroups)
        {
            return;
        }

        _repeat ??= GridLayout.Build(_env.WithoutTags(), _grid, _widths, _grid.HeaderRows, null, PdfArtifactKind.Pagination);
        foreach (var group in _repeat.Groups)
        {
            GridPainter.Draw(cursor.Page.Commands, _repeat, group, GetLeft(cursor, layout), cursor.Y, PdfArtifactKind.Pagination);
            cursor.Y -= group.Height;
        }

        _headerDrawn = true;
    }
}
