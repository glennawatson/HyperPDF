// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Finds the code a font shows a character with. It reads the font's own meaning of each code (its encoding and
/// <c>/Differences</c>, or its CMap and <c>/ToUnicode</c>) and turns that around, so text is written in the font's
/// encoding and not always in WinAnsi. Legacy multi-byte CMaps such as 90ms-RKSJ-H or GBK-EUC-H, which mix one-byte and
/// two-byte codes, are read through the CMap's own code lengths (ISO 32000-2 9.7.5). The codes are searched for the
/// characters of the text being encoded, once per character, and the answers are kept, so a font with thousands of codes
/// holds only the characters that were typed.
/// </summary>
[DebuggerDisplay("FormCodeMap: {UnitBytes} byte codes")]
internal sealed class FormCodeMap
{
    /// <summary>The number of codes of a one-byte font.</summary>
    private const int ByteCodes = 256;

    /// <summary>The number of codes of a two-byte font.</summary>
    private const int WideCodes = 65_536;

    /// <summary>The bytes in a code of a Type0 font with a fixed code length.</summary>
    private const int WideBytes = 2;

    /// <summary>The most characters one code maps to that are looked at.</summary>
    private const int MaxText = 8;

    /// <summary>The character a missing character is shown as.</summary>
    private const int Question = '?';

    /// <summary>The number of character requests held on the stack.</summary>
    private const int StackCharacters = 128;

    /// <summary>The loaded font whose codes are searched.</summary>
    private readonly PdfFont _font;

    /// <summary>How the font's codes are laid out.</summary>
    private readonly FormCodeLayout _layout;

    /// <summary>Guards adding to <see cref="_entries"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>The characters searched for so far; replaced, never changed.</summary>
    private FormCodeEntries _entries = FormCodeEntries.Empty;

    /// <summary>Initializes a new instance of the <see cref="FormCodeMap"/> class.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="layout">How the font's codes are laid out.</param>
    private FormCodeMap(PdfFont font, FormCodeLayout layout)
    {
        _font = font;
        _layout = layout;
        UnitBytes = layout switch
        {
            FormCodeLayout.Bytes => 1,
            FormCodeLayout.Mixed => 0,
            _ => WideBytes,
        };
    }

    /// <summary>Gets the bytes in a code; 0 when the font's codes are one or two bytes long.</summary>
    internal int UnitBytes { get; }

    /// <summary>Makes the map of a font.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The map.</returns>
    internal static FormCodeMap Create(PdfFont font, PdfNameTable names)
    {
        if (!font.Dictionary.IsName(KnownName.Subtype, KnownName.Type0))
        {
            return new(font, FormCodeLayout.Bytes);
        }

        var encoding = font.Dictionary.Get(KnownName.Encoding);
        if (encoding.Kind != PdfKind.Name)
        {
            return new(font, FormCodeLayout.Mixed);
        }

        var name = names.GetSpelling(encoding.AsName());
        if (name.IndexOf("UCS2"u8) >= 0 && name.EndsWith("-H"u8))
        {
            return new(font, FormCodeLayout.Unicode);
        }

        return new(font, name.EndsWith("Identity-H"u8) ? FormCodeLayout.Wide : FormCodeLayout.Mixed);
    }

    /// <summary>Looks for the codes of the characters of a text that have not been looked for yet, in one search of the font's codes.</summary>
    /// <param name="text">The text about to be encoded.</param>
    /// <param name="cancellationToken">Cancels the code search.</param>
    internal void Prepare(ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_layout == FormCodeLayout.Unicode || text.IsEmpty)
        {
            return;
        }

        var known = Volatile.Read(ref _entries);
        int[]? rented = null;
        Span<int> wanted = text.Length < StackCharacters
            ? stackalloc int[StackCharacters]
            : rented = ArrayPool<int>.Shared.Rent(text.Length + 1);
        try
        {
            Search(wanted[..CollectMissing(text, known, wanted, cancellationToken)], cancellationToken);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Finds the code a character is shown with.</summary>
    /// <param name="rune">The character.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when the font has the character.</returns>
    internal bool TryGetCode(Rune rune, out FormCode code)
    {
        if (_layout == FormCodeLayout.Unicode && rune.Value < WideCodes)
        {
            code = new(rune.Value, WideBytes);
            return true;
        }

        if (_layout == FormCodeLayout.Unicode)
        {
            code = default;
            return false;
        }

        _ = Volatile.Read(ref _entries).TryGet(rune.Value, out code);
        return code.Length > 0;
    }

    /// <summary>Gets the character a text rune is looked up as.</summary>
    /// <param name="rune">The rune.</param>
    /// <returns>The character to find a code for; a space for a tab; -1 for a line break.</returns>
    private static int Shown(Rune rune) => rune.Value switch
    {
        '\r' or '\n' => -1,
        '\t' => ' ',
        _ => rune.Value,
    };

    /// <summary>Lists the characters of a text, and the question mark a missing character is shown as, that have not been searched for.</summary>
    /// <param name="text">The text.</param>
    /// <param name="known">The characters searched for so far.</param>
    /// <param name="wanted">Receives the characters, which may repeat; it has room for one for each character of the text and one more.</param>
    /// <param name="cancellationToken">Cancels collecting requests.</param>
    /// <returns>The number of characters listed.</returns>
    private static int CollectMissing(ReadOnlySpan<char> text, FormCodeEntries known, Span<int> wanted, CancellationToken cancellationToken)
    {
        var count = 0;
        var visited = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if ((visited & (ByteCodes - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            visited++;
            var character = Shown(rune);
            if (character < 0 || known.TryGet(character, out _))
            {
                continue;
            }

            wanted[count] = character;
            count++;
        }

        if (known.TryGet(Question, out _))
        {
            return count;
        }

        wanted[count] = Question;
        return count + 1;
    }

    /// <summary>Searches the font's codes for some characters and keeps the answers.</summary>
    /// <param name="wanted">The characters, in any order; duplicates are allowed.</param>
    /// <param name="cancellationToken">Cancels searching codes.</param>
    private void Search(Span<int> wanted, CancellationToken cancellationToken)
    {
        if (wanted.IsEmpty)
        {
            return;
        }

        wanted.Sort();
        var distinct = 1;
        for (var i = 1; i < wanted.Length; i++)
        {
            if (wanted[i] == wanted[i - 1])
            {
                continue;
            }

            wanted[distinct] = wanted[i];
            distinct++;
        }

        var characters = wanted[..distinct];
        var found = ArrayPool<FormCode>.Shared.Rent(distinct);
        found.AsSpan(0, distinct).Clear();
        try
        {
            Scan(characters, found, cancellationToken);
            lock (_gate)
            {
                Volatile.Write(ref _entries, _entries.With(characters, found.AsSpan(0, distinct)));
            }
        }
        finally
        {
            ArrayPool<FormCode>.Shared.Return(found);
        }
    }

    /// <summary>Reads the codes of the font in order until every wanted character has its lowest code.</summary>
    /// <param name="wanted">The characters, ascending and distinct.</param>
    /// <param name="found">Receives the code found for each, left at length 0 when there is none.</param>
    /// <param name="cancellationToken">Cancels searching codes.</param>
    private void Scan(ReadOnlySpan<int> wanted, Span<FormCode> found, CancellationToken cancellationToken)
    {
        Span<char> text = stackalloc char[MaxText];
        var remaining = wanted.Length;
        if (_layout == FormCodeLayout.Mixed)
        {
            ScanMixed(wanted, found, text, ref remaining, cancellationToken);
            return;
        }

        var length = _layout == FormCodeLayout.Bytes ? 1 : WideBytes;
        var count = _layout == FormCodeLayout.Bytes ? ByteCodes : WideCodes;
        for (var code = 0; code < count && remaining > 0; code++)
        {
            if ((code & (ByteCodes - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            Record(code, length, wanted, found, text, ref remaining);
        }
    }

    /// <summary>
    /// Reads the codes of a font whose CMap may mix one-byte and two-byte codes. The CMap's codespace says how long the
    /// code that starts with each byte is, so every one-byte code and every two-byte code is tried.
    /// </summary>
    /// <param name="wanted">The characters, ascending and distinct.</param>
    /// <param name="found">Receives the codes.</param>
    /// <param name="text">Space for a code's text.</param>
    /// <param name="remaining">The characters still to find.</param>
    /// <param name="cancellationToken">Cancels searching codes.</param>
    private void ScanMixed(ReadOnlySpan<int> wanted, Span<FormCode> found, Span<char> text, ref int remaining, CancellationToken cancellationToken)
    {
        Span<byte> bytes = stackalloc byte[WideBytes];
        for (var lead = 0; lead < ByteCodes && remaining > 0; lead++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bytes[0] = (byte)lead;
            bytes[1] = 0;
            if (_font.ReadCode(bytes, out _) == 1)
            {
                Record(lead, 1, wanted, found, text, ref remaining);
                continue;
            }

            for (var trail = 0; trail < ByteCodes && remaining > 0; trail++)
            {
                bytes[1] = (byte)trail;
                if (_font.ReadCode(bytes, out var code) == WideBytes)
                {
                    Record(code, WideBytes, wanted, found, text, ref remaining);
                }
            }
        }
    }

    /// <summary>Records the character a code stands for when it is one that is wanted and has no code yet.</summary>
    /// <param name="code">The code.</param>
    /// <param name="length">The bytes in the code.</param>
    /// <param name="wanted">The characters, ascending and distinct.</param>
    /// <param name="found">Receives the code.</param>
    /// <param name="text">Space for the code's text.</param>
    /// <param name="remaining">The characters still to find; reduced when one is found.</param>
    private void Record(int code, int length, ReadOnlySpan<int> wanted, Span<FormCode> found, Span<char> text, ref int remaining)
    {
        var written = _font.GetUnicode(code, text);
        if (written <= 0 || Rune.DecodeFromUtf16(text[..written], out var rune, out var consumed) != OperationStatus.Done || consumed != written)
        {
            return;
        }

        var index = wanted.BinarySearch(rune.Value);
        if (index < 0 || found[index].Length != 0)
        {
            return;
        }

        found[index] = new(code, length);
        remaining--;
    }
}
