// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Drawing;

/// <summary>Creates owned drawing resources and reusable caller-buffer sessions.</summary>
public interface IPdfRenderBackend
{
    /// <summary>Gets a drawing session isolated to the calling thread.</summary>
    /// <returns>The reusable session.</returns>
    IPdfDrawingSession GetDrawingSession();

    /// <summary>Creates a content recording device.</summary>
    /// <param name="bounds">The recording bounds.</param>
    /// <returns>The owned recorder.</returns>
    IPictureDevice CreatePictureDevice(PdfRect bounds);

    /// <summary>Creates a content recording device for a picture shown in a known way.</summary>
    /// <param name="bounds">The recording bounds.</param>
    /// <param name="style">How the picture will be shown.</param>
    /// <returns>The owned recorder.</returns>
    IPictureDevice CreatePictureDevice(PdfRect bounds, in PdfPictureStyle style) => CreatePictureDevice(bounds);

    /// <summary>Creates an image from decoded PDF pixels.</summary>
    /// <param name="data">The decoded pixels.</param>
    /// <returns>The owned image, or null for invalid input.</returns>
    IPdfRenderImage? CreateImage(PdfImageData data);

    /// <summary>Copies raster pixels into an owned image.</summary>
    /// <param name="layout">The raster layout.</param>
    /// <param name="pixels">The borrowed pixels, copied before this method returns.</param>
    /// <param name="rowBytes">The bytes in each row.</param>
    /// <returns>The owned image.</returns>
    IPdfRenderImage CreateImage(PdfImagePixelLayout layout, ReadOnlySpan<byte> pixels, int rowBytes);

    /// <summary>Creates a linear gradient.</summary>
    /// <param name="start">The start point.</param>
    /// <param name="end">The end point.</param>
    /// <param name="colors">The stop colors.</param>
    /// <param name="positions">The stop positions.</param>
    /// <param name="mode">The edge behavior.</param>
    /// <returns>The owned shader.</returns>
    IPdfRenderShader CreateLinearGradient(PdfPoint start, PdfPoint end, ReadOnlySpan<PdfColor> colors, ReadOnlySpan<float> positions, PdfShaderTileMode mode);

    /// <summary>Creates a radial gradient between two circles.</summary>
    /// <param name="start">The first center.</param>
    /// <param name="startRadius">The first radius.</param>
    /// <param name="end">The second center.</param>
    /// <param name="endRadius">The second radius.</param>
    /// <param name="colors">The stop colors.</param>
    /// <param name="positions">The stop positions.</param>
    /// <param name="mode">The edge behavior.</param>
    /// <returns>The owned shader.</returns>
    IPdfRenderShader CreateTwoPointConicalGradient(
        PdfPoint start,
        float startRadius,
        PdfPoint end,
        float endRadius,
        ReadOnlySpan<PdfColor> colors,
        ReadOnlySpan<float> positions,
        PdfShaderTileMode mode);

    /// <summary>Creates an image shader.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="x">The horizontal edge behavior.</param>
    /// <param name="y">The vertical edge behavior.</param>
    /// <param name="linear">Whether to interpolate.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned shader.</returns>
    IPdfRenderShader CreateImageShader(IPdfRenderImage image, PdfShaderTileMode x, PdfShaderTileMode y, bool linear, Matrix3x2 matrix);

    /// <summary>Creates colored triangles.</summary>
    /// <param name="positions">The vertex positions.</param>
    /// <param name="colors">The vertex colors.</param>
    /// <returns>The owned mesh.</returns>
    IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfColor[] colors);

    /// <summary>Creates textured triangles.</summary>
    /// <param name="positions">The vertex positions.</param>
    /// <param name="texture">The texture coordinates.</param>
    /// <param name="colors">The optional vertex colors.</param>
    /// <returns>The owned mesh.</returns>
    IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfPoint[] texture, PdfColor[]? colors);

    /// <summary>Composes a repeated pattern period.</summary>
    /// <param name="cell">The recorded cell.</param>
    /// <param name="box">The cell bounds.</param>
    /// <param name="stepX">The horizontal step.</param>
    /// <param name="stepY">The vertical step.</param>
    /// <param name="scale">The drawing scale.</param>
    /// <returns>The owned period resources.</returns>
    PatternCell ComposeTile(IPdfRenderPicture cell, PdfRectangle box, float stepX, float stepY, float scale);
}
