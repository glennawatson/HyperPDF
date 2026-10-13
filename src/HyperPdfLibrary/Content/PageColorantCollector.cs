// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Content;

/// <summary>A device that draws nothing and records which colorants the content paints.</summary>
[DebuggerDisplay("PageColorantCollector: {_order.Count} colorants")]
internal sealed class PageColorantCollector : IContentDevice, IColorantSink
{
    /// <summary>The kind of each colorant seen.</summary>
    private readonly Dictionary<string, PdfColorantKind> _kinds = [with(StringComparer.Ordinal)];

    /// <summary>The colorant names in the order they were first painted.</summary>
    private readonly List<string> _order = [];

    /// <inheritdoc/>
    public bool RetainsPaths => false;

    /// <inheritdoc/>
    public void Painted(PdfColorSpace space, Vector4 components, bool exact)
    {
        if (ContentColorants.Names(space) is not { } names)
        {
            return;
        }

        var light = ContentColorants.IsLight(space);
        for (var i = 0; i < names.Count; i++)
        {
            if (names[i] != ContentColorants.None && (!exact || PageColorantCollector.HasInk(components, i, light)))
            {
                Add(space, names[i]);
            }
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
    }

    /// <inheritdoc/>
    public void Restore()
    {
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect cull) => new NullPictureDevice(cull);

    /// <summary>Gets the colorants seen: the process colorants in Cyan, Magenta, Yellow, Black order, then the rest in the order they were first painted.</summary>
    /// <returns>The colorants.</returns>
    internal ImmutableArray<PdfColorant> Result()
    {
        var builder = ImmutableArray.CreateBuilder<PdfColorant>(_order.Count);
        foreach (var process in new[] { ContentColorants.Cyan, ContentColorants.Magenta, ContentColorants.Yellow, ContentColorants.Black })
        {
            if (_kinds.TryGetValue(process, out var kind) && kind == PdfColorantKind.Process)
            {
                builder.Add(new(process, kind));
            }
        }

        foreach (var name in _order)
        {
            if (_kinds[name] != PdfColorantKind.Process)
            {
                builder.Add(new(name, _kinds[name]));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Determines whether a component puts colorant on the page; components past the four kept always do.</summary>
    /// <param name="components">The first four components.</param>
    /// <param name="index">The component's position.</param>
    /// <param name="light">Whether the space measures light, so that 1 is no colorant.</param>
    /// <returns><see langword="true"/> when the component paints its colorant.</returns>
    private static bool HasInk(Vector4 components, int index, bool light)
    {
        if (index >= ContentColorants.KeptComponents)
        {
            return true;
        }

        var value = components[index];
        return light ? value < 1 : value > 0;
    }

    /// <summary>Classifies a colorant.</summary>
    /// <param name="space">The colour space that painted it.</param>
    /// <param name="name">The colorant's name.</param>
    /// <returns>The kind.</returns>
    private static PdfColorantKind KindOf(PdfColorSpace space, string name)
    {
        if (name == ContentColorants.All)
        {
            return PdfColorantKind.All;
        }

        var process = space.ColorantNames is null || name is ContentColorants.Cyan or ContentColorants.Magenta or ContentColorants.Yellow or ContentColorants.Black;
        return process ? PdfColorantKind.Process : PdfColorantKind.Spot;
    }

    /// <summary>Records a colorant the first time it is painted.</summary>
    /// <param name="space">The colour space that painted it.</param>
    /// <param name="name">The colorant's name.</param>
    private void Add(PdfColorSpace space, string name)
    {
        if (_kinds.ContainsKey(name))
        {
            return;
        }

        _kinds.Add(name, PageColorantCollector.KindOf(space, name));
        _order.Add(name);
    }
}
