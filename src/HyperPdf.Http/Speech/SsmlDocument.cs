// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Http.Speech;

/// <summary>
/// The SSML for one sentence, written as UTF-8 into a pooled buffer so sending it needs no string. Dispose it after the
/// request has finished.
/// </summary>
/// <param name="Buffer">The pooled buffer holding the document.</param>
/// <param name="Length">The length of the document in bytes.</param>
[DebuggerDisplay("SsmlDocument: {Length} bytes")]
internal readonly record struct SsmlDocument(byte[] Buffer, int Length) : IDisposable
{
    /// <summary>The length of a voice id's language prefix, for example <c>en-GB</c>.</summary>
    private const int LanguageLength = 5;

    /// <summary>Converts a speed to a percentage.</summary>
    private const float Percent = 100F;

    /// <summary>The bytes the fixed parts of the document and the rate take, with room to spare.</summary>
    private const int FixedBytes = 256;

    /// <summary>The most bytes one character takes once escaped, as <c>&amp;quot;</c> or as UTF-8.</summary>
    private const int EscapedBytes = 6;

    /// <summary>The characters XML text and attribute values escape.</summary>
    private static readonly SearchValues<char> XmlSpecials = SearchValues.Create("<>&\"'");

    /// <summary>Gets the document's UTF-8 bytes.</summary>
    internal ReadOnlyMemory<byte> Bytes => Buffer.AsMemory(0, Length);

    /// <summary>Gives the buffer back to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => ArrayPool<byte>.Shared.Return(Buffer);

    /// <summary>Builds the SSML for a sentence.</summary>
    /// <param name="text">The text.</param>
    /// <param name="voiceId">The voice.</param>
    /// <param name="speed">The speed, 1 for normal.</param>
    /// <returns>The document; the caller disposes it.</returns>
    internal static SsmlDocument Build(ReadOnlySpan<char> text, string voiceId, float speed)
    {
        ArgumentNullException.ThrowIfNull(voiceId);
        var language = voiceId.Length > LanguageLength ? voiceId.AsSpan(0, LanguageLength) : "en-US".AsSpan();
        var buffer = ArrayPool<byte>.Shared.Rent(FixedBytes + ((text.Length + voiceId.Length + language.Length) * EscapedBytes));
        var length = 0;
        Literal(buffer, ref length, "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\""u8);
        Escaped(buffer, ref length, language);
        Literal(buffer, ref length, "\"><voice name=\""u8);
        Escaped(buffer, ref length, voiceId);
        Literal(buffer, ref length, "\"><prosody rate=\""u8);
        _ = ((speed - 1F) * Percent).TryFormat(buffer.AsSpan(length), out var rate, "+0;-0;+0", CultureInfo.InvariantCulture);
        length += rate;
        Literal(buffer, ref length, "%\">"u8);
        Escaped(buffer, ref length, text);
        Literal(buffer, ref length, "</prosody></voice></speak>"u8);
        return new(buffer, length);
    }

    /// <summary>Appends fixed UTF-8 bytes.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The bytes written so far; moved on.</param>
    /// <param name="bytes">The bytes.</param>
    private static void Literal(byte[] buffer, ref int length, ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(buffer.AsSpan(length));
        length += bytes.Length;
    }

    /// <summary>Appends text as UTF-8 with the five XML special characters escaped.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The bytes written so far; moved on.</param>
    /// <param name="text">The text.</param>
    private static void Escaped(byte[] buffer, ref int length, ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            var special = text.IndexOfAny(XmlSpecials);
            if (special < 0)
            {
                length += Encoding.UTF8.GetBytes(text, buffer.AsSpan(length));
                return;
            }

            length += Encoding.UTF8.GetBytes(text[..special], buffer.AsSpan(length));
            Literal(buffer, ref length, Replacement(text[special]));
            text = text[(special + 1)..];
        }
    }

    /// <summary>Gets the XML entity for an escaped character.</summary>
    /// <param name="special">The character.</param>
    /// <returns>The entity as UTF-8.</returns>
    private static ReadOnlySpan<byte> Replacement(char special) => special switch
    {
        '<' => "&lt;"u8,
        '>' => "&gt;"u8,
        '&' => "&amp;"u8,
        '"' => "&quot;"u8,
        _ => "&apos;"u8,
    };
}
