// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of one search of a page for transparency.</summary>
/// <param name="Names">The name table content streams are read with.</param>
/// <param name="Visited">The form XObjects and patterns already searched, so shared and looping forms are read once.</param>
/// <param name="CancellationToken">Stops the search.</param>
internal sealed record PdfATransparencyWalk(PdfNameTable Names, HashSet<PdfStream> Visited, CancellationToken CancellationToken);
