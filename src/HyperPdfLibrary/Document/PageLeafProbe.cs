// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Recognises a page of the page tree from its bytes, without building its dictionary, so the page is parsed only when
/// it is first used. A page is accepted only when parsing it would report nothing: the object sits where the
/// cross-reference table says, the file is not encrypted, the dictionary is well formed and has no <c>/Kids</c>, and
/// each of <c>/MediaBox</c> and <c>/CropBox</c> that it has is four plain numbers that make a usable box. The objects that
/// <c>/Resources</c>, <c>/Rotate</c> and <c>/UserUnit</c> refer to are read at once, as making the page reads them. Any
/// other page is left to the full parse, which makes the reports.
/// </summary>
internal static class PageLeafProbe
{
    /// <summary>The numbers in a page box.</summary>
    private const int BoxNumbers = 4;

    /// <summary>The entry flag for <c>/MediaBox</c>.</summary>
    private const int MediaBoxSeen = 1;

    /// <summary>The entry flag for <c>/CropBox</c>.</summary>
    private const int CropBoxSeen = 2;

    /// <summary>The entry flag for <c>/Resources</c>.</summary>
    private const int ResourcesSeen = 4;

    /// <summary>The entry flag for <c>/Rotate</c>.</summary>
    private const int RotateSeen = 8;

    /// <summary>The entry flag for <c>/UserUnit</c>.</summary>
    private const int UserUnitSeen = 16;

    /// <summary>Recognises a page that has not been read.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="id">The page's object id.</param>
    /// <returns><see langword="true"/> when the object is a page that parsing would not report on; the objects its entries refer to have been read.</returns>
    internal static bool TryRead(PdfObjectStore store, PdfObjectId id)
    {
        var state = default(ProbeState);
        bool probed;
        lock (store.Gate)
        {
            probed = CanProbe(store, id.Number) && Probe(store, id.Number, ref state);
        }

        if (!probed)
        {
            return false;
        }

        // Making the page reads these in this order; reading them here makes any report about them now.
        state.Follow(store);
        return true;
    }

    /// <summary>Determines whether an object is still in the file only: not read, not changed, in the file and not encrypted.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when it can be probed.</returns>
    private static bool CanProbe(PdfObjectStore store, int number)
    {
        var cache = store.CacheState;
        return store.Security is null
            && (uint)number < (uint)cache.Length
            && cache[number] is null
            && store.XrefState.OffsetBase == 0
            && store.XrefState.GetType(number) == XrefEntryType.InFile
            && !StoreEditing.TryGetEdited(store, number, out _);
    }

    /// <summary>Reads an object's bytes where the cross-reference table puts it.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="number">The object number.</param>
    /// <param name="state">Receives what the probe found.</param>
    /// <returns><see langword="true"/> when the object is a page parsing would not report on.</returns>
    private static bool Probe(PdfObjectStore store, int number, ref ProbeState state)
    {
        var offset = store.XrefState.GetLocation(number);
        if (offset < 0 || offset >= store.Source.Length)
        {
            return false;
        }

        if (store.ArrayState is { } array)
        {
            var inArray = new PdfParser(array, (int)offset, store, store.Names);
            return ReadObject(ref inArray, number, ref state);
        }

        using var lease = store.Source.Lease(offset, StoreParsing.FirstWindow(store, offset));
        var inWindow = PdfParser.ForWindow(lease.Span, 0, store, store.Names, null);
        return ReadObject(ref inWindow, number, ref state);
    }

    /// <summary>Reads "number generation obj", the dictionary, and "endobj".</summary>
    /// <param name="parser">The parser, at the object.</param>
    /// <param name="number">The object number expected.</param>
    /// <param name="state">Receives what the probe found.</param>
    /// <returns><see langword="true"/> when the object is a page parsing would not report on.</returns>
    private static bool ReadObject(ref PdfParser parser, int number, ref ProbeState state)
    {
        if (!StoreParsing.TryReadHeader(ref parser, out var header) || header.Number != number || parser.NextToken() != PdfTokenKind.DictionaryStart)
        {
            return false;
        }

        while (true)
        {
            var kind = parser.NextToken();
            if (kind == PdfTokenKind.DictionaryEnd)
            {
                break;
            }

            if (kind != PdfTokenKind.Name || !ReadEntry(ref parser, ref state))
            {
                return false;
            }
        }

        // A window that ended inside a token may have cut the object short.
        return parser.NextToken() == PdfTokenKind.Keyword && parser.Keyword == PdfKeyword.EndObj && !parser.ReachedEnd;
    }

    /// <summary>Reads the value of an entry whose key has been read.</summary>
    /// <param name="parser">The parser, after the key.</param>
    /// <param name="state">Receives what the probe found.</param>
    /// <returns><see langword="false"/> when the entry means the page is left to the full parse.</returns>
    private static bool ReadEntry(ref PdfParser parser, ref ProbeState state)
    {
        var key = parser.InternName(parser.Lexeme);
        var kind = parser.NextToken();
        if (key.Is(KnownName.Kids))
        {
            return false;
        }

        if (key.Is(KnownName.MediaBox))
        {
            return ReadBox(ref parser, kind, ref state, MediaBoxSeen);
        }

        if (key.Is(KnownName.CropBox))
        {
            return ReadBox(ref parser, kind, ref state, CropBoxSeen);
        }

        if (key.Is(KnownName.Resources))
        {
            return ReadFollowed(ref parser, kind, ref state, ResourcesSeen);
        }

        if (key.Is(KnownName.Rotate))
        {
            return ReadFollowed(ref parser, kind, ref state, RotateSeen);
        }

        return key.Is(KnownName.UserUnit)
            ? ReadFollowed(ref parser, kind, ref state, UserUnitSeen)
            : SkipValue(ref parser, kind, out _);
    }

    /// <summary>Reads a page box: four plain numbers that make a usable box with the corners the right way round.</summary>
    /// <param name="parser">The parser, after the key.</param>
    /// <param name="kind">The value's first token.</param>
    /// <param name="state">Holds the keys already seen.</param>
    /// <param name="flag">The key's flag.</param>
    /// <returns><see langword="false"/> when the box is repeated or anything but four numbers that make a valid box.</returns>
    private static bool ReadBox(ref PdfParser parser, PdfTokenKind kind, ref ProbeState state, int flag)
    {
        if (kind != PdfTokenKind.ArrayStart || !state.Mark(flag))
        {
            return false;
        }

        Span<float> corners = stackalloc float[BoxNumbers];
        for (var i = 0; i < corners.Length; i++)
        {
            if (parser.NextToken() != PdfTokenKind.Number || !PdfNumber.TryParse(parser.Lexeme, out var number))
            {
                return false;
            }

            corners[i] = number.AsSingle();
        }

        return parser.NextToken() == PdfTokenKind.ArrayEnd
            && PdfPage.IsUsableBox(PdfRectangle.FromCorners(corners[0], corners[1], corners[2], corners[3]))
            && corners[0] <= corners[2]
            && corners[1] <= corners[3];
    }

    /// <summary>Reads a value that making the page reads, noting the object it refers to when it is a reference.</summary>
    /// <param name="parser">The parser, after the key.</param>
    /// <param name="kind">The value's first token.</param>
    /// <param name="state">Holds the keys already seen and the references found.</param>
    /// <param name="flag">The key's flag.</param>
    /// <returns><see langword="false"/> when the key is repeated or the value is malformed.</returns>
    private static bool ReadFollowed(ref PdfParser parser, PdfTokenKind kind, ref ProbeState state, int flag)
    {
        if (!state.Mark(flag))
        {
            return false;
        }

        var ok = SkipValue(ref parser, kind, out var reference);
        state.Remember(flag, reference);
        return ok;
    }

    /// <summary>Skips a value, noting when it is a reference.</summary>
    /// <param name="parser">The parser, after the value's first token.</param>
    /// <param name="kind">The value's first token.</param>
    /// <param name="reference">Receives the reference when the value is one; otherwise an invalid id.</param>
    /// <returns><see langword="false"/> when the value is malformed or nested too deep to skip.</returns>
    private static bool SkipValue(ref PdfParser parser, PdfTokenKind kind, out PdfObjectId reference)
    {
        reference = default;
        return kind switch
        {
            PdfTokenKind.Number => SkipNumber(ref parser, out reference),
            PdfTokenKind.Name or PdfTokenKind.LiteralString or PdfTokenKind.HexString => true,
            PdfTokenKind.Keyword => IsLiteral(parser.Keyword),
            PdfTokenKind.ArrayStart => SkipNested(ref parser, false),
            PdfTokenKind.DictionaryStart => SkipNested(ref parser, true),
            _ => false,
        };
    }

    /// <summary>Skips a number, or a reference when the number is followed by a generation and <c>R</c>.</summary>
    /// <param name="parser">The parser, after the number.</param>
    /// <param name="reference">Receives the reference when there is one; otherwise an invalid id.</param>
    /// <returns><see langword="false"/> when the number is malformed.</returns>
    private static bool SkipNumber(ref PdfParser parser, out PdfObjectId reference)
    {
        reference = default;
        if (!PdfNumber.TryParse(parser.Lexeme, out var value))
        {
            return false;
        }

        if (value.Kind != PdfKind.Integer || value.AsInteger() is <= 0 or > PdfLimits.MaxObjectNumber)
        {
            return true;
        }

        // The same look ahead as the parser: "generation R" makes a reference, anything else is put back.
        var mark = parser.Position;
        if (parser.NextToken() == PdfTokenKind.Number
            && PdfNumber.TryParse(parser.Lexeme, out var generation)
            && generation.Kind == PdfKind.Integer
            && parser.NextToken() == PdfTokenKind.Keyword
            && parser.Keyword == PdfKeyword.Reference)
        {
            reference = new((int)value.AsInteger(), (int)Math.Clamp(generation.AsInteger(), 0, ushort.MaxValue));
            return true;
        }

        parser.Position = mark;
        return true;
    }

    /// <summary>Skips a well-formed array or dictionary, whose opening token has been read.</summary>
    /// <param name="parser">The parser, after the opening token.</param>
    /// <param name="isDictionary">Whether the opening token started a dictionary.</param>
    /// <returns><see langword="false"/> when the value is not well formed, or nests as deep as the parser allows.</returns>
    private static bool SkipNested(ref PdfParser parser, bool isDictionary)
    {
        // One bit per level, set for a dictionary, so a closing token that does not match its opener is noticed.
        var dictionaries = isDictionary ? 1UL : 0UL;
        var level = 1;
        while (level > 0)
        {
            var kind = parser.NextToken();
            var ok = kind switch
            {
                PdfTokenKind.ArrayStart or PdfTokenKind.DictionaryStart => TryOpen(kind, ref dictionaries, ref level),
                PdfTokenKind.ArrayEnd or PdfTokenKind.DictionaryEnd => TryClose(kind, ref dictionaries, ref level),
                _ => SkipValue(ref parser, kind, out _),
            };
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Enters a nested array or dictionary.</summary>
    /// <param name="kind">The opening token.</param>
    /// <param name="dictionaries">One bit per level, set for a dictionary.</param>
    /// <param name="level">The nesting level.</param>
    /// <returns><see langword="false"/> at the deepest level the parser allows.</returns>
    private static bool TryOpen(PdfTokenKind kind, ref ulong dictionaries, ref int level)
    {
        if (level + 1 >= PdfLimits.MaxNesting)
        {
            return false;
        }

        dictionaries = (dictionaries << 1) | (kind == PdfTokenKind.DictionaryStart ? 1UL : 0UL);
        level++;
        return true;
    }

    /// <summary>Leaves a nested array or dictionary.</summary>
    /// <param name="kind">The closing token.</param>
    /// <param name="dictionaries">One bit per level, set for a dictionary.</param>
    /// <param name="level">The nesting level.</param>
    /// <returns><see langword="false"/> when the closing token does not match the opener.</returns>
    private static bool TryClose(PdfTokenKind kind, ref ulong dictionaries, ref int level)
    {
        if (((dictionaries & 1UL) != 0) != (kind == PdfTokenKind.DictionaryEnd))
        {
            return false;
        }

        dictionaries >>= 1;
        level--;
        return true;
    }

    /// <summary>Determines whether a keyword is a value the parser reads: <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <returns><see langword="true"/> for those three.</returns>
    private static bool IsLiteral(PdfKeyword keyword) => keyword is PdfKeyword.True or PdfKeyword.False or PdfKeyword.Null;

    /// <summary>What a probe found in a page object.</summary>
    private struct ProbeState
    {
        /// <summary>The entry flags of the keys seen.</summary>
        private int _seen;

        /// <summary>The object <c>/Resources</c> refers to, or an invalid id.</summary>
        private PdfObjectId _resources;

        /// <summary>The object <c>/Rotate</c> refers to, or an invalid id.</summary>
        private PdfObjectId _rotate;

        /// <summary>The object <c>/UserUnit</c> refers to, or an invalid id.</summary>
        private PdfObjectId _userUnit;

        /// <summary>Notes that a key was seen.</summary>
        /// <param name="flag">The key's flag.</param>
        /// <returns><see langword="false"/> when the key was seen before.</returns>
        internal bool Mark(int flag)
        {
            if ((_seen & flag) != 0)
            {
                return false;
            }

            _seen |= flag;
            return true;
        }

        /// <summary>Notes the object a key's value refers to.</summary>
        /// <param name="flag">The key's flag.</param>
        /// <param name="id">The reference, or an invalid id.</param>
        internal void Remember(int flag, PdfObjectId id)
        {
            switch (flag)
            {
                case ResourcesSeen:
                {
                    _resources = id;
                    break;
                }

                case RotateSeen:
                {
                    _rotate = id;
                    break;
                }

                default:
                {
                    _userUnit = id;
                    break;
                }
            }
        }

        /// <summary>Reads the objects the keys refer to, in the order making the page reads them.</summary>
        /// <param name="store">The document's objects.</param>
        internal readonly void Follow(PdfObjectStore store)
        {
            Follow(store, _resources);
            Follow(store, _rotate);
            Follow(store, _userUnit);
        }

        /// <summary>Reads the object a reference names.</summary>
        /// <param name="store">The document's objects.</param>
        /// <param name="id">The reference, or an invalid id for none.</param>
        private static void Follow(PdfObjectStore store, PdfObjectId id)
        {
            if (id.IsValid)
            {
                _ = StoreReading.Resolve(store, PdfValue.FromReference(id));
            }
        }
    }
}
