// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What an optimisation run did: sizes, savings by category, the changes made and what was left alone.</summary>
/// <param name="BytesBefore">The source file's size.</param>
/// <param name="BytesAfter">The bytes written to the destination.</param>
/// <param name="Mode">How the file was written.</param>
/// <param name="Savings">The savings of each category that changed anything.</param>
/// <param name="Actions">The changes made, in the order they were made.</param>
/// <param name="Warnings">Things the reader should know, such as figures that need alternative text.</param>
/// <param name="Skipped">What was left alone, with the reason.</param>
[DebuggerDisplay("PdfOptimizeReport: {BytesBefore} to {BytesAfter} ({Mode})")]
public sealed record PdfOptimizeReport(
    long BytesBefore,
    long BytesAfter,
    PdfOptimizeMode Mode,
    IReadOnlyList<PdfOptimizeSaving> Savings,
    IReadOnlyList<PdfOptimizeAction> Actions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PdfOptimizeSkip> Skipped)
{
    /// <summary>Gets the bytes saved; negative when the file grew.</summary>
    public long BytesSaved => BytesBefore - BytesAfter;

    /// <summary>Gets the new size as a share of the old, such as 0.4 for a file cut to 40 percent.</summary>
    public double SizeRatio => BytesBefore > 0 ? (double)BytesAfter / BytesBefore : 1;

    /// <summary>Gets a value indicating whether a structure tree was inferred from the layout and marked as inferred in the XMP.</summary>
    public bool TagsInferred { get; init; }

    /// <summary>Gets the number of inferred figures that have no alternative text yet.</summary>
    public int FiguresNeedingAltText { get; init; }

    /// <summary>Gets the PDF/A part the document claims, or zero when it claims none.</summary>
    public int PdfAPart { get; init; }

    /// <summary>
    /// Gets the check of the written file's PDF/A claim, or <see langword="null"/> when it claims none or was copied
    /// unchanged. When <see cref="PdfAClaimCheck.Removed"/> is set, the copy no longer met its level, so its claim was
    /// removed; tell the user which rules it breaks.
    /// </summary>
    public PdfAClaimCheck? PdfAClaim { get; init; }

    /// <summary>Gets a value indicating whether the document was signed.</summary>
    public bool WasSigned { get; init; }

    /// <summary>Gets a value indicating whether the output is encrypted.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>Gets the saving of one category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The saving, or an empty one when the category changed nothing.</returns>
    public PdfOptimizeSaving GetSaving(PdfOptimizeCategory category)
    {
        foreach (var saving in Savings)
        {
            if (saving.Category == category)
            {
                return saving;
            }
        }

        return new(category, 0, 0, 0);
    }
}
