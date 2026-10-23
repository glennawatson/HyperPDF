// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Skia;

namespace HyperPdf.App.Tests;

/// <summary>Builds prepared tiles that fall back to Skia bitmaps, as the app's render head does.</summary>
internal static class TestTiles
{
    /// <summary>Makes the Skia software tile a prepared tile falls back to.</summary>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The tile.</returns>
    internal static IRenderSurface Software(int width, int height) => new SkiaBitmapRenderSurface(width, height);
}
