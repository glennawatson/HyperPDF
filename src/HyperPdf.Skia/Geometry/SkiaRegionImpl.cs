// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Platform region implementation wrapping an <see cref = "SKRegion"/>.</summary>
internal sealed class SkiaRegionImpl : IPlatformRenderInterfaceRegion
{
    /// <summary>The underlying Skia region.</summary>
    private SKRegion? _region = new();

    /// <summary>Whether the cached rectangle list is valid.</summary>
    private bool _rectsValid;

    /// <summary>Gets the Skia region.</summary>
    public SKRegion Region => _region ?? throw new ObjectDisposedException(nameof(SkiaRegionImpl));

    /// <inheritdoc/>
    public bool IsEmpty => Region.IsEmpty;

    /// <inheritdoc/>
    public LtrbPixelRect Bounds => Region.Bounds.ToAvaloniaLtrbPixelRect();

    /// <inheritdoc/>
    public IList<LtrbPixelRect> Rects
    {
        get
        {
            field ??= [];
            if (!_rectsValid)
            {
                field.Clear();
                using var iter = Region.CreateRectIterator();
                while (iter.Next(out var rc))
                {
                    field.Add(rc.ToAvaloniaLtrbPixelRect());
                }

                _rectsValid = true;
            }

            return field;
        }
    }

    /// <inheritdoc/>
    public void AddRect(LtrbPixelRect rect)
    {
        _rectsValid = false;
        _ = Region.Op(rect.Left, rect.Top, rect.Right, rect.Bottom, SKRegionOperation.Union);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _rectsValid = false;
        Region.SetEmpty();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(LtrbRect rect) => Region.Intersects(new SKRectI((int)rect.Left, (int)rect.Top, (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom)));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Point pt) => Region.Contains((int)pt.X, (int)pt.Y);

    /// <inheritdoc/>
    public void Dispose()
    {
        _region?.Dispose();
        _region = null;
    }
}
