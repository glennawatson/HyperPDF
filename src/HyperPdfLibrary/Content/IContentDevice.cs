// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>
/// What the content interpreter draws on. Paths and matrices are in user space; each call that paints receives the
/// graphics state in force, with the current transformation matrix, colours, alpha, blend mode and soft mask.
/// </summary>
public interface IContentDevice
{
    /// <summary>
    /// Gets a value indicating whether the device may keep a path after the call that received it returns. The default is
    /// <see langword="true"/>, and the interpreter then gives each call its own copy. A device that only reads the path
    /// during the call returns <see langword="false"/>, and the interpreter then passes one reused path that is valid
    /// only until the call returns.
    /// </summary>
    bool RetainsPaths => true;

    /// <summary>
    /// Gets a value indicating whether the device reads the pixels of the images it draws. The default is
    /// <see langword="true"/>. A device that only needs where an image lands returns <see langword="false"/>, and the
    /// interpreter then calls <see cref="DrawImageArea"/> instead of decoding the image and calling <see cref="DrawImage"/>.
    /// </summary>
    bool ReadsImagePixels => true;

    /// <summary>Saves the clip, as <c>q</c> does.</summary>
    void Save();

    /// <summary>Restores the clip saved by the matching <see cref="Save"/>.</summary>
    void Restore();

    /// <summary>Fills a path with the state's fill colour or pattern.</summary>
    /// <param name="path">The immutable path in user space.</param>
    /// <param name="evenOdd">Whether the even-odd rule applies.</param>
    /// <param name="state">The graphics state.</param>
    void Fill(PdfPath path, bool evenOdd, ref GraphicsState state);

    /// <summary>Strokes a path with the state's stroke colour or pattern and line style.</summary>
    /// <param name="path">The path in user space.</param>
    /// <param name="state">The graphics state.</param>
    void Stroke(PdfPath path, ref GraphicsState state);

    /// <summary>Intersects the clip with a path.</summary>
    /// <param name="path">The path in user space.</param>
    /// <param name="evenOdd">Whether the even-odd rule applies.</param>
    /// <param name="ctm">The matrix from user space to the page.</param>
    void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm);

    /// <summary>Draws an image into the unit square of user space; stencil masks use the fill colour.</summary>
    /// <param name="image">The borrowed image, as premultiplied BGRA, opaque gray or stencil coverage.</param>
    /// <param name="isMask">Whether the image is a stencil mask painted with the fill colour.</param>
    /// <param name="smooth">Whether the image asks for smoothing when scaled up.</param>
    /// <param name="state">The graphics state.</param>
    void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state);

    /// <summary>
    /// Reports an image drawn into the unit square of user space, without its pixels. The interpreter calls this instead of
    /// <see cref="DrawImage"/> for a device whose <see cref="ReadsImagePixels"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="state">The graphics state.</param>
    void DrawImageArea(ref GraphicsState state)
    {
        // A device that reads pixels receives DrawImage instead and has nothing to do here.
    }

    /// <summary>Reports one shown glyph; devices that draw fill, stroke or clip it according to the state's text render mode.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="state">The graphics state.</param>
    void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state);

    /// <summary>Paints a shading over the whole clip, as <c>sh</c> does.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="state">The graphics state.</param>
    void PaintShading(PdfShading shading, ref GraphicsState state);

    /// <summary>Starts a transparency group that is composited as one object when it ends.</summary>
    /// <param name="group">The group.</param>
    void BeginGroup(in GroupInfo group);

    /// <summary>Ends the group started by the matching <see cref="BeginGroup"/>.</summary>
    /// <param name="group">The same group.</param>
    void EndGroup(in GroupInfo group);

    /// <summary>Starts marked content.</summary>
    /// <param name="tag">The tag's bytes.</param>
    /// <param name="properties">The property list, or null.</param>
    void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties);

    /// <summary>Ends marked content.</summary>
    void EndMarkedContent();

    /// <summary>Starts recording a separate picture, for a pattern cell or soft mask group.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <returns>A device that records into the picture.</returns>
    IPictureDevice CreatePictureDevice(PdfRect cull);
}
