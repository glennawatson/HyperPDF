// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PageObjects;

/// <summary>The shares of an object's frame that a crop cuts off its four sides.</summary>
/// <param name="Left">The share of the width cut off the left.</param>
/// <param name="Top">The share of the height cut off the top.</param>
/// <param name="Right">The share of the width cut off the right.</param>
/// <param name="Bottom">The share of the height cut off the bottom.</param>
[DebuggerDisplay("CropShares: left {Left}, top {Top}, right {Right}, bottom {Bottom}")]
internal readonly record struct CropShares(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Checks that every share is a finite number from 0 up to 1 and that some of the frame is left.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A share is out of range, or the shares leave nothing.</exception>
    internal void Validate()
    {
        Check(Left, nameof(Left));
        Check(Top, nameof(Top));
        Check(Right, nameof(Right));
        Check(Bottom, nameof(Bottom));
        if (Left + Right >= 1F || Top + Bottom >= 1F)
        {
            throw new ArgumentOutOfRangeException(nameof(Right), "The crop must leave some of the picture.");
        }
    }

    /// <summary>Works out the shares of the whole frame left cut off when a second crop is made of what this crop left.</summary>
    /// <param name="next">The shares of the part that is left.</param>
    /// <returns>The shares of the whole frame.</returns>
    internal CropShares Then(CropShares next)
    {
        var width = 1F - Left - Right;
        var height = 1F - Top - Bottom;
        return new(Left + (width * next.Left), Top + (height * next.Top), Right + (width * next.Right), Bottom + (height * next.Bottom));
    }

    /// <summary>Checks one share.</summary>
    /// <param name="share">The share.</param>
    /// <param name="name">Its name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The share is negative, 1 or more, or not a finite number.</exception>
    private static void Check(float share, string name)
    {
        if (!float.IsFinite(share) || share < 0F || share >= 1F)
        {
            throw new ArgumentOutOfRangeException(name, "A crop share must be from 0 up to but not including 1.");
        }
    }
}
