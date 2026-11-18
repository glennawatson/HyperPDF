// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Icons;

/// <summary>
/// Draws the window's icons. Each icon is drawn once for a size and a colour into a surface of the render backend and kept, so a
/// frame only places surfaces it already has. Everything here runs on the window thread.
/// </summary>
[DebuggerDisplay("IconRuns: {_icons.Count} kept")]
public sealed class IconRuns : IDisposable
{
    /// <summary>The icons kept before all are dropped. The window shows a small, steady set, so this is rarely reached.</summary>
    private const int Capacity = 256;

    /// <summary>The thinnest a stroke gets in pixels, so a small icon keeps solid lines.</summary>
    private const float ThinnestStroke = 1.25F;

    /// <summary>Half, for half the weight of a stroke.</summary>
    private const float Half = 0.5F;

    /// <summary>The icons drawn so far.</summary>
    private readonly Dictionary<IconKey, IRenderSurface> _icons = [with(Capacity)];

    /// <summary>The coverage buffer every icon is drawn through.</summary>
    private readonly CoverageBuffer _coverage = new();

    /// <summary>The surfaces of the render backend.</summary>
    private readonly IRenderSurfaceFactory _surfaces;

    /// <summary>Initializes a new instance of the <see cref="IconRuns"/> class.</summary>
    /// <param name="surfaces">The surfaces of the render backend that draws the window.</param>
    public IconRuns(IRenderSurfaceFactory surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        _surfaces = surfaces;
    }

    /// <summary>Gets an icon as a square surface, drawing it the first time it is asked for.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="size">The side of the square in device pixels.</param>
    /// <param name="color">The colour as ARGB.</param>
    /// <returns>The drawn icon, or <see langword="null"/> for no icon or no size.</returns>
    public IRenderSurface? Get(ChromeIcon icon, int size, uint color)
    {
        if (icon == ChromeIcon.None || size <= 0)
        {
            return null;
        }

        var key = new IconKey(icon, size, color);
        if (_icons.TryGetValue(key, out var kept))
        {
            return kept;
        }

        if (_icons.Count >= Capacity)
        {
            Clear();
        }

        Cover(_coverage, icon, size);
        var surface = _surfaces.Create(size, size);
        _ = surface.Write(new TextInk(_coverage, color), TextRuns.WriteInk);
        _icons.Add(key, surface);
        return surface;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Clear();

    /// <summary>Strokes an icon into a coverage buffer of its own size.</summary>
    /// <param name="coverage">The buffer, which is cleared first.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="size">The side of the icon in device pixels.</param>
    internal static void Cover(CoverageBuffer coverage, ChromeIcon icon, int size)
    {
        coverage.Reset(size, size);
        var scale = size / IconPaths.Grid;
        var pen = new IconStroker(coverage, scale, 0F, MathF.Max(ThinnestStroke, IconPaths.Stroke * scale) * Half);
        IconPathReader.Draw(IconPaths.Of(icon), ref pen);
    }

    /// <summary>Frees every surface kept.</summary>
    private void Clear()
    {
        foreach (var kept in _icons)
        {
            kept.Value.Dispose();
        }

        _icons.Clear();
    }
}
