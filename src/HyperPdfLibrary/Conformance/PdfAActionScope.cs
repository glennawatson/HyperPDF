// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of one walk over a document's actions.</summary>
/// <param name="Context">The validation.</param>
/// <param name="Types">The allowed action types.</param>
/// <param name="Visited">The action dictionaries already checked, so chains and shared actions are checked once.</param>
internal sealed record PdfAActionScope(PdfAValidationContext Context, PdfAActionTypes Types, HashSet<PdfDictionary> Visited);
