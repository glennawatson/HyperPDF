// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// An annotation's border line read once (ISO 32000-2 12.5.4): its width, and the dash lengths when the style is dashed.
/// The dash lengths live inline in the value, so reading and holding a border allocates nothing.
/// </summary>
/// <param name="Width">The line width in user space units.</param>
/// <param name="Dashes">The dash lengths.</param>
/// <param name="DashCount">How many of the dash lengths are used; 0 for a solid line.</param>
[DebuggerDisplay("PaintStroke: width {Width}, {DashCount} dashes")]
internal readonly record struct PaintStroke(float Width, DashLengths Dashes, int DashCount)
{
    /// <summary>The most dash lengths kept from a border; a longer dash pattern is not meaningful on a border.</summary>
    internal const int MaxDashes = 10;

    /// <summary>The position of the width in a /Border array.</summary>
    private const int BorderWidthSlot = 2;

    /// <summary>The length of a /Border array that holds a dash array.</summary>
    private const int BorderWithDash = 4;

    /// <summary>The position of the dash array in a /Border array.</summary>
    private const int BorderDashSlot = 3;

    /// <summary>
    /// Reads the border: <c>/BS</c> <c>/W</c>, else the third entry of <c>/Border</c>, else 1; dashed by <c>/BS</c>
    /// <c>/D</c> when <c>/S</c> is <c>/D</c>, else by the fourth entry of <c>/Border</c>.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The border line.</returns>
    internal static PaintStroke Read(PdfDictionary annotation)
    {
        var style = annotation.GetDictionary(KnownName.BS);
        var border = annotation.GetArray(KnownName.Border);
        var dashes = border is { Count: BorderWithDash } ? border.GetArray(BorderDashSlot) : null;
        if (style is not null && style.IsName(KnownName.S, KnownName.D))
        {
            dashes = style.GetArray(KnownName.D);
        }

        var lengths = default(DashLengths);
        var count = Math.Min(dashes?.Count ?? 0, MaxDashes);
        for (var i = 0; i < count; i++)
        {
            lengths[i] = dashes!.GetSingle(i);
        }

        return new(ReadWidth(style, border), lengths, count);
    }

    /// <summary>Sets the line width and, when dashed, the dash pattern.</summary>
    /// <param name="builder">The content.</param>
    internal void Apply(ref PdfContentBuilder builder)
    {
        builder.SetLineWidth(Width);
        if (DashCount != 0)
        {
            DashLengths.Apply(ref builder, Dashes, DashCount);
        }
    }

    /// <summary>Reads the width from the style dictionary, else the border array.</summary>
    /// <param name="style">The <c>/BS</c> dictionary, or <see langword="null"/>.</param>
    /// <param name="border">The <c>/Border</c> array, or <see langword="null"/>.</param>
    /// <returns>The width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadWidth(PdfDictionary? style, PdfArray? border)
    {
        if (style is not null && style.ContainsKey(KnownName.W))
        {
            return style.GetSingle(KnownName.W);
        }

        return border is { Count: > BorderWidthSlot } ? border.GetSingle(BorderWidthSlot) : 1;
    }
}
