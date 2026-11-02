// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Decodes scene node blocks: group, model, light and view nodes (ECMA-363 9.5).</summary>
internal static class U3dNodeDecoder
{
    /// <summary>The fewest bytes a parent takes: an empty name's length and the sixteen-float transform.</summary>
    private const int ParentMinimum = 66;

    /// <summary>The bytes of a backdrop or overlay besides its texture name: eight 32-bit values.</summary>
    private const int LayerValues = 32;

    /// <summary>The bytes of a backdrop or overlay with an empty texture name.</summary>
    private const int LayerMinimum = LayerValues + 2;

    /// <summary>The projection bits of a view node's attributes.</summary>
    private const uint ProjectionMask = 6;

    /// <summary>The attribute value of an orthographic projection.</summary>
    private const uint Orthographic = 2;

    /// <summary>The attribute value of a three-point perspective projection.</summary>
    private const uint ThreePoint = 0;

    /// <summary>Reads a node block.</summary>
    /// <param name="kind">The kind of node the block type declares.</param>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the node.</param>
    internal static void Read(U3dNodeKind kind, ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        if (builder.Nodes.Count >= builder.Limits.MaxNodes)
        {
            builder.Problem("The file has more scene nodes than will be read.");
            return;
        }

        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var count = reader.ReadU32();
        if (reader.Failed || (long)count * ParentMinimum > reader.RemainingBytes)
        {
            builder.Problem("A scene node declares more parents than its block can hold.");
            return;
        }

        var parents = new string[count];
        var transforms = new Matrix4x4[count];
        for (var i = 0; i < parents.Length; i++)
        {
            parents[i] = reader.ReadString();
            transforms[i] = reader.ReadMatrix();
        }

        var resource = kind == U3dNodeKind.Group ? string.Empty : reader.ReadString();
        var visibility = kind == U3dNodeKind.Model ? reader.ReadU32() : 0;
        var view = kind == U3dNodeKind.View ? ReadView(ref reader) : null;
        if (reader.Failed)
        {
            builder.Problem("A scene node block is cut short.");
            return;
        }

        builder.Nodes.Add(new(kind, name, parents, transforms, resource, visibility, view));
    }

    /// <summary>Reads the instance fields of a view node: attributes, clipping, projection, view port, backdrops and overlays.</summary>
    /// <param name="reader">The block's reader, placed after the view resource name.</param>
    /// <returns>The settings, or <see langword="null"/> when the block is cut short.</returns>
    private static U3dViewSettings? ReadView(ref U3dBitReader reader)
    {
        var attributes = reader.ReadU32();
        var near = reader.ReadF32();
        var far = reader.ReadF32();
        float fieldOfView = 0;
        float height = 0;
        switch (attributes & ProjectionMask)
        {
            case ThreePoint:
            {
                fieldOfView = reader.ReadF32();
                break;
            }

            case Orthographic:
            {
                height = reader.ReadF32();
                break;
            }

            default:
            {
                // One-point and two-point views carry a vector instead of a field of view; it is read past, not used.
                _ = reader.ReadVector3();
                break;
            }
        }

        _ = reader.ReadVector4();
        var backdrops = SkipLayers(ref reader);
        var overlays = SkipLayers(ref reader);
        return reader.Failed ? null : new(attributes, near, far, fieldOfView, height, backdrops, overlays);
    }

    /// <summary>Reads past the backdrops or overlays of a view node, which are not drawn.</summary>
    /// <param name="reader">The block's reader, placed at the count.</param>
    /// <returns>The number of layers, or zero when the block is cut short.</returns>
    private static int SkipLayers(ref U3dBitReader reader)
    {
        var count = reader.ReadU32();
        if (reader.Failed || (long)count * LayerMinimum > reader.RemainingBytes)
        {
            reader.Fail();
            return 0;
        }

        for (var i = 0; i < count; i++)
        {
            _ = reader.ReadString();
            for (var value = 0; value < LayerValues / sizeof(uint); value++)
            {
                _ = reader.ReadU32();
            }
        }

        return (int)count;
    }
}
