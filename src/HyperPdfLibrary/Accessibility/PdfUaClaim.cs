// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>What a file says about PDF/UA. The file's word is not checked: veraPDF is the tool that proves a claim.</summary>
/// <param name="Part">The XMP <c>pdfuaid:part</c> (1 for ISO 14289-1, 2 for ISO 14289-2), or <see langword="null"/> when the file makes no claim.</param>
/// <param name="Revision">The XMP <c>pdfuaid:rev</c>, or <see langword="null"/>.</param>
/// <param name="PdfVersion">The file's format version, for example "1.7" or "2.0"; empty when unknown.</param>
/// <param name="UsesPdf20Namespaces">Whether the structure tree lists namespaces or uses the PDF 2.0 structure namespace.</param>
/// <param name="Amendment">The XMP <c>pdfuaid:amd</c>, which names an amendment to the part, or <see langword="null"/>. A file need not have one.</param>
/// <param name="Corrigendum">The XMP <c>pdfuaid:corr</c>, which names a technical corrigendum to the part, or <see langword="null"/>. A file need not have one.</param>
[DebuggerDisplay("PdfUaClaim: part {Part} (PDF {PdfVersion})")]
public readonly record struct PdfUaClaim(int? Part, string? Revision, string PdfVersion, bool UsesPdf20Namespaces, string? Amendment, string? Corrigendum)
{
    /// <summary>The part number of ISO 14289-1 (PDF/UA-1).</summary>
    internal const int Part1 = 1;

    /// <summary>The part number of ISO 14289-2 (PDF/UA-2).</summary>
    internal const int Part2 = 2;

    /// <summary>Initializes a new instance of the <see cref="PdfUaClaim"/> struct for a file that names no amendment and no corrigendum.</summary>
    /// <param name="part">The XMP <c>pdfuaid:part</c>, or <see langword="null"/> when the file makes no claim.</param>
    /// <param name="revision">The XMP <c>pdfuaid:rev</c>, or <see langword="null"/>.</param>
    /// <param name="pdfVersion">The file's format version.</param>
    /// <param name="usesPdf20Namespaces">Whether the structure tree lists namespaces or uses the PDF 2.0 structure namespace.</param>
    public PdfUaClaim(int? part, string? revision, string pdfVersion, bool usesPdf20Namespaces)
        : this(part, revision, pdfVersion, usesPdf20Namespaces, null, null)
    {
    }

    /// <summary>Gets a value indicating whether the file claims PDF/UA-1 or PDF/UA-2.</summary>
    public bool IsClaimed => Part is Part1 or Part2;
}
