// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Chooses a system font for a font the PDF does not embed (ISO 32000-2, 9.6.2.2 and 9.8.2): the named family in
/// the asked weight and slant, then a standard stand-in chosen by the standard 14 name or the serif, fixed-pitch and
/// symbolic descriptor flags. The selected font provider resolves platform-specific faces.
/// Faces are cached for the drawing service instance by family, weight and slant.
/// </summary>
/// <param name="provider">The provider of system faces, or null for bundled faces only.</param>
/// <param name="resources">The selected cached font resources.</param>
internal sealed class SystemFontMatcher(IPdfFontProvider? provider, PdfFontResources resources)
{
    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldThreshold = 600;

    /// <summary>Sans serif stand-ins, most metric-compatible with Helvetica first.</summary>
    private static readonly string[] SansFamilies = ["Helvetica", "Arial", "Liberation Sans", "Nimbus Sans", "Arimo", "DejaVu Sans", "Noto Sans"];

    /// <summary>Serif stand-ins, most metric-compatible with Times first.</summary>
    private static readonly string[] SerifFamilies = ["Times New Roman", "Times", "Liberation Serif", "Nimbus Roman", "Tinos", "DejaVu Serif", "Noto Serif"];

    /// <summary>Fixed-pitch stand-ins, most metric-compatible with Courier first.</summary>
    private static readonly string[] MonoFamilies = ["Courier New", "Liberation Mono", "Nimbus Mono PS", "Cousine", "Courier", "DejaVu Sans Mono", "Noto Sans Mono"];

    /// <summary>Stand-ins for the Symbol font; the URW face maps the Adobe Symbol codes directly.</summary>
    private static readonly string[] SymbolFamilies = ["Standard Symbols PS", "Symbol", "Symbola", "DejaVu Sans"];

    /// <summary>Stand-ins for the ZapfDingbats font; the URW face maps the Adobe Dingbats codes directly.</summary>
    private static readonly string[] DingbatsFamilies = ["D050000L", "ZapfDingbats", "Dingbats", "Symbola", "DejaVu Sans"];

    /// <summary>Japanese stand-ins.</summary>
    private static readonly string[] JapaneseFamilies = ["Noto Sans CJK JP", "Source Han Sans JP", "Yu Gothic", "MS Gothic", "Hiragino Sans", "Droid Sans Japanese", "Droid Sans Fallback"];

    /// <summary>Simplified Chinese stand-ins.</summary>
    private static readonly string[] SimplifiedChineseFamilies = ["Noto Sans CJK SC", "Source Han Sans SC", "Microsoft YaHei", "SimSun", "PingFang SC", "FandolSong", "Droid Sans Fallback"];

    /// <summary>Traditional Chinese stand-ins.</summary>
    private static readonly string[] TraditionalChineseFamilies = ["Noto Sans CJK TC", "Source Han Sans TC", "Microsoft JhengHei", "PMingLiU", "PingFang TC", "Droid Sans Fallback"];

    /// <summary>Korean stand-ins.</summary>
    private static readonly string[] KoreanFamilies = ["Noto Sans CJK KR", "Source Han Sans KR", "Malgun Gothic", "Apple SD Gothic Neo", "Droid Sans Fallback"];

    /// <summary>Guards the face cache.</summary>
    private readonly Lock _gate = new();

    /// <summary>The faces found so far; null records a family the system does not have.</summary>
    private readonly Dictionary<FaceKey, SubstituteFace?> _faces = [];

    /// <summary>The face used when nothing else matches.</summary>
    private SubstituteFace? _fallback;

    /// <summary>Gets the selected matcher, sharing the supplied resources for detached dictionaries.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="resources">The selected font resources.</param>
    /// <returns>The matcher for that document or direct font load.</returns>
    internal static SystemFontMatcher For(PdfDictionary dictionary, PdfFontResources resources) =>
        dictionary.Owner?.Context?.DrawingServices.FontMatcher ?? resources.DetachedMatcher;

    /// <summary>Finds a system font for a request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The face; the platform default font when nothing matches.</returns>
    internal SubstituteFace Match(in SubstituteRequest request)
    {
        var resolved = ResolveStandard(request);

        // A standard font's name says nothing the standard font does not, so it need not be split into family and style.
        var parsed = resolved.Standard == StandardFont.None ? ParsedFontName.Parse(resolved.BaseFont) : new ParsedFontName(string.Empty, 0, false);
        var weight = ChooseWeight(resolved, parsed);
        var italic = parsed.Italic || (resolved.Flags & FontFlags.Italic) != 0 || IsStandardItalic(resolved.Standard);

        // Standard fonts use the same open font pack on every platform.
        return resolved.Standard != StandardFont.None && BundledFor(resources, resolved, weight, italic) is { } bundled
            ? bundled
            : MatchByName(resolved, parsed, weight, italic);
    }

    /// <summary>Finds a typeface for a Unicode code point the primary face lacks, through the system fallback list.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The face, or <see langword="null"/> when no system font has the character.</returns>
    internal SubstituteFace? MatchCharacter(int codePoint)
    {
        var font = provider?.MatchCharacter(codePoint);
        if (font is null || string.IsNullOrEmpty(font.FamilyName))
        {
            font?.Dispose();
            return null;
        }

        var key = new FaceKey(font.FamilyName, font.Weight, font.Italic, false);
        lock (_gate)
        {
            if (_faces.TryGetValue(key, out var cached) && cached is not null)
            {
                font.Dispose();
                return cached;
            }

            var face = new SubstituteFace(font, false);
            _faces[key] = face;
            return face;
        }
    }

    /// <summary>
    /// Names the standard font a request means. A TrueType font called Arial or TimesNewRoman is a standard font under
    /// another name; the standard font name lookup knows these aliases.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The request, with its standard font set when the name is an alias.</returns>
    private static SubstituteRequest ResolveStandard(in SubstituteRequest request) =>
        request.Standard == StandardFont.None && request.Script == CjkScript.None
            ? request with { Standard = StandardFonts.Find(Encoding.UTF8.GetBytes(request.BaseFont)) }
            : request;

    /// <summary>Gets the bundled face for a request: the family the standard font or the flags name, in the weight and slant.</summary>
    /// <param name="resources">The selected cached font resources.</param>
    /// <param name="request">The request.</param>
    /// <param name="weight">The chosen weight.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled resource is missing.</returns>
    private static SubstituteFace? BundledFor(PdfFontResources resources, in SubstituteRequest request, int weight, bool italic)
    {
        var family = request.Standard switch
        {
            StandardFont.Symbol => BundledFamily.Symbol,
            StandardFont.ZapfDingbats => BundledFamily.Dingbats,
            _ when IsFixed(request) => BundledFamily.Fixed,
            _ when IsSerif(request) => BundledFamily.Serif,
            _ => BundledFamily.Sans,
        };
        return BundledFaces.Get(resources, family, weight >= BoldThreshold, italic);
    }

    /// <summary>Determines whether a request means a fixed-pitch face: a Courier standard font, or the flag on another font.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> for a fixed-pitch request.</returns>
    private static bool IsFixed(in SubstituteRequest request) =>
        request.Standard is >= StandardFont.Courier and <= StandardFont.CourierOblique || (request.Standard == StandardFont.None && (request.Flags & FontFlags.FixedPitch) != 0);

    /// <summary>Determines whether a request means a serif face: a Times standard font, or the flag on another font.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> for a serif request.</returns>
    private static bool IsSerif(in SubstituteRequest request) =>
        request.Standard is >= StandardFont.TimesRoman and <= StandardFont.TimesItalic || (request.Standard == StandardFont.None && (request.Flags & FontFlags.Serif) != 0);

    /// <summary>Chooses the weight: the name's style, then the descriptor's weight, then bold for ForceBold.</summary>
    /// <param name="request">The request.</param>
    /// <param name="parsed">The parsed name.</param>
    /// <returns>The weight.</returns>
    private static int ChooseWeight(in SubstituteRequest request, ParsedFontName parsed)
    {
        if (request.Standard != StandardFont.None)
        {
            return StandardFonts.IsBold(request.Standard) ? ParsedFontName.BoldWeight : ParsedFontName.NormalWeight;
        }

        var weight = parsed.Weight > 0 ? parsed.Weight : request.Weight;
        if (weight <= 0)
        {
            weight = ParsedFontName.NormalWeight;
        }

        return (request.Flags & FontFlags.ForceBold) != 0 ? Math.Max(weight, ParsedFontName.BoldWeight) : weight;
    }

    /// <summary>Determines whether a standard font slants.</summary>
    /// <param name="font">The font.</param>
    /// <returns><see langword="true"/> for the oblique and italic faces.</returns>
    private static bool IsStandardItalic(StandardFont font) => font is StandardFont.CourierBoldOblique or StandardFont.CourierOblique
        or StandardFont.HelveticaBoldOblique or StandardFont.HelveticaOblique or StandardFont.TimesBoldItalic or StandardFont.TimesItalic;

    /// <summary>Lists the families the PDF names, then those that suit the script of a CID font.</summary>
    /// <param name="request">The request.</param>
    /// <param name="parsed">The parsed name.</param>
    /// <returns>The families.</returns>
    private static List<FamilyCandidate> NamedCandidates(in SubstituteRequest request, ParsedFontName parsed)
    {
        var list = new List<FamilyCandidate>();
        if (parsed.Family.Length > 0)
        {
            list.Add(new(parsed.Family, true));
            var words = ParsedFontName.ToWords(parsed.Family);
            if (!string.Equals(words, parsed.Family, StringComparison.Ordinal))
            {
                list.Add(new(words, true));
            }
        }

        Add(list, ScriptFamilies(request.Script), false);
        return list;
    }

    /// <summary>Lists the generic system families for a request, in order.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The families.</returns>
    private static List<FamilyCandidate> GenericCandidates(in SubstituteRequest request)
    {
        var list = new List<FamilyCandidate>();
        switch (request.Standard)
        {
            case StandardFont.Symbol:
                {
                    Add(list, SymbolFamilies, false);
                    break;
                }

            case StandardFont.ZapfDingbats:
                {
                    Add(list, DingbatsFamilies, false);
                    break;
                }

            default:
                {
                    Add(list, GenericFamilies(request), false);
                    break;
                }
        }

        return list;
    }

    /// <summary>Gets the stand-ins for a CJK script.</summary>
    /// <param name="script">The script.</param>
    /// <returns>The families, or empty for Latin text.</returns>
    private static string[] ScriptFamilies(CjkScript script) => script switch
    {
        CjkScript.Japanese => JapaneseFamilies,
        CjkScript.SimplifiedChinese => SimplifiedChineseFamilies,
        CjkScript.TraditionalChinese => TraditionalChineseFamilies,
        CjkScript.Korean => KoreanFamilies,
        _ => [],
    };

    /// <summary>Gets the generic stand-ins: by the standard font, then by the fixed-pitch and serif flags.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The families.</returns>
    private static string[] GenericFamilies(in SubstituteRequest request)
    {
        var standard = request.Standard;
        if (standard is >= StandardFont.Courier and <= StandardFont.CourierOblique || (standard == StandardFont.None && (request.Flags & FontFlags.FixedPitch) != 0))
        {
            return MonoFamilies;
        }

        return standard is >= StandardFont.TimesRoman and <= StandardFont.TimesItalic || (standard == StandardFont.None && (request.Flags & FontFlags.Serif) != 0)
            ? SerifFamilies
            : SansFamilies;
    }

    /// <summary>Adds families that are not yet listed.</summary>
    /// <param name="list">The list.</param>
    /// <param name="families">The families.</param>
    /// <param name="requested">Whether the PDF named them.</param>
    private static void Add(List<FamilyCandidate> list, string[] families, bool requested)
    {
        foreach (var family in families)
        {
            if (!Contains(list, family))
            {
                list.Add(new(family, requested));
            }
        }
    }

    /// <summary>Determines whether a family is already listed, ignoring case.</summary>
    /// <param name="list">The list.</param>
    /// <param name="family">The family.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool Contains(List<FamilyCandidate> list, string family)
    {
        foreach (var candidate in list)
        {
            if (string.Equals(candidate.Family, family, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares family names, ignoring case and spaces.</summary>
    /// <param name="actual">The typeface's family.</param>
    /// <param name="wanted">The family asked for.</param>
    /// <returns><see langword="true"/> when they name the same family.</returns>
    private static bool NamesMatch(string actual, string wanted)
    {
        var a = 0;
        var w = 0;
        a = SkipSpaces(actual, a);
        w = SkipSpaces(wanted, w);
        while (a < actual.Length && w < wanted.Length)
        {
            if (char.ToUpperInvariant(actual[a]) != char.ToUpperInvariant(wanted[w]))
            {
                return false;
            }

            a = SkipSpaces(actual, a + 1);
            w = SkipSpaces(wanted, w + 1);
        }

        return a == actual.Length && w == wanted.Length;
    }

    /// <summary>Moves past a run of spaces.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index of the first character that is not a space, or the length of the text.</returns>
    private static int SkipSpaces(string text, int index)
    {
        while (index < text.Length && text[index] == ' ')
        {
            index++;
        }

        return index;
    }

    /// <summary>Finds a system font by name, then the bundled generic face, then the generic system families.</summary>
    /// <param name="request">The request.</param>
    /// <param name="parsed">The parsed name.</param>
    /// <param name="weight">The chosen weight.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>The face.</returns>
    private SubstituteFace MatchByName(in SubstituteRequest request, ParsedFontName parsed, int weight, bool italic)
    {
        // A font the system lacks falls back to the bundled generic face; the system generic list only covers a missing resource.
        return FindFirst(NamedCandidates(request, parsed), weight, italic)
            ?? BundledFor(resources, request, weight, italic)
            ?? FindFirst(GenericCandidates(request), weight, italic)
            ?? Volatile.Read(ref _fallback)
            ?? CreateFallback();
    }

    /// <summary>Finds the first listed family the system has.</summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>The face, or <see langword="null"/>.</returns>
    private SubstituteFace? FindFirst(List<FamilyCandidate> candidates, int weight, bool italic)
    {
        foreach (var candidate in candidates)
        {
            if (Find(new(candidate.Family, weight, italic, candidate.Requested)) is { } face)
            {
                return face;
            }
        }

        return null;
    }

    /// <summary>Finds a cached face, or asks the font manager for it.</summary>
    /// <param name="key">The face key.</param>
    /// <returns>The face, or <see langword="null"/> when the system lacks the family.</returns>
    private SubstituteFace? Find(FaceKey key)
    {
        lock (_gate)
        {
            if (_faces.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var created = Create(key);
        lock (_gate)
        {
            if (_faces.TryGetValue(key, out var raced))
            {
                created?.Dispose();
                return raced;
            }

            _faces[key] = created;
            return created;
        }
    }

    /// <summary>Asks the font manager for a family in a weight and slant.</summary>
    /// <param name="key">The face key.</param>
    /// <returns>The face, or <see langword="null"/>.</returns>
    private SubstituteFace? Create(FaceKey key)
    {
        var font = provider?.Match(key.Family, key.Weight, key.Italic);
        if (font is null || string.IsNullOrEmpty(font.FamilyName) || font.GlyphCount == 0)
        {
            font?.Dispose();
            return null;
        }

        var requested = key.Requested && NamesMatch(font.FamilyName, key.Family);
        return new(font, requested);
    }

    /// <summary>Makes the face used when no family matches: the platform default.</summary>
    /// <returns>The face.</returns>
    /// <exception cref="InvalidOperationException">No bundled default face or selected font provider is available.</exception>
    private SubstituteFace CreateFallback()
    {
        lock (_gate)
        {
            if (_fallback is { } cached)
            {
                return cached;
            }

            var font = provider?.DefaultFace;
            var face = font is null
                ? BundledFaces.Get(resources, BundledFamily.Sans, false, false)
                    ?? throw new InvalidOperationException("No bundled default font or selected font provider is available.")
                : new SubstituteFace(font, false);
            Volatile.Write(ref _fallback, face);
            return face;
        }
    }
}
