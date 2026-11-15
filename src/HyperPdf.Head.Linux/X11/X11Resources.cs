// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;

namespace HyperPdf.Head.Linux.X11;

/// <summary>Reads settings from the X resource text desktops keep on the root window, which they rewrite when the user changes a setting.</summary>
internal static class X11Resources
{
    /// <summary>Gets the name of the resource that holds the dots per inch of the display.</summary>
    private static ReadOnlySpan<byte> DpiName => "Xft.dpi:"u8;

    /// <summary>Finds the dots per inch the desktop set.</summary>
    /// <param name="resources">The resource text: one setting a line, as <c>name:</c>, white space and the value.</param>
    /// <returns>The dots per inch, or 0 when the text sets none.</returns>
    internal static double ReadDpi(ReadOnlySpan<byte> resources)
    {
        while (!resources.IsEmpty)
        {
            var end = resources.IndexOf((byte)'\n');
            var line = end < 0 ? resources : resources[..end];
            resources = end < 0 ? default : resources[(end + 1)..];
            if (!line.StartsWith(DpiName))
            {
                continue;
            }

            var value = line[DpiName.Length..].Trim(" \t\r"u8);
            return Utf8Parser.TryParse(value, out double dpi, out _) ? dpi : 0;
        }

        return 0;
    }
}
