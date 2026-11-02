// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A texture declaration and the pieces of image data that have arrived for it.</summary>
/// <param name="Name">The texture's name.</param>
/// <param name="Width">The declared width.</param>
/// <param name="Height">The declared height.</param>
/// <param name="Images">The continuation images the declaration lists.</param>
[DebuggerDisplay("U3dTexturePieces: {Name} {Width}x{Height}")]
internal sealed record U3dTexturePieces(string Name, int Width, int Height, U3dContinuationImage[] Images)
{
    /// <summary>Gets the image data received for each continuation image, joined in the order it arrived.</summary>
    internal List<byte>[] Data { get; } = NewData(Images.Length);

    /// <summary>Makes one empty buffer for each continuation image.</summary>
    /// <param name="count">The number of images.</param>
    /// <returns>The buffers.</returns>
    private static List<byte>[] NewData(int count)
    {
        var data = new List<byte>[count];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = [];
        }

        return data;
    }
}
