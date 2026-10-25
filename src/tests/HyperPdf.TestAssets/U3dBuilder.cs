// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes small U3D files by hand from the block layouts of ECMA-363 (fourth edition, clause 9). The files are written by this
/// test helper from the specification, not by another program, so they check that a reader follows the written layout and
/// not that it agrees with other software.
/// </summary>
public sealed class U3dBuilder
{
    /// <summary>The bytes of a 32-bit value.</summary>
    private const int WordSize = 4;

    /// <summary>The bytes of a string's length.</summary>
    private const int LengthSize = 2;

    /// <summary>The bytes of a block's type, data size and meta data size.</summary>
    private const int FrameSize = 12;

    /// <summary>The bytes of a node's parent transform: sixteen 32-bit floats.</summary>
    private const int TransformSize = 64;

    /// <summary>The bytes of the file size field.</summary>
    private const int FileSizeLength = 8;

    /// <summary>The position of the data size in a frame.</summary>
    private const int DataSizeOffset = 4;

    /// <summary>The position of the file size field in the file: after the header's frame, version, profile and declaration size.</summary>
    private const int FileSizeOffset = FrameSize + WordSize + WordSize + WordSize;

    /// <summary>The character encoding number for UTF-8.</summary>
    private const uint Utf8 = 106;

    /// <summary>The block type of the file header.</summary>
    private const uint HeaderType = 0x00443355;

    /// <summary>The block type of a modifier chain.</summary>
    private const uint ChainType = 0xFFFFFF14;

    /// <summary>The block type of a group node.</summary>
    private const uint GroupNodeType = 0xFFFFFF21;

    /// <summary>The block type of a model node.</summary>
    private const uint ModelNodeType = 0xFFFFFF22;

    /// <summary>The block type of a light node.</summary>
    private const uint LightNodeType = 0xFFFFFF23;

    /// <summary>The block type of a view node.</summary>
    private const uint ViewNodeType = 0xFFFFFF24;

    /// <summary>The block type of a continuous level of detail mesh declaration.</summary>
    private const uint MeshType = 0xFFFFFF31;

    /// <summary>The block type of a material resource.</summary>
    private const uint MaterialType = 0xFFFFFF54;

    /// <summary>The block type of a texture declaration.</summary>
    private const uint TextureType = 0xFFFFFF55;

    /// <summary>The block type of the base mesh data.</summary>
    private const uint BaseMeshDataType = 0xFFFFFF3B;

    /// <summary>The block type of the texture image data.</summary>
    private const uint TextureDataType = 0xFFFFFF5C;

    /// <summary>The faces of the box in the sample model.</summary>
    private const uint BoxFaces = 12;

    /// <summary>The positions of the box in the sample model.</summary>
    private const uint BoxPositions = 8;

    /// <summary>The normals of the box in the sample model.</summary>
    private const uint BoxNormals = 6;

    /// <summary>A model node's visibility field: front and back visible.</summary>
    private const uint FrontAndBack = 3;

    /// <summary>A word of data for blocks that are kept but not decoded.</summary>
    private const uint Filler = 7;

    /// <summary>The name of the sample model's mesh.</summary>
    private const string BoxMesh = "BoxMesh";

    /// <summary>The blocks after the file header.</summary>
    private readonly List<byte[]> _blocks = [];

    /// <summary>Gets the profile flag for defined units.</summary>
    public static uint DefinedUnits { get; } = 8;

    /// <summary>Gets the units factor written for a file with units: millimetres.</summary>
    public static double Millimetres { get; } = 0.001;

    /// <summary>Gets or sets the profile flags written in the header.</summary>
    public uint Profile { get; set; }

    /// <summary>Writes a string: a 16-bit length and the UTF-8 bytes.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Text(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var result = new byte[LengthSize + bytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)bytes.Length);
        bytes.CopyTo(result, LengthSize);
        return result;
    }

    /// <summary>Writes 32-bit values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Words(params uint[] values)
    {
        var result = new byte[values.Length * WordSize];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * WordSize), values[i]);
        }

        return result;
    }

    /// <summary>Joins pieces of data.</summary>
    /// <param name="pieces">The pieces.</param>
    /// <returns>The joined bytes.</returns>
    public static byte[] Join(params byte[][] pieces) => [.. pieces.SelectMany(static piece => piece)];

    /// <summary>Writes a block: its type, sizes, data and padding.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="data">The data section.</param>
    /// <returns>The bytes of the block.</returns>
    public static byte[] Block(uint type, byte[] data)
    {
        var block = new byte[FrameSize + Padded(data.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(block, type);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(DataSizeOffset), (uint)data.Length);
        data.CopyTo(block, FrameSize);
        return block;
    }

    /// <summary>Writes a node block's data: name, parents with zeroed transforms, and the extra fields.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="parents">The parents' names.</param>
    /// <param name="extra">The fields after the parents.</param>
    /// <returns>The data.</returns>
    public static byte[] NodeData(string name, string[] parents, byte[] extra)
    {
        var parts = new List<byte[]> { Text(name), Words((uint)parents.Length) };
        foreach (var parent in parents)
        {
            parts.Add(Text(parent));
            parts.Add(new byte[TransformSize]);
        }

        parts.Add(extra);
        return Join([.. parts]);
    }

    /// <summary>Writes a shape declaration's data.</summary>
    /// <param name="name">The shape's name.</param>
    /// <param name="primitives">The faces, points or lines.</param>
    /// <param name="positions">The positions.</param>
    /// <param name="normals">The normals.</param>
    /// <returns>The data.</returns>
    public static byte[] ShapeData(string name, uint primitives, uint positions, uint normals) =>
        Join(Text(name), Words(0, 0, primitives, positions, normals, 0, 0, 0, 0));

    /// <summary>Writes a modifier chain block holding blocks.</summary>
    /// <param name="name">The chain's name.</param>
    /// <param name="chainType">0 for a node chain, 1 for a model resource chain, 2 for a texture chain.</param>
    /// <param name="count">The modifier count to write.</param>
    /// <param name="blocks">The blocks inside the chain.</param>
    /// <returns>The chain block.</returns>
    public static byte[] Chain(string name, uint chainType, uint count, params byte[][] blocks)
    {
        var head = Join(Text(name), Words(chainType, 0));
        var padding = new byte[Padded(head.Length) - head.Length];
        return Block(ChainType, Join(head, padding, Words(count), Join(blocks)));
    }

    /// <summary>
    /// Writes a sample model: a root group, a box model with its mesh (12 faces, 8 positions, 6 normals), a light and a view,
    /// with a material, a texture and two blocks of data that are kept but not decoded.
    /// </summary>
    /// <returns>The file's bytes.</returns>
    public static byte[] SampleModel() => new U3dBuilder()
        .Add(Block(GroupNodeType, NodeData("Root", [], [])))
        .Add(Chain("Box", 0, 1, Block(ModelNodeType, NodeData("Box", ["Root"], Join(Text(BoxMesh), Words(FrontAndBack))))))
        .Add(Block(LightNodeType, NodeData("Sun", ["Root"], Text("SunLight"))))
        .Add(Block(ViewNodeType, NodeData("Camera", ["Root"], Text("DefaultView"))))
        .Add(Chain(BoxMesh, 1, 1, Block(MeshType, ShapeData(BoxMesh, BoxFaces, BoxPositions, BoxNormals))))
        .Add(Block(MaterialType, Text("Steel")))
        .Add(Block(TextureType, Text("Grain")))
        .Add(Block(BaseMeshDataType, Words(Filler, Filler)))
        .Add(Block(TextureDataType, Words(Filler)))
        .ToArray();

    /// <summary>Adds a block to the file.</summary>
    /// <param name="block">The block's bytes.</param>
    /// <returns>This builder.</returns>
    public U3dBuilder Add(byte[] block)
    {
        _blocks.Add(block);
        return this;
    }

    /// <summary>Writes the file.</summary>
    /// <returns>The file's bytes.</returns>
    public byte[] ToArray()
    {
        var body = Join([.. _blocks]);
        var units = (Profile & DefinedUnits) != 0 ? BitConverter.GetBytes(Millimetres) : [];
        var fixedPart = Join(new byte[WordSize], Words(Profile, (uint)body.Length), new byte[FileSizeLength], Words(Utf8), units);
        var file = Join(Block(HeaderType, fixedPart), body);
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(FileSizeOffset), (ulong)file.Length);
        return file;
    }

    /// <summary>Rounds a length up to a whole number of 32-bit words.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The padded length.</returns>
    private static int Padded(int length) => (length + WordSize - 1) / WordSize * WordSize;
}
