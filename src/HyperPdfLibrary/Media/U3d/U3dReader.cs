// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Reads the structure of a U3D file (ECMA-363, fourth edition, clause 9): the file header, the blocks, the scene nodes, the size
/// of each shape and the names of the resources. It never throws for a damaged file: it reports what is wrong and keeps what it
/// read. It does not decode shape data, textures or compressed values, and it never loads another file.
/// </summary>
public static class U3dReader
{
    /// <summary>The most blocks read, counting those inside modifier chains.</summary>
    private const int BlockLimit = 200_000;

    /// <summary>The most modifiers one modifier chain may declare.</summary>
    private const int ChainModifierLimit = 1_000;

    /// <summary>The position of the data size in a block's frame.</summary>
    private const int DataSizeOffset = 4;

    /// <summary>The position of the meta data size in a block's frame.</summary>
    private const int MetaSizeOffset = 8;

    /// <summary>The bytes of a block's type, data size and meta data size.</summary>
    private const int FrameLength = 12;

    /// <summary>The alignment of blocks and their sections.</summary>
    private const int Alignment = 4;

    /// <summary>The profile flag for defined units.</summary>
    private const uint DefinedUnits = 8;

    /// <summary>The bytes of a chain's bounding sphere: four 32-bit floats.</summary>
    private const int BoundingSphereLength = 16;

    /// <summary>The bytes of a chain's bounding box: six 32-bit floats.</summary>
    private const int BoundingBoxLength = 24;

    /// <summary>The chain attribute for a bounding sphere.</summary>
    private const uint HasBoundingSphere = 1;

    /// <summary>The chain attribute for a bounding box.</summary>
    private const uint HasBoundingBox = 2;

    /// <summary>Gets the most blocks read, counting those inside modifier chains.</summary>
    public static int MaxBlocks => BlockLimit;

    /// <summary>Gets the most modifiers one modifier chain may declare.</summary>
    public static int MaxChainModifiers => ChainModifierLimit;

    /// <summary>Reads a U3D file.</summary>
    /// <param name="data">The file's bytes, which the 3D stream of a PDF holds.</param>
    /// <returns>What the file declares, and what could not be read.</returns>
    public static U3dScene Read(ReadOnlySpan<byte> data)
    {
        var builder = new U3dSceneBuilder();
        var at = 0;
        if (NextBlock(data, ref at, out var type, out var header) && type == (uint)U3dBlockType.FileHeader)
        {
            builder.BlockCount++;
            ReadHeader(header, builder);
            ReadBlocks(data[at..], builder, false);
            CheckSize(data.Length, header, builder);
            U3dHierarchy.Check(builder);
        }
        else
        {
            builder.Problem("This is not a U3D file: it does not start with a file header.");
        }

        return builder.Build();
    }

    /// <summary>Gets the length of a section after padding it to the alignment.</summary>
    /// <param name="length">The section's length.</param>
    /// <returns>The padded length.</returns>
    private static long Padded(uint length) => ((long)length + Alignment - 1) / Alignment * Alignment;

    /// <summary>Finds the next block in a region.</summary>
    /// <param name="region">The bytes from the start of the block to the end of the region.</param>
    /// <param name="at">The position of the block, moved past it.</param>
    /// <param name="type">The block's type.</param>
    /// <param name="payload">The block's data section.</param>
    /// <returns><see langword="false"/> when the block is cut short.</returns>
    private static bool NextBlock(ReadOnlySpan<byte> region, ref int at, out uint type, out ReadOnlySpan<byte> payload)
    {
        type = 0;
        payload = default;
        if (region.Length - at < FrameLength)
        {
            return false;
        }

        var frame = region[at..];
        type = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(frame[DataSizeOffset..]);
        var metaSize = BinaryPrimitives.ReadUInt32LittleEndian(frame[MetaSizeOffset..]);
        var total = FrameLength + Padded(dataSize) + Padded(metaSize);
        if (dataSize > frame.Length - FrameLength || (total > frame.Length && FrameLength + (long)dataSize + metaSize > frame.Length))
        {
            return false;
        }

        payload = frame.Slice(FrameLength, (int)dataSize);
        at += (int)Math.Min(total, frame.Length);
        return true;
    }

    /// <summary>Reads the file header block.</summary>
    /// <param name="payload">The block's data.</param>
    /// <param name="builder">Receives the header's values.</param>
    private static void ReadHeader(ReadOnlySpan<byte> payload, U3dSceneBuilder builder)
    {
        var cursor = new U3dCursor(payload);
        builder.MajorVersion = cursor.ReadI16();
        builder.MinorVersion = cursor.ReadI16();
        builder.Profile = cursor.ReadU32();
        _ = cursor.ReadU32();
        _ = cursor.ReadU64();
        _ = cursor.ReadU32();
        builder.Units = (builder.Profile & DefinedUnits) != 0 ? cursor.ReadF64() : null;
        if (cursor.Failed)
        {
            builder.Stop("The file header is cut short.");
        }
    }

    /// <summary>Compares the size the header gives with the bytes there are.</summary>
    /// <param name="length">The number of bytes in the file.</param>
    /// <param name="header">The header's data.</param>
    /// <param name="builder">Receives a problem when the sizes differ.</param>
    private static void CheckSize(int length, ReadOnlySpan<byte> header, U3dSceneBuilder builder)
    {
        var cursor = new U3dCursor(header);
        _ = cursor.ReadI16();
        _ = cursor.ReadI16();
        _ = cursor.ReadU32();
        _ = cursor.ReadU32();
        var declared = cursor.ReadU64();
        if (!cursor.Failed && declared > (ulong)length)
        {
            builder.Problem("The file is shorter than its header says, so it was cut short.");
        }
    }

    /// <summary>Reads the blocks of a region one after another.</summary>
    /// <param name="region">The bytes of the blocks.</param>
    /// <param name="builder">Receives what the blocks declare.</param>
    /// <param name="insideChain">Whether the region is the inside of a modifier chain.</param>
    private static void ReadBlocks(ReadOnlySpan<byte> region, U3dSceneBuilder builder, bool insideChain)
    {
        var at = 0;
        while (at < region.Length && !builder.Stopped)
        {
            if (builder.BlockCount >= BlockLimit)
            {
                builder.Stop("The file has more blocks than will be read.");
                return;
            }

            if (!NextBlock(region, ref at, out var type, out var payload))
            {
                builder.Stop("A block is cut short or declares more data than the file has.");
                return;
            }

            builder.BlockCount++;
            if (type == (uint)U3dBlockType.ModifierChain && !insideChain)
            {
                ReadChain(payload, builder);
                continue;
            }

            U3dDeclarations.Apply(type, payload, builder);
        }
    }

    /// <summary>Reads a modifier chain: its header, then the declaration blocks it holds.</summary>
    /// <param name="payload">The chain block's data.</param>
    /// <param name="builder">Receives what the chain declares.</param>
    private static void ReadChain(ReadOnlySpan<byte> payload, U3dSceneBuilder builder)
    {
        var cursor = new U3dCursor(payload);
        _ = cursor.ReadString();
        _ = cursor.ReadU32();
        var attributes = cursor.ReadU32();
        _ = cursor.Skip((attributes & HasBoundingSphere) != 0 ? BoundingSphereLength : 0);
        _ = cursor.Skip((attributes & HasBoundingBox) != 0 ? BoundingBoxLength : 0);
        cursor.Align();
        var count = cursor.ReadU32();
        if (cursor.Failed)
        {
            builder.Problem("A modifier chain is cut short.");
            return;
        }

        if (count > ChainModifierLimit)
        {
            builder.Stop("A modifier chain declares more modifiers than will be read.");
            return;
        }

        ReadBlocks(payload[cursor.Position..], builder, true);
    }
}
