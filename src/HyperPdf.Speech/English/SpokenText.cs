// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Speech.English;

/// <summary>A growing run of characters that is cleared and reused, so saying text aloud allocates only while the run is still growing.</summary>
[DebuggerDisplay("SpokenText: {Length} characters")]
internal sealed class SpokenText
{
    /// <summary>The characters the run holds before it first grows.</summary>
    private const int InitialLength = 512;

    /// <summary>The most digits an int has.</summary>
    private const int MaxNumberDigits = 10;

    /// <summary>How many times larger the run grows when full.</summary>
    private const int Growth = 2;

    /// <summary>The characters.</summary>
    private char[] _chars = new char[InitialLength];

    /// <summary>Gets the number of characters in the run.</summary>
    internal int Length { get; private set; }

    /// <summary>Gets the characters.</summary>
    /// <returns>The characters, valid until the run next changes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<char> AsSpan() => _chars.AsSpan(0, Length);

    /// <summary>Empties the run, keeping its room.</summary>
    internal void Clear() => Length = 0;

    /// <summary>Cuts the run back to a shorter length.</summary>
    /// <param name="length">The new length, at most the current one.</param>
    internal void Truncate(int length) => Length = length;

    /// <summary>Adds characters.</summary>
    /// <param name="text">The characters.</param>
    internal void Append(ReadOnlySpan<char> text)
    {
        text.CopyTo(Room(text.Length));
        Length += text.Length;
    }

    /// <summary>Adds a character.</summary>
    /// <param name="c">The character.</param>
    internal void Append(char c)
    {
        Room(1)[0] = c;
        Length++;
    }

    /// <summary>Adds characters using invariant lowercase casing.</summary>
    /// <param name="text">The characters.</param>
    internal void AppendLowerInvariant(ReadOnlySpan<char> text) => Length += text.ToLowerInvariant(Room(text.Length));

    /// <summary>Adds a whole number in digits.</summary>
    /// <param name="value">The number, which is not negative.</param>
    internal void AppendNumber(int value)
    {
        var room = Room(MaxNumberDigits);
        _ = value.TryFormat(room, out var written, default, CultureInfo.InvariantCulture);
        Length += written;
    }

    /// <summary>Gets room at the end of the run for some characters.</summary>
    /// <param name="length">The characters to be written.</param>
    /// <returns>The room.</returns>
    private Span<char> Room(int length)
    {
        if (Length + length > _chars.Length)
        {
            Array.Resize(ref _chars, Math.Max(Length + length, _chars.Length * Growth));
        }

        return _chars.AsSpan(Length);
    }
}
