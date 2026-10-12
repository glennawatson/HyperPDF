// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The result of converting a written file that broke its claimed PDF/A level.</summary>
/// <param name="Changes">What the conversion changed.</param>
/// <param name="Problems">Why it could not convert; empty when it did.</param>
/// <param name="File">The converted file, or <see langword="null"/> when it could not convert.</param>
internal readonly record struct PdfAClaimRepair(PdfAConversionNote[] Changes, PdfAConversionNote[] Problems, byte[]? File);
