// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Json;

/// <summary>Turns PDF strings and names into JSON strings and back, so every byte survives the round trip.</summary>
internal static class PdfJsonStrings
{
    /// <summary>The prefix of a string that is text.</summary>
    internal const string TextPrefix = "u:";

    /// <summary>The prefix of a string written as hexadecimal bytes.</summary>
    internal const string BinaryPrefix = "b:";

    /// <summary>The prefix of a name whose bytes are not UTF-8.</summary>
    internal const string RawNamePrefix = "n:";

    /// <summary>The slash that starts a name.</summary>
    internal const char NameSlash = '/';

    /// <summary>The words in a reference: number, generation and R.</summary>
    private const int ReferenceParts = 3;

    /// <summary>Writes a PDF string as <c>u:text</c> when it is text that encodes back to the same bytes, and as <c>b:hex</c> otherwise.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The JSON string.</returns>
    internal static string EncodeString(ReadOnlySpan<byte> bytes)
    {
        var text = PdfText.Decode(bytes);
        return PdfText.Encode(text).AsSpan().SequenceEqual(bytes) ? TextPrefix + text : BinaryPrefix + Convert.ToHexString(bytes);
    }

    /// <summary>Reads a JSON string as the bytes of a PDF string.</summary>
    /// <param name="value">The JSON string.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="FormatException">A <c>b:</c> string is not hexadecimal.</exception>
    internal static byte[] DecodeString(string value)
    {
        if (value.StartsWith(BinaryPrefix, StringComparison.Ordinal))
        {
            return Convert.FromHexString(value.AsSpan(BinaryPrefix.Length));
        }

        return PdfText.Encode(value.StartsWith(TextPrefix, StringComparison.Ordinal) ? value[TextPrefix.Length..] : value);
    }

    /// <summary>Writes a name as <c>/Name</c>, or as <c>n:/Name</c> with <c>#</c> escapes when its bytes are not UTF-8.</summary>
    /// <param name="spelling">The name's bytes, without the slash.</param>
    /// <returns>The JSON string.</returns>
    internal static string EncodeName(ReadOnlySpan<byte> spelling)
    {
        if (System.Text.Unicode.Utf8.IsValid(spelling))
        {
            return NameSlash + Encoding.UTF8.GetString(spelling);
        }

        var builder = new StringBuilder(RawNamePrefix).Append(NameSlash);
        foreach (var value in spelling)
        {
            var plain = value is >= (byte)'!' and <= (byte)'~' and not (byte)'#';
            _ = plain ? builder.Append((char)value) : builder.Append('#').Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>Reads the spelling of a name from its JSON string.</summary>
    /// <param name="value">The JSON string, beginning with <c>/</c> or <c>n:/</c>.</param>
    /// <returns>The name's bytes, without the slash.</returns>
    internal static byte[] DecodeName(string value)
    {
        if (!value.StartsWith(RawNamePrefix, StringComparison.Ordinal))
        {
            return Encoding.UTF8.GetBytes(value[1..]);
        }

        var escaped = Encoding.ASCII.GetBytes(value[(RawNamePrefix.Length + 1)..]);
        var decoded = new byte[escaped.Length];
        return decoded[..PdfStringDecoder.DecodeName(escaped, decoded)];
    }

    /// <summary>Determines whether a JSON string names an object, such as <c>12 0 R</c>.</summary>
    /// <param name="value">The JSON string.</param>
    /// <param name="id">The object it names.</param>
    /// <returns><see langword="true"/> when the string is a reference.</returns>
    internal static bool TryParseReference(string value, out PdfObjectId id)
    {
        id = default;
        var parts = value.Split(' ');
        if (parts.Length != ReferenceParts || parts[ReferenceParts - 1] != "R"
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var generation))
        {
            return false;
        }

        id = new(number, generation);
        return true;
    }
}
