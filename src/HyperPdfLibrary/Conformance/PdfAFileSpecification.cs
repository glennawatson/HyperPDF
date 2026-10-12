// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A file specification dictionary found in the document.</summary>
/// <param name="Dictionary">The dictionary.</param>
/// <param name="Number">The number of the indirect object it is, or -1 when it sits inside another object.</param>
internal sealed record PdfAFileSpecification(PdfDictionary Dictionary, int Number);
