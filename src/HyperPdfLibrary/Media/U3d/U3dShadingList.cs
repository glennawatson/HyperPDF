// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A shading description: which per-vertex values a shader list uses besides position and normal (ECMA-363 9.6.1.1.3.9).</summary>
/// <param name="Attributes">The flags: 1 per-vertex diffuse colour, 2 per-vertex specular colour.</param>
/// <param name="LayerDimensions">The number of dimensions of each texture coordinate layer.</param>
[DebuggerDisplay("U3dShadingList: {Attributes} {LayerDimensions.Length} layers")]
internal sealed record U3dShadingList(uint Attributes, uint[] LayerDimensions)
{
    /// <summary>The fewest bytes a shading description takes: the attributes, a layer count and the original index.</summary>
    private const int ListMinimum = 12;

    /// <summary>The bytes of one texture layer's dimension count.</summary>
    private const int LayerBytes = 4;

    /// <summary>The attribute flag for a diffuse colour at each corner.</summary>
    private const uint DiffuseFlag = 1;

    /// <summary>The attribute flag for a specular colour at each corner.</summary>
    private const uint SpecularFlag = 2;

    /// <summary>Gets a value indicating whether corners carry a diffuse colour index.</summary>
    internal bool UsesDiffuse => (Attributes & DiffuseFlag) != 0;

    /// <summary>Gets a value indicating whether corners carry a specular colour index.</summary>
    internal bool UsesSpecular => (Attributes & SpecularFlag) != 0;

    /// <summary>Reads the shading descriptions of a declaration.</summary>
    /// <param name="reader">The block's reader, placed at the shading count.</param>
    /// <returns>The descriptions, or <see langword="null"/> when the block is cut short or the counts could not fit in it.</returns>
    internal static U3dShadingList[]? ReadAll(ref U3dBitReader reader)
    {
        var count = reader.ReadU32();
        if (reader.Failed || (long)count * ListMinimum > reader.RemainingBytes)
        {
            return null;
        }

        var lists = new U3dShadingList[count];
        for (var i = 0; i < lists.Length; i++)
        {
            var attributes = reader.ReadU32();
            var layers = reader.ReadU32();
            if (reader.Failed || (long)layers * LayerBytes > reader.RemainingBytes)
            {
                return null;
            }

            var dimensions = new uint[layers];
            for (var layer = 0; layer < dimensions.Length; layer++)
            {
                dimensions[layer] = reader.ReadU32();
            }

            _ = reader.ReadU32();
            lists[i] = new(attributes, dimensions);
        }

        return reader.Failed ? null : lists;
    }
}
