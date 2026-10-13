// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.Scripts;

/// <summary>
/// Builds the compact Unicode tables that text extraction needs from Unicode Character Database files: Bidi_Class
/// (UAX #9), Bidi_Mirroring_Glyph, decompositions of presentation forms and base letters of precomposed letters.
/// Only the Basic Multilingual Plane is covered.
/// </summary>
internal static class UnicodeTextTableBuilder
{
    /// <summary>The last code point of the Basic Multilingual Plane.</summary>
    private const int BmpEnd = 0xFFFF;

    /// <summary>The values per generated data line.</summary>
    private const int ValuesPerLine = 12;

    /// <summary>The marker of a default range in DerivedBidiClass.txt.</summary>
    private const string MissingMarker = "# @missing:";

    /// <summary>The C# type name of 16-bit table values.</summary>
    private const string UShort = "ushort";

    /// <summary>The separator between the ends of a code point range.</summary>
    private const string RangeSeparator = "..";

    /// <summary>The number of fields that UnicodeData.txt lines carry up to the decomposition.</summary>
    private const int DecompositionFields = 6;

    /// <summary>The index of the general category field of UnicodeData.txt.</summary>
    private const int CategoryField = 2;

    /// <summary>The index of the decomposition field of UnicodeData.txt.</summary>
    private const int DecompositionField = 5;

    /// <summary>The general category of non-spacing marks.</summary>
    private const string NonSpacingMark = "Mn";

    /// <summary>The Bidi_Class names in the order of the library's enum, each with its long alias.</summary>
    private static readonly string[] ClassNames =
    [
        "L|Left_To_Right",
        "R|Right_To_Left",
        "AL|Arabic_Letter",
        "EN|European_Number",
        "ES|European_Separator",
        "ET|European_Terminator",
        "AN|Arabic_Number",
        "CS|Common_Separator",
        "NSM|Nonspacing_Mark",
        "BN|Boundary_Neutral",
        "B|Paragraph_Separator",
        "S|Segment_Separator",
        "WS|White_Space",
        "ON|Other_Neutral",
        "LRE|Left_To_Right_Embedding",
        "LRO|Left_To_Right_Override",
        "RLE|Right_To_Left_Embedding",
        "RLO|Right_To_Left_Override",
        "PDF|Pop_Directional_Format",
        "LRI|Left_To_Right_Isolate",
        "RLI|Right_To_Left_Isolate",
        "FSI|First_Strong_Isolate",
        "PDI|Pop_Directional_Isolate",
    ];

    /// <summary>The blocks whose decomposable characters are presentation forms: alphabetic, Arabic A and Arabic B.</summary>
    private static readonly CodeRange[] PresentationBlocks =
    [
        new(0xFB00, 0xFB4F),
        new(0xFB50, 0xFDFF),
        new(0xFE70, 0xFEFF),
    ];

    /// <summary>Builds the generated source.</summary>
    /// <param name="version">The Unicode version.</param>
    /// <param name="derivedBidiClass">The text of DerivedBidiClass.txt.</param>
    /// <param name="bidiMirroring">The text of BidiMirroring.txt.</param>
    /// <param name="unicodeData">The text of UnicodeData.txt.</param>
    /// <returns>The source of the table class.</returns>
    internal static string Emit(string version, string derivedBidiClass, string bidiMirroring, string unicodeData)
    {
        var characters = ParseUnicodeData(unicodeData);
        var runs = BuildBidiRuns(derivedBidiClass);
        var mirrors = ParseMirroring(bidiMirroring);
        var presentation = BuildPresentation(characters);
        var fold = BuildFold(characters);
        var output = new StringBuilder();
        AppendHeader(output, version);
        AppendTable(output, "BidiStarts", UShort, "Gets the first code point of each run of characters with the same Bidi_Class.", runs.Keys);
        AppendTable(output, "BidiClasses", "byte", "Gets the Bidi_Class of each run, as a <see cref=\"TextBidiClass\"/> value.", runs.Values);
        AppendTable(output, "MirrorKeys", UShort, "Gets the characters that have a mirrored counterpart, in order.", mirrors.Keys);
        AppendTable(output, "MirrorValues", UShort, "Gets the mirrored counterpart of each entry of <see cref=\"MirrorKeys\"/>.", mirrors.Values);
        AppendTable(output, "PresentationKeys", UShort, "Gets the presentation-form characters that decompose, in order.", presentation.Keys);
        AppendTable(
            output,
            "PresentationOffsets",
            UShort,
            "Gets where each entry of <see cref=\"PresentationKeys\"/> starts in <see cref=\"PresentationValues\"/>, with a final end offset.",
            presentation.Offsets);
        AppendTable(output, "PresentationValues", UShort, "Gets the characters the presentation forms decompose into.", presentation.Values);
        AppendTable(output, "FoldKeys", UShort, "Gets the precomposed letters that have a base letter, in order.", fold.Keys);
        AppendTable(output, "FoldValues", UShort, "Gets the base letter of each entry of <see cref=\"FoldKeys\"/>.", fold.Values);
        _ = output.AppendLine("}");
        return output.ToString().ReplaceLineEndings("\n");
    }

    /// <summary>Reads the code points of UnicodeData.txt that have a category or a decomposition.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The characters by code point.</returns>
    private static Dictionary<int, UcdCharacter> ParseUnicodeData(string text)
    {
        var characters = new Dictionary<int, UcdCharacter>();
        foreach (var line in text.Split('\n'))
        {
            var fields = line.Split(';');
            if (fields.Length < DecompositionFields)
            {
                continue;
            }

            var code = int.Parse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (code <= BmpEnd)
            {
                characters[code] = ParseCharacter(fields);
            }
        }

        return characters;
    }

    /// <summary>Reads one UnicodeData.txt line.</summary>
    /// <param name="fields">The semicolon separated fields.</param>
    /// <returns>The character.</returns>
    private static UcdCharacter ParseCharacter(string[] fields)
    {
        var decomposition = fields[DecompositionField];
        var compatibility = decomposition.StartsWith('<');
        var list = compatibility ? decomposition[(decomposition.IndexOf('>', StringComparison.Ordinal) + 1)..] : decomposition;
        var parts = new List<int>();
        foreach (var part in list.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            parts.Add(int.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return new(fields[CategoryField], compatibility, [.. parts]);
    }

    /// <summary>Builds the Bidi_Class runs from the default ranges and the listed ranges.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The first code point and class of each run.</returns>
    private static Pair BuildBidiRuns(string text)
    {
        var classes = new byte[BmpEnd + 1];
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith(MissingMarker, StringComparison.Ordinal))
            {
                ApplyClass(classes, line[MissingMarker.Length..]);
            }
        }

        foreach (var line in lines)
        {
            var data = StripComment(line);
            if (data.Length > 0)
            {
                ApplyClass(classes, data);
            }
        }

        var starts = new List<int>();
        var values = new List<int>();
        for (var i = 0; i <= BmpEnd; i++)
        {
            if (i != 0 && classes[i] == classes[i - 1])
            {
                continue;
            }

            starts.Add(i);
            values.Add(classes[i]);
        }

        return new(starts, values);
    }

    /// <summary>Sets a range of classes from a "range; class" line.</summary>
    /// <param name="classes">The classes by code point.</param>
    /// <param name="line">The line without its comment.</param>
    private static void ApplyClass(byte[] classes, string line)
    {
        var parts = line.Split(';');
        var range = ParseRange(parts[0].Trim());
        var value = (byte)ClassIndex(parts[1].Trim());
        for (var i = range.Low; i <= Math.Min(range.High, BmpEnd); i++)
        {
            classes[i] = value;
        }
    }

    /// <summary>Gets the enum value of a Bidi_Class name.</summary>
    /// <param name="name">The short or long name.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The name is unknown.</exception>
    private static int ClassIndex(string name)
    {
        for (var i = 0; i < ClassNames.Length; i++)
        {
            var names = ClassNames[i].Split('|');
            if (names[0].Equals(name, StringComparison.Ordinal) || names[1].Equals(name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidDataException($"Unknown Bidi_Class {name}.");
    }

    /// <summary>Reads the mirrored pairs inside the Basic Multilingual Plane.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The pairs sorted by character.</returns>
    private static Pair ParseMirroring(string text)
    {
        var pairs = new SortedDictionary<int, int>();
        foreach (var line in text.Split('\n'))
        {
            var data = StripComment(line);
            if (data.Length == 0)
            {
                continue;
            }

            var parts = data.Split(';');
            var key = int.Parse(parts[0].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var value = int.Parse(parts[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (key <= BmpEnd && value <= BmpEnd)
            {
                pairs[key] = value;
            }
        }

        return new([.. pairs.Keys], [.. pairs.Values]);
    }

    /// <summary>Builds the decompositions of presentation forms.</summary>
    /// <param name="characters">The characters.</param>
    /// <returns>The keys, offsets and values.</returns>
    private static PresentationTable BuildPresentation(Dictionary<int, UcdCharacter> characters)
    {
        var keys = new List<int>();
        var offsets = new List<int> { 0 };
        var values = new List<int>();
        for (var code = 0; code <= BmpEnd; code++)
        {
            if (!IsPresentation(code) || !characters.TryGetValue(code, out var entry) || entry.Parts.Length == 0)
            {
                continue;
            }

            var full = Expand(characters, code, true);
            if (!Array.TrueForAll(full, static value => value <= BmpEnd))
            {
                continue;
            }

            keys.Add(code);
            values.AddRange(full);
            offsets.Add(values.Count);
        }

        return new(keys, offsets, values);
    }

    /// <summary>Determines whether a code point is in a presentation-form block.</summary>
    /// <param name="code">The code point.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPresentation(int code)
    {
        foreach (var block in PresentationBlocks)
        {
            if (code >= block.Low && code <= block.High)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the base letter of each precomposed letter whose canonical decomposition adds only marks.</summary>
    /// <param name="characters">The characters.</param>
    /// <returns>The keys and base letters.</returns>
    private static Pair BuildFold(Dictionary<int, UcdCharacter> characters)
    {
        var keys = new List<int>();
        var values = new List<int>();
        for (var code = 0; code <= BmpEnd; code++)
        {
            if (!characters.TryGetValue(code, out var entry) || entry.Compatibility || entry.Parts.Length == 0)
            {
                continue;
            }

            var full = Expand(characters, code, false);
            if (full.Length < 2 || full[0] > BmpEnd || IsMark(characters, full[0]) || !AllMarks(characters, full))
            {
                continue;
            }

            keys.Add(code);
            values.Add(full[0]);
        }

        return new(keys, values);
    }

    /// <summary>Determines whether every character after the first is a non-spacing mark.</summary>
    /// <param name="characters">The characters.</param>
    /// <param name="full">The decomposition.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool AllMarks(Dictionary<int, UcdCharacter> characters, int[] full)
    {
        for (var i = 1; i < full.Length; i++)
        {
            if (!IsMark(characters, full[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a code point is a non-spacing mark.</summary>
    /// <param name="characters">The characters.</param>
    /// <param name="code">The code point.</param>
    /// <returns><see langword="true"/> for category Mn.</returns>
    private static bool IsMark(Dictionary<int, UcdCharacter> characters, int code) =>
        characters.TryGetValue(code, out var entry) && entry.Category.Equals(NonSpacingMark, StringComparison.Ordinal);

    /// <summary>Expands a decomposition repeatedly.</summary>
    /// <param name="characters">The characters.</param>
    /// <param name="code">The code point.</param>
    /// <param name="compatibility">Whether compatibility mappings apply.</param>
    /// <returns>The final code points.</returns>
    private static int[] Expand(Dictionary<int, UcdCharacter> characters, int code, bool compatibility)
    {
        if (!characters.TryGetValue(code, out var entry) || entry.Parts.Length == 0 || (entry.Compatibility && !compatibility))
        {
            return [code];
        }

        var result = new List<int>();
        foreach (var part in entry.Parts)
        {
            result.AddRange(Expand(characters, part, compatibility));
        }

        return [.. result];
    }

    /// <summary>Removes a trailing comment and surrounding spaces.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The data part.</returns>
    private static string StripComment(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        return (hash >= 0 ? line[..hash] : line).Trim();
    }

    /// <summary>Parses "XXXX" or "XXXX..YYYY".</summary>
    /// <param name="text">The text.</param>
    /// <returns>The range.</returns>
    private static CodeRange ParseRange(string text)
    {
        var dots = text.IndexOf(RangeSeparator, StringComparison.Ordinal);
        var low = int.Parse(dots < 0 ? text : text[..dots], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var high = dots < 0 ? low : int.Parse(text[(dots + RangeSeparator.Length)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(low, high);
    }

    /// <summary>Writes the file header and class opening.</summary>
    /// <param name="output">The output.</param>
    /// <param name="version">The Unicode version.</param>
    private static void AppendHeader(StringBuilder output, string version)
    {
        _ = output.AppendLine("// Copyright (c) 2026 Glenn Watson. All rights reserved.");
        _ = output.AppendLine("// Glenn Watson licenses this file to you under the MIT license.");
        _ = output.AppendLine("// See the LICENSE file in the project root for full license information.");
        _ = output.AppendLine();
        _ = output.AppendLine("// <auto-generated/> by scripts/UnicodeTextTableGenerator.cs.");
        _ = output.AppendLine();
        _ = output.AppendLine("namespace HyperPdfLibrary.Text;");
        _ = output.AppendLine();
        _ = output.AppendLine("/// <summary>");
        _ = output.AppendLine(CultureInfo.InvariantCulture, $"/// Tables from the Unicode Character Database {version} (Unicode License v3): Bidi_Class, Bidi_Mirroring_Glyph,");
        _ = output.AppendLine("/// decompositions of presentation forms and base letters of precomposed letters, for the Basic Multilingual Plane.");
        _ = output.AppendLine("/// </summary>");
        _ = output.AppendLine("internal static class TextUnicodeData");
        _ = output.AppendLine("{");
        _ = output.AppendLine("    /// <summary>The Unicode version the tables come from.</summary>");
        _ = output.AppendLine(CultureInfo.InvariantCulture, $"    internal const string UnicodeVersion = \"{version}\";");
        _ = output.AppendLine();
    }

    /// <summary>Writes one table as a span property.</summary>
    /// <param name="output">The output.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The element type.</param>
    /// <param name="summary">The documentation.</param>
    /// <param name="values">The values.</param>
    private static void AppendTable(StringBuilder output, string name, string type, string summary, List<int> values)
    {
        _ = output.AppendLine(CultureInfo.InvariantCulture, $"    /// <summary>{summary}</summary>");
        _ = output.AppendLine(CultureInfo.InvariantCulture, $"    internal static ReadOnlySpan<{type}> {name} =>");
        _ = output.AppendLine("    [");
        for (var i = 0; i < values.Count; i += ValuesPerLine)
        {
            var count = Math.Min(ValuesPerLine, values.Count - i);
            var formatted = new string[count];
            for (var j = 0; j < count; j++)
            {
                formatted[j] = string.Create(CultureInfo.InvariantCulture, $"0x{values[i + j]:X4}");
            }

            _ = output.Append("        ").Append(string.Join(", ", formatted)).AppendLine(",");
        }

        _ = output.AppendLine("    ];");
        _ = output.AppendLine();
    }

    /// <summary>A range of code points.</summary>
    /// <param name="Low">The first code point.</param>
    /// <param name="High">The last code point.</param>
    private readonly record struct CodeRange(int Low, int High);

    /// <summary>One line of UnicodeData.txt.</summary>
    /// <param name="Category">The general category.</param>
    /// <param name="Compatibility">Whether the decomposition is a compatibility mapping.</param>
    /// <param name="Parts">The decomposition, empty for none.</param>
    private sealed record UcdCharacter(string Category, bool Compatibility, int[] Parts);

    /// <summary>Two parallel lists.</summary>
    /// <param name="Keys">The keys.</param>
    /// <param name="Values">The values.</param>
    private sealed record Pair(List<int> Keys, List<int> Values);

    /// <summary>The presentation-form table.</summary>
    /// <param name="Keys">The decomposable characters.</param>
    /// <param name="Offsets">Where each decomposition starts in the values.</param>
    /// <param name="Values">The decomposed characters.</param>
    private sealed record PresentationTable(List<int> Keys, List<int> Offsets, List<int> Values);
}
