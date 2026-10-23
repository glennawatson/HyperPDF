// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A figure, such as an image, chart or barcode, placed whole on a page; it shrinks to fit the text area when it is too big.</summary>
[DebuggerDisplay("FigureFlow")]
internal sealed class FigureFlow : Flowable
{
    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The document and its styles.</summary>
    private readonly LayoutEnv _env;

    /// <summary>What the figure needs.</summary>
    private readonly FigureSpec _spec;

    /// <summary>The indent from the left edge of the text area.</summary>
    private readonly float _indent;

    /// <summary>The structure node the figure's node goes under, or <see langword="null"/> for the document.</summary>
    private readonly StructNode? _parent;

    /// <summary>Initializes a new instance of the <see cref="FigureFlow"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="spec">What the figure needs.</param>
    /// <param name="indent">The indent from the left edge of the text area.</param>
    /// <param name="parent">The structure node the figure's node goes under, or <see langword="null"/> for the document.</param>
    internal FigureFlow(LayoutEnv env, FigureSpec spec, float indent, StructNode? parent)
    {
        _env = env;
        _spec = spec;
        _indent = indent;
        _parent = parent;
    }

    /// <inheritdoc/>
    internal override float SpaceBefore => _spec.SpaceBefore;

    /// <inheritdoc/>
    internal override float SpaceAfter => _spec.SpaceAfter;

    /// <inheritdoc/>
    internal override float MeasureStart(FlowCursor cursor) => Resolve(cursor).Height;

    /// <inheritdoc/>
    internal override PlaceResult Place(FlowCursor cursor)
    {
        var size = Resolve(cursor);
        if (size.Height > cursor.Remaining && !cursor.AtTop)
        {
            return PlaceResult.NeedsPage;
        }

        var available = cursor.Width - _indent;
        var x = cursor.Left + _indent + _spec.Alignment switch
        {
            PdfTextAlignment.Center => (available - size.Width) * Half,
            PdfTextAlignment.Right => available - size.Width,
            _ => 0F,
        };
        var decorative = _spec.AlternativeText is null;
        var node = decorative ? null : CreateNode();
        cursor.Page.Commands.Add(_spec.Create(new(x, cursor.Y - size.Height, size.Width, size.Height), node, cursor.Artifact));
        cursor.Y -= size.Height;
        return PlaceResult.Finished;
    }

    /// <summary>Makes the figure's structure node when the document is tagged.</summary>
    /// <returns>The node, or <see langword="null"/>.</returns>
    private StructNode? CreateNode() => _env.Tagged ? new("Figure", _parent, PdfTagOptions.Default with { AlternativeText = _spec.AlternativeText }) : null;

    /// <summary>Works out the size of the figure: the size asked for, or its natural size, shrunk to fit the text area and the page.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <returns>The width and height in points.</returns>
    private FigureSize Resolve(FlowCursor cursor)
    {
        var available = cursor.Width - _indent;
        var (width, height) = GetRequested(available);
        var scale = MathF.Min(1F, MathF.Min(available / width, (cursor.Top - cursor.BottomBase) / height));
        return new(width * scale, height * scale);
    }

    /// <summary>Works out the width and height before shrinking.</summary>
    /// <param name="available">The width of the text area less the indent.</param>
    /// <returns>The width and height in points.</returns>
    private FigureSize GetRequested(float available)
    {
        var naturalWidth = _spec.NaturalWidth > 0F ? _spec.NaturalWidth : available;
        var ratio = _spec.NaturalHeight > 0F ? _spec.NaturalHeight / naturalWidth : 0F;
        var width = _spec.Width ?? (_spec.Height is { } h && ratio > 0F ? h / ratio : naturalWidth);
        var height = _spec.Height ?? (ratio > 0F ? width * ratio : width);
        return new(width, height);
    }
}
