// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Printing;

/// <summary>Works out how much a page is enlarged or shrunk on the paper for a scaling choice.</summary>
public static class PrintScale
{
    /// <summary>The smallest custom percentage offered.</summary>
    private const int Smallest = 10;

    /// <summary>The largest custom percentage offered.</summary>
    private const int Largest = 400;

    /// <summary>The percentage that is the page's true size.</summary>
    private const int Whole = 100;

    /// <summary>Gets the smallest custom percentage offered.</summary>
    public static int MinPercent => Smallest;

    /// <summary>Gets the largest custom percentage offered.</summary>
    public static int MaxPercent => Largest;

    /// <summary>Gets the percentage that is the page's true size.</summary>
    public static int TrueSize => Whole;

    /// <summary>Gets the scale for a page on the paper.</summary>
    /// <param name="scaling">The scaling choice.</param>
    /// <param name="percent">The custom percentage, used with <see cref="PrintScaling.Custom"/>.</param>
    /// <param name="contentWidth">The page's visible width in points.</param>
    /// <param name="contentHeight">The page's visible height in points.</param>
    /// <param name="availableWidth">The paper's width inside its margins, in points.</param>
    /// <param name="availableHeight">The paper's height inside its margins, in points.</param>
    /// <returns>The scale; 1 is true size.</returns>
    public static float For(PrintScaling scaling, int percent, float contentWidth, float contentHeight, float availableWidth, float availableHeight)
    {
        if (contentWidth <= 0 || contentHeight <= 0)
        {
            return 1F;
        }

        var fit = Math.Min(availableWidth / contentWidth, availableHeight / contentHeight);
        return scaling switch
        {
            PrintScaling.ActualSize => 1F,
            PrintScaling.ShrinkOversized => Math.Min(1F, fit),
            PrintScaling.Custom => ClampPercent(percent) / (float)Whole,
            _ => fit,
        };
    }

    /// <summary>Keeps a custom percentage within the offered range.</summary>
    /// <param name="percent">The percentage typed.</param>
    /// <returns>The percentage used.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ClampPercent(int percent) => Math.Clamp(percent, Smallest, Largest);
}
