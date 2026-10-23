// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;
using static HyperPdfLibrary.Composition.PdfStyleNames;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// The named styles of a document. A style can be based on another, so changing a base style changes everything built on it.
/// <see cref="CreateDefault"/> makes the usual set: Normal, six heading levels, Title, Subtitle, list, header, footer, footnote
/// and hyperlink styles.
/// </summary>
[DebuggerDisplay("PdfStyleSheet: {_styles.Count} styles")]
public sealed class PdfStyleSheet
{
    /// <summary>The deepest heading level.</summary>
    private const int HeadingLevels = 6;

    /// <summary>The line spacing of normal text, as a multiple of the line's natural height.</summary>
    private const float NormalLineSpacing = 1.2F;

    /// <summary>The space after normal text, as a multiple of its size.</summary>
    private const float NormalSpaceAfter = 0.6F;

    /// <summary>The space before a heading, as a multiple of the normal text size.</summary>
    private const float HeadingSpaceBefore = 1F;

    /// <summary>The space after a heading, as a multiple of the normal text size.</summary>
    private const float HeadingSpaceAfter = 0.4F;

    /// <summary>The size of a title, as a multiple of the normal text size.</summary>
    private const float TitleScale = 2.4F;

    /// <summary>The size of a subtitle, as a multiple of the normal text size.</summary>
    private const float SubtitleScale = 1.3F;

    /// <summary>The size of small print such as headers, footers and footnotes, as a multiple of the normal text size.</summary>
    private const float SmallScale = 0.85F;

    /// <summary>The space after a footnote, in points.</summary>
    private const float FootnoteSpaceAfter = 2F;

    /// <summary>The indent of a quotation, in points.</summary>
    private const float QuoteIndent = 24F;

    /// <summary>The grey level of subdued text.</summary>
    private const float SubduedGray = 0.35F;

    /// <summary>The level of the blue channel of a link, which gives a contrast above 7 to 1 against white.</summary>
    private const float LinkBlue = 0.7F;

    /// <summary>The level of the green channel of a link.</summary>
    private const float LinkGreen = 0.2F;

    /// <summary>The size of each heading level, as a multiple of the normal text size.</summary>
    private static readonly float[] HeadingScales = [2F, 1.6F, 1.35F, 1.2F, 1.1F, 1F];

    /// <summary>The styles by name.</summary>
    private readonly Dictionary<string, PdfStyle> _styles = [with(StringComparer.Ordinal)];

    /// <summary>The styles with their inheritance settled, by name.</summary>
    private readonly Dictionary<string, ResolvedStyle> _resolved = [with(StringComparer.Ordinal)];

    /// <summary>Gets the number of styles.</summary>
    public int Count => _styles.Count;

    /// <summary>Makes the usual set of styles for a typeface and body size.</summary>
    /// <param name="family">The typeface of the body and, unless a style changes it, of every other style.</param>
    /// <param name="size">The size of body text in points.</param>
    /// <returns>The style sheet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    public static PdfStyleSheet CreateDefault(PdfFontFamily family, float size)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var sheet = new PdfStyleSheet();
        sheet.Add(new(Normal)
        {
            Character = new()
            {
                Family = family,
                Size = size,
                Bold = false,
                Italic = false,
                Underline = false,
                Strikeout = false,
                Script = PdfScript.Normal,
                SmallCaps = false,
                AllCaps = false,
                Color = PdfCanvasColor.Black,
            },
            Paragraph = new() { LineSpacing = NormalLineSpacing, LineSpacingRule = PdfLineSpacingRule.Multiple, SpaceAfter = size * NormalSpaceAfter },
        });
        sheet.AddHeadings(size);
        sheet.AddDerived(size);
        return sheet;
    }

    /// <summary>Adds a style, replacing any style of the same name.</summary>
    /// <param name="style">The style.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is <see langword="null"/>.</exception>
    public void Add(PdfStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentException.ThrowIfNullOrEmpty(style.Name);
        _styles[style.Name] = style;
        _resolved.Clear();
    }

    /// <summary>Determines whether a style exists.</summary>
    /// <param name="name">The style's name.</param>
    /// <returns><see langword="true"/> when the sheet has the style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(string name) => _styles.ContainsKey(name);

    /// <summary>Gets a style.</summary>
    /// <param name="name">The style's name.</param>
    /// <returns>The style, or <see langword="null"/> when the sheet has none of that name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfStyle? Get(string name) => _styles.GetValueOrDefault(name);

    /// <summary>Settles everything a style inherits.</summary>
    /// <param name="name">The style's name, or <see langword="null"/> for <see cref="PdfStyleNames.Normal"/>.</param>
    /// <returns>The style with every property set.</returns>
    /// <exception cref="InvalidOperationException">The style or one it is based on does not exist, the styles are based on each other in a circle, or the chain sets no typeface.</exception>
    internal ResolvedStyle Resolve(string? name)
    {
        name ??= Normal;
        if (_resolved.TryGetValue(name, out var known))
        {
            return known;
        }

        var chain = GetChain(name);
        var paragraph = ResolvedParagraph.Empty;
        var character = ResolvedCharacter.Empty;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            paragraph = paragraph.Apply(chain[i].Paragraph);
            character = character.Apply(chain[i].Character);
        }

        if (character.Family is null)
        {
            throw new InvalidOperationException($"The style '{name}' sets no typeface.");
        }

        var resolved = new ResolvedStyle(paragraph, character);
        _resolved[name] = resolved;
        return resolved;
    }

    /// <summary>Lays a style's character formatting over formatting already settled, for a run that names a character style.</summary>
    /// <param name="format">The formatting of the paragraph the run is in.</param>
    /// <param name="name">The style's name, or <see langword="null"/> for none.</param>
    /// <returns>The combined formatting.</returns>
    /// <exception cref="InvalidOperationException">The style or one it is based on does not exist, or the styles are based on each other in a circle.</exception>
    internal ResolvedCharacter ApplyCharacterStyle(ResolvedCharacter format, string? name)
    {
        if (name is null)
        {
            return format;
        }

        var chain = GetChain(name);
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            format = format.Apply(chain[i].Character);
        }

        return format;
    }

    /// <summary>Lists a style and the styles it is based on, the style itself first.</summary>
    /// <param name="name">The style's name.</param>
    /// <returns>The chain.</returns>
    /// <exception cref="InvalidOperationException">A style in the chain does not exist, or the chain loops.</exception>
    private List<PdfStyle> GetChain(string name)
    {
        var chain = new List<PdfStyle>();
        var next = name;
        while (next is not null)
        {
            var style = _styles.GetValueOrDefault(next) ?? throw new InvalidOperationException($"There is no style named '{next}'.");
            if (chain.Contains(style))
            {
                throw new InvalidOperationException($"The style '{name}' is based on itself.");
            }

            chain.Add(style);
            next = style.BasedOn;
        }

        return chain;
    }

    /// <summary>Adds the six heading styles.</summary>
    /// <param name="size">The body size.</param>
    private void AddHeadings(float size)
    {
        for (var level = 1; level <= HeadingLevels; level++)
        {
            Add(new(GetHeading(level))
            {
                BasedOn = Normal,
                Character = new() { Size = size * HeadingScales[level - 1], Bold = true },
                Paragraph = new()
                {
                    KeepWithNext = true,
                    KeepTogether = true,
                    SpaceBefore = size * HeadingSpaceBefore * HeadingScales[level - 1],
                    SpaceAfter = size * HeadingSpaceAfter,
                    OutlineLevel = level,
                },
            });
        }
    }

    /// <summary>Adds the title, subtitle, list, header, footer, footnote, hyperlink, table, caption and quote styles.</summary>
    /// <param name="size">The body size.</param>
    private void AddDerived(float size)
    {
        var subdued = PdfCanvasColor.Gray(SubduedGray);
        Add(new(Title) { BasedOn = Normal, Character = new() { Size = size * TitleScale, Bold = true }, Paragraph = new() { Alignment = PdfTextAlignment.Left, KeepWithNext = true } });
        Add(new(Subtitle) { BasedOn = Normal, Character = new() { Size = size * SubtitleScale, Color = subdued }, Paragraph = new() { KeepWithNext = true } });
        Add(new(ListBullet) { BasedOn = Normal, Paragraph = new() { SpaceAfter = size * HeadingSpaceAfter } });
        Add(new(ListNumber) { BasedOn = Normal, Paragraph = new() { SpaceAfter = size * HeadingSpaceAfter } });
        Add(new(Header) { BasedOn = Normal, Character = new() { Size = size * SmallScale, Color = subdued }, Paragraph = new() { SpaceAfter = 0F } });
        Add(new(Footer) { BasedOn = Normal, Character = new() { Size = size * SmallScale, Color = subdued }, Paragraph = new() { SpaceAfter = 0F } });
        Add(new(FootnoteText) { BasedOn = Normal, Character = new() { Size = size * SmallScale }, Paragraph = new() { SpaceAfter = FootnoteSpaceAfter } });
        Add(new(FootnoteReference) { Character = new() { Script = PdfScript.Superscript } });
        Add(new(Hyperlink) { BasedOn = Normal, Character = new() { Underline = true, Color = PdfCanvasColor.Rgb(0F, LinkGreen, LinkBlue) } });
        Add(new(TableText) { BasedOn = Normal, Paragraph = new() { SpaceAfter = 0F } });
        Add(new(TableHeader) { BasedOn = TableText, Character = new() { Bold = true } });
        Add(new(Caption) { BasedOn = Normal, Character = new() { Italic = true, Size = size * SmallScale }, Paragraph = new() { KeepWithNext = false } });
        Add(new(Quote) { BasedOn = Normal, Character = new() { Italic = true }, Paragraph = new() { LeftIndent = QuoteIndent, RightIndent = QuoteIndent } });
    }
}
