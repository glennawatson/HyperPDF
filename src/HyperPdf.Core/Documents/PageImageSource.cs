// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A picture that becomes a page of its own.</summary>
/// <param name="Pixels">Tightly packed, straight-alpha BGRA pixels, in rows from top to bottom.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("PageImageSource: {Width} x {Height}")]
public readonly record struct PageImageSource(ReadOnlyMemory<byte> Pixels, int Width, int Height);
