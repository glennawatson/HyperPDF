// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>A page that holds at least one trap network annotation, as it is now.</summary>
/// <param name="Store">The document's objects.</param>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Id">The page object, or an invalid id for a page written inside its parent's <c>/Kids</c>.</param>
/// <param name="Page">The page dictionary.</param>
/// <param name="Annotations">The page's <c>/Annots</c> array.</param>
/// <param name="NetworkIndex">The index in <paramref name="Annotations"/> of the last trap network annotation.</param>
/// <param name="NetworkCount">The number of trap network annotations on the page.</param>
[DebuggerDisplay("PdfTrapNetworkPage: page {PageIndex}, {NetworkCount} networks")]
internal sealed record PdfTrapNetworkPage(PdfObjectStore Store, int PageIndex, PdfObjectId Id, PdfDictionary Page, PdfArray Annotations, int NetworkIndex, int NetworkCount)
{
    /// <summary>Gets the trap network annotation the checks read: the last one on the page.</summary>
    internal PdfDictionary Network => PdfTrapNetworkReader.Get(Store, Annotations, NetworkIndex)!;
}
