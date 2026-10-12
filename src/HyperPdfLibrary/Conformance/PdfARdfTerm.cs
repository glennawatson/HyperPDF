// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The RDF syntax terms the XMP serialization rules name (XMP Specification Part 1, 7.9 and Annex C).</summary>
internal enum PdfARdfTerm
{
    /// <summary>Any name that is not an RDF syntax term.</summary>
    Other = 0,

    /// <summary><c>rdf:RDF</c>.</summary>
    Rdf = 1,

    /// <summary><c>rdf:ID</c>.</summary>
    Id = 2,

    /// <summary><c>rdf:about</c>.</summary>
    About = 3,

    /// <summary><c>rdf:parseType</c>.</summary>
    ParseType = 4,

    /// <summary><c>rdf:resource</c>.</summary>
    Resource = 5,

    /// <summary><c>rdf:nodeID</c>.</summary>
    NodeId = 6,

    /// <summary><c>rdf:datatype</c>.</summary>
    Datatype = 7,

    /// <summary><c>rdf:Description</c>.</summary>
    Description = 8,

    /// <summary><c>rdf:li</c>.</summary>
    Item = 9,

    /// <summary>The obsolete <c>rdf:aboutEach</c>.</summary>
    AboutEach = 10,

    /// <summary>The obsolete <c>rdf:aboutEachPrefix</c>.</summary>
    AboutEachPrefix = 11,

    /// <summary>The obsolete <c>rdf:bagID</c>.</summary>
    BagId = 12,
}
