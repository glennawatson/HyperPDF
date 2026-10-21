// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Services;

/// <summary>An image read from a file, as tightly packed, straight-alpha BGRA pixels.</summary>
/// <param name="Name">The file name, shown so the user knows which picture was chosen.</param>
/// <param name="Pixels">The pixels, in rows from top to bottom.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("DecodedImage: {Name}, {Width} x {Height}")]
public sealed record DecodedImage(string Name, ReadOnlyMemory<byte> Pixels, int Width, int Height);
