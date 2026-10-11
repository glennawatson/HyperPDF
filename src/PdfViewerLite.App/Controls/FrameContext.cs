// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.App.Controls;

/// <summary>State shared by everything drawn in one canvas frame.</summary>
/// <param name="Tab">The tab.</param>
/// <param name="Document">The open document.</param>
/// <param name="Hub">The render hub.</param>
/// <param name="Viewport">The viewport in canvas coordinates.</param>
/// <param name="RenderScaling">Device pixels per canvas unit.</param>
/// <param name="Generation">The render generation for requests made this frame.</param>
internal readonly record struct FrameContext(DocumentTabViewModel Tab, IDocument Document, RenderHub Hub, Rect Viewport, double RenderScaling, int Generation)
{
    /// <summary>Queues a render request for the tab's canvas.</summary>
    /// <param name="key">The tile key.</param>
    /// <param name="info">What to render.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="priority">The priority.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Request(in TileKey key, in PageRenderInfo info, int width, int height, RenderPriority priority) =>
        Hub.Scheduler.Request(new(key, Document, info, width, height, priority, Tab.CanvasClient, Generation, Tab.PageTone) { CancellationToken = Tab.SelectedWorkToken });
}
