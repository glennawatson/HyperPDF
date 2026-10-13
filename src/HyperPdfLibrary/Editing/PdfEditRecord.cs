// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>A committed transaction: each object and trailer entry it touched, before and after.</summary>
/// <param name="Label">The transaction's description.</param>
/// <param name="Kinds">The kinds of change the transaction declared.</param>
/// <param name="Scopes">The parts of the document the transaction's changes can affect.</param>
/// <param name="Before">The objects before the transaction, in the order first touched.</param>
/// <param name="After">The same objects after it.</param>
/// <param name="TrailerBefore">The trailer entries before the transaction.</param>
/// <param name="TrailerAfter">The same entries after it.</param>
[DebuggerDisplay("PdfEditRecord: {Label}")]
internal sealed record PdfEditRecord(
    string Label,
    PdfChangeKinds Kinds,
    PdfCacheScopes Scopes,
    PdfObjectState[] Before,
    PdfObjectState[] After,
    PdfTrailerEntry[] TrailerBefore,
    PdfTrailerEntry[] TrailerAfter);
