// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>One open marked-content sequence in the PDF/A text walk.</summary>
/// <param name="ActualText">Whether it, an enclosing sequence or its structure element carries <c>/ActualText</c>.</param>
/// <param name="Mcid">The marked-content id it or an enclosing sequence gives, or -1.</param>
internal readonly record struct PdfAMarkState(bool ActualText, int Mcid);
