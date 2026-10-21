// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;
using HyperPdf.App.Rendering;

namespace HyperPdf.App.Tests;

/// <summary>Adds the Avalonia view of prepared tiles to the tests.</summary>
internal static class GpuTileMixins
{
    /// <summary>Adds the software bitmap view of a prepared tile.</summary>
    /// <param name="tile">The tile.</param>
    extension(GpuPreparedRenderSurface tile)
    {
        /// <summary>Gets the fallback bitmap after it has been rendered.</summary>
        internal Bitmap? SoftwareBitmap => (tile.SoftwareSurface as AvaloniaRenderSurface)?.Bitmap;
    }
}
