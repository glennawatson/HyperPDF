// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Describes PDF/A levels: their part, conformance letter, standard and label.</summary>
public static class PdfALevels
{
    /// <summary>The first part, based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The second part.</summary>
    private const int Part2 = 2;

    /// <summary>The third part.</summary>
    private const int Part3 = 3;

    /// <summary>The fourth part, based on PDF 2.0.</summary>
    private const int Part4 = 4;

    /// <summary>Gets the part of ISO 19005 a level belongs to.</summary>
    /// <param name="level">The level.</param>
    /// <returns>1, 2, 3 or 4.</returns>
    public static int Part(PdfALevel level) => level switch
    {
        PdfALevel.PdfA1B or PdfALevel.PdfA1A => Part1,
        PdfALevel.PdfA2B or PdfALevel.PdfA2U or PdfALevel.PdfA2A => Part2,
        PdfALevel.PdfA3B or PdfALevel.PdfA3U or PdfALevel.PdfA3A => Part3,
        _ => Part4,
    };

    /// <summary>Gets the conformance letter <c>pdfaid:conformance</c> holds for a level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>A, B, U, E or F; <see langword="null"/> for plain PDF/A-4, which has none.</returns>
    public static string? Conformance(PdfALevel level) => level switch
    {
        PdfALevel.PdfA1B or PdfALevel.PdfA2B or PdfALevel.PdfA3B => "B",
        PdfALevel.PdfA1A or PdfALevel.PdfA2A or PdfALevel.PdfA3A => "A",
        PdfALevel.PdfA2U or PdfALevel.PdfA3U => "U",
        PdfALevel.PdfA4E => "E",
        PdfALevel.PdfA4F => "F",
        _ => null,
    };

    /// <summary>Gets the standard a level is defined in, as veraPDF names it.</summary>
    /// <param name="level">The level.</param>
    /// <returns>For example "ISO 19005-2:2011".</returns>
    public static string Specification(PdfALevel level) => Part(level) switch
    {
        Part1 => "ISO 19005-1:2005",
        Part2 => "ISO 19005-2:2011",
        Part3 => "ISO 19005-3:2012",
        _ => "ISO 19005-4:2020",
    };

    /// <summary>Gets the level as people write it, such as PDF/A-2b or PDF/A-4.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The label.</returns>
    public static string Label(PdfALevel level) => level switch
    {
        PdfALevel.PdfA1B => "PDF/A-1b",
        PdfALevel.PdfA1A => "PDF/A-1a",
        PdfALevel.PdfA2B => "PDF/A-2b",
        PdfALevel.PdfA2U => "PDF/A-2u",
        PdfALevel.PdfA2A => "PDF/A-2a",
        PdfALevel.PdfA3B => "PDF/A-3b",
        PdfALevel.PdfA3U => "PDF/A-3u",
        PdfALevel.PdfA3A => "PDF/A-3a",
        PdfALevel.PdfA4 => "PDF/A-4",
        PdfALevel.PdfA4E => "PDF/A-4e",
        _ => "PDF/A-4f",
    };

    /// <summary>Gets the level a claim names.</summary>
    /// <param name="claim">The claim read from a file.</param>
    /// <returns>The level, or <see langword="null"/> when the claim names no level the standards define.</returns>
    public static PdfALevel? FromClaim(PdfAClaim? claim) => claim switch
    {
        null => null,
        { Part: Part1, Conformance: "B" } => PdfALevel.PdfA1B,
        { Part: Part1, Conformance: "A" } => PdfALevel.PdfA1A,
        { Part: Part2, Conformance: "B" } => PdfALevel.PdfA2B,
        { Part: Part2, Conformance: "U" } => PdfALevel.PdfA2U,
        { Part: Part2, Conformance: "A" } => PdfALevel.PdfA2A,
        { Part: Part3, Conformance: "B" } => PdfALevel.PdfA3B,
        { Part: Part3, Conformance: "U" } => PdfALevel.PdfA3U,
        { Part: Part3, Conformance: "A" } => PdfALevel.PdfA3A,
        { Part: Part4, Conformance: null } => PdfALevel.PdfA4,
        { Part: Part4, Conformance: "E" } => PdfALevel.PdfA4E,
        { Part: Part4, Conformance: "F" } => PdfALevel.PdfA4F,
        _ => null,
    };
}
