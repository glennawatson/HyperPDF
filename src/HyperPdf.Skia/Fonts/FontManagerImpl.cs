// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Fonts;

/// <summary>Platform font manager implementation using modern SkiaSharp APIs.</summary>
internal sealed class FontManagerImpl : IFontManagerImpl
{
    /// <summary>The minimum font weight considered bold for simulations.</summary>
    private const int BoldWeightThreshold = 600;

    /// <summary>The underlying Skia font manager.</summary>
    private SKFontManager _fontManager = SKFontManager.Default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetDefaultFontFamilyName() => SKTypeface.Default.FamilyName;

    /// <inheritdoc/>
    public string[] GetInstalledFontFamilyNames(bool checkForUpdates = false)
    {
        if (checkForUpdates)
        {
            _fontManager = SKFontManager.CreateDefault();
        }

        return _fontManager.GetFontFamilies();
    }

    /// <inheritdoc/>
    public bool TryMatchCharacter(
        int codepoint,
        FontStyle fontStyle,
        FontWeight fontWeight,
        FontStretch fontStretch,
        string? familyName,
        CultureInfo? culture,
        [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        var style = new SKFontStyle((SKFontStyleWeight)fontWeight, (SKFontStyleWidth)fontStretch, fontStyle.ToSkia());
        culture ??= CultureInfo.CurrentUICulture;
        string[] bcp47 = [culture.Name];
        var matched = _fontManager.MatchCharacter(string.IsNullOrEmpty(familyName) ? null : familyName, style, bcp47, codepoint);
        if (matched is not null)
        {
            platformTypeface = new SkiaTypeface(matched, FontSimulations.None);
            return true;
        }

        platformTypeface = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryCreateGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        platformTypeface = null;
        var fontStyle = new SKFontStyle((SKFontStyleWeight)weight, (SKFontStyleWidth)stretch, style.ToSkia());
        var matched = _fontManager.MatchFamily(familyName, fontStyle);
        if (matched is null)
        {
            return false;
        }

        var simulations = FontSimulations.None;
        if ((int)weight >= BoldWeightThreshold && !matched.IsBold)
        {
            simulations |= FontSimulations.Bold;
        }

        if (style == FontStyle.Italic && !matched.IsItalic)
        {
            simulations |= FontSimulations.Oblique;
        }

        platformTypeface = new SkiaTypeface(matched, simulations);
        return true;
    }

    /// <inheritdoc/>
    public bool TryCreateGlyphTypeface(Stream stream, FontSimulations fontSimulations, [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        var typeface = SKTypeface.FromStream(stream);
        if (typeface is not null)
        {
            platformTypeface = new SkiaTypeface(typeface, fontSimulations);
            return true;
        }

        platformTypeface = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetFamilyTypefaces(string familyName, [NotNullWhen(true)] out IReadOnlyList<Typeface>? familyTypefaces)
    {
        using var set = _fontManager.GetFontStyles(familyName);
        if (set.Count == 0)
        {
            familyTypefaces = null;
            return false;
        }

        var result = new List<Typeface>(set.Count);
        for (var i = 0; i < set.Count; i++)
        {
            using var style = set[i];
            result.Add(new(familyName, style.Slant.ToAvalonia(), (FontWeight)style.Weight, (FontStretch)style.Width));
        }

        familyTypefaces = result;
        return true;
    }
}
