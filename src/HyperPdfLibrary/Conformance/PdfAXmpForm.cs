// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The form of an XMP node, as the XMP data model describes it (XMP Specification Part 1, clause 6).</summary>
[Flags]
internal enum PdfAXmpForm
{
    /// <summary>A simple value.</summary>
    None = 0,

    /// <summary>An array: <c>rdf:Bag</c>, <c>rdf:Seq</c> or <c>rdf:Alt</c>.</summary>
    Array = 1 << 0,

    /// <summary>An ordered array: <c>rdf:Seq</c> or <c>rdf:Alt</c>.</summary>
    Ordered = 1 << 1,

    /// <summary>An alternative array: <c>rdf:Alt</c>.</summary>
    Alternate = 1 << 2,

    /// <summary>An alternative array whose items carry <c>xml:lang</c>.</summary>
    AltText = 1 << 3,

    /// <summary>A structure.</summary>
    Struct = 1 << 4,

    /// <summary>A simple value given by <c>rdf:resource</c>.</summary>
    Uri = 1 << 5,

    /// <summary>The node has an <c>xml:lang</c> qualifier.</summary>
    Language = 1 << 6,
}
