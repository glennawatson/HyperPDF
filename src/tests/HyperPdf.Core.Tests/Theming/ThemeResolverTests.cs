// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Theming;

namespace HyperPdf.Core.Tests.Theming;

/// <summary>Tests for <see cref="ThemeResolver"/>.</summary>
public sealed class ThemeResolverTests
{
    /// <summary>Masks off the alpha channel.</summary>
    private const uint RgbMask = 0xFFFFFFU;

    /// <summary>A dark desktop window colour.</summary>
    private const uint DarkWindow = 0xFF202326U;

    /// <summary>A dark desktop view colour.</summary>
    private const uint DarkView = 0xFF141618U;

    /// <summary>A light desktop text colour.</summary>
    private const uint LightText = 0xFFFCFCFCU;

    /// <summary>High contrast applies light ink on dark paper when pages match the interface.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HighContrastIncludesDarkPages()
    {
        var theme = ThemeResolver.Resolve(new AppSettings { ColorScheme = ColorSchemeChoice.HighContrast, PageTone = PageToneChoice.MatchScheme }, null);

        await Assert.That(theme.PageTone.Paper).IsEqualTo(0U);
        await Assert.That(theme.PageTone.Ink).IsEqualTo(RgbMask);
    }

    /// <summary>Verifies the Calm fallback and its defaults when there is no desktop palette.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FallsBackToCalm()
    {
        var theme = ThemeResolver.Resolve(Defaults(), null);

        await Assert.That(theme.Scheme).IsEqualTo(ColorSchemes.Calm);
        await Assert.That(theme.PageTone).IsEqualTo(ColorSchemes.CalmNightTone);
        await Assert.That(theme.SteadyCaret).IsTrue();
        await Assert.That(theme.ReduceMotion).IsFalse();
        await Assert.That(theme.ShowLabels).IsTrue();
    }

    /// <summary>Verifies the desktop palette drives the scheme, tone, motion and caret.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsDesktop()
    {
        const int blinkRate = 1000;
        var desktop = Desktop() with { AnimationDurationFactor = 0, CursorBlinkRateMilliseconds = blinkRate };

        var theme = ThemeResolver.Resolve(Defaults(), desktop);

        await Assert.That(theme.Scheme.Window).IsEqualTo(DarkWindow & RgbMask);
        await Assert.That(theme.Scheme.Tints).IsEqualTo(ColorSchemes.CalmTints);
        await Assert.That(theme.PageTone.Paper).IsEqualTo(DarkView & RgbMask);
        await Assert.That(theme.ReduceMotion).IsTrue();
        await Assert.That(theme.SteadyCaret).IsFalse();
    }

    /// <summary>Verifies explicit choices win over the desktop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplicitChoicesWin()
    {
        var settings = new AppSettings
        {
            ColorScheme = ColorSchemeChoice.Light,
            PageTone = PageToneChoice.White,
            Motion = MotionPreference.Reduced,
            Caret = CaretPreference.Blinking,
            ToolbarStyle = ToolbarStyle.IconsOnly,
        };

        var theme = ThemeResolver.Resolve(settings, Desktop());

        await Assert.That(theme.Scheme).IsEqualTo(ColorSchemes.Light);
        await Assert.That(theme.PageTone).IsEqualTo(PageTone.None);
        await Assert.That(theme.ReduceMotion).IsTrue();
        await Assert.That(theme.SteadyCaret).IsFalse();
        await Assert.That(theme.ShowLabels).IsFalse();
    }

    /// <summary>Verifies turning page tones off gives plain pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToneCanBeDisabled() =>
        await Assert.That(ThemeResolver.Resolve(new AppSettings { PageToneEnabled = false }, null).PageTone.IsIdentity).IsTrue();

    /// <summary>Verifies that page colour follows the desktop independently of the interface scheme.</summary>
    /// <param name="dark">Whether the desktop uses a dark appearance.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task PageColourFollowsDesktop(bool dark)
    {
        var settings = new AppSettings { ColorScheme = ColorSchemeChoice.Calm, PageTone = PageToneChoice.FollowDesktop };
        var theme = ThemeResolver.Resolve(settings, Desktop() with { IsDark = dark });

        await Assert.That(theme.Scheme).IsEqualTo(ColorSchemes.Calm);
        await Assert.That(theme.PageTone).IsEqualTo(dark ? ColorSchemes.DarkTone : PageTone.None);
    }

    /// <summary>Verifies white and dark page overrides ignore the desktop appearance.</summary>
    /// <param name="choice">The page override.</param>
    /// <param name="dark">Whether the desktop uses a dark appearance.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageToneChoice.White, true)]
    [Arguments(PageToneChoice.Dark, false)]
    public async Task PageOverrideIgnoresDesktop(PageToneChoice choice, bool dark)
    {
        var theme = ThemeResolver.Resolve(new AppSettings { PageTone = choice }, Desktop() with { IsDark = dark });

        await Assert.That(theme.PageTone).IsEqualTo(choice == PageToneChoice.Dark ? ColorSchemes.DarkTone : PageTone.None);
    }

    /// <summary>Verifies that desktop page colour falls back to the interface brightness without a desktop palette.</summary>
    /// <param name="scheme">The interface scheme.</param>
    /// <param name="dark">Whether pages should be dark.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ColorSchemeChoice.Calm, true)]
    [Arguments(ColorSchemeChoice.Light, false)]
    public async Task PageColourFallsBackToInterface(ColorSchemeChoice scheme, bool dark)
    {
        var theme = ThemeResolver.Resolve(new AppSettings { ColorScheme = scheme, PageTone = PageToneChoice.FollowDesktop }, null);

        await Assert.That(theme.PageTone).IsEqualTo(dark ? ColorSchemes.DarkTone : PageTone.None);
    }

    /// <summary>Creates default settings.</summary>
    /// <returns>The settings.</returns>
    private static AppSettings Defaults() => new();

    /// <summary>Creates a Breeze Dark like palette.</summary>
    /// <returns>The palette.</returns>
    private static DesktopPalette Desktop() => new()
    {
        WindowBackground = DarkWindow,
        WindowForeground = LightText,
        ViewBackground = DarkView,
        ViewForeground = LightText,
        ButtonBackground = DarkWindow,
        ButtonForeground = LightText,
        HeaderBackground = DarkWindow,
        Accent = 0xFF3DAEE9U,
        AccentForeground = LightText,
        InactiveForeground = 0xFFA1A9B1U,
        IsDark = true,
    };
}
