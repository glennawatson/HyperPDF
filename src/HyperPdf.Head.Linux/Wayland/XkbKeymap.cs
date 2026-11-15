// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;
using System.Diagnostics;
using System.Text;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>
/// The keyboard layout a Wayland compositor sends: which key symbol each key gives in each layout group and at each shift level. It is
/// read from the XKB map text, so keys type what the user's layout says rather than what a US keyboard has in the same place.
/// </summary>
[DebuggerDisplay("XkbKeymap: {GroupCount} groups")]
internal sealed class XkbKeymap
{
    /// <summary>The layout groups kept for a key.</summary>
    internal const int Groups = 4;

    /// <summary>The shift levels kept for a group: plain, Shift, the third level and the third level with Shift.</summary>
    internal const int Levels = 4;

    /// <summary>The key codes a map can name.</summary>
    private const int Keys = 256;

    /// <summary>The longest key symbol name read. Longer names are not symbols the viewer can use.</summary>
    private const int LongestName = 64;

    /// <summary>The length of the mark before a key symbol written as a hexadecimal number.</summary>
    private const int HexPrefix = 2;

    /// <summary>The key symbols by key code, group and level.</summary>
    private readonly uint[] _symbols;

    /// <summary>Initializes a new instance of the <see cref="XkbKeymap"/> class.</summary>
    /// <param name="symbols">The key symbols by key code, group and level.</param>
    /// <param name="groupCount">The groups the map uses.</param>
    private XkbKeymap(uint[] symbols, int groupCount)
    {
        _symbols = symbols;
        GroupCount = groupCount;
    }

    /// <summary>Gets the layout groups the map uses, at least one.</summary>
    internal int GroupCount { get; }

    /// <summary>Reads a map.</summary>
    /// <param name="text">The XKB map text.</param>
    /// <returns>The layout, or <see langword="null"/> when the text names no keys.</returns>
    internal static XkbKeymap? Parse(ReadOnlySpan<byte> text)
    {
        // Key symbol names and the characters they type come from libX11's tables.
        X11.NativeMethods.Register();
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        var symbols = new uint[Keys * Groups * Levels];
        var groups = 0;
        var words = new XkbTokens(text);
        while (words.Next(out var word))
        {
            if (word.SequenceEqual("xkb_keycodes"u8))
            {
                ReadKeycodes(ref words, codes);
            }
            else if (word.SequenceEqual("xkb_symbols"u8))
            {
                groups = ReadSymbols(ref words, codes, symbols);
            }
        }

        return groups == 0 ? null : new(symbols, groups);
    }

    /// <summary>Gets the key symbol of a key.</summary>
    /// <param name="keycode">The XKB key code, which is the kernel's key code plus eight.</param>
    /// <param name="group">The layout group in use.</param>
    /// <param name="level">The shift level.</param>
    /// <returns>The key symbol, or 0 when the key has none there.</returns>
    internal uint Symbol(uint keycode, int group, int level)
    {
        if (keycode >= Keys || (uint)level >= Levels)
        {
            return 0;
        }

        // A group the key does not have wraps to its first, as XKB does by default.
        var at = (int)keycode * Groups * Levels;
        var symbol = _symbols[at + (Math.Clamp(group, 0, Groups - 1) * Levels) + level];
        return symbol != 0 ? symbol : _symbols[at + level];
    }

    /// <summary>Reads the section that gives each key name its key code.</summary>
    /// <param name="words">The map, placed after the section's keyword.</param>
    /// <param name="codes">Receives the key codes by name.</param>
    private static void ReadKeycodes(ref XkbTokens words, Dictionary<string, int> codes)
    {
        if (!SkipTo(ref words, (byte)'{'))
        {
            return;
        }

        while (words.Next(out var word) && !Is(word, (byte)'}'))
        {
            if (word.SequenceEqual("alias"u8))
            {
                ReadAlias(ref words, codes);
            }
            else if (word[0] == (byte)'<')
            {
                ReadKeycode(ref words, codes, word);
            }

            if (!Is(word, (byte)';'))
            {
                SkipStatement(ref words);
            }
        }
    }

    /// <summary>Reads the key code a key name is given.</summary>
    /// <param name="words">The map, placed after the key name.</param>
    /// <param name="codes">The key codes by name, which gains the name.</param>
    /// <param name="name">The key name.</param>
    private static void ReadKeycode(ref XkbTokens words, Dictionary<string, int> codes, ReadOnlySpan<byte> name)
    {
        if (words.Next(out var equals) && Is(equals, (byte)'=') && words.Next(out var number) && Utf8Parser.TryParse(number, out int code, out _))
        {
            codes[Encoding.ASCII.GetString(name)] = code;
        }
    }

    /// <summary>Reads a second name for a key.</summary>
    /// <param name="words">The map, placed after the alias keyword.</param>
    /// <param name="codes">The key codes by name, which gains the second name.</param>
    private static void ReadAlias(ref XkbTokens words, Dictionary<string, int> codes)
    {
        if (words.Next(out var alias) && words.Next(out var equals) && Is(equals, (byte)'=') && words.Next(out var real)
            && codes.TryGetValue(Encoding.ASCII.GetString(real), out var code))
        {
            codes[Encoding.ASCII.GetString(alias)] = code;
        }
    }

    /// <summary>Reads the section that gives each key its symbols.</summary>
    /// <param name="words">The map, placed after the section's keyword.</param>
    /// <param name="codes">The key codes by name.</param>
    /// <param name="symbols">Receives the key symbols.</param>
    /// <returns>The most groups any key has.</returns>
    private static int ReadSymbols(ref XkbTokens words, Dictionary<string, int> codes, uint[] symbols)
    {
        var groups = 0;
        if (!SkipTo(ref words, (byte)'{'))
        {
            return groups;
        }

        while (words.Next(out var word) && !Is(word, (byte)'}'))
        {
            if (word.SequenceEqual("key"u8))
            {
                groups = Math.Max(groups, ReadNamedKey(ref words, codes, symbols));
            }

            if (!Is(word, (byte)';'))
            {
                SkipStatement(ref words);
            }
        }

        return groups;
    }

    /// <summary>Reads a key statement: the key's name and its symbols.</summary>
    /// <param name="words">The map, placed after the key keyword.</param>
    /// <param name="codes">The key codes by name.</param>
    /// <param name="symbols">Receives the key symbols.</param>
    /// <returns>The groups the key has.</returns>
    private static int ReadNamedKey(ref XkbTokens words, Dictionary<string, int> codes, uint[] symbols)
    {
        if (!words.Next(out var name) || !words.Next(out var open) || !Is(open, (byte)'{'))
        {
            return 0;
        }

        var known = codes.TryGetValue(Encoding.ASCII.GetString(name), out var code) && (uint)code < Keys;
        return ReadKey(ref words, known ? symbols.AsSpan(code * Groups * Levels, Groups * Levels) : default);
    }

    /// <summary>Reads the symbols of one key, up to the brace that closes it.</summary>
    /// <param name="words">The map, placed inside the key's braces.</param>
    /// <param name="key">Receives the symbols by group and level, or empty to read past a key that is not known.</param>
    /// <returns>The groups the key has.</returns>
    private static int ReadKey(ref XkbTokens words, Span<uint> key)
    {
        var group = 0;
        var groups = 0;
        var skipList = false;
        while (words.Next(out var word) && !Is(word, (byte)'}'))
        {
            if (word.SequenceEqual("symbols"u8))
            {
                // symbols[Group2] names its group; the list comes after the equals sign.
                group = ReadGroupIndex(ref words, group);
            }
            else if (word.SequenceEqual("actions"u8) || word.SequenceEqual("type"u8))
            {
                // Their brackets hold a group name or actions, never symbols.
                skipList = true;
            }
            else if (Is(word, (byte)','))
            {
                skipList = false;
            }
            else if (Is(word, (byte)'['))
            {
                group += ReadList(ref words, key, group, skipList);
                groups = Math.Max(groups, group);
            }
        }

        return groups;
    }

    /// <summary>Reads a bracketed list of a key: the symbols of a group, or a list that holds none.</summary>
    /// <param name="words">The map, placed inside the list.</param>
    /// <param name="key">Receives the symbols by group and level, or empty.</param>
    /// <param name="group">The group the list is for.</param>
    /// <param name="skip">Whether the list holds a group name or actions rather than symbols.</param>
    /// <returns>1 when the list was a group's symbols, otherwise 0.</returns>
    private static int ReadList(ref XkbTokens words, Span<uint> key, int group, bool skip)
    {
        if (skip)
        {
            _ = SkipTo(ref words, (byte)']');
            return 0;
        }

        ReadLevels(ref words, group < Groups && !key.IsEmpty ? key.Slice(group * Levels, Levels) : default);
        return 1;
    }

    /// <summary>Reads the group a symbols list is for, from the bracket after the word.</summary>
    /// <param name="words">The map, placed after the word.</param>
    /// <param name="fallback">The group to use when none is named.</param>
    /// <returns>The group, from zero.</returns>
    private static int ReadGroupIndex(ref XkbTokens words, int fallback)
    {
        if (!words.Next(out var open) || !Is(open, (byte)'[') || !words.Next(out var name))
        {
            return fallback;
        }

        _ = SkipTo(ref words, (byte)'=');
        var digit = name[^1] - (byte)'1';
        return (uint)digit < Groups ? digit : fallback;
    }

    /// <summary>Reads the symbols of one group, up to the bracket that closes the list.</summary>
    /// <param name="words">The map, placed inside the list.</param>
    /// <param name="levels">Receives the symbols by level, or empty to read past the list.</param>
    private static void ReadLevels(ref XkbTokens words, Span<uint> levels)
    {
        var level = 0;
        var taken = false;
        var braced = false;
        while (words.Next(out var word) && !Is(word, (byte)']'))
        {
            // A level that gives several symbols is written in braces; the first is the one that types.
            if (Is(word, (byte)'{') || Is(word, (byte)'}'))
            {
                braced = Is(word, (byte)'{');
                continue;
            }

            if (Is(word, (byte)',') && !braced)
            {
                level++;
                taken = false;
                continue;
            }

            if (taken || level >= levels.Length || Is(word, (byte)','))
            {
                continue;
            }

            levels[level] = ToSymbol(word);
            taken = true;
        }
    }

    /// <summary>Finds the key symbol a name stands for.</summary>
    /// <param name="name">The name, such as <c>q</c>, <c>adiaeresis</c> or <c>Cyrillic_a</c>.</param>
    /// <returns>The key symbol, or 0 when the name is not known.</returns>
    private static unsafe uint ToSymbol(ReadOnlySpan<byte> name)
    {
        if (name.Length == 1 && char.IsAsciiLetterOrDigit((char)name[0]))
        {
            return name[0];
        }

        if (name.StartsWith("0x"u8))
        {
            return Utf8Parser.TryParse(name[HexPrefix..], out uint number, out _, 'X') ? number : 0;
        }

        if (name.Length >= LongestName)
        {
            return 0;
        }

        byte* ended = stackalloc byte[LongestName];
        name.CopyTo(new(ended, LongestName));
        ended[name.Length] = 0;
        return (uint)X11.NativeMethods.XStringToKeysym(ended);
    }

    /// <summary>Tells whether a word is one punctuation mark.</summary>
    /// <param name="word">The word.</param>
    /// <param name="mark">The mark.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool Is(ReadOnlySpan<byte> word, byte mark) => word.Length == 1 && word[0] == mark;

    /// <summary>Moves past the next occurrence of a mark.</summary>
    /// <param name="words">The map.</param>
    /// <param name="mark">The mark.</param>
    /// <returns><see langword="false"/> when the map ended first.</returns>
    private static bool SkipTo(ref XkbTokens words, byte mark)
    {
        while (words.Next(out var word))
        {
            if (Is(word, mark))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Moves past the rest of a statement: to its semicolon, stepping over any braces on the way.</summary>
    /// <param name="words">The map.</param>
    private static void SkipStatement(ref XkbTokens words)
    {
        var depth = 0;
        while (words.Next(out var word))
        {
            if (Is(word, (byte)'{'))
            {
                depth++;
            }
            else if (Is(word, (byte)'}'))
            {
                depth--;
            }
            else if (Is(word, (byte)';') && depth <= 0)
            {
                return;
            }
        }
    }
}
