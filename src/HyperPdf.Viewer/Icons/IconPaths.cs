// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Icons;

/// <summary>
/// The outlines of the icons: simple line drawings on a 24 by 24 grid, stroked with round ends. Each is one line of path data,
/// with the absolute commands M, L, C, A and Z, after a letter that names its tone.
/// </summary>
internal static class IconPaths
{
    /// <summary>The side of the grid every icon is drawn on.</summary>
    internal const float Grid = 24F;

    /// <summary>The weight of the stroke on that grid.</summary>
    internal const float Stroke = 1.92F;

    /// <summary>The byte that ends the outline of one icon.</summary>
    private const byte LineEnd = (byte)'\n';

    /// <summary>The bytes before the path data of a line: the tone letter and a space.</summary>
    private const int ToneLength = 2;

    /// <summary>The tone of every icon, at the number of the icon.</summary>
    private static readonly IconTone[] Tones = ReadTones();

    /// <summary>Gets every outline, one line each, in the order of <see cref="ChromeIcon"/> from its first icon.</summary>
    private static ReadOnlySpan<byte> Table =>
        """
        a M3,7 L3,19 L21,19 L21,9 L11,9 L9,7 Z
        n M7,8 L7,3 L17,3 L17,8 M7,17 L3,17 L3,8 L21,8 L21,17 L17,17 M7,14 L17,14 L17,21 L7,21 Z M17,11 L18,11
        n M3,5 L21,5 L21,19 L3,19 Z M9,5 L9,19
        n M15,5 L8,12 L15,19
        n M9,5 L16,12 L9,19
        n M5,15.5 L12,8.5 L19,15.5
        n M5,8.5 L12,15.5 L19,8.5
        n M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 Z M15,15 L21,21 M10,7 L10,13 M7,10 L13,10
        n M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 Z M15,15 L21,21 M7,10 L13,10
        n M7,8 L3,12 L7,16 M17,8 L21,12 L17,16 M3,12 L21,12
        n M6,3 L18,3 L18,21 L6,21 Z M9,8 L15,8 M9,12 L15,12 M9,16 L13,16
        e M4,13 A8,8 0 1 0 7,6 M4,3 L4,9 L10,9
        e M20,13 A8,8 0 1 1 17,6 M20,3 L20,9 L14,9
        n M3,5 L11,5 L11,19 L3,19 Z M13,5 L21,5 L21,19 L13,19 Z
        n M16,10 A6,6 0 1 1 4,10 A6,6 0 1 1 16,10 Z M14.5,14.5 L21,21
        n M20,14 A8,8 0 1 1 10,4 A6,6 0 0 0 20,14 Z
        p M4,6 L20,6 M4,12 L20,12 M4,18 L20,18
        r M7,7 L17,17 M17,7 L7,17
        a M12,5 L12,19 M5,12 L19,12
        n M6,3 L14,3 L19,8 L19,21 L6,21 Z M14,3 L14,8 L19,8
        n M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z
        n M4,6 L6,6 M9,6 L20,6 M7,12 L9,12 M12,12 L20,12 M7,18 L9,18 M12,18 L20,18
        n M3,18 L7.5,6 L12,18 M4.7,14 L10.3,14 M19,11 A3,3 0 1 0 19,17 L19,10
        n M3,7 L5,17 L7,10 L9,17 L11,7 M14,17 L21,17 M14,7 L14,17 M21,7 L21,17
        n M4,20 L9.5,8 L15,20 M5.9,16 L13.1,16 M13,5 L17,3
        n M4,9 C7,5 9,13 12,9 C15,5 17,13 20,9 M4,16 C7,12 9,20 12,16 C15,12 17,20 20,16
        e M6,3 L6,19 L10,15 L13,21 L15,20 L12,14 L18,14 Z
        e M4,21 L11,21 M14,4 L20,10 L11,19 L7,19 L5,17 L5,13 Z
        e M7,4 L7,11 A5,5 0 0 0 17,11 L17,4 M5,20 L19,20
        e M5,12 L19,12 M16,6.5 C15,4.8 13.6,4 12,4 C9.6,4 8,5.4 8,7.4 M8,17 C9,19 10.4,20 12,20 C14.6,20 16,18.6 16,16.6
        e M4,20 L8,19 L19,8 L16,5 L5,16 Z M14,7 L17,10
        e M6,20 C10,20 11,16 12,6 C13,16 14,20 18,20 Z
        e M4,21 L10,21 M8,17 L15,6 L19,9 L12,19 L9,19 Z
        e M5,4 L19,4 L19,15 L14,20 L5,20 Z M14,20 L14,15 L19,15
        e M3,13 L11,13 L11,21 L3,21 Z M17,3 A4,4 0 1 1 16.99,3 M13,11 L21,3 M21,3 L16,3 M21,3 L21,8
        e M9,3 L15,3 L14,11 L19,11 L19,16 L5,16 L5,11 L10,11 Z M4,20 L20,20
        e M5,7 L5,4 L19,4 L19,7 M12,4 L12,20 M9,20 L15,20
        e M3,16 C6,16 6,8 8,8 C10,8 8,16 11,16 C13,16 13,12 15,12 C16,12 16,16 18,16 L21,16 M3,20 L21,20
        e M9,6 L4,11 L9,16 M4,11 L14,11 A5,5 0 0 1 14,21 L11,21
        e M15,6 L20,11 L15,16 M20,11 L10,11 A5,5 0 0 0 10,21 L13,21
        e M4,6 L20,6 M4,11 L20,11 M4,11.5 L20,11.5 M4,16 L20,16 M4,16.5 L20,16.5 M4,17 L20,17
        e M3,19 L8,6 L13,19 M4.8,15 L11.2,15 M15,19 L18,12 L21,19 M15.8,17 L20.2,17
        n M4,5 L20,5 L14,12 L14,19 L10,17 L10,12 Z
        a M5,4 L16,4 L20,8 L20,20 L5,20 Z M8,4 L8,9 L15,9 L15,4 M8,20 L8,14 L17,14 L17,20
        e M5,4 L19,4 L19,20 L5,20 Z M8,9 L16,9 M8,13 L13,13 M8,17 C9.5,15 10.5,18 12,16
        n M5,4 L19,4 L19,15 L14,20 L5,20 Z M8,9 L16,9 M8,13 L13,13
        n M16,7 L8.5,14.5 A2,2 0 0 0 11.5,17.5 L18.5,10.5 A4,4 0 0 0 12.5,4.5 L5.5,11.5 A6,6 0 0 0 14,20 L20,14
        n M4,4 L20,4 L20,15 L4,15 Z M7,8 L17,8 M7,11 L12,11 M15,13 A3,3 0 1 0 15,19 A3,3 0 1 0 15,13 Z M13.5,18.5 L13,22 L15,21 L17,22 L16.5,18.5
        n M12,3 L21,8 L12,13 L3,8 Z M3,12 L12,17 L21,12 M3,16 L12,21 L21,16
        n M4,5 L20,5 L20,19 L4,19 Z M10,9 L16,12 L10,15 Z
        n M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,17 L20,17 M17,14 L17,20
        n M3,12 L11,4 L20,4 L20,13 L12,21 Z M16,8 L16,8.5
        a M5,12 L10,17 L19,7
        n M4,9 L8,9 L13,5 L13,19 L8,15 L4,15 Z M16,9 A4,4 0 0 1 16,15 M18.5,6 A8,8 0 0 1 18.5,18
        n M8,5 L19,12 L8,19 Z
        n M8,5 L8,19 M16,5 L16,19
        n M6,5 L6,19 M18,5 L10,12 L18,19
        n M18,5 L18,19 M6,5 L14,12 L6,19
        n M3,15 L15,3 L21,9 L9,21 Z M7,11 L9,13 M10,8 L13,11 M13,5 L15,7
        n M4,18 L9,6 L15,14 L20,5 M3,17 L5,19 M8,5 L10,7 M14,13 L16,15 M19,4 L21,6
        n M4,6 L14,4 L20,12 L12,20 L4,16 Z M8,9 L10,11 M11,8 L15,12 M8,13 L13,16
        n M6,6 L18,6 M6,10 L18,10 M6,14 L18,14 M6,18 L13,18
        e M4,6 L20,6 L20,18 L4,18 Z
        e M21,12 A9,6 0 1 1 3,12 A9,6 0 1 1 21,12 Z
        e M4,20 L20,4 M11,4 L20,4 L20,13
        e M4,20 L20,4
        n M6,6 L18,6 L18,18 L6,18 Z
        p M5,12 L5.5,12 M12,12 L12.5,12 M19,12 L19.5,12

        """u8;

    /// <summary>Gets the path data of an icon.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The path data, or nothing for no icon.</returns>
    internal static ReadOnlySpan<byte> Of(ChromeIcon icon)
    {
        var line = LineOf(icon);
        return line.Length > ToneLength ? line[ToneLength..] : default;
    }

    /// <summary>Gets the kind of action an icon stands for.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The tone.</returns>
    internal static IconTone ToneOf(ChromeIcon icon) => (uint)icon < (uint)Tones.Length ? Tones[(int)icon] : IconTone.Plain;

    /// <summary>Reads the tone of every icon from the table once, so a frame never searches it.</summary>
    /// <returns>The tones, at the number of each icon.</returns>
    private static IconTone[] ReadTones()
    {
        var tones = new IconTone[(int)ChromeIcon.More + 1];
        for (var i = 1; i < tones.Length; i++)
        {
            var line = LineOf((ChromeIcon)i);
            tones[i] = line.IsEmpty ? IconTone.Plain : ToneOf(line[0]);
        }

        return tones;
    }

    /// <summary>Reads the letter that names a tone.</summary>
    /// <param name="letter">The letter.</param>
    /// <returns>The tone.</returns>
    private static IconTone ToneOf(byte letter) => letter switch
    {
        (byte)'n' => IconTone.Navigation,
        (byte)'a' => IconTone.Add,
        (byte)'e' => IconTone.Edit,
        (byte)'r' => IconTone.Remove,
        _ => IconTone.Plain,
    };

    /// <summary>Finds the line of the table that belongs to an icon.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The line without its end, or nothing when the table has no such line.</returns>
    private static ReadOnlySpan<byte> LineOf(ChromeIcon icon)
    {
        var rest = Table;
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
}
