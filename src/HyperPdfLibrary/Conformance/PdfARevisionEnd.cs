// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The end of a signed revision.</summary>
/// <param name="End">The offset after <c>%%EOF</c> and its line end.</param>
/// <param name="LineEnd">The bytes of the line end after <c>%%EOF</c>, which a byte range may leave out.</param>
/// <param name="AtFileEnd">Whether nothing follows, in which case the byte range must reach the end exactly.</param>
internal readonly record struct PdfARevisionEnd(long End, int LineEnd, bool AtFileEnd);
