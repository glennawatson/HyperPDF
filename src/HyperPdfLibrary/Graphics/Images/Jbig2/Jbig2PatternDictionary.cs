// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// A pattern dictionary (T.88 section 6.7): GRAYMAX + 1 patterns of one size, cut from a collective bitmap decoded
/// with the generic region procedure.
/// </summary>
[DebuggerDisplay("Jbig2PatternDictionary: {Patterns.Count} patterns of {Width}x{Height}")]
internal sealed class Jbig2PatternDictionary : IDisposable
{
    /// <summary>The template whose other adaptive pixels are set for pattern dictionaries.</summary>
    private const int FullTemplate = 0;

    /// <summary>Initializes a new instance of the <see cref="Jbig2PatternDictionary"/> class.</summary>
    /// <param name="patterns">The patterns, now owned by the dictionary.</param>
    /// <param name="width">The pattern width.</param>
    /// <param name="height">The pattern height.</param>
    private Jbig2PatternDictionary(Jbig2SymbolStore patterns, int width, int height)
    {
        Patterns = patterns;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the patterns.</summary>
    internal Jbig2SymbolStore Patterns { get; }

    /// <summary>Gets the pattern width.</summary>
    internal int Width { get; }

    /// <summary>Gets the pattern height.</summary>
    internal int Height { get; }

    /// <summary>Returns the patterns' buffers to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Patterns.Dispose();

    /// <summary>Decodes an arithmetic-coded pattern dictionary.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The cleared generic contexts for the template.</param>
    /// <param name="template">The generic template, HDTEMPLATE.</param>
    /// <param name="width">The pattern width, HDPW.</param>
    /// <param name="height">The pattern height, HDPH.</param>
    /// <param name="grayMax">The highest pattern index, GRAYMAX.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns>The dictionary, or <see langword="null"/> when the sizes are invalid.</returns>
    internal static Jbig2PatternDictionary? DecodeArithmetic(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, int template, int width, int height, int grayMax, Jbig2Workspace workspace)
    {
        using var collective = CreateCollective(width, height, grayMax, workspace);
        if (collective is null)
        {
            return null;
        }

        var at = template == FullTemplate
            ? Jbig2AtPixels.Template0 with { X1 = unchecked((sbyte)-width), Y1 = 0 }
            : new(unchecked((sbyte)-width), 0, 0, 0, 0, 0, 0, 0);
        _ = Jbig2GenericRegion.Decode(ref decoder, contexts, new(template, false, at), collective, null);
        return Split(collective, width, height, grayMax);
    }

    /// <summary>Decodes an MMR-coded pattern dictionary.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="width">The pattern width, HDPW.</param>
    /// <param name="height">The pattern height, HDPH.</param>
    /// <param name="grayMax">The highest pattern index, GRAYMAX.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns>The dictionary, or <see langword="null"/> when the sizes are invalid.</returns>
    internal static Jbig2PatternDictionary? DecodeMmr(ReadOnlySpan<byte> data, int width, int height, int grayMax, Jbig2Workspace workspace)
    {
        using var collective = CreateCollective(width, height, grayMax, workspace);
        if (collective is null)
        {
            return null;
        }

        _ = Jbig2GenericRegion.DecodeMmr(data, collective);
        return Split(collective, width, height, grayMax);
    }

    /// <summary>Creates the white collective bitmap.</summary>
    /// <param name="width">The pattern width.</param>
    /// <param name="height">The pattern height.</param>
    /// <param name="grayMax">The highest pattern index.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is invalid or the budget is spent.</returns>
    private static Jbig2Bitmap? CreateCollective(int width, int height, int grayMax, Jbig2Workspace workspace)
    {
        var totalWidth = ((long)grayMax + 1) * width;
        return Jbig2Limits.IsValidSize(totalWidth, height) && workspace.TryCharge(totalWidth * height)
            ? Jbig2Bitmap.Create((int)totalWidth, height, workspace.ScratchPools)
            : null;
    }

    /// <summary>Cuts the collective bitmap into patterns.</summary>
    /// <param name="collective">The collective bitmap.</param>
    /// <param name="width">The pattern width.</param>
    /// <param name="height">The pattern height.</param>
    /// <param name="grayMax">The highest pattern index.</param>
    /// <returns>The dictionary, or <see langword="null"/> when the patterns pass the store limit.</returns>
    private static Jbig2PatternDictionary? Split(Jbig2Bitmap collective, int width, int height, int grayMax)
    {
        var patterns = new Jbig2SymbolStore(collective.ScratchPools);
        for (var gray = 0; gray <= grayMax; gray++)
        {
            if (patterns.TryAddColumns(collective.View, gray * width, width))
            {
                continue;
            }

            patterns.Dispose();
            return null;
        }

        return new(patterns, width, height);
    }
}
