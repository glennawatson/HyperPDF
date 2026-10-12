// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Platform;

namespace HyperPdf.Platform.Linux.Kde;

/// <summary>Reads the colour scheme and font from KDE's <c>kdeglobals</c> INI file.</summary>
public static class KdeGlobalsParser
{
    /// <summary>The maximum value of a colour channel.</summary>
    private const int ChannelMax = 255;

    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>The bit offset of the red channel.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of the green channel.</summary>
    private const int GreenShift = 8;

    /// <summary>The bit offset of the alpha channel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The number of components in an RGB triple.</summary>
    private const int RgbComponents = 3;

    /// <summary>Relative luminance below which a palette is considered dark.</summary>
    private const double DarkThreshold = 0.5;

    /// <summary>Rec. 709 luma weight of red.</summary>
    private const double RedWeight = 0.2126;

    /// <summary>Rec. 709 luma weight of green.</summary>
    private const double GreenWeight = 0.7152;

    /// <summary>Rec. 709 luma weight of blue.</summary>
    private const double BlueWeight = 0.0722;

    /// <summary>The section holding window colours.</summary>
    private const string WindowSection = "Colors:Window";

    /// <summary>The key of a normal background colour.</summary>
    private const string BackgroundKey = "BackgroundNormal";

    /// <summary>The general settings section.</summary>
    private const string GeneralSection = "General";

    /// <summary>The KDE behaviour section.</summary>
    private const string KdeSection = "KDE";

    /// <summary>The number of components in an RGBA quadruple.</summary>
    private const int RgbaComponents = 4;

    /// <summary>The key of a normal foreground colour.</summary>
    private const string ForegroundKey = "ForegroundNormal";

    /// <summary>Parses <c>kdeglobals</c> content.</summary>
    /// <param name="content">The file content.</param>
    /// <returns>The palette, or <see langword="null"/> when no window colours are defined.</returns>
    public static DesktopPalette? Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var ini = ParseIni(content);
        if (!TryGetColor(ini, WindowSection, BackgroundKey, out var windowBackground))
        {
            return null;
        }

        var windowForeground = GetColor(ini, WindowSection, ForegroundKey, OpaqueAlpha);
        var accent = TryGetColor(ini, GeneralSection, "AccentColor", out var generalAccent)
            ? generalAccent
            : GetColor(ini, "Colors:Selection", BackgroundKey, OpaqueAlpha | 0x3DAEE9U);
        ParseFont(Get(ini, GeneralSection, "font"), out var family, out var size);
        var animation = double.TryParse(Get(ini, KdeSection, "AnimationDurationFactor"), NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) ? factor : (double?)null;
        var blink = int.TryParse(Get(ini, KdeSection, "CursorBlinkRate"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate) ? rate : (int?)null;
        return new()
        {
            SchemeName = Get(ini, GeneralSection, "ColorScheme"),
            WindowBackground = windowBackground,
            WindowForeground = windowForeground,
            ViewBackground = GetColor(ini, "Colors:View", BackgroundKey, windowBackground),
            ViewForeground = GetColor(ini, "Colors:View", ForegroundKey, windowForeground),
            ButtonBackground = GetColor(ini, "Colors:Button", BackgroundKey, windowBackground),
            ButtonForeground = GetColor(ini, "Colors:Button", ForegroundKey, windowForeground),
            HeaderBackground = GetColor(ini, "Colors:Header", BackgroundKey, windowBackground),
            Accent = accent,
            AccentForeground = GetColor(ini, "Colors:Selection", ForegroundKey, OpaqueAlpha | 0xFFFFFFU),
            InactiveForeground = GetColor(ini, WindowSection, "ForegroundInactive", windowForeground),
            FontFamily = family,
            FontSizePoints = size,
            AnimationDurationFactor = animation,
            CursorBlinkRateMilliseconds = blink,
            IsDark = Luminance(windowBackground) < DarkThreshold,
        };
    }

    /// <summary>Parses a colour of the form <c>r,g,b</c> or <c>r,g,b,a</c>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    /// <returns><see langword="true"/> when parsed.</returns>
    public static bool TryParseColor(string? value, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Span<Range> parts = stackalloc Range[RgbaComponents + 1];
        var span = value.AsSpan();
        var count = span.Split(parts, ',');
        if (count is < RgbComponents or > RgbaComponents)
        {
            return false;
        }

        Span<uint> channels = stackalloc uint[RgbaComponents];
        channels[RgbComponents] = ChannelMax;
        for (var i = 0; i < count; i++)
        {
            if (!uint.TryParse(span[parts[i]].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var channel) || channel > ChannelMax)
            {
                return false;
            }

            channels[i] = channel;
        }

        argb = (channels[RgbComponents] << AlphaShift) | (channels[0] << RedShift) | (channels[1] << GreenShift) | channels[2];
        return true;
    }

    /// <summary>Parses a Qt font description such as <c>Noto Sans,10,-1,5,50,0,0,0,0,0</c>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="family">The family.</param>
    /// <param name="size">The size in points.</param>
    private static void ParseFont(string? value, out string? family, out double? size)
    {
        family = null;
        size = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var comma = value.IndexOf(',', StringComparison.Ordinal);
        family = (comma < 0 ? value : value[..comma]).Trim();
        if (comma < 0)
        {
            return;
        }

        var rest = value.AsSpan(comma + 1);
        var next = rest.IndexOf(',');
        var sizeText = next < 0 ? rest : rest[..next];
        if (double.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && points > 0)
        {
            size = points;
        }
    }

    /// <summary>Computes the relative luminance of a colour.</summary>
    /// <param name="argb">The colour.</param>
    /// <returns>A value from 0 to 1.</returns>
    private static double Luminance(uint argb)
    {
        var r = ((argb >> RedShift) & ChannelMax) / (double)ChannelMax;
        var g = ((argb >> GreenShift) & ChannelMax) / (double)ChannelMax;
        return (RedWeight * r) + (GreenWeight * g) + (BlueWeight * ((argb & ChannelMax) / (double)ChannelMax));
    }

    /// <summary>Gets a colour or a fallback.</summary>
    /// <param name="ini">The parsed file.</param>
    /// <param name="section">The section.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The colour.</returns>
    private static uint GetColor(Dictionary<string, Dictionary<string, string>> ini, string section, string key, uint fallback) =>
        TryGetColor(ini, section, key, out var color) ? color : fallback;

    /// <summary>Gets a colour.</summary>
    /// <param name="ini">The parsed file.</param>
    /// <param name="section">The section.</param>
    /// <param name="key">The key.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetColor(Dictionary<string, Dictionary<string, string>> ini, string section, string key, out uint color) =>
        TryParseColor(Get(ini, section, key), out color);

    /// <summary>Gets a raw value.</summary>
    /// <param name="ini">The parsed file.</param>
    /// <param name="section">The section.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value or <see langword="null"/>.</returns>
    private static string? Get(Dictionary<string, Dictionary<string, string>> ini, string section, string key) =>
        ini.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value : null;

    /// <summary>Parses INI text into sections of key/value pairs.</summary>
    /// <param name="content">The text.</param>
    /// <returns>The sections.</returns>
    private static Dictionary<string, Dictionary<string, string>> ParseIni(string content)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (IsSection(line))
            {
                ref var section = ref CollectionsMarshal.GetValueRefOrAddDefault(result, line[1..^1].ToString(), out _);
                section ??= [with(StringComparer.Ordinal)];
                current = section;
            }
            else if (current is not null)
            {
                AddEntry(current, line);
            }
        }

        return result;
    }

    /// <summary>Determines whether a line is a section header.</summary>
    /// <param name="line">The trimmed line.</param>
    /// <returns><see langword="true"/> for <c>[name]</c>.</returns>
    private static bool IsSection(ReadOnlySpan<char> line) => line.Length > 1 && line[0] == '[' && line[^1] == ']';

    /// <summary>Adds a <c>key=value</c> line to a section, ignoring comments and blank lines.</summary>
    /// <param name="section">The section.</param>
    /// <param name="line">The trimmed line.</param>
    private static void AddEntry(Dictionary<string, string> section, ReadOnlySpan<char> line)
    {
        var equals = line.IndexOf('=');
        if (line.IsEmpty || line[0] == '#' || line[0] == ';' || equals <= 0)
        {
            return;
        }

        // Keys may carry a locale or "[$e]" suffix; keep the plain key.
        var key = line[..equals].Trim();
        var bracket = key.IndexOf('[');
        if (bracket > 0)
        {
            key = key[..bracket];
        }

        section[key.ToString()] = line[(equals + 1)..].Trim().ToString();
    }
}
