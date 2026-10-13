// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>Which page of the overlay document is drawn on which page of the target.</summary>
/// <param name="TargetPage">The zero based index of the page drawn on.</param>
/// <param name="SourcePage">The zero based index of the page drawn.</param>
[DebuggerDisplay("PdfOverlayMapping: {SourcePage} onto {TargetPage}")]
public readonly record struct PdfOverlayMapping(int TargetPage, int SourcePage);
