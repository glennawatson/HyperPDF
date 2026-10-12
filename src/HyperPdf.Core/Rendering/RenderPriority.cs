// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Render queue priority; lower values render first.</summary>
public enum RenderPriority
{
    /// <summary>Low resolution previews of visible pages, so something appears immediately.</summary>
    VisiblePreview = 0,

    /// <summary>Full resolution tiles that are on screen.</summary>
    Visible = 1,

    /// <summary>Tiles just outside the viewport.</summary>
    Prefetch = 2,

    /// <summary>Sidebar thumbnails.</summary>
    Thumbnail = 3,

    /// <summary>Work for documents that are not in the active tab.</summary>
    Background = 4,
}
