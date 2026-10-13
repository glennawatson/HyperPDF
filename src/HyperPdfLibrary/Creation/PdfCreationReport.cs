// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>What a finished created document contains and what it could not promise.</summary>
/// <param name="PageCount">The number of pages.</param>
/// <param name="IsTagged">Whether the document has a structure tree.</param>
/// <param name="ClaimsPdfUa">Whether the metadata claims PDF/UA.</param>
/// <param name="Notes">Plain-language notes, such as why a requested PDF/UA claim was left out.</param>
[DebuggerDisplay("PdfCreationReport: {PageCount} pages, tagged {IsTagged}, PDF/UA {ClaimsPdfUa}, {Notes.Length} notes")]
public sealed record PdfCreationReport(int PageCount, bool IsTagged, bool ClaimsPdfUa, string[] Notes);
