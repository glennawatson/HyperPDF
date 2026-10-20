// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.ViewModels;

/// <summary>The panel shown in the sidebar.</summary>
public enum SidebarMode
{
    /// <summary>Page thumbnails.</summary>
    Thumbnails = 0,

    /// <summary>The document outline.</summary>
    Outline = 1,

    /// <summary>Search results.</summary>
    Search = 2,

    /// <summary>The document's annotations.</summary>
    Annotations = 3,

    /// <summary>Files embedded in the document.</summary>
    Attachments = 4,

    /// <summary>The document's layers.</summary>
    Layers = 5,

    /// <summary>The document's tags, which tell assistive technology how it reads.</summary>
    Tags = 6,

    /// <summary>The document's parts and article threads.</summary>
    Parts = 7,

    /// <summary>The document's sound and video, which can be saved or opened with the system player.</summary>
    Media = 8,
}
