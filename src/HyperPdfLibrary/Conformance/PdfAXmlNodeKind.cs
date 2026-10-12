// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The kinds of XML node an XMP packet check reads.</summary>
internal enum PdfAXmlNodeKind
{
    /// <summary>The document itself.</summary>
    Document = 0,

    /// <summary>An element.</summary>
    Element = 1,

    /// <summary>Character data, including whitespace.</summary>
    Text = 2,

    /// <summary>A CDATA section, which XMP's RDF rules treat apart from plain text.</summary>
    CData = 3,

    /// <summary>A processing instruction.</summary>
    Instruction = 4,
}
