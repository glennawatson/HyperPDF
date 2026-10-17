// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The Type 1 font encryption of Adobe's Type 1 Font Format, chapter 7: eexec and charstring encryption.</summary>
internal static class Type1Encryption
{
    /// <summary>The initial key of eexec encryption.</summary>
    internal const ushort EexecKey = 55_665;

    /// <summary>The initial key of charstring encryption.</summary>
    internal const ushort CharstringKey = 4330;

    /// <summary>The number of random bytes that start eexec-encrypted data.</summary>
    internal const int EexecPrefix = 4;

    /// <summary>The first cipher constant.</summary>
    private const int C1 = 52_845;

    /// <summary>The second cipher constant.</summary>
    private const int C2 = 22_719;

    /// <summary>The shift that takes the high byte of the key.</summary>
    private const int KeyShift = 8;

    /// <summary>The hexadecimal digits per byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The bits in a hexadecimal digit.</summary>
    private const int NibbleBits = 4;

    /// <summary>Decrypts bytes in place.</summary>
    /// <param name="data">The bytes, replaced by the plain text.</param>
    /// <param name="key">The initial key.</param>
    internal static void Decrypt(Span<byte> data, ushort key)
    {
        var r = key;
        for (var i = 0; i < data.Length; i++)
        {
            var cipher = data[i];
            data[i] = (byte)(cipher ^ (r >> KeyShift));
            r = (ushort)(((cipher + r) * C1) + C2);
        }
    }

    /// <summary>Determines whether eexec data is written as hexadecimal text: its first four bytes are hexadecimal digits.</summary>
    /// <param name="data">The eexec data.</param>
    /// <returns><see langword="true"/> when hexadecimal.</returns>
    internal static bool IsHex(ReadOnlySpan<byte> data) =>
        data.Length >= EexecPrefix && !data[..EexecPrefix].ContainsAnyExcept(PdfCharacters.HexDigits);

    /// <summary>Decodes hexadecimal text, skipping white space and stopping at any other byte.</summary>
    /// <param name="text">The text.</param>
    /// <param name="output">Receives the decoded bytes.</param>
    internal static void DecodeHex(ReadOnlySpan<byte> text, ref PooledBuffer output)
    {
        var target = output.GetSpan((text.Length / DigitsPerByte) + 1);
        var count = 0;
        var high = -1;
        foreach (var c in text)
        {
            var nibble = PdfCharacters.HexValue(c);
            if (nibble < 0)
            {
                if (PdfCharacters.IsWhitespace(c))
                {
                    continue;
                }

                break;
            }

            if (high < 0)
            {
                high = nibble;
                continue;
            }

            target[count] = (byte)((high << NibbleBits) | nibble);
            count++;
            high = -1;
        }

        output.Advance(count);
    }
}
