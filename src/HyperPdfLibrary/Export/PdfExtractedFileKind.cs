// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>What a <see cref="PdfExtractedFile"/> holds.</summary>
public enum PdfExtractedFileKind
{
    /// <summary>An image: a JPEG or JPEG 2000 file as the document stores it, or a PNG made from the decoded pixels.</summary>
    Image = 0,

    /// <summary>An embedded font program: TrueType, OpenType, CFF or Type 1.</summary>
    Font = 1,
}
