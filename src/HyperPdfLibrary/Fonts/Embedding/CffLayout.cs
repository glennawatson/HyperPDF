// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Places the pieces of a subset CFF font in order: header, Name, Top DICT, String and Global Subr INDEXes, charset,
/// FDSelect, CharStrings, FDArray, then each Private DICT with its local subroutines. DICT offsets are fixed-size
/// integers, so a DICT's length is known before the offsets in it are.
/// </summary>
internal static class CffLayout
{
    /// <summary>The bytes of the CFF header.</summary>
    private const int HeaderSize = 4;

    /// <summary>The major version of the header.</summary>
    private const byte MajorVersion = 1;

    /// <summary>The offset size written in the header, which sizes absolute offsets.</summary>
    private const byte HeaderOffsetSize = 4;

    /// <summary>The <c>charset</c> operator.</summary>
    private const int CharsetOperator = 15;

    /// <summary>The <c>CharStrings</c> operator.</summary>
    private const int CharStringsOperator = 17;

    /// <summary>The <c>Private</c> operator.</summary>
    private const int PrivateOperator = 18;

    /// <summary>The <c>Subrs</c> operator.</summary>
    private const int SubrsOperator = 19;

    /// <summary>The <c>ROS</c> operator.</summary>
    private const int RosOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 30;

    /// <summary>The <c>CIDCount</c> operator.</summary>
    private const int CidCountOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 34;

    /// <summary>The <c>FDArray</c> operator.</summary>
    private const int FdArrayOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 36;

    /// <summary>The <c>FDSelect</c> operator.</summary>
    private const int FdSelectOperator = (CffDictScanner.Escape << CffDictScanner.EscapeShift) | 37;

    /// <summary>The bytes of a fixed-size DICT integer and the operator that follows it.</summary>
    private const int EntryBytes = 6;

    /// <summary>Gets the size of a Private DICT written for a Font DICT.</summary>
    /// <param name="dict">The source Font DICT.</param>
    /// <returns>The DICT's size, which excludes the subroutines after it.</returns>
    internal static int PrivateSize(in CffFontDict dict) => dict.PrivateKept.Length + (dict.Subrs.Count > 0 ? EntryBytes : 0);

    /// <summary>Gets the bytes of a Private DICT that is followed by its local subroutines.</summary>
    /// <param name="dict">The source Font DICT.</param>
    /// <returns>The DICT's entries, ending with a <c>Subrs</c> entry that points just past the DICT when there are subroutines.</returns>
    internal static byte[] PrivateDict(in CffFontDict dict)
    {
        var entries = new List<byte>(dict.PrivateKept);
        if (dict.Subrs.Count > 0)
        {
            CffWriting.Entry(entries, SubrsOperator, dict.PrivateKept.Length + EntryBytes);
        }

        return [.. entries];
    }

    /// <summary>Joins the pieces into a font.</summary>
    /// <param name="face">The font being cut down.</param>
    /// <param name="sections">The pieces.</param>
    /// <param name="fontDictOrder">The source Font DICT of each output Font DICT.</param>
    /// <returns>The CFF font.</returns>
    internal static byte[] Assemble(CffFace face, CffSections sections, int[] fontDictOrder)
    {
        var position = HeaderSize + sections.Name.Length + CffWriting.SingleIndex(Top(face, sections, new(0, 0, 0, 0, 0, 0))).Length
            + sections.Strings.Length + sections.GlobalSubrs.Length;
        var charsetAt = position;
        position += sections.Charset.Length;
        var selectAt = position;
        position += sections.Select.Length;
        var charStringsAt = position;
        position += sections.CharStrings.Length;
        var arrayAt = position;
        var privateAt = new int[sections.Blocks.Length];
        var array = sections.IsCid ? FdArray(face, sections, fontDictOrder, privateAt) : [];
        position += array.Length;
        for (var i = 0; i < privateAt.Length; i++)
        {
            privateAt[i] = position;
            position += sections.Blocks[i].Length;
        }

        array = sections.IsCid ? FdArray(face, sections, fontDictOrder, privateAt) : [];
        var privateSize = PrivateSize(in face.Dicts[fontDictOrder[0]]);
        var offsets = new TopOffsets(charsetAt, selectAt, charStringsAt, arrayAt, privateSize, privateAt[0]);
        var top = CffWriting.SingleIndex(Top(face, sections, offsets));
        byte[][] head = [[MajorVersion, 0, HeaderSize, HeaderOffsetSize], sections.Name, top, sections.Strings, sections.GlobalSubrs];
        byte[][] body = [sections.Charset, sections.Select, sections.CharStrings, array, .. sections.Blocks];
        return Join(position, [.. head, .. body]);
    }

    /// <summary>Concatenates byte arrays.</summary>
    /// <param name="length">The total length.</param>
    /// <param name="parts">The arrays.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] Join(int length, byte[][] parts)
    {
        var result = new byte[length];
        var at = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, at);
            at += part.Length;
        }

        return result;
    }

    /// <summary>Builds the FDArray INDEX.</summary>
    /// <param name="face">The font.</param>
    /// <param name="sections">The pieces.</param>
    /// <param name="fontDictOrder">The source Font DICT of each output Font DICT.</param>
    /// <param name="privateAt">Where each Private DICT is, or zeros to size the INDEX.</param>
    /// <returns>The INDEX.</returns>
    private static byte[] FdArray(CffFace face, CffSections sections, int[] fontDictOrder, int[] privateAt)
    {
        var items = new byte[sections.Blocks.Length][];
        for (var i = 0; i < items.Length; i++)
        {
            ref readonly var source = ref face.Dicts[fontDictOrder[i]];
            var dict = new List<byte>(source.Kept);
            CffWriting.Entry(dict, PrivateOperator, PrivateSize(source), privateAt[i]);
            items[i] = [.. dict];
        }

        return CffWriting.Index(items);
    }

    /// <summary>Builds the Top DICT.</summary>
    /// <param name="face">The font.</param>
    /// <param name="sections">The pieces.</param>
    /// <param name="at">The offsets to write.</param>
    /// <returns>The DICT.</returns>
    private static byte[] Top(CffFace face, CffSections sections, in TopOffsets at)
    {
        var dict = new List<byte>();
        if (sections.IsCid)
        {
            CffWriting.Entry(dict, RosOperator, sections.Ros);
            CffWriting.Entry(dict, CidCountOperator, sections.CidCount);
        }

        dict.AddRange(face.FontBox);
        dict.AddRange(face.FontMatrix);
        CffWriting.Entry(dict, CharsetOperator, at.Charset);
        if (sections.IsCid)
        {
            CffWriting.Entry(dict, FdSelectOperator, at.Select);
            CffWriting.Entry(dict, CharStringsOperator, at.CharStrings);
            CffWriting.Entry(dict, FdArrayOperator, at.FdArray);
            return [.. dict];
        }

        CffWriting.Entry(dict, CharStringsOperator, at.CharStrings);
        CffWriting.Entry(dict, PrivateOperator, at.PrivateSize, at.PrivateAt);
        return [.. dict];
    }

    /// <summary>The offsets written in the Top DICT.</summary>
    /// <param name="Charset">The charset offset.</param>
    /// <param name="Select">The FDSelect offset.</param>
    /// <param name="CharStrings">The CharStrings offset.</param>
    /// <param name="FdArray">The FDArray offset.</param>
    /// <param name="PrivateSize">The Private DICT's size, for a font that is not CID-keyed.</param>
    /// <param name="PrivateAt">The Private DICT's offset, for a font that is not CID-keyed.</param>
    private readonly record struct TopOffsets(int Charset, int Select, int CharStrings, int FdArray, int PrivateSize, int PrivateAt);
}
