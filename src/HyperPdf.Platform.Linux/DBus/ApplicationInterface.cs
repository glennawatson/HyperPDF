// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>Constants and message helpers for the <c>org.freedesktop.Application</c> interface.</summary>
internal static class ApplicationInterface
{
    /// <summary>The interface name.</summary>
    internal const string Name = "org.freedesktop.Application";

    /// <summary>The <c>Open</c> method.</summary>
    internal const string OpenMethod = "Open";

    /// <summary>The <c>Activate</c> method.</summary>
    internal const string ActivateMethod = "Activate";

    /// <summary>The <c>ActivateAction</c> method.</summary>
    internal const string ActivateActionMethod = "ActivateAction";

    /// <summary>The platform data key carrying an XDG activation token (Wayland).</summary>
    internal const string ActivationTokenKey = "activation-token";

    /// <summary>The platform data key carrying a startup notification id (X11).</summary>
    internal const string StartupIdKey = "desktop-startup-id";

    /// <summary>Reads the activation token from platform data.</summary>
    /// <param name="platformData">The platform data.</param>
    /// <returns>The token, if any.</returns>
    internal static string? GetActivationToken(Dictionary<string, VariantValue> platformData)
    {
        foreach (var key in (ReadOnlySpan<string>)[ActivationTokenKey, StartupIdKey])
        {
            if (platformData.TryGetValue(key, out var value) && value.Type == VariantValueType.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    /// <summary>Creates platform data carrying an activation token.</summary>
    /// <param name="activationToken">The token.</param>
    /// <returns>The platform data.</returns>
    internal static Dictionary<string, VariantValue> CreatePlatformData(string? activationToken)
    {
        var data = new Dictionary<string, VariantValue>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(activationToken))
        {
            data[ActivationTokenKey] = VariantValue.String(activationToken);
            data[StartupIdKey] = VariantValue.String(activationToken);
        }

        return data;
    }
}
