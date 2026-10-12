// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Platform.MacOS;

/// <summary>
/// Follows macOS's light or dark appearance, accent colour and Reduce Motion setting, and publishes them again when the
/// appearance or accent changes (macOS announces both as distributed notifications).
/// </summary>
[DebuggerDisplay("MacThemeSource: macOS colours")]
public sealed unsafe class MacThemeSource : IDesktopThemeSource
{
    /// <summary>The global preferences domain.</summary>
    private const string GlobalDomain = "kCFPreferencesAnyApplication";

    /// <summary>The accessibility preferences domain.</summary>
    private const string AccessibilityDomain = "com.apple.universalaccess";

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

    /// <summary>The red channel shift.</summary>
    private const int RedShift = 16;

    /// <summary>The green channel shift.</summary>
    private const int GreenShift = 8;

    /// <summary>A byte mask.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>The default blue accent of macOS.</summary>
    private const uint DefaultAccent = 0x007AFF;

    /// <summary>The accent colours by their <c>AppleAccentColor</c> number.</summary>
    private static readonly FrozenDictionary<long, uint> Accents = new Dictionary<long, uint>
    {
        [-1] = 0x8C8C8C,
        [0] = 0xFF5257,
        [1] = 0xF7821B,
        [2] = 0xFFC600,
        [3] = 0x62BA46,
        [4] = 0x007AFF,
        [5] = 0xA550A7,
        [6] = 0xF74F9E,
    }.ToFrozenDictionary();

    /// <summary>The dark appearance colours of macOS.</summary>
    private static readonly DesktopPalette DarkPalette = new()
    {
        SchemeName = "macOS dark",
        IsDark = true,
        WindowBackground = 0x1E1E1E,
        WindowForeground = 0xDFDFDF,
        ViewBackground = 0x282828,
        ViewForeground = 0xDFDFDF,
        ButtonBackground = 0x3A3A3A,
        ButtonForeground = 0xDFDFDF,
        HeaderBackground = 0x2D2D2D,
        InactiveForeground = 0x8C8C8C,
    };

    /// <summary>The light appearance colours of macOS.</summary>
    private static readonly DesktopPalette LightPalette = new()
    {
        SchemeName = "macOS light",
        WindowBackground = 0xECECEC,
        WindowForeground = 0x262626,
        ViewBackground = 0xFFFFFF,
        ViewForeground = 0x262626,
        ButtonBackground = 0xFFFFFF,
        ButtonForeground = 0x262626,
        HeaderBackground = 0xE8E8E8,
        InactiveForeground = 0x808080,
    };

    /// <summary>Initializes a new instance of the <see cref="MacThemeSource"/> class.</summary>
    public MacThemeSource() =>
        Palette = new(Signal.Defer(static () => Signal.Using(static () => new ChangeObserver(), static observer => observer.Changes)
            .Select(static _ => Read())
            .StartWith(Read())
            .DistinctUntilChanged()));

    /// <inheritdoc/>
    public AsObservableSignal<DesktopPalette?> Palette { get; }

    /// <summary>Builds the palette.</summary>
    /// <param name="dark">Whether the appearance is dark.</param>
    /// <param name="accent">The <c>AppleAccentColor</c> number, or <see langword="null"/> for the default blue.</param>
    /// <param name="reduceMotion">Whether Reduce Motion is on.</param>
    /// <returns>The palette.</returns>
    public static DesktopPalette Build(bool dark, long? accent, bool reduceMotion)
    {
        var colour = accent is { } number && Accents.TryGetValue(number, out var found) ? found : DefaultAccent;
        return (dark ? DarkPalette : LightPalette) with
        {
            Accent = colour,
            AccentForeground = Luminance(colour) > LightAccent ? 0x000000U : 0xFFFFFFU,
            AnimationDurationFactor = reduceMotion ? 0 : 1,
        };
    }

    /// <summary>Gets a colour's relative brightness.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>From 0 (black) to 1 (white).</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double Luminance(uint rgb) =>
        ((RedWeight * ((rgb >> RedShift) & ByteMask)) + (GreenWeight * ((rgb >> GreenShift) & ByteMask)) + (BlueWeight * (rgb & ByteMask))) / FullScale;

    /// <summary>Reads the current settings.</summary>
    /// <returns>The palette.</returns>
    private static DesktopPalette Read()
    {
        var dark = Foundation.ReadPreference("AppleInterfaceStyle", GlobalDomain) is string style && style.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        var accent = Foundation.ReadPreference("AppleAccentColor", GlobalDomain) as long?;
        var reduceMotion = Foundation.ReadPreference("reduceMotion", AccessibilityDomain) is true or 1L;
        return Build(dark, accent, reduceMotion);
    }

    /// <summary>Listens for appearance changes on the distributed notification centre while alive.</summary>
    [DebuggerDisplay("ChangeObserver: Appearance changes")]
    private sealed class ChangeObserver : IDisposable
    {
        /// <summary>Deliver notifications straight away (CFNotificationSuspensionBehaviorDeliverImmediately).</summary>
        private const nint DeliverImmediately = 4;

        /// <summary>The notifications announcing an appearance or accent change.</summary>
        private static readonly string[] ChangeNotifications = ["AppleInterfaceThemeChangedNotification", "AppleColorPreferencesChangedNotification"];

        /// <summary>The changes.</summary>
        private readonly Signal<RxVoid> _changes = new();

        /// <summary>Identifies this observer to Core Foundation.</summary>
        private GCHandle _self;

        /// <summary>Initializes a new instance of the <see cref="ChangeObserver"/> class and starts listening.</summary>
        internal ChangeObserver()
        {
            _self = GCHandle.Alloc(this);
            var center = NativeMethods.GetDistributedCenter();
            foreach (var name in ChangeNotifications)
            {
                var notification = Foundation.CreateString(name);
                NativeMethods.AddObserver(center, GCHandle.ToIntPtr(_self), &OnChanged, notification, 0, DeliverImmediately);
                NativeMethods.Release(notification);
            }

            Changes = new(_changes);
        }

        /// <summary>Gets the changes.</summary>
        internal AsObservableSignal<RxVoid> Changes { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            NativeMethods.RemoveEveryObserver(NativeMethods.GetDistributedCenter(), GCHandle.ToIntPtr(_self));
            _self.Free();
            _changes.Dispose();
        }

        /// <summary>Called by Core Foundation when the appearance changes.</summary>
        /// <param name="center">The centre.</param>
        /// <param name="observer">This observer's handle.</param>
        /// <param name="name">The notification name.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="userInfo">The details.</param>
        [UnmanagedCallersOnly]
        private static void OnChanged(nint center, nint observer, nint name, nint sender, nint userInfo)
        {
            if (GCHandle.FromIntPtr(observer).Target is ChangeObserver self)
            {
                self._changes.OnNext(RxVoid.Default);
            }
        }
    }
}
