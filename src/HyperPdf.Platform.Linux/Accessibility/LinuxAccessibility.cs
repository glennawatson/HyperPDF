// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Platform.Linux.Accessibility.AtSpi;
using HyperPdf.Platform.Linux.DBus;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility;

/// <summary>
/// The Linux side of screen reader access: tells whether a screen reader or other assistive technology is switched on, and serves
/// the app's elements to it over AT-SPI2. It does not depend on X11 or Wayland.
/// </summary>
public static class LinuxAccessibility
{
    /// <summary>Checks whether assistive technologies are switched on for this session.</summary>
    /// <returns>Whether a screen reader or another assistive technology is enabled; <see langword="false"/> when the session has no accessibility service.</returns>
    public static async Task<bool> IsAssistiveTechnologyEnabledAsync()
    {
        using var session = await SessionBus.TryConnectAsync().ConfigureAwait(false);
        if (session is null)
        {
            return false;
        }

        var enabled = await ReadFlagAsync(session, AtSpiNames.IsEnabledProperty).ConfigureAwait(false);
        return enabled || await ReadFlagAsync(session, AtSpiNames.ScreenReaderEnabledProperty).ConfigureAwait(false);
    }

    /// <summary>Starts serving the app's elements to screen readers.</summary>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>The running server, to dispose when the app no longer serves elements, or <see langword="null"/> when there is no accessibility bus.</returns>
    public static async Task<IDisposable?> StartAsync(IAccessibleTree tree) => await AtSpiServer.TryStartAsync(tree).ConfigureAwait(false);

    /// <summary>Reads one of the accessibility service's switches.</summary>
    /// <param name="session">The session bus.</param>
    /// <param name="property">The switch.</param>
    /// <returns>Whether it is on.</returns>
    private static async Task<bool> ReadFlagAsync(DBusConnection session, string property)
    {
        MessageBuffer message;
        using (var writer = session.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(AtSpiNames.BusService, AtSpiNames.BusPath, AtSpiNames.PropertiesInterface, "Get", "ss");
            writer.WriteString(AtSpiNames.StatusInterface);
            writer.WriteString(property);
            message = writer.CreateMessage();
        }

        try
        {
            return await session.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadVariantValue().GetBool()).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException ex)
        {
            AppLog.Warning(ex, $"Could not read {property}.");
            return false;
        }
    }
}
