// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A text device that records the codes each font dictionary shows, and whether it shows any visibly. PDF/A exempts text
/// drawn in render mode 3, which is invisible, from the font embedding and glyph rules (ISO 19005-2 §6.2.11.4.1).
/// </summary>
[DebuggerDisplay("PdfAConversionTextDevice: {Fonts.Count} fonts")]
internal sealed class PdfAConversionTextDevice : ITextObjectDevice
{
    /// <summary>The render mode that draws nothing.</summary>
    private const int Invisible = 3;

    /// <summary>Gets the codes each font dictionary shows.</summary>
    internal Dictionary<PdfDictionary, PdfAConversionTextCodes> Fonts { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        ref var codes = ref CollectionsMarshal.GetValueRefOrAddDefault(Fonts, glyph.Font.Dictionary, out _);
        codes ??= new();
        if (state.RenderMode == Invisible)
        {
            codes.Invisible = true;
            return;
        }

        _ = codes.Visible.Add(glyph.Code);
    }

    /// <inheritdoc/>
    public void BeginTextObject()
    {
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
}
