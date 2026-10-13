// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The PDF/UA part a created document claims in its metadata. The claim is written only when the document meets what the creator can check.</summary>
public enum PdfUaConformance
{
    /// <summary>No claim.</summary>
    None = 0,

    /// <summary>PDF/UA-1 (ISO 14289-1), which uses a PDF 1.7 file.</summary>
    Part1 = 1,

    /// <summary>PDF/UA-2 (ISO 14289-2), which uses a PDF 2.0 file.</summary>
    Part2 = 2,
}
