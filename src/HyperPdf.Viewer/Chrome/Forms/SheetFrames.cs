// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels.Output;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Draws the sheet the print window shows, once for each sheet and size.</summary>
internal static class SheetFrames
{
    /// <summary>Brings the surface up to date with the sheet that is shown.</summary>
    /// <param name="surfaces">Makes the surfaces.</param>
    /// <param name="frame">The surface as it is now.</param>
    /// <param name="sheet">The sheet to show.</param>
    /// <param name="room">The room for it.</param>
    /// <returns>The surface as it is after, which is <paramref name="frame"/> when the same sheet is shown at the same size.</returns>
    internal static SheetFrame Get(IRenderSurfaceFactory surfaces, SheetFrame frame, PrintPreviewPage sheet, SurfaceSize room)
    {
        var size = sheet.Size;
        if (size.Width <= 0 || size.Height <= 0 || room.IsEmpty)
        {
            return frame;
        }

        var scale = Math.Min(room.Width / size.Width, room.Height / size.Height);
        var width = Math.Max(1, (int)(size.Width * scale));
        var height = Math.Max(1, (int)(size.Height * scale));
        if (ReferenceEquals(sheet, frame.Sheet) && width == frame.Width && height == frame.Height)
        {
            return frame;
        }

        frame.Surface?.Dispose();
        var surface = surfaces.Create(width, height);
        var job = new SheetJob(sheet.Document, new(sheet.PageIndex, scale, PageRotation.None, 0, 0, RenderFlags.Annotations));
        if (surface.Write(job, static (RenderTarget target, in SheetJob drawn) => drawn.Document.Render(drawn.Info, target)))
        {
            return new(surface, sheet, width, height);
        }

        surface.Dispose();
        return SheetFrame.Empty;
    }

    /// <summary>Lets go of the surface.</summary>
    /// <param name="frame">The surface.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Release(SheetFrame frame) => frame.Surface?.Dispose();
}
