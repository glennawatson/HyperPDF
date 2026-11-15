// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>The surface that shows the latest picture of a film, and which picture and size it shows.</summary>
/// <param name="Surface">The surface, or <see langword="null"/> before the first picture.</param>
/// <param name="Width">The width of the surface.</param>
/// <param name="Height">The height of the surface.</param>
/// <param name="Version">The number of the picture the surface shows, or -1 when it shows none yet.</param>
[DebuggerDisplay("MediaFrame: {Width} x {Height}, picture {Version}")]
internal sealed record MediaFrame(IRenderSurface? Surface, int Width, int Height, int Version)
{
    /// <summary>Gets the state before any picture was shown.</summary>
    internal static MediaFrame Empty { get; } = new(null, 0, 0, -1);
}
