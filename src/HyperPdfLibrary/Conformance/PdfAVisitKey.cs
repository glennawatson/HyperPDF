// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Identifies one visit of an object during PDF/A validation.</summary>
/// <param name="Id">The object; not valid for a direct object.</param>
/// <param name="Kind">The role it was visited in.</param>
/// <param name="Variant">Extra state that makes a visit distinct, such as the overprint settings of a CMYK space.</param>
internal readonly record struct PdfAVisitKey(PdfObjectId Id, PdfAVisitKind Kind, int Variant);
