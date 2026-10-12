// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Archiving;

namespace HyperPdf.Core.Optimizing;

/// <summary>What an optimisation did: sizes, what was left alone and what the reader should know.</summary>
/// <param name="BytesBefore">The source file's size.</param>
/// <param name="BytesAfter">The size of the new file.</param>
/// <param name="Mode">How the file was written.</param>
/// <param name="Warnings">Things the reader should know, in plain words.</param>
/// <param name="Skipped">What was left alone, with the reason.</param>
[DebuggerDisplay("OptimizeReport: {BytesBefore} to {BytesAfter} ({Mode})")]
public sealed record OptimizeReport(
    long BytesBefore,
    long BytesAfter,
    OptimizeWriteMode Mode,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<OptimizeSkip> Skipped)
{
    /// <summary>Gets the bytes saved; negative when the file grew.</summary>
    public long BytesSaved => BytesBefore - BytesAfter;

    /// <summary>Gets the share of the old size that was saved, from 0 to 1; negative when the file grew.</summary>
    public double FractionSaved => BytesBefore > 0 ? (double)BytesSaved / BytesBefore : 0;

    /// <summary>Gets a value indicating whether a structure was inferred from the layout.</summary>
    public bool TagsInferred { get; init; }

    /// <summary>Gets the number of inferred figures that have no alternative text yet.</summary>
    public int FiguresNeedingAltText { get; init; }

    /// <summary>Gets a value indicating whether the document is signed.</summary>
    public bool WasSigned { get; init; }

    /// <summary>Gets a value indicating whether the new file is encrypted.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>Gets the PDF/A part the document claims, or zero when it claims none.</summary>
    public int PdfAPart { get; init; }

    /// <summary>
    /// Gets the PDF/A claim the copy lost because it no longer met the level, or <see langword="null"/> when the copy kept
    /// its claim or had none.
    /// </summary>
    public ArchiveClaimRemoval? ClaimRemoved { get; init; }
}
