// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Drawing;

/// <summary>A drawing session for one calling thread that borrows target pixels only until each draw call returns.</summary>
public interface IPdfDrawingSession
{
    /// <summary>Draws recorded page content into the caller's target buffer.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="annotations">The optional annotation recording.</param>
    /// <param name="matrix">The page transform.</param>
    /// <param name="target">The caller-owned raster target, which is borrowed only until this call returns.</param>
    /// <param name="grayscale">Whether to convert to grayscale.</param>
    /// <returns>Whether the target was drawn.</returns>
    bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, PdfTileTarget target, bool grayscale);

    /// <summary>Submits recorded page content to a caller-owned target on its graphics thread.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="annotations">The optional annotation recording.</param>
    /// <param name="matrix">The page transform.</param>
    /// <param name="target">The target, retained by the caller through GPU completion and presentation.</param>
    /// <param name="grayscale">Whether to convert to grayscale.</param>
    /// <returns>Whether drawing was submitted.</returns>
    bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, IPdfRenderTarget target, bool grayscale);

    /// <summary>Draws an image fitted to the caller's target buffer.</summary>
    /// <param name="image">The image.</param>
    /// <param name="target">The caller-owned raster target, which is borrowed only until this call returns.</param>
    /// <returns>Whether the target was drawn.</returns>
    bool DrawImage(IPdfRenderImage image, PdfTileTarget target);
}
