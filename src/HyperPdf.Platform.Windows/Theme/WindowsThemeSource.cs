// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
using Microsoft.Win32;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Platform.Windows.Theme;

/// <summary>
/// Follows Windows' light or dark app mode, accent colour, high contrast, animation and caret blink settings, and
/// publishes them again whenever the personalisation settings change.
/// </summary>
[DebuggerDisplay("WindowsThemeSource: Windows colours")]
public sealed class WindowsThemeSource : IDesktopThemeSource
{
    /// <summary>The key holding the app mode.</summary>
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The key holding the accent colour.</summary>
    private const string AccentKey = @"Software\Microsoft\Windows\DWM";

    /// <summary>The key holding accessibility and desktop settings.</summary>
    private const string ThemesKey = @"Software\Microsoft\Windows\CurrentVersion\Themes";

    /// <summary>Windows' default accent blue.</summary>
    private const uint DefaultAccent = 0x0078D4;

    /// <summary>The Windows interface font.</summary>
    private const string FontFamily = "Segoe UI";

    /// <summary>The Windows interface font size in points.</summary>
    private const double FontSizePoints = 9;

    /// <summary>The caret blink time meaning "never blink".</summary>
    private const uint NoBlink = uint.MaxValue;

    /// <summary>The SystemParametersInfo action reading whether animations are on (SPI_GETCLIENTAREAANIMATION).</summary>
    private const uint GetClientAreaAnimation = 0x1042;

    /// <summary>The red channel shift.</summary>
    private const int RedShift = 16;

    /// <summary>The green channel shift.</summary>
    private const int GreenShift = 8;

    /// <summary>A byte mask.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>The red share of perceived brightness.</summary>
    private const double RedWeight = 0.299;

    /// <summary>The green share of perceived brightness.</summary>
    private const double GreenWeight = 0.587;

    /// <summary>The blue share of perceived brightness.</summary>
    private const double BlueWeight = 0.114;

    /// <summary>The largest channel value.</summary>
    private const double FullScale = 255;

    /// <summary>The luminance above which text on the accent is dark.</summary>
    private const double LightAccent = 0.6;

    /// <summary>Windows 11's dark app colours.</summary>
    private static readonly DesktopPalette DarkPalette = new()
    {
        SchemeName = "Windows dark",
        IsDark = true,
        WindowBackground = 0x202020,
        WindowForeground = 0xFFFFFF,
        ViewBackground = 0x2B2B2B,
        ViewForeground = 0xFFFFFF,
        ButtonBackground = 0x2D2D2D,
        ButtonForeground = 0xFFFFFF,
        HeaderBackground = 0x202020,
        InactiveForeground = 0x9D9D9D,
        FontFamily = FontFamily,
        FontSizePoints = FontSizePoints,
    };

    /// <summary>Windows 11's light app colours.</summary>
    private static readonly DesktopPalette LightPalette = new()
    {
        SchemeName = "Windows light",
        WindowBackground = 0xF3F3F3,
        WindowForeground = 0x1A1A1A,
        ViewBackground = 0xFFFFFF,
        ViewForeground = 0x1A1A1A,
        ButtonBackground = 0xFBFBFB,
        ButtonForeground = 0x1A1A1A,
        HeaderBackground = 0xF3F3F3,
        InactiveForeground = 0x5D5D5D,
        FontFamily = FontFamily,
        FontSizePoints = FontSizePoints,
    };

    /// <summary>Initializes a new instance of the <see cref="WindowsThemeSource"/> class.</summary>
    public WindowsThemeSource() =>
        Palette = new(Signal.Defer(static () => WatchChanges().Select(static _ => Read()).StartWith(Read()).DistinctUntilChanged()));

    /// <inheritdoc/>
    public AsObservableSignal<DesktopPalette?> Palette { get; }

    /// <summary>Builds the palette for light or dark app mode with an accent.</summary>
    /// <param name="dark">Whether apps use dark mode.</param>
    /// <param name="accent">The accent as 0xRRGGBB.</param>
    /// <param name="animations">Whether animations are on.</param>
    /// <param name="blinkMilliseconds">The caret blink interval, zero for none.</param>
    /// <returns>The palette, matching Windows 11's own colours.</returns>
    public static DesktopPalette Build(bool dark, uint accent, bool animations, int blinkMilliseconds) => (dark ? DarkPalette : LightPalette) with
    {
        Accent = accent,
        AccentForeground = Luminance(accent) > LightAccent ? 0x000000U : 0xFFFFFFU,
        AnimationDurationFactor = animations ? 1 : 0,
        CursorBlinkRateMilliseconds = blinkMilliseconds,
    };

    /// <summary>Converts the registry's 0xAABBGGRR accent to 0xRRGGBB.</summary>
    /// <param name="abgr">The registry value.</param>
    /// <returns>The colour.</returns>
    public static uint FromAbgr(uint abgr) => ((abgr & ByteMask) << RedShift) | (abgr & (ByteMask << GreenShift)) | ((abgr >> RedShift) & ByteMask);

    /// <summary>Gets a colour's relative brightness.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>From 0 (black) to 1 (white).</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double Luminance(uint rgb) =>
        ((RedWeight * ((rgb >> RedShift) & ByteMask)) + (GreenWeight * ((rgb >> GreenShift) & ByteMask)) + (BlueWeight * (rgb & ByteMask))) / FullScale;

    /// <summary>Reads the current settings.</summary>
    /// <returns>The palette.</returns>
    private static unsafe DesktopPalette Read()
    {
        using var personalize = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        using var dwm = Registry.CurrentUser.OpenSubKey(AccentKey);
        var dark = personalize?.GetValue("AppsUseLightTheme") is int light && light == 0;
        var accent = dwm?.GetValue("AccentColor") is int abgr ? FromAbgr(unchecked((uint)abgr)) : DefaultAccent;
        Span<int> animations = [1];
        fixed (int* value = animations)
        {
            _ = NativeMethods.SystemParametersInfo(GetClientAreaAnimation, 0, value, 0);
        }

        var blink = NativeMethods.GetCaretBlinkTime();
        return Build(dark, accent, animations[0] != 0, blink == NoBlink ? 0 : (int)blink);
    }

    /// <summary>Signals each change to the personalisation settings, on a background thread, while subscribed.</summary>
    /// <returns>The changes.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static AsObservableSignal<RxVoid> WatchChanges() =>
        new(Signal.Using(static () => new RegistryWatcher([(ThemesKey, true), (AccentKey, false)]), static watcher => watcher.Changes));
}
