// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Drawing;

/// <summary>Checks caller-buffer layout validation without using a drawing backend.</summary>
public sealed class PdfTileTargetTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The width of a small test tile.</summary>
    private const int Width = 2;

    /// <summary>The height of a small test tile.</summary>
    private const int Height = 2;

    /// <summary>The stride of a tightly packed test row.</summary>
    private const int Stride = Width * BytesPerPixel;

    /// <summary>The number of bytes in a valid two-row tile.</summary>
    private const int ValidPixelBytes = ((Height - 1) * Stride) + (Width * BytesPerPixel);

    /// <summary>An unrepresentable row width.</summary>
    private const int LargestWidth = int.MaxValue;

    /// <summary>Rejects a row whose byte width overflows an integer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsValidRejectsOverflowedRowWidth()
    {
        var isValid = new PdfTileTarget(new byte[1], LargestWidth, 1, int.MaxValue).IsValid;

        await Assert.That(isValid).IsFalse();
    }

    /// <summary>Accepts a buffer that covers every row including its stride.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsValidAcceptsCompletePixelRows()
    {
        var isValid = new PdfTileTarget(new byte[ValidPixelBytes], Width, Height, Stride).IsValid;

        await Assert.That(isValid).IsTrue();
    }
}
