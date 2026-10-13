// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>Where an image sits on its page, in points.</summary>
/// <param name="PageWidth">The page width.</param>
/// <param name="PageHeight">The page height.</param>
/// <param name="ImageWidth">The drawn image's width.</param>
/// <param name="ImageHeight">The drawn image's height.</param>
/// <param name="X">The image's left edge.</param>
/// <param name="Y">The image's bottom edge.</param>
[DebuggerDisplay("PdfImageLayout: page {PageWidth}x{PageHeight}, image {ImageWidth}x{ImageHeight} at {X},{Y}")]
internal readonly record struct PdfImageLayout(float PageWidth, float PageHeight, float ImageWidth, float ImageHeight, float X, float Y)
{
    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72;

    /// <summary>Splits spare room into two equal margins.</summary>
    private const float Half = 2;

    /// <summary>Works out the page and where the image sits on it.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">The page options.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The image or an option is not usable.</exception>
    internal static PdfImageLayout Create(in PdfPageImage image, PdfImagePageOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image.Height);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Margin);
        if (options.PaperWidth > 0 && options.PaperHeight > 0)
        {
            return OnPaper(image, options);
        }

        if (options.PaperWidth > 0 || options.PaperHeight > 0 || !float.IsFinite(options.Dpi) || options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Give both paper sizes or neither, and a resolution above zero.");
        }

        var width = image.Width * PointsPerInch / options.Dpi;
        var height = image.Height * PointsPerInch / options.Dpi;
        return new(width, height, width, height, 0, 0);
    }

    /// <summary>Fits an image on paper, centred, inside the margin.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">The page options.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The paper is not finite or the margin leaves no room.</exception>
    private static PdfImageLayout OnPaper(in PdfPageImage image, PdfImagePageOptions options)
    {
        var room = new System.Numerics.Vector2(options.PaperWidth - (options.Margin * Half), options.PaperHeight - (options.Margin * Half));
        if (!(room.X > 0 && room.Y > 0) || !float.IsFinite(options.PaperWidth) || !float.IsFinite(options.PaperHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The margin leaves no room for the image.");
        }

        var scale = Math.Min(room.X / image.Width, room.Y / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new(options.PaperWidth, options.PaperHeight, width, height, (options.PaperWidth - width) / Half, (options.PaperHeight - height) / Half);
    }
}
