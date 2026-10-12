// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Engine;

/// <summary>The size of a picture in pixels.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("PixelSize: {Width}x{Height}")]
internal readonly record struct PixelSize(int Width, int Height);
