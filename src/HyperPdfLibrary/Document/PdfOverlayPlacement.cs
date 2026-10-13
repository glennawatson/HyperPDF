// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Whether drawn pages go in front of a page's content or behind it.</summary>
public enum PdfOverlayPlacement
{
    /// <summary>In front of the page's own content.</summary>
    Overlay = 0,

    /// <summary>Behind the page's own content.</summary>
    Underlay = 1,
}
