// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdf.Ocr.Lstm;

/// <summary>The characters a model can recognise, numbered as the model numbers them. Number 0 is the space.</summary>
[DebuggerDisplay("CharacterSet: {Count} characters")]
public sealed class CharacterSet
{
    /// <summary>The name the character set file gives the space.</summary>
    private const string SpaceName = "NULL";

    /// <summary>The characters, indexed by number.</summary>
    private readonly string[] _characters;

    /// <summary>Initializes a new instance of the <see cref="CharacterSet"/> class.</summary>
    /// <param name="characters">The characters, indexed by number.</param>
    private CharacterSet(string[] characters) => _characters = characters;

    /// <summary>Gets the number of characters.</summary>
    public int Count => _characters.Length;

    /// <summary>Gets a character.</summary>
    /// <param name="id">The character's number.</param>
    /// <returns>The character's text.</returns>
    public string this[int id] => _characters[id];

    /// <summary>Reads a character set from its text form: a count, then a line for each character that starts with the character.</summary>
    /// <param name="text">The UTF-8 bytes of the file.</param>
    /// <returns>The character set.</returns>
    /// <exception cref="InvalidDataException">The file is damaged.</exception>
    public static CharacterSet Read(ReadOnlySpan<byte> text)
    {
        var content = Encoding.UTF8.GetString(text).AsSpan();
        var lines = content.EnumerateLines();
        if (!lines.MoveNext() || !int.TryParse(lines.Current.Trim(), out var count) || count < 1)
        {
            throw new InvalidDataException("The character set has no count.");
        }

        var characters = new string[count];
        for (var id = 0; id < count; id++)
        {
            if (!lines.MoveNext())
            {
                throw new InvalidDataException("The character set is shorter than its count.");
            }

            characters[id] = NameOf(lines.Current);
        }

        return new(characters);
    }

    /// <summary>Gets the character a line of the file describes.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The character's text.</returns>
    private static string NameOf(ReadOnlySpan<char> line)
    {
        var end = line.IndexOfAny(' ', '\t');
        var name = end < 0 ? line : line[..end];
        return name.SequenceEqual(SpaceName) ? " " : name.ToString();
    }
}
