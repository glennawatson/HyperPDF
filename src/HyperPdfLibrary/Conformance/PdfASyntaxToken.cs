// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The kinds of token the PDF/A syntax reader returns.</summary>
internal enum PdfASyntaxToken
{
    /// <summary>A byte sequence that is not a valid token.</summary>
    None = 0,

    /// <summary>The end of the data.</summary>
    End = 1,

    /// <summary>An integer.</summary>
    Integer = 2,

    /// <summary>A real number.</summary>
    Real = 3,

    /// <summary>A literal string in parentheses.</summary>
    LiteralString = 4,

    /// <summary>A hexadecimal string in angle brackets.</summary>
    HexString = 5,

    /// <summary>A name.</summary>
    Name = 6,

    /// <summary>The opening bracket of an array.</summary>
    ArrayStart = 7,

    /// <summary>The closing bracket of an array.</summary>
    ArrayEnd = 8,

    /// <summary>The opening brackets of a dictionary.</summary>
    DictionaryStart = 9,

    /// <summary>The closing brackets of a dictionary.</summary>
    DictionaryEnd = 10,

    /// <summary>A bare word, such as a keyword or an operator.</summary>
    Keyword = 11,

    /// <summary>A byte sequence the reader cannot recover from, such as a lone closing angle bracket.</summary>
    Broken = 12,
}
