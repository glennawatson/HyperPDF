// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>
/// Keeps the <see cref="PageFitExtents"/> of the documents a canvas shows, so a viewport resize, and coming back to a tab, find
/// the fit zoom without walking every page. The extents of a document are held only while its page sizes are, so they go when
/// the tab does. The owner calls <see cref="Invalidate"/> when the page structure changes; a different rotation or arrangement
/// is noticed here.
/// </summary>
[DebuggerDisplay("PageFitExtentsCache: {_extents}")]
public sealed class PageFitExtentsCache
{
    /// <summary>The extents last measured for each document's pages.</summary>
    private readonly ConditionalWeakTable<PageSize[], Measured> _kept = [];

    /// <summary>The page sizes the current extents were measured from.</summary>
    private PageSize[]? _sizes;

    /// <summary>The rotation the current extents were measured with.</summary>
    private PageRotation _rotation;

    /// <summary>The arrangement the current extents were measured with.</summary>
    private PageLayoutMode _mode;

    /// <summary>The current extents.</summary>
    private PageFitExtents? _extents;

    /// <summary>Gets the extents for the given pages, measuring them only when the cache does not hold them.</summary>
    /// <param name="sizes">The unrotated page sizes.</param>
    /// <param name="rotation">The page rotation.</param>
    /// <param name="mode">The page arrangement.</param>
    /// <returns>The extents.</returns>
    public PageFitExtents Get(PageSize[] sizes, PageRotation rotation, PageLayoutMode mode)
    {
        if (_extents is { } cached && ReferenceEquals(_sizes, sizes) && _rotation == rotation && _mode == mode && cached.PageCount == sizes.Length)
        {
            return cached;
        }

        // The pages of a tab shown before were measured then, and are measured again only when turned or arranged another way.
        if (!_kept.TryGetValue(sizes, out var measured) || measured.Rotation != rotation || measured.Mode != mode || measured.Extents.PageCount != sizes.Length)
        {
            measured = new(rotation, mode, ZoomCalculator.GetFitExtents(sizes, rotation, mode));
            _kept.AddOrUpdate(sizes, measured);
        }

        _sizes = sizes;
        _rotation = rotation;
        _mode = mode;
        _extents = measured.Extents;
        return measured.Extents;
    }

    /// <summary>Forgets the extents of the pages last asked for.</summary>
    public void Invalidate()
    {
        if (_sizes is { } sizes)
        {
            _ = _kept.Remove(sizes);
        }

        _sizes = null;
        _extents = null;
    }

    /// <summary>The extents of a document's pages and what they were measured with.</summary>
    /// <param name="Rotation">The rotation.</param>
    /// <param name="Mode">The arrangement.</param>
    /// <param name="Extents">The extents.</param>
    [DebuggerDisplay("Measured: {Rotation} {Mode}")]
    private sealed record Measured(PageRotation Rotation, PageLayoutMode Mode, PageFitExtents Extents);
}
