// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>How the PDF/A content rules treat one content operator.</summary>
/// <param name="Role">What the operator means to the rules.</param>
/// <param name="Stroke">Whether a colour operator sets the stroke colour rather than the fill colour.</param>
/// <param name="Device">The device colour space a device colour operator selects, else none.</param>
internal readonly record struct PdfAOperatorTraits(PdfAOperatorRole Role, bool Stroke, KnownName Device);
