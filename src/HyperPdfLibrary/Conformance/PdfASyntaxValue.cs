// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The kinds of value the PDF/A syntax checks read.</summary>
internal enum PdfASyntaxValue
{
    /// <summary>No value: a closing bracket, an unknown word or the end of the data.</summary>
    Empty = 0,

    /// <summary>The null object.</summary>
    Null = 1,

    /// <summary>A boolean.</summary>
    Boolean = 2,

    /// <summary>An integer.</summary>
    Integer = 3,

    /// <summary>A real number.</summary>
    Real = 4,

    /// <summary>A string.</summary>
    String = 5,

    /// <summary>A name.</summary>
    Name = 6,

    /// <summary>An array.</summary>
    Array = 7,

    /// <summary>A dictionary.</summary>
    Dictionary = 8,

    /// <summary>An indirect reference.</summary>
    Reference = 9,

    /// <summary>Syntax the reader cannot recover from, which makes the whole object unreadable.</summary>
    Broken = 10,
}
