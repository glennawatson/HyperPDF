// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Answers the methods of <c>org.a11y.atspi.Document</c>, which the pages element has.</summary>
internal static class AtSpiDocumentMethods
{
    /// <summary>Gets the methods by name.</summary>
    internal static Dictionary<string, AtSpiMethod> Methods { get; } = new(StringComparer.Ordinal)
    {
        ["GetLocale"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(string.Empty),
        ["GetAttributeValue"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(string.Empty),
        ["GetAttributes"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyStringMap(new Dictionary<string, string>(StringComparer.Ordinal)),
    };
}
