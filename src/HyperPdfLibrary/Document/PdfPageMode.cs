// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Which panel shows when the document opens (ISO 32000-2 7.7.2, <c>/PageMode</c>).</summary>
public enum PdfPageMode
{
    /// <summary>No panel.</summary>
    UseNone = 0,

    /// <summary>The bookmarks panel.</summary>
    UseOutlines = 1,

    /// <summary>The page thumbnails panel.</summary>
    UseThumbs = 2,

    /// <summary>Full screen.</summary>
    FullScreen = 3,

    /// <summary>The layers panel.</summary>
    UseOC = 4,

    /// <summary>The attachments panel.</summary>
    UseAttachments = 5,
}
