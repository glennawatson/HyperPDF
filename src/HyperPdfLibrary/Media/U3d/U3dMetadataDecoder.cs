// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Reads the meta data section of a block: pairs of a key and a value, which are only recorded (ECMA-363 9.2.6).</summary>
internal static class U3dMetadataDecoder
{
    /// <summary>The most entries kept for a whole file.</summary>
    private const int MaxEntries = 1_000;

    /// <summary>The fewest bytes an entry takes: its attributes and two empty strings.</summary>
    private const int EntryMinimum = 4 + 2 + 2;

    /// <summary>The attribute flag for a binary value.</summary>
    private const uint BinaryFlag = 1;

    /// <summary>Reads a block's meta data.</summary>
    /// <param name="type">The type of the block the meta data came from.</param>
    /// <param name="meta">The meta data section.</param>
    /// <param name="builder">Receives the entries.</param>
    internal static void Read(uint type, ReadOnlyMemory<byte> meta, U3dModelBuilder builder)
    {
        if (meta.IsEmpty)
        {
            return;
        }

        var reader = builder.Open(meta, null);
        var count = reader.ReadU32();
        if (reader.Failed || (long)count * EntryMinimum > reader.RemainingBytes)
        {
            builder.Problem("A block's meta data declares more entries than it can hold.");
            return;
        }

        for (var i = 0; i < count && builder.Metadata.Count < MaxEntries; i++)
        {
            var attributes = reader.ReadU32();
            var key = reader.ReadString();
            var value = ReadValue(ref reader, attributes);
            if (reader.Failed)
            {
                builder.Problem("A block's meta data is cut short.");
                return;
            }

            builder.Metadata.Add(new(type, key, value, attributes));
        }
    }

    /// <summary>Reads a value: a string, or the size and bytes of a binary value, which are skipped.</summary>
    /// <param name="reader">The meta data reader.</param>
    /// <param name="attributes">The entry's attributes.</param>
    /// <returns>The text of a string value; empty for a binary value.</returns>
    private static string ReadValue(ref U3dBitReader reader, uint attributes)
    {
        if ((attributes & BinaryFlag) == 0)
        {
            return reader.ReadString();
        }

        var size = reader.ReadU32();
        if (size > reader.RemainingBytes)
        {
            reader.Fail();
            return string.Empty;
        }

        for (uint i = 0; i < size; i++)
        {
            _ = reader.ReadU8();
        }

        return string.Empty;
    }
}
