// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The XMP value types known in one context, by simplified lower-case name.</summary>
[DebuggerDisplay("PdfAXmpTypeSet: {Types.Count} types")]
internal sealed class PdfAXmpTypeSet
{
    /// <summary>Initializes a new instance of the <see cref="PdfAXmpTypeSet"/> class with no types.</summary>
    internal PdfAXmpTypeSet()
        : this(new Dictionary<string, PdfAXmpType>(StringComparer.Ordinal))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfAXmpTypeSet"/> class.</summary>
    /// <param name="types">The types.</param>
    private PdfAXmpTypeSet(Dictionary<string, PdfAXmpType> types) => Types = types;

    /// <summary>Gets the types by simplified name.</summary>
    internal Dictionary<string, PdfAXmpType> Types { get; }

    /// <summary>Copies the set; structure types keep looking up their fields where they were defined.</summary>
    /// <returns>The copy.</returns>
    internal PdfAXmpTypeSet Copy() => new(new Dictionary<string, PdfAXmpType>(Types, StringComparer.Ordinal));
}
