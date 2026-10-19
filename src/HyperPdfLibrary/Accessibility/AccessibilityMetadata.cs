// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>What the accessibility report reads from the document's already parsed XMP packet.</summary>
/// <param name="HasMetadata">Whether the catalog has a readable XMP packet.</param>
/// <param name="HasTitle">Whether the packet has a title.</param>
/// <param name="Part">The claimed PDF/UA part (<c>pdfuaid:part</c>), or <see langword="null"/>.</param>
/// <param name="Revision">The claimed <c>pdfuaid:rev</c>, or <see langword="null"/> when missing or empty.</param>
/// <param name="Amendment">The claimed <c>pdfuaid:amd</c>, or <see langword="null"/> when missing or empty.</param>
/// <param name="Corrigendum">The claimed <c>pdfuaid:corr</c>, or <see langword="null"/> when missing or empty.</param>
[DebuggerDisplay("AccessibilityMetadata: part {Part}, revision {Revision}")]
internal readonly record struct AccessibilityMetadata(bool HasMetadata, bool HasTitle, int? Part, string? Revision, string? Amendment, string? Corrigendum);
