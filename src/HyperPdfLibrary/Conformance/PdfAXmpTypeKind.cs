// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>How an XMP value type is checked.</summary>
internal enum PdfAXmpTypeKind
{
    /// <summary>A simple value, optionally matching a pattern.</summary>
    Simple = 0,

    /// <summary>A simple value that reads as an XMP date.</summary>
    Date = 1,

    /// <summary>A language alternative.</summary>
    LanguageAlternative = 2,

    /// <summary>A simple value that compiles as an XPath expression.</summary>
    XPath = 3,

    /// <summary>A structure whose fields have value types.</summary>
    Structure = 4,
}
