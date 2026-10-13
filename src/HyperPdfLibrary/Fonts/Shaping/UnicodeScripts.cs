// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Finds the script of a character (Unicode Standard Annex #24) for the scripts the shaper tells apart.</summary>
internal static class UnicodeScripts
{
    /// <summary>The scripts, by index.</summary>
    private static readonly ShapingScript[] Scripts =
    [
        Make("Latn", ShapingKind.Default, "latn"),
        Make("Grek", ShapingKind.Default, "grek"),
        Make("Cyrl", ShapingKind.Default, "cyrl"),
        Make("Armn", ShapingKind.Default, "armn"),
        Make("Hebr", ShapingKind.Default, "hebr"),
        Make("Arab", ShapingKind.Arabic, "arab"),
        Make("Syrc", ShapingKind.Default, "syrc"),
        Make("Thaa", ShapingKind.Default, "thaa"),
        Make("Nkoo", ShapingKind.Default, "nko"),
        Make("Deva", ShapingKind.Indic, "dev2", "deva"),
        Make("Beng", ShapingKind.Indic, "bng2", "beng"),
        Make("Guru", ShapingKind.Indic, "gur2", "guru"),
        Make("Gujr", ShapingKind.Indic, "gjr2", "gujr"),
        Make("Orya", ShapingKind.Indic, "ory2", "orya"),
        Make("Taml", ShapingKind.Indic, "tml2", "taml"),
        Make("Telu", ShapingKind.Indic, "tel2", "telu"),
        Make("Knda", ShapingKind.Indic, "knd2", "knda"),
        Make("Mlym", ShapingKind.Indic, "mlm2", "mlym"),
        Make("Sinh", ShapingKind.Default, "sinh"),
        Make("Thai", ShapingKind.Default, "thai"),
        Make("Laoo", ShapingKind.Default, "lao"),
        Make("Tibt", ShapingKind.Default, "tibt"),
        Make("Mymr", ShapingKind.Default, "mymr"),
        Make("Geor", ShapingKind.Default, "geor"),
        Make("Hang", ShapingKind.Default, "hang"),
        Make("Ethi", ShapingKind.Default, "ethi"),
        Make("Khmr", ShapingKind.Default, "khmr"),
        Make("Mong", ShapingKind.Default, "mong"),
        Make("Hira", ShapingKind.Default, "kana"),
        Make("Kana", ShapingKind.Default, "kana"),
        Make("Bopo", ShapingKind.Default, "bopo"),
        Make("Hani", ShapingKind.Default, "hani"),
    ];

    /// <summary>The ranges of characters in each script, in ascending order, as the start, end and index in <see cref="Scripts"/>.</summary>
    private static readonly ScriptRange[] Ranges =
    [
        new(0x0041, 0x005A, 0), new(0x0061, 0x007A, 0), new(0x00AA, 0x00AA, 0), new(0x00BA, 0x00BA, 0),
        new(0x00C0, 0x00D6, 0), new(0x00D8, 0x00F6, 0), new(0x00F8, 0x02B8, 0), new(0x0370, 0x03FF, 1),
        new(0x0400, 0x052F, 2), new(0x0531, 0x058F, 3), new(0x0591, 0x05F4, 4), new(0x0600, 0x06FF, 5),
        new(0x0700, 0x074F, 6), new(0x0750, 0x077F, 5), new(0x0780, 0x07BF, 7), new(0x07C0, 0x07FF, 8),
        new(0x08A0, 0x08FF, 5), new(0x0900, 0x097F, 9), new(0x0980, 0x09FF, 10), new(0x0A00, 0x0A7F, 11),
        new(0x0A80, 0x0AFF, 12), new(0x0B00, 0x0B7F, 13), new(0x0B80, 0x0BFF, 14), new(0x0C00, 0x0C7F, 15),
        new(0x0C80, 0x0CFF, 16), new(0x0D00, 0x0D7F, 17), new(0x0D80, 0x0DFF, 18), new(0x0E00, 0x0E7F, 19),
        new(0x0E80, 0x0EFF, 20), new(0x0F00, 0x0FFF, 21), new(0x1000, 0x109F, 22), new(0x10A0, 0x10FF, 23),
        new(0x1100, 0x11FF, 24), new(0x1200, 0x139F, 25), new(0x1780, 0x17FF, 26), new(0x1800, 0x18AF, 27),
        new(0x1C80, 0x1C8F, 2), new(0x1C90, 0x1CBF, 23), new(0x1E00, 0x1EFF, 0), new(0x1F00, 0x1FFF, 1),
        new(0x2C60, 0x2C7F, 0), new(0x2DE0, 0x2DFF, 2), new(0x3040, 0x309F, 28), new(0x30A0, 0x30FF, 29),
        new(0x3100, 0x312F, 30), new(0x3130, 0x318F, 24), new(0x3400, 0x4DBF, 31), new(0x4E00, 0x9FFF, 31),
        new(0xA640, 0xA69F, 2), new(0xA720, 0xA7FF, 0), new(0xA8E0, 0xA8FF, 9), new(0xAC00, 0xD7AF, 24),
        new(0xF900, 0xFAFF, 31), new(0xFB00, 0xFB06, 0), new(0xFB1D, 0xFB4F, 4), new(0xFB50, 0xFDFF, 5),
        new(0xFE70, 0xFEFF, 5), new(0xFF21, 0xFF3A, 0), new(0xFF41, 0xFF5A, 0), new(0x20000, 0x2FA1F, 31),
    ];

    /// <summary>Finds the script of a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The script; <see cref="ShapingScript.Common"/> when the character belongs to none, as digits, punctuation, spaces and combining marks do.</returns>
    internal static ShapingScript Of(int codePoint)
    {
        var low = 0;
        var high = Ranges.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var range = Ranges[middle];
            if (codePoint < range.Start)
            {
                high = middle - 1;
            }
            else if (codePoint > range.End)
            {
                low = middle + 1;
            }
            else
            {
                return Scripts[range.Script];
            }
        }

        return ShapingScript.Common;
    }

    /// <summary>Finds a script by its ISO 15924 code.</summary>
    /// <param name="name">The code, such as <c>Arab</c> or <c>deva</c>.</param>
    /// <returns>The script; a default-model script with the code as its OpenType tag when the code is not one the shaper knows.</returns>
    internal static ShapingScript Named(string name)
    {
        foreach (var script in Scripts)
        {
            if (script.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return script;
            }
        }

        return new(name, ShapingKind.Default, [OtTag.Of(name.ToLowerInvariant())]);
    }

    /// <summary>Makes a script entry.</summary>
    /// <param name="name">The ISO 15924 code.</param>
    /// <param name="kind">The shaping model.</param>
    /// <param name="tags">The OpenType script tags, best first.</param>
    /// <returns>The script.</returns>
    private static ShapingScript Make(string name, ShapingKind kind, params string[] tags)
    {
        var numbers = new uint[tags.Length];
        for (var i = 0; i < numbers.Length; i++)
        {
            numbers[i] = OtTag.Of(tags[i]);
        }

        return new(name, kind, numbers);
    }

    /// <summary>The characters from one value to another that belong to a script.</summary>
    /// <param name="Start">The first character.</param>
    /// <param name="End">The last character.</param>
    /// <param name="Script">The index of the script.</param>
    private readonly record struct ScriptRange(int Start, int End, int Script);
}
