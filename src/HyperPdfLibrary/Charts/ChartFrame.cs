// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>The plot area of a bar, column, line or area chart and how values and categories map onto it.</summary>
/// <param name="Plot">The plot area.</param>
/// <param name="Scale">The value axis.</param>
/// <param name="Categories">The number of categories.</param>
/// <param name="Horizontal">Whether the value axis runs left to right, as in a bar chart.</param>
[DebuggerDisplay("ChartFrame: {Plot}")]
internal readonly record struct ChartFrame(PdfCanvasBox Plot, ChartScale Scale, int Categories, bool Horizontal)
{
    /// <summary>The share of a category's space the bars of its series take up.</summary>
    private const float GroupShare = 0.8F;

    /// <summary>Half, for finding middles.</summary>
    private const float Half = 0.5F;

    /// <summary>Gets the size of a category's space along the category axis.</summary>
    internal float SlotSize => (Horizontal ? Plot.Height : Plot.Width) / Categories;

    /// <summary>Gets where a value falls along the value axis.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The x of a horizontal axis or the y of a vertical one, in points.</returns>
    internal float MapValue(double value)
    {
        var fraction = (float)Scale.GetFraction(value);
        return Horizontal ? Plot.X + (fraction * Plot.Width) : Plot.Y + (fraction * Plot.Height);
    }

    /// <summary>Gets the lower edge of a category's space along the category axis; the first category is at the top of a horizontal chart.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The coordinate.</returns>
    internal float GetSlotStart(int category) => Horizontal ? Plot.Top - ((category + 1) * SlotSize) : Plot.X + (category * SlotSize);

    /// <summary>Gets the middle of a category's space along the category axis.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The coordinate.</returns>
    internal float GetSlotCenter(int category) => GetSlotStart(category) + (SlotSize * Half);

    /// <summary>Gets the rectangle of one series' bar in a category.</summary>
    /// <param name="category">The category.</param>
    /// <param name="series">The series.</param>
    /// <param name="seriesCount">How many series share the category.</param>
    /// <param name="value">The value.</param>
    /// <returns>The bar, growing from the zero of the value axis.</returns>
    internal PdfCanvasBox GetBar(int category, int series, int seriesCount, double value)
    {
        var group = SlotSize * GroupShare;
        var bar = group / seriesCount;
        var groupStart = GetSlotStart(category) + ((SlotSize - group) * Half);
        var along = groupStart + ((Horizontal ? seriesCount - 1 - series : series) * bar);
        var zero = MapValue(0D);
        var end = MapValue(value);
        var low = MathF.Min(zero, end);
        var length = MathF.Abs(end - zero);
        return Horizontal ? new(low, along, length, bar) : new(along, low, bar, length);
    }
}
