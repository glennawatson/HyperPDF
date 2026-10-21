// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Media;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.Rendering;

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
        var batch = new TileDrawBatch();
        batch.Draw(context, surface, destination, smooth);
        batch.Flush(context);
    }
}
