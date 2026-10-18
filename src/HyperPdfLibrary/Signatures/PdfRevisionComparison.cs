// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Holds two revisions and the ownership index for one comparison.</summary>
/// <param name="Current">The latest object definitions.</param>
/// <param name="Signed">The definitions covered by the signature.</param>
/// <param name="Tables">The revisions' name tables.</param>
/// <param name="Root">The latest catalog's object number, or -1.</param>
/// <param name="Info">The latest information dictionary's object number, or -1.</param>
/// <param name="AcroForm">The latest interactive form's object number, or -1.</param>
/// <param name="Owned">Objects serving another change, or null before any ownership walk.</param>
/// <param name="CancellationToken">Cancels this comparison's object and ownership walks.</param>
internal readonly record struct PdfRevisionComparison(
    PdfObjectStore Current,
    PdfObjectStore Signed,
    NameTables Tables,
    int Root,
    int Info,
    int AcroForm,
    Dictionary<int, PdfObjectChange>? Owned,
    CancellationToken CancellationToken);
