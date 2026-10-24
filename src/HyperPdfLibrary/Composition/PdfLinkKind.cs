// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>What a link goes to.</summary>
public enum PdfLinkKind
{
    /// <summary>An address, such as a web page or a file.</summary>
    Uri = 0,

    /// <summary>A bookmark in the document.</summary>
    Bookmark = 1,

    /// <summary>A page of the document.</summary>
    Page = 2,
}
