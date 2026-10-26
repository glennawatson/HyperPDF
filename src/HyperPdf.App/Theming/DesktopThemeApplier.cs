// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using HyperPdf.Core.Theming;

namespace HyperPdf.App.Theming;

/// <summary>
/// Applies a <see cref="ResolvedTheme"/> (a built-in scheme or the desktop's, plus the comfort settings) to the
/// application's resources, the Fluent palette and a few comfort styles.
/// </summary>
internal static class DesktopThemeApplier
{
    /// <summary>Points to device independent pixels.</summary>
    private const double PixelsPerPoint = 96.0 / 72.0;

    /// <summary>The alpha of the tab hover highlight.</summary>
    private const byte HoverAlpha = 0x26;

    /// <summary>The alpha of search hit and text selection fills, soft enough to keep the text readable.</summary>
    private const byte HighlightAlpha = 0x59;

    /// <summary>The opacity of the paper laid over text away from what is read aloud, when the focus band is on.</summary>
    private const byte DimAlpha = 0x99;

    /// <summary>The opacity of the mark on the sentence being read aloud, lighter than a highlight.</summary>
    private const byte SpokenAlpha = 0x40;

    /// <summary>How much darker the tab bar is than the tool bar.</summary>
    private const double TabBarShade = 0.04;

    /// <summary>How much of the text colour untinted icons use.</summary>
    private const double IconMix = 0.78;

    /// <summary>How much of the text colour the Fluent "medium high" base uses.</summary>
    private const double MediumHighMix = 0.8;

    /// <summary>How much of the text colour the Fluent "medium low" base uses.</summary>
    private const double MediumLowMix = 0.4;

    /// <summary>How much of the text colour the Fluent "low" base uses.</summary>
    private const double LowMix = 0.2;

    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint Opaque = 0xFF000000U;

    /// <summary>The bit offset of the alpha channel.</summary>
    private const int AlphaShift = 24;

    /// <summary>A caret blink interval long enough to read as steady.</summary>
    private static readonly TimeSpan SteadyCaretInterval = TimeSpan.FromDays(1);

    /// <summary>Turns off every transition when motion is reduced.</summary>
    private static readonly Style ReduceMotionStyle = new(static x => x.Is<Control>()) { Setters = { new Setter(Animatable.TransitionsProperty, null) } };

    /// <summary>Stops the text caret from blinking.</summary>
    private static readonly Style SteadyCaretStyle = new(static x => x.Is<TextBox>()) { Setters = { new Setter(TextBox.CaretBlinkIntervalProperty, SteadyCaretInterval) } };

    /// <summary>Hides tool bar text labels when the user prefers icons only.</summary>
    private static readonly Style HideLabelsStyle = new(static x => x.OfType<TextBlock>().Class("label")) { Setters = { new Setter(Visual.IsVisibleProperty, false) } };

    /// <summary>Applies a theme to the application.</summary>
    /// <param name="application">The application.</param>
    /// <param name="theme">The theme.</param>
    internal static void Apply(Application application, ResolvedTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(theme);
        var scheme = theme.Scheme;
        var variant = scheme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        application.RequestedThemeVariant = variant;
        ApplyResources(application.Resources, scheme, theme);
        foreach (var style in application.Styles)
        {
            if (style is FluentTheme fluent)
            {
                fluent.Palettes[variant] = CreatePalette(scheme);
            }
        }

        SetStyle(application.Styles, ReduceMotionStyle, theme.ReduceMotion);
        SetStyle(application.Styles, SteadyCaretStyle, theme.SteadyCaret);
        SetStyle(application.Styles, HideLabelsStyle, !theme.ShowLabels);
    }

    /// <summary>Applies the theme's font to a window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="theme">The theme.</param>
    internal static void ApplyFont(Window window, ResolvedTheme theme)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(theme);
        if (theme.FontFamily is { Length: > 0 } family)
        {
            window.FontFamily = new($"{family}, Noto Sans, sans-serif");
        }
        else
        {
            window.ClearValue(TemplatedControl.FontFamilyProperty);
        }

        if (theme.FontSizePoints is { } points)
        {
            window.FontSize = Math.Round(points * PixelsPerPoint);
        }
        else
        {
            window.ClearValue(TemplatedControl.FontSizeProperty);
        }
    }

    /// <summary>Converts 0xRRGGBB to an opaque colour.</summary>
    /// <param name="rgb">The colour.</param>
    /// <returns>The Avalonia colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Color ToColor(uint rgb) => Color.FromUInt32(Opaque | rgb);

    /// <summary>Sets the theme's brushes in a resource dictionary, so a single window can show a theme without changing the application.</summary>
    /// <param name="resources">The resources to fill, such as the application's or a window's.</param>
    /// <param name="scheme">The scheme.</param>
    /// <param name="theme">The theme.</param>
    internal static void ApplyResources(IResourceDictionary resources, ColorScheme scheme, ResolvedTheme theme)
    {
        var tints = scheme.Tints;
        resources["AppWindowBackground"] = Brush(ColorMath.Shade(scheme.Window, TabBarShade));
        resources["AppHeaderBackground"] = Brush(scheme.Header);
        resources["AppViewBackground"] = Brush(scheme.View);
        resources["AppCanvasBackground"] = Brush(scheme.Canvas);
        resources["AppForeground"] = Brush(scheme.Text);
        resources["AppSecondaryForeground"] = Brush(scheme.InactiveText);
        resources["AppAccentBrush"] = Brush(scheme.Accent);
        resources["AppBorderBrush"] = Brush(scheme.Border);
        resources["AppTabHoverBackground"] = new SolidColorBrush(ToColor(scheme.Accent, HoverAlpha));
        resources["AppSelectionBrush"] = Brush(scheme.Selection);
        resources["AppIconBrush"] = Brush(ColorMath.Mix(scheme.Text, scheme.Header, IconMix));
        resources["AppIconNav"] = Brush(tints.Navigation);
        resources["AppIconAdd"] = Brush(tints.Add);
        resources["AppIconEdit"] = Brush(tints.Edit);
        resources["AppIconRemove"] = Brush(tints.Remove);
        resources["AppHitBrush"] = new SolidColorBrush(ToColor(tints.Edit, HighlightAlpha));
        resources["AppCurrentHitOutline"] = Brush(scheme.Accent);
        resources["AppTextSelectionBrush"] = new SolidColorBrush(ToColor(tints.Navigation, HighlightAlpha));
        resources["AppSpokenBrush"] = new SolidColorBrush(ToColor(tints.Add, SpokenAlpha));
        var paper = theme.PageTone.IsIdentity ? 0xFFFFFFU : theme.PageTone.Paper;
        resources["AppPaperBrush"] = Brush(paper);
        resources["AppDimBrush"] = new SolidColorBrush(ToColor(paper, DimAlpha));
    }

    /// <summary>Converts 0xRRGGBB and an alpha to a colour.</summary>
    /// <param name="rgb">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The Avalonia colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Color ToColor(uint rgb, byte alpha) => Color.FromUInt32(((uint)alpha << AlphaShift) | rgb);

    /// <summary>Builds a Fluent palette from a scheme.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The palette.</returns>
    private static ColorPaletteResources CreatePalette(ColorScheme scheme) => new()
    {
        Accent = ToColor(scheme.Accent),
        RegionColor = ToColor(scheme.Window),
        ErrorText = ToColor(scheme.Tints.Remove),
        BaseHigh = ToColor(scheme.Text),
        BaseMediumHigh = ToColor(ColorMath.Mix(scheme.Text, scheme.Window, MediumHighMix)),
        BaseMedium = ToColor(scheme.InactiveText),
        BaseMediumLow = ToColor(ColorMath.Mix(scheme.Text, scheme.Window, MediumLowMix)),
        BaseLow = ToColor(ColorMath.Mix(scheme.Text, scheme.Window, LowMix)),
        AltHigh = ToColor(scheme.View),
        AltMediumHigh = ToColor(scheme.View),
        AltMedium = ToColor(scheme.View),
        AltLow = ToColor(scheme.View),
        ChromeLow = ToColor(scheme.Header),
        ChromeMedium = ToColor(scheme.Header),
        ChromeMediumLow = ToColor(scheme.Window),
        ChromeHigh = ToColor(scheme.Border),
        ListLow = ToColor(ColorMath.Mix(scheme.Selection, scheme.View, MediumLowMix)),
        ListMedium = ToColor(scheme.Selection),
    };

    /// <summary>Creates a solid brush.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>The brush.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SolidColorBrush Brush(uint rgb) => new(ToColor(rgb));

    /// <summary>Adds or removes a comfort style.</summary>
    /// <param name="styles">The application styles.</param>
    /// <param name="style">The style.</param>
    /// <param name="enabled">Whether the style applies.</param>
    private static void SetStyle(Styles styles, Style style, bool enabled)
    {
        var present = styles.Contains(style);
        if (enabled && !present)
        {
            styles.Add(style);
        }
        else if (!enabled && present)
        {
            _ = styles.Remove(style);
        }
    }
}
