// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Where one symbol's bitmap lives in a <see cref="Jbig2SymbolStore"/>.</summary>
/// <param name="Offset">The offset of the first row in the store's buffer.</param>
/// <param name="Width">The width in pixels, or -1 for a symbol that has no bitmap at all.</param>
/// <param name="Height">The height in pixels.</param>
internal readonly record struct Jbig2Symbol(int Offset, int Width, int Height)
{
    /// <summary>Gets a symbol that has no bitmap, which stands for an empty (zero-size) symbol.</summary>
    internal static Jbig2Symbol Absent { get; } = new(0, -1, 0);

    /// <summary>Gets a value indicating whether the symbol has a bitmap, which may still be empty.</summary>
    internal bool IsPresent => Width >= 0;

    /// <summary>Gets the number of bytes of the symbol's rows.</summary>
    internal int Length => IsPresent ? Jbig2Bits.Stride(Width) * Height : 0;
}
