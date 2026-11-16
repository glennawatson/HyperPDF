// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>What is taken off each side of a page when it is cropped, as the page is shown.</summary>
/// <param name="Left">The width taken off the left.</param>
/// <param name="Top">The height taken off the top.</param>
/// <param name="Right">The width taken off the right.</param>
/// <param name="Bottom">The height taken off the bottom.</param>
[DebuggerDisplay("CropMargins: left {Left}, top {Top}, right {Right}, bottom {Bottom}")]
internal readonly record struct CropMargins(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Checks that every margin is a number that is not negative.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A margin is negative or not a finite number.</exception>
    internal void Validate()
    {
        CheckMargin(Left, nameof(Left));
        CheckMargin(Top, nameof(Top));
        CheckMargin(Right, nameof(Right));
        CheckMargin(Bottom, nameof(Bottom));
    }

    /// <summary>Works out the crop box a page has after the margins are taken off.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The box in the page's user space: the media box when no margin is set, and otherwise the current crop box less the margins.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The margins leave no page.</exception>
    internal PdfRectangle ApplyTo(PdfPage page)
    {
        if (Left == 0 && Top == 0 && Right == 0 && Bottom == 0)
        {
            return page.MediaBox;
        }

        PdfPageImporter.GetDisplayedSize(page.CropBox, page.Rotation, out var width, out var height);
        var shown = new Vector2(Left, Bottom);
        var far = new Vector2(width - Right, height - Top);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(far.X, shown.X, nameof(Right));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(far.Y, shown.Y, nameof(Top));
        var toUser = PdfPageStamping.ToUserSpace(Matrix3x2.Identity, page);
        var first = Vector2.Transform(shown, toUser);
        var second = Vector2.Transform(far, toUser);
        return new(
            Math.Max(Math.Min(first.X, second.X), page.MediaBox.Left),
            Math.Max(Math.Min(first.Y, second.Y), page.MediaBox.Bottom),
            Math.Min(Math.Max(first.X, second.X), page.MediaBox.Right),
            Math.Min(Math.Max(first.Y, second.Y), page.MediaBox.Top));
    }

    /// <summary>Checks one margin.</summary>
    /// <param name="margin">The margin.</param>
    /// <param name="name">Its name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The margin is negative or not finite.</exception>
    private static void CheckMargin(float margin, string name)
    {
        if (!float.IsFinite(margin) || margin < 0)
        {
            throw new ArgumentOutOfRangeException(name, "A margin must be a number that is not negative.");
        }
    }
}
