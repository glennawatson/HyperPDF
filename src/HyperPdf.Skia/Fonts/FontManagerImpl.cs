// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Fonts;

/// <summary>Platform font manager implementation using modern SkiaSharp APIs.</summary>
internal sealed class FontManagerImpl : IFontManagerImpl
{
    /// <summary>The minimum font weight considered bold for simulations.</summary>
    private const int BoldWeightThreshold = 600;

    /// <summary>The most fallback answers kept.</summary>
    private const int MaxFallbacks = 512;

    /// <summary>The answers to earlier fallback requests, by what was asked; a typeface kept here is shared.</summary>
    private readonly Dictionary<FallbackKey, SkiaTypeface?> _fallbacks = [];

    /// <summary>Guards <see cref="_fallbacks"/>.</summary>
    private readonly Lock _fallbackGate = new();

    /// <summary>The typefaces made from the application's own fonts, by what was asked; each is made on first use and kept.</summary>
    private readonly Dictionary<BundledKey, SkiaTypeface?> _bundled = [];

    /// <summary>Guards <see cref="_bundled"/>.</summary>
    private readonly Lock _bundledGate = new();

    /// <summary>The family names of the application's own fonts.</summary>
    private readonly IReadOnlyList<string> _bundledFamilies;

    /// <summary>Reads the font file of an application font, or <see langword="null"/> when there are none.</summary>
    private readonly Func<string, bool, bool, byte[]?>? _bundledReader;

    /// <summary>The underlying Skia font manager.</summary>
    private SKFontManager _fontManager = SKFontManager.Default;

    /// <summary>Initializes a new instance of the <see cref="FontManagerImpl"/> class that uses only the system's fonts.</summary>
    public FontManagerImpl()
        : this(new SkiaOptions())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontManagerImpl"/> class.</summary>
    /// <param name="options">The options, which can name fonts the application carries itself.</param>
    public FontManagerImpl(SkiaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _bundledFamilies = options.BundledFamilies;
        _bundledReader = options.BundledFontReader;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetDefaultFontFamilyName() => SKTypeface.Default.FamilyName;

    /// <inheritdoc/>
    public string[] GetInstalledFontFamilyNames(bool checkForUpdates = false)
    {
        if (checkForUpdates)
        {
            _fontManager = SKFontManager.CreateDefault();

            // New fonts can change which font has a character.
            lock (_fallbackGate)
            {
                _fallbacks.Clear();
            }
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
        culture ??= CultureInfo.CurrentUICulture;
        var key = new FallbackKey(codepoint, fontStyle, fontWeight, fontStretch, string.IsNullOrEmpty(familyName) ? null : familyName, culture.Name);

        // Asking the system for a font that has a character searches every installed font, which takes under a millisecond to
        // several; text asks again for each unmatched character of each run, so the answer, including "none has it", is kept.
        lock (_fallbackGate)
        {
            if (!_fallbacks.TryGetValue(key, out var matched))
            {
                var keep = _fallbacks.Count < MaxFallbacks;
                matched = MatchUncached(key, keep);
                if (keep)
                {
                    _fallbacks[key] = matched;
                }
            }

            platformTypeface = matched;
            return matched is not null;
        }
    }

    /// <inheritdoc/>
    public bool TryCreateGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        platformTypeface = null;
        if (TryBundled(familyName, (int)weight >= BoldWeightThreshold, style != FontStyle.Normal) is { } own)
        {
            // The application's own face has the style asked for, so nothing is simulated.
            platformTypeface = own;
            return true;
        }

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

    /// <summary>Gets the typeface of one of the application's own fonts, making it on first use.</summary>
    /// <param name="familyName">The family asked for.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>The shared typeface, or <see langword="null"/> when the family is not one of the application's.</returns>
    private SkiaTypeface? TryBundled(string familyName, bool bold, bool italic)
    {
        if (_bundledReader is null || FindBundledFamily(familyName) is not { } family)
        {
            return null;
        }

        var key = new BundledKey(family, bold, italic);
        lock (_bundledGate)
        {
            ref var typeface = ref CollectionsMarshal.GetValueRefOrAddDefault(_bundled, key, out var exists);
            if (!exists)
            {
                typeface = _bundledReader(family, bold, italic) is { } file && SKTypeface.FromData(SKData.CreateCopy(file)) is { } made
                    ? new SkiaTypeface(made, FontSimulations.None) { IsShared = true }
                    : null;
            }

            return typeface;
        }
    }

    /// <summary>Finds which of the application's families a name is, ignoring case.</summary>
    /// <param name="familyName">The name asked for.</param>
    /// <returns>The family as the application spells it, or <see langword="null"/>.</returns>
    private string? FindBundledFamily(string familyName)
    {
        // An indexed loop: foreach over the interface would allocate an enumerator for every font lookup.
        for (var i = 0; i < _bundledFamilies.Count; i++)
        {
            if (string.Equals(_bundledFamilies[i], familyName, StringComparison.OrdinalIgnoreCase))
            {
                return _bundledFamilies[i];
            }
        }

        return null;
    }

    /// <summary>Asks the system font manager for a font that has a character.</summary>
    /// <param name="key">What to match.</param>
    /// <param name="shared">Whether the font manager keeps the typeface, so a caller's dispose must leave it alone.</param>
    /// <returns>The typeface, or <see langword="null"/> when no font has the character.</returns>
    private SkiaTypeface? MatchUncached(in FallbackKey key, bool shared)
    {
        var style = new SKFontStyle((SKFontStyleWeight)key.Weight, (SKFontStyleWidth)key.Stretch, key.Style.ToSkia());
        var matched = _fontManager.MatchCharacter(key.Family, style, [key.Culture], key.Codepoint);
        return matched is null ? null : new SkiaTypeface(matched, FontSimulations.None) { IsShared = shared };
    }

    /// <summary>Which face of the application's own fonts was asked for.</summary>
    /// <param name="Family">The family.</param>
    /// <param name="Bold">Whether the face is bold.</param>
    /// <param name="Italic">Whether the face slants.</param>
    private readonly record struct BundledKey(string Family, bool Bold, bool Italic);

    /// <summary>What a fallback request asked for.</summary>
    /// <param name="Codepoint">The character.</param>
    /// <param name="Style">The style.</param>
    /// <param name="Weight">The weight.</param>
    /// <param name="Stretch">The stretch.</param>
    /// <param name="Family">The family asked for first, or <see langword="null"/>.</param>
    /// <param name="Culture">The culture's BCP 47 name.</param>
    private readonly record struct FallbackKey(int Codepoint, FontStyle Style, FontWeight Weight, FontStretch Stretch, string? Family, string Culture);
}
