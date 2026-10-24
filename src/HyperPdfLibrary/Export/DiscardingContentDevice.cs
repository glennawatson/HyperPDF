// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Export;

/// <summary>A device that draws nothing. A vector writer hands it to the interpreter for the recordings it cannot use, such as a pattern cell.</summary>
[DebuggerDisplay("DiscardingContentDevice")]
internal sealed class DiscardingContentDevice : IPictureDevice
{
    /// <inheritdoc/>
    public bool RetainsPaths => false;

    /// <inheritdoc/>
    public bool ReadsImagePixels => false;

    /// <inheritdoc/>
    public PictureWeight Weight { get; } = new();

    /// <inheritdoc/>
    public void Save()
    {
        // Nothing is drawn, so there is no state to save.
    }

    /// <inheritdoc/>
    public void Restore()
    {
        // See Save.
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        // Nothing is drawn.
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
        // Nothing is drawn.
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
        // Nothing is drawn, so there is nothing to clip.
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        // Nothing is drawn.
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        // Nothing is drawn.
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        // Nothing is drawn.
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
        // Nothing is drawn, so a group has nothing to composite.
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
        // See BeginGroup.
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        // Marked content has no visual effect.
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        // See BeginMarkedContent.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect cull) => new DiscardingContentDevice();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderPicture Finish() => new PdfEmptyPicture();

    /// <inheritdoc/>
    public void Dispose()
    {
        // There is nothing to release.
    }
}
