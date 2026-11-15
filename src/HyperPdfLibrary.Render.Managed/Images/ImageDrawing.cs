// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Render.Managed.Pictures;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Render.Managed.Images;

/// <summary>Draws an image step of a picture: the image fills the unit square of the step's matrix, top line at the top.</summary>
internal static class ImageDrawing
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>Draws an image.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="image">The image.</param>
    /// <param name="matrix">The matrix from the image's unit square to device pixels.</param>
    /// <param name="op">The step, which holds the colour, the alpha and the smoothing.</param>
    internal static void Draw(RasterCanvas canvas, ManagedRenderImage image, Matrix3x2 matrix, in PictureOp op)
    {
        // An image's first line is at the top of the unit square, whose y runs upwards.
        var pixelToDevice = new Matrix3x2(1F / image.Width, 0, 0, -1F / image.Height, 0, 1) * matrix;
        if (!Matrix3x2.Invert(pixelToDevice, out var toImage))
        {
            return;
        }

        canvas.Images.Set(image, toImage, op.Color, (uint)MathF.Round(Math.Clamp(op.Alpha, 0F, 1F) * Full), op.Flag);
        canvas.FillUnitSquare(matrix, new(0, RasterShaderKind.Image) { Blend = op.Blend });
    }
}
