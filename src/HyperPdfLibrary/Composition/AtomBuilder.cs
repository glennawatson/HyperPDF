// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdfLibrary.Composition;

/// <summary>Cuts the inlines of a paragraph into measured atoms: words, spaces, tabs, breaks, footnote numbers and marks.</summary>
[DebuggerDisplay("AtomBuilder: {_atoms.Count} atoms")]
internal sealed class AtomBuilder
{
    /// <summary>The styles.</summary>
    private readonly PdfStyleSheet _styles;

    /// <summary>The values fields show.</summary>
    private readonly FieldContext _fields;

    /// <summary>The footnote counter.</summary>
    private readonly NoteNumbers _numbers;

    /// <summary>The atoms made so far.</summary>
    private readonly List<Atom> _atoms = [];

    /// <summary>The text of the word being collected.</summary>
    private readonly StringBuilder _word = new();

    /// <summary>The link the text being added is part of, or <see langword="null"/>.</summary>
    private PdfHyperlink? _link;

    /// <summary>Whether the last atom made was a word, so the next word with no space before it joins it.</summary>
    private bool _lastWasWord;

    /// <summary>Initializes a new instance of the <see cref="AtomBuilder"/> class.</summary>
    /// <param name="styles">The styles.</param>
    /// <param name="fields">The values fields show.</param>
    /// <param name="numbers">The footnote counter.</param>
    internal AtomBuilder(PdfStyleSheet styles, FieldContext fields, NoteNumbers numbers)
    {
        _styles = styles;
        _fields = fields;
        _numbers = numbers;
    }

    /// <summary>Gets the footnotes met while building, in order.</summary>
    internal List<NoteRef> Notes { get; } = [];

    /// <summary>Cuts inlines into atoms.</summary>
    /// <param name="inlines">The inlines.</param>
    /// <param name="format">The character formatting of the paragraph.</param>
    /// <returns>The atoms.</returns>
    internal List<Atom> Build(IReadOnlyList<PdfInline> inlines, ResolvedCharacter format)
    {
        foreach (var inline in inlines)
        {
            AddInline(inline, format);
        }

        return _atoms;
    }

    /// <summary>Adds one inline.</summary>
    /// <param name="inline">The inline.</param>
    /// <param name="format">The formatting it inherits.</param>
    /// <exception cref="ArgumentException">The inline is not one of the library's.</exception>
    private void AddInline(PdfInline inline, ResolvedCharacter format)
    {
        switch (inline)
        {
            case PdfRun run:
            {
                AddText(run.Text, _styles.ApplyCharacterStyle(format, run.Style).Apply(run.Format));
                break;
            }

            case PdfField field:
            {
                AddText(_fields.GetText(field), _styles.ApplyCharacterStyle(format, field.Style).Apply(field.Format));
                break;
            }

            case PdfHyperlink link:
            {
                AddLink(link, format);
                break;
            }

            case PdfFootnote footnote:
            {
                AddNote(footnote, format);
                break;
            }

            case PdfBookmark bookmark:
            {
                AddMark(bookmark, format);
                break;
            }

            case PdfTab:
            {
                AddPlain(AtomKind.Tab, format);
                break;
            }

            case PdfLineBreak:
            {
                AddPlain(AtomKind.Break, format);
                break;
            }

            default:
            {
                throw new ArgumentException($"The inline '{inline}' is not supported.", nameof(inline));
            }
        }
    }

    /// <summary>Adds the inlines of a link, in the hyperlink style unless they carry their own formatting.</summary>
    /// <param name="link">The link.</param>
    /// <param name="format">The formatting it inherits.</param>
    private void AddLink(PdfHyperlink link, ResolvedCharacter format)
    {
        var outer = _link;
        _link = link;
        var linked = _styles.Contains(PdfStyleNames.Hyperlink) ? _styles.ApplyCharacterStyle(format, PdfStyleNames.Hyperlink) : format;
        foreach (var inline in link.Content)
        {
            AddInline(inline, linked);
        }

        _link = outer;
    }

    /// <summary>Adds a footnote's number.</summary>
    /// <param name="footnote">The footnote.</param>
    /// <param name="format">The formatting it inherits.</param>
    private void AddNote(PdfFootnote footnote, ResolvedCharacter format)
    {
        FlushWord(format);
        var number = _numbers.Next();
        var label = NumberText.Format(number, footnote.NumberFormat);
        var noteFormat = _styles.Contains(PdfStyleNames.FootnoteReference) ? _styles.ApplyCharacterStyle(format, PdfStyleNames.FootnoteReference) : format;
        var atom = new Atom(AtomKind.Note, label, noteFormat) { NoteIndex = Notes.Count, JoinsPrevious = _lastWasWord };
        Notes.Add(new(footnote, number, label));
        Add(atom);
        _lastWasWord = true;
    }

    /// <summary>Adds a bookmark's place.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <param name="format">The formatting it inherits.</param>
    private void AddMark(PdfBookmark bookmark, ResolvedCharacter format)
    {
        FlushWord(format);
        Add(new(AtomKind.Mark, string.Empty, format) { Bookmark = bookmark.Name, JoinsPrevious = _lastWasWord });
    }

    /// <summary>Adds a tab or a line break.</summary>
    /// <param name="kind">The kind of atom.</param>
    /// <param name="format">The formatting it inherits.</param>
    private void AddPlain(AtomKind kind, ResolvedCharacter format)
    {
        FlushWord(format);
        Add(new(kind, string.Empty, format));
        _lastWasWord = false;
    }

    /// <summary>Adds text, cutting it into words and spaces.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The formatting.</param>
    private void AddText(string text, ResolvedCharacter format)
    {
        foreach (var character in text)
        {
            switch (character)
            {
                case ' ':
                {
                    FlushWord(format);
                    Add(new(AtomKind.Space, " ", format));
                    _lastWasWord = false;
                    break;
                }

                case '\t':
                {
                    AddPlain(AtomKind.Tab, format);
                    break;
                }

                case '\n':
                {
                    AddPlain(AtomKind.Break, format);
                    break;
                }

                case '\r':
                {
                    break;
                }

                default:
                {
                    _ = _word.Append(character);
                    break;
                }
            }
        }

        FlushWord(format);
    }

    /// <summary>Makes atoms of the word collected so far.</summary>
    /// <param name="format">The formatting of the word.</param>
    private void FlushWord(ResolvedCharacter format)
    {
        if (_word.Length == 0)
        {
            return;
        }

        var text = _word.ToString();
        _ = _word.Clear();
        if (format.AllCaps)
        {
            text = text.ToUpperInvariant();
        }

        if (format.SmallCaps && !format.AllCaps)
        {
            AddSmallCapitals(text, format);
        }
        else
        {
            Add(new(AtomKind.Word, text, format) { JoinsPrevious = _lastWasWord });
        }

        _lastWasWord = true;
    }

    /// <summary>Adds a word with its lower case letters as smaller capitals, in as few atoms as it takes.</summary>
    /// <param name="text">The word.</param>
    /// <param name="format">The formatting.</param>
    private void AddSmallCapitals(string text, ResolvedCharacter format)
    {
        var start = 0;
        var joins = _lastWasWord;
        while (start < text.Length)
        {
            var small = char.IsLower(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsLower(text[end]) == small)
            {
                end++;
            }

            var piece = text[start..end];
            Add(new(AtomKind.Word, small ? piece.ToUpperInvariant() : piece, format) { IsSmallCapital = small, JoinsPrevious = joins });
            joins = true;
            start = end;
        }
    }

    /// <summary>Measures an atom, gives it the current link and adds it.</summary>
    /// <param name="atom">The atom.</param>
    private void Add(Atom atom)
    {
        atom.Link = _link?.Target;
        atom.LinkDescription = _link?.Description;
        AtomMetrics.Measure(atom);
        _atoms.Add(atom);
    }
}
