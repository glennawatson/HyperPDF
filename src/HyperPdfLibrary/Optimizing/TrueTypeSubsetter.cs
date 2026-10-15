// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Cuts an embedded TrueType program down to the glyphs a document shows while keeping every glyph number: unused
/// glyphs keep their slot in /loca with no outline, so /Widths, /W, CIDToGIDMap, cmap and the content streams stay valid
/// unchanged. Components of composite glyphs are kept with them, glyph 0 is always kept, and layout and signature tables
/// that PDF drawing never reads are dropped. A font whose licence forbids subsetting is left whole.
/// </summary>
internal static class TrueTypeSubsetter
{
    /// <summary>Subsets a font program.</summary>
    /// <param name="program">The /FontFile2 stream.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="scratchPools">The document or registered service's compression owner.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The subset program stream, or <see langword="null"/> when the font cannot be subset or nothing is gained.</returns>
    internal static PdfStream? Subset(PdfStream program, CodeBitSet glyphs, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        var font = default(PooledBuffer);
        var subset = default(PooledBuffer);
        try
        {
            _ = program.Decode(ref font, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryRebuild(font.Array.AsMemory(0, font.Length), glyphs, default, true, ref subset, cancellationToken))
            {
                return null;
            }

            return subset.Length < font.Length ? Compress(program, subset.WrittenSpan, scratchPools) : null;
        }
        finally
        {
            font.Dispose();
            subset.Dispose();
        }
    }

    /// <summary>
    /// Cuts a whole TrueType font down to some glyphs and gives it a new cmap table, for embedding a substitute font. Glyph
    /// numbers are kept, as in <see cref="Subset"/>.
    /// </summary>
    /// <param name="font">The font program.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="cmap">The cmap table to write in place of the font's own.</param>
    /// <param name="cancellationToken">Stops rebuilding the font.</param>
    /// <returns>The new font, or <see langword="null"/> when the font is not a whole TrueType-outline font.</returns>
    internal static byte[]? SubsetWithCmap(byte[] font, CodeBitSet glyphs, byte[] cmap, CancellationToken cancellationToken)
    {
        var subset = default(PooledBuffer);
        try
        {
            return TryRebuild(font, glyphs, cmap, false, ref subset, cancellationToken) ? subset.ToArray() : null;
        }
        finally
        {
            subset.Dispose();
        }
    }

    /// <summary>Cuts a font down and writes the result.</summary>
    /// <param name="font">The source font.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="cmap">A cmap table to write in place of the font's own, or empty to keep it.</param>
    /// <param name="honourLicence">Whether a font whose licence forbids subsetting is refused.</param>
    /// <param name="output">Receives the new font.</param>
    /// <param name="cancellationToken">Stops rebuilding the font.</param>
    /// <returns><see langword="true"/> when the font was cut down.</returns>
    private static bool TryRebuild(ReadOnlyMemory<byte> font, CodeBitSet glyphs, ReadOnlyMemory<byte> cmap, bool honourLicence, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        var rebuild = new TrueTypeRebuild(font, cancellationToken);
        try
        {
            if (!rebuild.TryRead(glyphs, honourLicence))
            {
                return false;
            }

            rebuild.Write(cmap, ref output);
            return true;
        }
        finally
        {
            rebuild.Dispose();
        }
    }

    /// <summary>Compresses the subset into a new /FontFile2 stream with /Length1 updated.</summary>
    /// <param name="program">The original stream.</param>
    /// <param name="subset">The subset font.</param>
    /// <param name="scratchPools">The compression owner.</param>
    /// <returns>The stream, or <see langword="null"/> when it is not smaller than the original.</returns>
    private static PdfStream? Compress(PdfStream program, ReadOnlySpan<byte> subset, ScratchPools scratchPools)
    {
        var compressed = default(PooledBuffer);
        try
        {
            StreamRecompressor.Deflate(subset, ref compressed, scratchPools);
            if (compressed.Length >= program.RawLength)
            {
                return null;
            }

            var dictionary = program.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Length);
            _ = dictionary.Remove(KnownName.DecodeParms);
            _ = dictionary.Remove(KnownName.DL);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            dictionary.Set(OptimizerNames.Length1, PdfValue.FromInteger(subset.Length));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
