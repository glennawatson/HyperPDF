// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of collecting associated file specifications.</summary>
/// <param name="store">The objects.</param>
[DebuggerDisplay("PdfAAssociatedWalk: {Keys.Count} associated")]
internal sealed class PdfAAssociatedWalk(PdfObjectStore store)
{
    /// <summary>Gets the objects.</summary>
    internal PdfObjectStore Store { get; } = store;

    /// <summary>Gets the object numbers of the associated file specifications.</summary>
    internal HashSet<int> Keys { get; } = [];

    /// <summary>Gets the dictionaries already searched.</summary>
    internal HashSet<PdfDictionary> Visited { get; } = [with(ReferenceEqualityComparer.Instance)];
}
