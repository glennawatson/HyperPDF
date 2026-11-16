// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// A line of text to write into a page's own content. The built-in font of the same look is used when it can show every letter, so the line stays one object that is
/// picked and retyped as a whole; otherwise the bundled font of that look is embedded, which also shows Greek, Cyrillic and the Latin letters Windows ANSI lacks.
/// </summary>
[DebuggerDisplay("PdfNewTextRun: {_name}")]
public sealed class PdfNewTextRun
{
    /// <summary>The prefix of the font resource name.</summary>
    private const string FontPrefix = "EdF";

    /// <summary>The longest line, in bytes, written with a built-in font.</summary>
    private const int LineBytes = 4096;

    /// <summary>The bit of a font's number that makes it bold.</summary>
    private const int BoldBit = 1;

    /// <summary>The bit of a font's number that makes it slanted.</summary>
    private const int ItalicBit = 2;

    /// <summary>The number of looks each family of built-in fonts has.</summary>
    private const int LooksPerFamily = 4;

    /// <summary>The page content the run is written into.</summary>
    private readonly PdfPageContent _content;

    /// <summary>The text.</summary>
    private readonly string _text;

    /// <summary>The name of the font resource.</summary>
    private readonly PdfName _name;

    /// <summary>The embedded font, or <see langword="null"/> when a built-in font is used.</summary>
    private readonly PdfEmbeddedFont? _embedded;

    /// <summary>Initializes a new instance of the <see cref="PdfNewTextRun"/> class.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="text">The text.</param>
    /// <param name="name">The font resource's name.</param>
    /// <param name="embedded">The embedded font, or <see langword="null"/>.</param>
    private PdfNewTextRun(PdfPageContent content, string text, PdfName name, PdfEmbeddedFont? embedded)
    {
        _content = content;
        _text = text;
        _name = name;
        _embedded = embedded;
    }

    /// <summary>Gets a value indicating whether the line is written with an embedded font, because a built-in font cannot show all of it.</summary>
    public bool IsEmbedded => _embedded is not null;

    /// <summary>Gets the name of the font resource.</summary>
    public PdfName FontName => _name;

    /// <summary>Gets the text.</summary>
    public string Text => _text;

    /// <summary>Chooses the font for a line and adds it to the page content's resources.</summary>
    /// <param name="content">The page content the line goes into.</param>
    /// <param name="text">The line.</param>
    /// <param name="font">The look of the line.</param>
    /// <returns>The run. Write it between a text object's start and end, then call <see cref="Finish"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfNewTextRun Create(PdfPageContent content, string text, AppearanceFont font) => Create(content, text, font, null);

    /// <summary>Chooses the font for a line and adds it to the page content's resources, with a font to use for letters the bundled font of that look lacks.</summary>
    /// <param name="content">The page content the line goes into.</param>
    /// <param name="text">The line.</param>
    /// <param name="font">The look of the line.</param>
    /// <param name="fallback">A font that has the letters the bundled font lacks, or <see langword="null"/> to use the bundled font whatever it lacks.</param>
    /// <returns>The run. Write it between a text object's start and end, then call <see cref="Finish"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfNewTextRun Create(PdfPageContent content, string text, AppearanceFont font, PdfFontSource? fallback)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(text);
        var name = PdfPageContentEditing.AllocateName(content, KnownName.Font, FontPrefix);
        if (!NeedsEmbedding(text))
        {
            PdfPageContentEditing.AddResource(content, KnownName.Font, name, PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(content.Document.Objects, font)));
            return new(content, text, name, null);
        }

        var source = fallback is not null && !BundledCovers(text, font) ? fallback : OpenBundled(font);
        var embedded = PdfEmbeddedFont.Create(content.Document.Objects, source);
        PdfPageContentEditing.AddResource(content, KnownName.Font, name, embedded.Resource);
        return new(content, text, name, embedded);
    }

    /// <summary>Tells whether a line needs a font that is neither built in nor bundled: it has a letter that the bundled font of its look lacks too.</summary>
    /// <param name="text">The line.</param>
    /// <param name="font">The look of the line.</param>
    /// <returns><see langword="true"/> when a font with those letters must be found.</returns>
    public static bool NeedsFallback(string text, AppearanceFont font) => NeedsEmbedding(text) && !BundledCovers(text, font);

    /// <summary>Tells whether the bundled font of a look has a glyph for every letter of a line.</summary>
    /// <param name="text">The line.</param>
    /// <param name="font">The look of the line.</param>
    /// <returns><see langword="true"/> when it has them all.</returns>
    public static bool BundledCovers(string text, AppearanceFont font)
    {
        ArgumentNullException.ThrowIfNull(text);
        var source = OpenBundled(font);
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune) && !Rune.IsControl(rune) && !source.HasGlyph(rune.Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Tells whether the built-in fonts cannot show every letter of a line.</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> when a letter has no built-in glyph.</returns>
    public static bool NeedsEmbedding(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value >= ' ' && !AppearanceFontMetrics.TryEncode(rune.Value, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Selects the font in a text object.</summary>
    /// <param name="builder">The content, inside a text object.</param>
    /// <param name="size">The size in points.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SelectFont(ref PdfContentBuilder builder, float size) => builder.SetFont(_content.Document.Objects.Names.GetSpelling(_name), size);

    /// <summary>Writes the line in a text object whose font was selected and whose matrix was set.</summary>
    /// <param name="builder">The content, inside a text object.</param>
    /// <param name="size">The size in points, as selected.</param>
    public void Show(ref PdfContentBuilder builder, float size)
    {
        if (_embedded is { } font)
        {
            font.ShowText(ref builder, _text, size);
            return;
        }

        var codes = new byte[Math.Min(_text.Length, LineBytes)];
        var count = AppearanceFontMetrics.Encode(_text.AsSpan(0, codes.Length), codes);
        builder.ShowText(codes.AsSpan(0, count));
    }

    /// <summary>Writes the objects of an embedded font once the line is written. A built-in font has none.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Finish() => _embedded?.Finish();

    /// <summary>Opens the bundled font of a look.</summary>
    /// <param name="font">The look.</param>
    /// <returns>The font.</returns>
    private static PdfFontSource OpenBundled(AppearanceFont font)
    {
        var index = (int)font;
        return PdfBundledFonts.OpenOrThrow((PdfBundledFontFamily)(index / LooksPerFamily), (index & BoldBit) != 0, (index & ItalicBit) != 0);
    }
}
