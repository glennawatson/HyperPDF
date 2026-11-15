// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels.Output;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>The surface that shows a sheet of the print window, and which sheet and size it shows.</summary>
/// <param name="Surface">The surface, or <see langword="null"/> before the first sheet.</param>
/// <param name="Sheet">The sheet the surface shows.</param>
/// <param name="Width">The width of the surface.</param>
/// <param name="Height">The height of the surface.</param>
[DebuggerDisplay("SheetFrame: {Width} x {Height}")]
internal sealed record SheetFrame(IRenderSurface? Surface, PrintPreviewPage? Sheet, int Width, int Height)
{
    /// <summary>Gets the state before any sheet was shown.</summary>
    internal static SheetFrame Empty { get; } = new(null, null, 0, 0);
}
