// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Theming;

/// <summary>The built-in colour schemes and page tones. All four schemes are offered as equals.</summary>
public static class ColorSchemes
{
    /// <summary>Gets the Calm night page tone: warm grey paper, soft light ink.</summary>
    public static PageTone CalmNightTone { get; } = new(0x2A2826U, 0xD2CDC5U);

    /// <summary>Gets the soft paper page tone: warm off-white paper, dark warm ink.</summary>
    public static PageTone SoftPaperTone { get; } = new(0xFAF7F0U, 0x282624U);

    /// <summary>Gets the dark page tone: neutral dark paper, light ink.</summary>
    public static PageTone DarkTone { get; } = new(0x202326U, 0xE8E8E8U);

    /// <summary>Gets white ink on black paper for the high-contrast scheme.</summary>
    public static PageTone HighContrastTone { get; } = new(0x000000U, 0xFFFFFFU);

    /// <summary>Gets yellow ink on black paper, a high-contrast pair (19.6 to 1).</summary>
    public static PageTone YellowOnBlackTone { get; } = new(0x000000U, 0xFFFF00U);

    /// <summary>Gets green ink on black paper, a high-contrast pair (15.3 to 1).</summary>
    public static PageTone GreenOnBlackTone { get; } = new(0x000000U, 0x00FF00U);

    /// <summary>Gets the Calm tints: slate, sage, sand and clay.</summary>
    public static IconTints CalmTints { get; } = new(0x8FA8B8U, 0x9DBF93U, 0xCDB683U, 0xD49A94U);

    /// <summary>Gets tints for light backgrounds: darker versions of the Calm hues.</summary>
    public static IconTints LightTints { get; } = new(0x3F6478U, 0x4A7240U, 0x7A6230U, 0x9A4A42U);

    /// <summary>Gets tints for high contrast.</summary>
    public static IconTints HighContrastTints { get; } = new(0xA8D4F0U, 0xA8F0A0U, 0xF5DC8CU, 0xFFB0A8U);

    /// <summary>Gets Calm: low contrast warm neutrals with muted accents and no pure white.</summary>
    public static ColorScheme Calm { get; } = new(
        nameof(Calm),
        true,
        Window: 0x2A2826U,
        Header: 0x312F2CU,
        View: 0x2E2C29U,
        Canvas: 0x22201EU,
        Text: 0xD2CDC5U,
        InactiveText: 0x9E9992U,
        Border: 0x807B75U,
        Accent: 0x8FA8B8U,
        Selection: 0x343C42U,
        CalmTints,
        CalmNightTone);

    /// <summary>Gets High Contrast: maximum legibility.</summary>
    public static ColorScheme HighContrast { get; } = new(
        "High Contrast",
        true,
        Window: 0x000000U,
        Header: 0x101010U,
        View: 0x000000U,
        Canvas: 0x000000U,
        Text: 0xFFFFFFU,
        InactiveText: 0xC8C8C8U,
        Border: 0xC8C8C8U,
        Accent: 0xFFD600U,
        Selection: 0x003C78U,
        HighContrastTints,
        HighContrastTone);

    /// <summary>Gets Dark: a neutral dark scheme.</summary>
    public static ColorScheme Dark { get; } = new(
        nameof(Dark),
        true,
        Window: 0x1E2124U,
        Header: 0x26292DU,
        View: 0x1B1E20U,
        Canvas: 0x141618U,
        Text: 0xF0F0F0U,
        InactiveText: 0xA1A9B1U,
        Border: 0x6E757CU,
        Accent: 0x3DAEE9U,
        Selection: 0x1F3E52U,
        CalmTints,
        DarkTone);

    /// <summary>Gets Light: a neutral light scheme with no pure white.</summary>
    public static ColorScheme Light { get; } = new(
        nameof(Light),
        false,
        Window: 0xE8E6E2U,
        Header: 0xF2F0ECU,
        View: 0xF6F4F0U,
        Canvas: 0xCFCCC7U,
        Text: 0x232629U,
        InactiveText: 0x5C5F63U,
        Border: 0x7A7D81U,
        Accent: 0x2E6E96U,
        Selection: 0xC9DCE8U,
        LightTints,
        SoftPaperTone);

    /// <summary>Gets every built-in scheme.</summary>
    public static IReadOnlyList<ColorScheme> All { get; } = [Calm, HighContrast, Dark, Light];

    /// <summary>Gets the built-in scheme for a choice; <see cref="ColorSchemeChoice.FollowDesktop"/> gives Calm.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns>The scheme.</returns>
    public static ColorScheme Get(ColorSchemeChoice choice) => choice switch
    {
        ColorSchemeChoice.HighContrast => HighContrast,
        ColorSchemeChoice.Dark => Dark,
        ColorSchemeChoice.Light => Light,
        _ => Calm,
    };
}
