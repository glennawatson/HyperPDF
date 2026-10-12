// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Rendering;

/// <summary>A completed render, waiting to be moved into the <see cref="TileCache"/> on the UI thread.</summary>
/// <param name="Key">The tile key.</param>
/// <param name="Surface">The rendered surface; ownership passes to the receiver.</param>
[DebuggerDisplay("RenderedTile: {Key}")]
public readonly record struct RenderedTile(TileKey Key, IRenderSurface Surface);
