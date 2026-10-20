// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Printing;

/// <summary>The print settings a document suggests through its viewer preferences.</summary>
/// <param name="ActualSize">Whether the document asks for no print scaling (<c>/PrintScaling /None</c>).</param>
/// <param name="Copies">The number of copies the document asks for; 1 when it names none.</param>
/// <param name="Duplex">Whether the document asks for two-sided printing.</param>
/// <param name="Binding">The edge two-sided pages are bound on, when the document asks for two-sided printing.</param>
[DebuggerDisplay("PrintDefaults: {Copies} copies, duplex {Duplex}")]
public readonly record struct PrintDefaults(bool ActualSize, int Copies, bool Duplex, DuplexBinding Binding)
{
    /// <summary>Gets defaults that ask for nothing.</summary>
    public static PrintDefaults None => new(false, 1, false, DuplexBinding.LongEdge);
}
