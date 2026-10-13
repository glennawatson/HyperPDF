// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>One page's separation dictionary as read, before the pages are grouped.</summary>
/// <param name="page">The page's colourant and colour space.</param>
/// <param name="pagesArray">The <c>/Pages</c> array, or <see langword="null"/>.</param>
/// <param name="members">The page indexes <c>/Pages</c> lists, in order and each once; <see langword="null"/> when it is missing or does not list the page.</param>
[DebuggerDisplay("SeparationEntry: {Page.PageIndex} group {Group}")]
internal sealed class SeparationEntry(PdfSeparationPage page, PdfArray? pagesArray, int[]? members)
{
    /// <summary>Gets the page's colourant and colour space.</summary>
    internal PdfSeparationPage Page { get; } = page;

    /// <summary>Gets the <c>/Pages</c> array, or <see langword="null"/>.</summary>
    internal PdfArray? PagesArray { get; } = pagesArray;

    /// <summary>Gets the page indexes <c>/Pages</c> lists, or <see langword="null"/> when the page cannot start a group.</summary>
    internal int[]? Members { get; } = members;

    /// <summary>Gets or sets the position of the group the page joined, or -1 while it has none.</summary>
    internal int Group { get; set; } = -1;
}
