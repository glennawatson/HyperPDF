// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Viewer.Icons;

/// <summary>
/// Looks the icons up in the table the build makes from the SVG files in <c>Icons/Svg</c>: simple line drawings on a 24 by 24 grid,
/// stroked with round ends. To add or change an icon, add or edit its SVG file; the file's name is the member of <see cref="ChromeIcon"/>.
/// </summary>
internal static class IconPaths
{
    /// <summary>The side of the grid every icon is drawn on.</summary>
    internal const float Grid = 24F;

    /// <summary>The weight of the stroke on that grid.</summary>
    internal const float Stroke = 1.92F;

    /// <summary>The byte that ends the outline of one icon.</summary>
    private const byte LineEnd = (byte)'\n';

    /// <summary>Gets the path data of an icon, with the absolute commands M, L, C, A and Z.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The path data, or nothing for no icon.</returns>
    internal static ReadOnlySpan<byte> Of(ChromeIcon icon)
    {
        var rest = IconTable.Paths;
        for (var skip = (int)icon - 1; skip >= 0; skip--)
        {
            var end = rest.IndexOf(LineEnd);
            if (end < 0)
            {
                return default;
            }

            if (skip == 0)
            {
                return rest[..end];
            }

            rest = rest[(end + 1)..];
        }

        return default;
    }

    /// <summary>Gets the kind of action an icon stands for.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The tone.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IconTone ToneOf(ChromeIcon icon) => (uint)icon < (uint)IconTable.Tones.Length ? (IconTone)IconTable.Tones[(int)icon] : IconTone.Plain;
}
