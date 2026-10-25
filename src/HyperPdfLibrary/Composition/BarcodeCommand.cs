// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Barcodes;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Draws a barcode to fit a rectangle.</summary>
/// <param name="Node">The structure node.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Barcode">The barcode.</param>
/// <param name="Style">The barcode's colours.</param>
/// <param name="Box">The rectangle.</param>
internal sealed record BarcodeCommand(StructNode? Node, PdfArtifactKind Artifact, PdfBarcode Barcode, PdfBarcodeStyle Style, PdfCanvasBox Box) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state) => canvas.DrawBarcode(Barcode, Style, Box);

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { Box = Box with { X = Box.X + dx, Y = Box.Y + dy } };
}
