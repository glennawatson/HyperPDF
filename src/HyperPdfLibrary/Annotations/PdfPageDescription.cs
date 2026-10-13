// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// The indirect objects that make up a page's description, as a trap network's <c>/Version</c> array lists them
/// (ISO 32000-2 Table 403): the content streams, the resources other than procedure sets of the page and of its form
/// XObjects, and the OPI dictionaries of its XObjects. The arrays and dictionaries that only hold those objects are kept
/// apart, as an edit to them also changes the page.
/// </summary>
[DebuggerDisplay("PdfPageDescription: {Described.Count} objects")]
internal sealed class PdfPageDescription
{
    /// <summary>Gets the numbers of the objects a <c>/Version</c> array must list.</summary>
    internal HashSet<int> Described { get; } = [];

    /// <summary>Gets the numbers of the indirect <c>/Contents</c> arrays and resource dictionaries that hold them.</summary>
    internal HashSet<int> Containers { get; } = [];
}
