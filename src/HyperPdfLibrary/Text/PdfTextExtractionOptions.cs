// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>Opt-in text filtering. The default preserves invisible OCR and accessibility replacement text.</summary>
/// <param name="SkipNonVisibleText">Omits text with no paint, zero size or scale, zero paint opacity, or provably empty clipping bounds. This is not a raster visibility guarantee.</param>
/// <param name="StripFormattingControls">Removes Unicode formatting controls before bidirectional reordering, including joiners that legitimate scripts may need.</param>
[DebuggerDisplay("SkipNonVisibleText: {SkipNonVisibleText}, StripFormattingControls: {StripFormattingControls}")]
public readonly record struct PdfTextExtractionOptions(bool SkipNonVisibleText, bool StripFormattingControls);
