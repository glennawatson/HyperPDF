// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Reads the parts of a CFF font a subset needs (Adobe Technical Note #5176).</summary>
internal static class CffFaceReader
{
    /// <summary>The CFF major version supported.</summary>
    private const int SupportedMajor = 1;

    /// <summary>The offset of the header size in the header.</summary>
    private const int HeaderSizeOffset = 2;

    /// <summary>The smallest header.</summary>
    private const int MinHeaderSize = 4;

    /// <summary>The most Font DICTs read.</summary>
    private const int MaxFontDicts = 256;

    /// <summary>The <c>FontBBox</c> operator.</summary>
    private const int FontBoxOperator = 5;

    /// <summary>The <c>charset</c> operator.</summary>
    private const int CharsetOperator = 15;

    /// <summary>The <c>CharStrings</c> operator.</summary>
    private const int CharStringsOperator = 17;

    /// <summary>The <c>Private</c> operator.</summary>
    private const int PrivateOperator = 18;

    /// <summary>The <c>Subrs</c> operator.</summary>
    private const int SubrsOperator = 19;

    /// <summary>The <c>FontMatrix</c> operator.</summary>
    private const int FontMatrixOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 7;

    /// <summary>The <c>ROS</c> operator.</summary>
    private const int RosOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 30;

    /// <summary>The <c>FDArray</c> operator.</summary>
    private const int FdArrayOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 36;

    /// <summary>The <c>FDSelect</c> operator.</summary>
    private const int FdSelectOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 37;

    /// <summary>The <c>FontName</c> operator of a Font DICT.</summary>
    private const int FontNameOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 38;

    /// <summary>The index of the ordering operand of <c>ROS</c>.</summary>
    private const int OrderingOperand = 1;

    /// <summary>The index of the supplement operand of <c>ROS</c>.</summary>
    private const int SupplementOperand = 2;

    /// <summary>Reads a font.</summary>
    /// <param name="data">The CFF data, which the face keeps.</param>
    /// <param name="face">Receives the face.</param>
    /// <returns><see langword="true"/> when the data is a CFF version 1 font with charstrings.</returns>
    internal static bool TryRead(ReadOnlyMemory<byte> data, [NotNullWhen(true)] out CffFace? face)
    {
        face = null;
        var span = data.Span;
        if (span.Length < MinHeaderSize || span[0] != SupportedMajor)
        {
            return false;
        }

        _ = CffIndex.Read(span, span[HeaderSizeOffset], out var position);
        var topDicts = CffIndex.Read(span, position, out position);
        var strings = CffIndex.Read(span, position, out position);
        var globalSubrs = CffIndex.Read(span, position, out _);
        var top = topDicts.Get(span, 0);
        var entries = CffDictScanner.Scan(top);
        var offsets = ReadOffsets(top, entries);
        var charStrings = offsets.CharStrings > 0 ? CffIndex.Read(span, offsets.CharStrings, out _) : default;
        if (charStrings.Count == 0)
        {
            return false;
        }

        var charset = CffCharsets.ReadCharset(span, offsets.Charset, charStrings.Count, offsets.IsCid);
        var dicts = offsets.IsCid ? ReadFontDicts(span, offsets.FdArray) : [ReadPrivate(span, [], offsets.PrivateSize, offsets.PrivateOffset)];
        var select = offsets.IsCid ? CffCharsets.ReadFdSelect(span, offsets.FdSelect, charStrings.Count, dicts.Length) : [];
        face = new(
            data,
            offsets.IsCid,
            charStrings,
            globalSubrs,
            strings,
            charset,
            select,
            dicts,
            CffDictScanner.Bytes(top, offsets.FontBox).ToArray(),
            CffDictScanner.Bytes(top, offsets.FontMatrix).ToArray(),
            [offsets.Registry, offsets.Ordering, offsets.Supplement]);
        return true;
    }

    /// <summary>Reads the entries of the Top DICT that the face keeps or follows.</summary>
    /// <param name="top">The Top DICT's bytes.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>The values.</returns>
    private static CffTopValues ReadOffsets(ReadOnlySpan<byte> top, List<CffDictEntry> entries)
    {
        var offsets = new CffTopValues();
        foreach (var entry in entries)
        {
            switch (entry.Operator)
            {
                case CharStringsOperator:
                {
                    offsets.CharStrings = CffDictScanner.GetInt(top, entry, 0);
                    break;
                }

                case CharsetOperator:
                {
                    offsets.Charset = CffDictScanner.GetInt(top, entry, 0);
                    break;
                }

                case PrivateOperator:
                {
                    offsets.PrivateSize = CffDictScanner.GetInt(top, entry, 0);
                    offsets.PrivateOffset = CffDictScanner.GetInt(top, entry, 1);
                    break;
                }

                case FdArrayOperator:
                {
                    offsets.FdArray = CffDictScanner.GetInt(top, entry, 0);
                    break;
                }

                case FdSelectOperator:
                {
                    offsets.FdSelect = CffDictScanner.GetInt(top, entry, 0);
                    break;
                }

                case RosOperator:
                {
                    offsets.IsCid = true;
                    offsets.Registry = CffDictScanner.GetInt(top, entry, 0);
                    offsets.Ordering = CffDictScanner.GetInt(top, entry, OrderingOperand);
                    offsets.Supplement = CffDictScanner.GetInt(top, entry, SupplementOperand);
                    break;
                }

                case FontBoxOperator:
                {
                    offsets.FontBox = entry;
                    break;
                }

                case FontMatrixOperator:
                {
                    offsets.FontMatrix = entry;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        return offsets;
    }

    /// <summary>Reads the Font DICTs of a CID-keyed font.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="arrayOffset">The offset of the FDArray INDEX.</param>
    /// <returns>The Font DICTs.</returns>
    private static CffFontDict[] ReadFontDicts(ReadOnlySpan<byte> data, int arrayOffset)
    {
        var index = arrayOffset > 0 ? CffIndex.Read(data, arrayOffset, out _) : default;
        var count = Math.Clamp(index.Count, 1, MaxFontDicts);
        var dicts = new CffFontDict[count];
        for (var i = 0; i < count; i++)
        {
            var dict = index.Get(data, i);
            var entries = CffDictScanner.Scan(dict);
            var kept = new List<byte>(dict.Length);
            var size = 0;
            var offset = 0;
            foreach (var entry in entries)
            {
                if (entry.Operator == PrivateOperator)
                {
                    size = CffDictScanner.GetInt(dict, entry, 0);
                    offset = CffDictScanner.GetInt(dict, entry, 1);
                }
                else if (entry.Operator != FontNameOperator)
                {
                    kept.AddRange(CffDictScanner.Bytes(dict, entry));
                }
            }

            dicts[i] = ReadPrivate(data, [.. kept], size, offset);
        }

        return dicts;
    }

    /// <summary>Reads a Private DICT and its local subroutines.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="kept">The Font DICT's kept entries.</param>
    /// <param name="size">The Private DICT's size.</param>
    /// <param name="offset">The Private DICT's offset.</param>
    /// <returns>The Font DICT's parts.</returns>
    private static CffFontDict ReadPrivate(ReadOnlySpan<byte> data, byte[] kept, int size, int offset)
    {
        var dict = size == 0 ? [] : FontBytes.Slice(data, offset, size);
        var entries = CffDictScanner.Scan(dict);
        var privateKept = new List<byte>(dict.Length);
        var subrs = default(CffIndex);
        foreach (var entry in entries)
        {
            if (entry.Operator == SubrsOperator)
            {
                subrs = CffIndex.Read(data, CffPrivate.RelativeOffset(data, offset, CffDictScanner.GetInt(dict, entry, 0)), out _);
            }
            else
            {
                privateKept.AddRange(CffDictScanner.Bytes(dict, entry));
            }
        }

        return new(kept, [.. privateKept], subrs);
    }
}
