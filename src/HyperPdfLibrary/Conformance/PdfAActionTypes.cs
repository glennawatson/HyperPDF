// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The action types, named actions and trigger events each PDF/A part allows, interned for one document.</summary>
/// <param name="PdfA1">The action types PDF/A-1 allows.</param>
/// <param name="PdfA2">The action types PDF/A-2 and PDF/A-3 allow.</param>
/// <param name="PdfA4">The action types PDF/A-4 and PDF/A-4f allow.</param>
/// <param name="PdfA4E">The action types PDF/A-4e adds.</param>
/// <param name="NamedActions">The named actions every part allows.</param>
/// <param name="Triggers">The additional-action triggers PDF/A-4 allows outside widgets and fields.</param>
internal sealed record PdfAActionTypes(
    HashSet<PdfName> PdfA1,
    HashSet<PdfName> PdfA2,
    HashSet<PdfName> PdfA4,
    HashSet<PdfName> PdfA4E,
    HashSet<PdfName> NamedActions,
    HashSet<PdfName> Triggers);
