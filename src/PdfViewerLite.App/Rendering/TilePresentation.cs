// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Media;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.App.Rendering;

/// <summary>Composes tiles through GPU replay or their prepared software bitmap.</summary>
internal static class TilePresentation
{
    /// <summary>Draws one tile in canvas coordinates.</summary>
    /// <param name="context">The recorded drawing context.</param>
    /// <param name="surface">The cached tile.</param>
    /// <param name="destination">The destination bounds.</param>
    /// <param name="smooth">Whether the tile is being scaled as a preview or stand-in.</param>
    internal static void Draw(DrawingContext context, IRenderSurface surface, in Rect destination, bool smooth)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(surface);
        if (surface is GpuPreparedRenderSurface gpu)
        {
            if (gpu.SoftwareBitmap is { } bitmap)
            {
                context.DrawImage(bitmap, new(0, 0, surface.Width, surface.Height), destination);
            }
            else if (GpuTileDrawOperation.TryCreate(gpu, destination, smooth) is { } operation)
            {
                try
                {
                    context.Custom(operation);
                }
                catch
                {
                    operation.Dispose();
                    throw;
                }
            }

            return;
        }

        if (surface is AvaloniaRenderSurface cpu)
        {
            context.DrawImage(cpu.Bitmap, new(0, 0, surface.Width, surface.Height), destination);
        }
    }
}
