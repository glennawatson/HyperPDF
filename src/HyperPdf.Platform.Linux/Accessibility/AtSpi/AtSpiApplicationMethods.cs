// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Answers the methods and properties of <c>org.a11y.atspi.Application</c>, which only the root element has.</summary>
internal static class AtSpiApplicationMethods
{
    /// <summary>The version of the app, read once from the assembly.</summary>
    private static readonly string AppVersion = typeof(AtSpiApplicationMethods).Assembly.GetName().Version?.ToString() ?? "0";

    /// <summary>Gets the methods by name.</summary>
    internal static Dictionary<string, AtSpiMethod> Methods { get; } = new(StringComparer.Ordinal)
    {
        ["GetLocale"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(string.Empty),
        ["GetApplicationBusAddress"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(string.Empty),
    };

    /// <summary>Gets the properties by name.</summary>
    internal static Dictionary<string, AtSpiProperty> Properties { get; } = new(StringComparer.Ordinal)
    {
        ["ToolkitName"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(AtSpiNames.ToolkitName),
        [AtSpiNames.VersionProperty] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(AppVersion),
        ["AtspiVersion"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(AtSpiNames.ProtocolVersion),
        ["Id"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantInt32(Environment.ProcessId),
    };
}
