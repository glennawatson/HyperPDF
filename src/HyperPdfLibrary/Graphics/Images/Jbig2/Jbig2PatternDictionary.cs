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

    /// <summary>The patterns owned by this dictionary while it is in use.</summary>
    private Jbig2SymbolStore? _patterns;

    /// <summary>The workspace that keeps this dictionary after disposal.</summary>
    private Jbig2Workspace? _owner;

    /// <summary>Initializes a new instance of the <see cref="Jbig2PatternDictionary"/> class.</summary>
    /// <param name="patterns">The patterns, now owned by the dictionary.</param>
    /// <param name="width">The pattern width.</param>
    /// <param name="height">The pattern height.</param>
    private Jbig2PatternDictionary(Jbig2SymbolStore patterns, int width, int height)
    {
        _patterns = patterns;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the patterns.</summary>
    internal Jbig2SymbolStore Patterns => _patterns ?? throw new ObjectDisposedException(nameof(Jbig2PatternDictionary));

    /// <summary>Gets the pattern width.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the pattern height.</summary>
    internal int Height { get; private set; }

    /// <summary>Returns the patterns' buffers to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _patterns?.Dispose();
        _patterns = null;
        var owner = _owner;
        _owner = null;
        owner?.Patterns.Give(this);
    }

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

        _ = Jbig2GenericRegion.Decode(ref decoder, contexts, new(template, false, AtPixels(template, width)), collective, null);
        return Split(collective, width, height, grayMax, workspace);
    }

    /// <summary>
    /// Gets the adaptive pixels of a pattern dictionary's collective bitmap (T.88 table 27): the first one sits one
    /// pattern width to the left on the same row, so it can reach the previous pattern, and the others are nominal.
    /// </summary>
    /// <param name="template">The generic template, HDTEMPLATE.</param>
    /// <param name="width">The pattern width, HDPW, up to 255.</param>
    /// <returns>The pixels.</returns>
    internal static Jbig2AtPixels AtPixels(int template, int width) => template == FullTemplate
        ? Jbig2AtPixels.Template0 with { X1 = (short)-width, Y1 = 0 }
        : new((short)-width, 0, 0, 0, 0, 0, 0, 0);

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
        return Split(collective, width, height, grayMax, workspace);
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
            ? workspace.RentBitmap((int)totalWidth, height)
            : null;
    }

    /// <summary>Cuts the collective bitmap into patterns.</summary>
    /// <param name="collective">The collective bitmap.</param>
    /// <param name="width">The pattern width.</param>
    /// <param name="height">The pattern height.</param>
    /// <param name="grayMax">The highest pattern index.</param>
    /// <param name="workspace">The owner of the dictionary and its patterns.</param>
    /// <returns>The dictionary, or <see langword="null"/> when the patterns pass the store limit.</returns>
    private static Jbig2PatternDictionary? Split(Jbig2Bitmap collective, int width, int height, int grayMax, Jbig2Workspace workspace)
    {
        var patterns = Jbig2SymbolStore.Rent(workspace);
        for (var gray = 0; gray <= grayMax; gray++)
        {
            if (patterns.TryAddColumns(collective.View, gray * width, width))
            {
                continue;
            }

            patterns.Dispose();
            return null;
        }

        var dictionary = workspace.Patterns.Take() ?? new(patterns, width, height);
        dictionary._patterns = patterns;
        dictionary.Width = width;
        dictionary.Height = height;
        dictionary._owner = workspace;
        return dictionary;
    }
}
