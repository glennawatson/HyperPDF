// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Controls;

/// <summary>
/// What a frame's prefetch and preview-reach passes depended on. When the stamp is the same as the last frame's and that
/// frame found every tile already cached, the passes would find the same thing again and are skipped.
/// </summary>
/// <param name="Generation">The render generation, which changes with the tab, document, viewport, scale, layout, rotation and tone.</param>
/// <param name="First">The first drawn page.</param>
/// <param name="Last">The last drawn page.</param>
/// <param name="ScrollingDown">Which side previews are made further ahead on.</param>
/// <param name="ZoomEasing">Whether a zoom is easing, when requests are held back.</param>
/// <param name="WorkCancelled">Whether the tab's work token was cancelled, so requests would be refused.</param>
/// <param name="CacheCount">The tiles in the cache, which changes when one arrives or is evicted.</param>
/// <param name="CacheBytes">The bytes the cache holds.</param>
/// <param name="PreviewBytes">The bytes previews hold.</param>
internal readonly record struct PrefetchStamp(
    int Generation,
    int First,
    int Last,
    bool ScrollingDown,
    bool ZoomEasing,
    bool WorkCancelled,
    int CacheCount,
    long CacheBytes,
    long PreviewBytes);
