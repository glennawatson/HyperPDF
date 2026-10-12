// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>Classifies bytes the way the PDF/A syntax checks read them (ISO 32000-1, 7.2.2).</summary>
internal static class PdfASyntaxCharacters
{
    /// <summary>The line feed byte.</summary>
    internal const byte LineFeed = (byte)'\n';

    /// <summary>The carriage return byte.</summary>
    internal const byte CarriageReturn = (byte)'\r';

    /// <summary>The space byte.</summary>
    internal const byte Space = (byte)' ';

    /// <summary>The value <see cref="HexValue"/> gives a byte that is not a hexadecimal digit.</summary>
    internal const int NotHex = -1;

    /// <summary>The value of the hexadecimal digit a.</summary>
    private const int TenValue = 10;

    /// <summary>The white-space bytes: NUL, tab, line feed, form feed, carriage return and space.</summary>
    private static readonly SearchValues<byte> WhiteSpace = SearchValues.Create([0, 9, 10, 12, 13, 32]);

    /// <summary>The bytes that end a regular token: white space and the delimiters.</summary>
    private static readonly SearchValues<byte> TokenEnds = SearchValues.Create("\0\t\n\f\r %()/<>[]{}"u8);

    /// <summary>Determines whether a byte is white space.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSpace(byte value) => WhiteSpace.Contains(value);

    /// <summary>Determines whether a byte ends a regular token.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> when it is white space or a delimiter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsTokenEnd(byte value) => TokenEnds.Contains(value);

    /// <summary>Determines whether a byte is a carriage return or a line feed.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsLineEnd(byte value) => value is LineFeed or CarriageReturn;

    /// <summary>Determines whether a byte is a decimal digit.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    /// <summary>Gets the value of a hexadecimal digit.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The value, or <see cref="NotHex"/>.</returns>
    internal static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'a' and <= (byte)'f' => value - 'a' + TenValue,
        >= (byte)'A' and <= (byte)'F' => value - 'A' + TenValue,
        _ => NotHex,
    };
}
