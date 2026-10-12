// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Theming;

/// <summary>Combines the user's comfort settings with the desktop palette into a <see cref="ResolvedTheme"/>.</summary>
public static class ThemeResolver
{
    /// <summary>How much darker than the window the page area is, for dark schemes.</summary>
    private const double DarkCanvasShade = 0.25;

    /// <summary>How much darker than the window the page area is, for light schemes.</summary>
    private const double LightCanvasShade = 0.12;

    /// <summary>How much of the text colour goes into borders.</summary>
    private const double BorderMix = 0.4;

    /// <summary>How much of the accent goes into the selection background.</summary>
    private const double SelectionMix = 0.25;

    /// <summary>Resolves the theme.</summary>
    /// <param name="settings">The user settings.</param>
    /// <param name="desktop">The desktop palette, if any.</param>
    /// <returns>The theme.</returns>
    public static ResolvedTheme Resolve(AppSettings settings, DesktopPalette? desktop)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var scheme = settings.ColorScheme == ColorSchemeChoice.FollowDesktop && desktop is not null
            ? FromDesktop(desktop)
            : ColorSchemes.Get(settings.ColorScheme);
        var pageScheme = scheme;
        if (settings.PageTone == PageToneChoice.FollowDesktop && desktop is not null)
        {
            pageScheme = desktop.IsDark ? ColorSchemes.Dark : ColorSchemes.Light;
        }

        var tone = settings.PageToneEnabled ? GetTone(settings.PageTone, pageScheme) : PageTone.None;
        return new(
            scheme,
            tone,
            desktop?.FontFamily,
            settings.InterfaceFontSizePoints ?? desktop?.FontSizePoints,
            ShouldReduceMotion(settings.Motion, desktop),
            ShouldSteadyCaret(settings.Caret, desktop),
            settings.ToolbarStyle == ToolbarStyle.TextBesideIcons);
    }

    /// <summary>Builds a scheme from the desktop palette, keeping the built-in tints that suit its brightness.</summary>
    /// <param name="desktop">The palette.</param>
    /// <returns>The scheme.</returns>
    public static ColorScheme FromDesktop(DesktopPalette desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        var basis = desktop.IsDark ? ColorSchemes.Calm : ColorSchemes.Light;
        var paper = Rgb(desktop.ViewBackground);
        var ink = Rgb(desktop.ViewForeground);
        return basis with
        {
            Name = desktop.SchemeName ?? "Desktop",
            Window = Rgb(desktop.WindowBackground),
            Header = Rgb(desktop.HeaderBackground),
            View = paper,
            Canvas = ColorMath.Shade(Rgb(desktop.WindowBackground), desktop.IsDark ? DarkCanvasShade : LightCanvasShade),
            Text = Rgb(desktop.WindowForeground),
            InactiveText = Rgb(desktop.InactiveForeground),
            Border = ColorMath.Mix(Rgb(desktop.WindowForeground), Rgb(desktop.WindowBackground), BorderMix),
            Accent = Rgb(desktop.Accent),
            Selection = ColorMath.Mix(Rgb(desktop.Accent), Rgb(desktop.WindowBackground), SelectionMix),
            PageTone = desktop.IsDark ? new(paper, ink) : ColorSchemes.SoftPaperTone,
        };
    }

    /// <summary>Gets the page tone for a choice.</summary>
    /// <param name="choice">The choice.</param>
    /// <param name="scheme">The active scheme.</param>
    /// <returns>The tone.</returns>
    public static PageTone GetTone(PageToneChoice choice, ColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return choice switch
        {
            PageToneChoice.White => PageTone.None,
            PageToneChoice.SoftPaper => ColorSchemes.SoftPaperTone,
            PageToneChoice.CalmNight => ColorSchemes.CalmNightTone,
            PageToneChoice.Dark => ColorSchemes.DarkTone,
            PageToneChoice.FollowDesktop => scheme.IsDark ? ColorSchemes.DarkTone : PageTone.None,
            _ => scheme.PageTone,
        };
    }

    /// <summary>Decides whether motion is reduced.</summary>
    /// <param name="motion">The user preference.</param>
    /// <param name="desktop">The desktop palette, if any.</param>
    /// <returns><see langword="true"/> to turn transitions off.</returns>
    private static bool ShouldReduceMotion(MotionPreference motion, DesktopPalette? desktop) => motion switch
    {
        MotionPreference.Reduced => true,
        MotionPreference.Normal => false,
        _ => desktop?.AnimationDurationFactor is <= 0,
    };

    /// <summary>Decides whether the text caret is steady.</summary>
    /// <param name="caret">The user preference.</param>
    /// <param name="desktop">The desktop palette, if any.</param>
    /// <returns><see langword="true"/> for a caret that does not blink.</returns>
    /// <remarks>With no desktop preference a steady caret is the default: nothing should flash indefinitely.</remarks>
    private static bool ShouldSteadyCaret(CaretPreference caret, DesktopPalette? desktop) => caret switch
    {
        CaretPreference.Steady => true,
        CaretPreference.Blinking => false,
        _ => desktop?.CursorBlinkRateMilliseconds is null or <= 0,
    };

    /// <summary>Drops the alpha channel.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    /// <returns>The colour as 0xRRGGBB.</returns>
    private static uint Rgb(uint argb) => argb & ColorMath.RgbMask;
}
