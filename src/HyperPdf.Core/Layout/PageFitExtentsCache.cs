// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>
/// Keeps the <see cref="PageFitExtents"/> of one document so a viewport resize finds the fit zoom without walking every page.
/// The owner calls <see cref="Invalidate"/> when the page structure changes; a different size array, rotation or arrangement is noticed here.
/// </summary>
[DebuggerDisplay("PageFitExtentsCache: {_extents}")]
public sealed class PageFitExtentsCache
{
    /// <summary>The page sizes the cached extents were measured from.</summary>
    private PageSize[]? _sizes;

    /// <summary>The rotation the cached extents were measured with.</summary>
    private PageRotation _rotation;

    /// <summary>The arrangement the cached extents were measured with.</summary>
    private PageLayoutMode _mode;

    /// <summary>The cached extents.</summary>
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

        var extents = ZoomCalculator.GetFitExtents(sizes, rotation, mode);
        _sizes = sizes;
        _rotation = rotation;
        _mode = mode;
        _extents = extents;
        return extents;
    }

    /// <summary>Forgets the cached extents.</summary>
    public void Invalidate()
    {
        _sizes = null;
        _extents = null;
    }
}
