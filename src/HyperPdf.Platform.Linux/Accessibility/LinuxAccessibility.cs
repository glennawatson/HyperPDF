// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Platform.Linux.Accessibility.AtSpi;
using HyperPdf.Platform.Linux.DBus;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility;

/// <summary>
/// The Linux side of screen reader access: tells whether a screen reader or other assistive technology is switched on, and serves
/// the app's elements to it over AT-SPI2. It does not depend on X11 or Wayland.
/// </summary>
public static class LinuxAccessibility
{
    /// <summary>Checks whether assistive technologies are switched on for this session.</summary>
    /// <returns>A cold signal giving once whether a screen reader or another assistive technology is enabled; <see langword="false"/> when the session has no accessibility service.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<bool> IsAssistiveTechnologyEnabled() =>
        SessionBus.TryConnect()
            .SwitchMap(static session => session is null
                ? Signal.Return(false)

                // The connection lives as long as the two reads; the first switch that is on ends the check.
                : Signal.Using(
                    () => session,
                    static open => ReadFlag(open, AtSpiNames.IsEnabledProperty)
                        .SwitchMap(enabled => enabled ? Signal.Return(true) : ReadFlag(open, AtSpiNames.ScreenReaderEnabledProperty))));

    /// <summary>Starts serving the app's elements to screen readers.</summary>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>A cold signal giving the running server, to dispose when the app no longer serves elements, or <see langword="null"/> when there is no accessibility bus.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<IDisposable?> Start(IAccessibleTree tree) =>
        Signal.FromAsync(async () => (IDisposable?)await AtSpiServer.TryStartAsync(tree).ConfigureAwait(false));

    /// <summary>Reads one of the accessibility service's switches.</summary>
    /// <param name="session">The session bus.</param>
    /// <param name="property">The switch.</param>
    /// <returns>A cold signal giving once whether it is on.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<bool> ReadFlag(DBusConnection session, string property) =>
        Signal.Defer(() =>
        {
            MessageBuffer message;
            using (var writer = session.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(AtSpiNames.BusService, AtSpiNames.BusPath, AtSpiNames.PropertiesInterface, "Get", "ss");
                writer.WriteString(AtSpiNames.StatusInterface);
                writer.WriteString(property);
                message = writer.CreateMessage();
            }

            // The D-Bus library's own async call, awaited once here.
            return Signal.FromAsync(() => session.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadVariantValue().GetBool()));
        }).Catch<bool, DBusErrorReplyException>(ex =>
        {
            AppLog.Warning(ex, $"Could not read {property}.");
            return Signal.Return(false);
        });
}
