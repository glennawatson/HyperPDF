// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace HyperPdf.Scripts;

/// <summary>Builds the standards rule table from veraPDF's PDF/UA, tagged PDF, WTPDF and WCAG validation profiles.</summary>
internal static class StandardsRuleTableBuilder
{
    /// <summary>The profile files, in the bit order of <c>PdfStandardsProfile</c>.</summary>
    private static readonly string[] ProfileFiles =
    [
        "PDFUA-1.xml",
        "PDFUA-2.xml",
        "ISO-32000-1-Tagged.xml",
        "ISO-32000-2-Tagged.xml",
        "ISO-32005-Tagged.xml",
        "WTPDF-1-0-Reuse.xml",
        "WTPDF-1-0-Accessibility.xml",
        "WCAG-2-2.xml",
        "WCAG-2-2-Dev.xml",
        "WCAG-2-2-Machine.xml",
        "WCAG-2-2-Machine-PDF20.xml",
        "WCAG-2-2-Complete.xml",
        "WCAG-2-2-Complete-PDF20.xml",
    ];

    /// <summary>Reads every profile and joins identical rules.</summary>
    /// <param name="folder">The folder that holds the profile files.</param>
    /// <returns>The table, one line per distinct rule: profile bits in hex, specification, clause, test, tags, description.</returns>
    internal static string Build(string folder)
    {
        var rules = new Dictionary<string, RuleRow>(StringComparer.Ordinal);
        for (var index = 0; index < ProfileFiles.Length; index++)
        {
            foreach (var rule in Read(Path.Combine(folder, ProfileFiles[index])))
            {
                ref var held = ref CollectionsMarshal.GetValueRefOrAddDefault(rules, $"{rule.Specification}\t{rule.Clause}\t{rule.Test}\t{rule.Description}", out var exists);
                if (!exists)
                {
                    held = rule;
                }

                held!.Profiles |= 1 << index;
                held.Tags = MergeTags(held.Tags, rule.Tags);
            }
        }

        var ordered = new List<RuleRow>(rules.Values);
        ordered.Sort(Compare);
        var text = new StringBuilder();
        foreach (var rule in ordered)
        {
            _ = text.Append(rule.Profiles.ToString("x", CultureInfo.InvariantCulture)).Append('\t')
                .Append(rule.Specification).Append('\t')
                .Append(rule.Clause).Append('\t')
                .Append(rule.Test).Append('\t')
                .Append(rule.Tags).Append('\t')
                .Append(rule.Description).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Reads the rules of one profile.</summary>
    /// <param name="path">The profile file.</param>
    /// <returns>Its rules.</returns>
    private static List<RuleRow> Read(string path)
    {
        var found = new List<RuleRow>();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        RuleRow? current = null;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            current = ReadElement(reader, current, found);
        }

        return found;
    }

    /// <summary>Joins the tags two profiles give the same rule, so the rule is tagged the same way in every profile that holds it.</summary>
    /// <param name="held">The tags kept so far.</param>
    /// <param name="added">The tags another profile gives.</param>
    /// <returns>The tags of both, in sorted order.</returns>
    private static string MergeTags(string held, string added)
    {
        if (held == added)
        {
            return held;
        }

        var tags = new SortedSet<string>(held.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        tags.UnionWith(added.Split(',', StringSplitOptions.RemoveEmptyEntries));
        return string.Join(',', tags);
    }

    /// <summary>Handles one element of a profile.</summary>
    /// <param name="reader">The reader, positioned on the element.</param>
    /// <param name="current">The rule being read, or <see langword="null"/> outside a rule.</param>
    /// <param name="found">Receives a rule once its description is read.</param>
    /// <returns>The rule being read after the element.</returns>
    private static RuleRow? ReadElement(XmlReader reader, RuleRow? current, List<RuleRow> found)
    {
        if (reader.LocalName == "rule")
        {
            return new() { Tags = reader.GetAttribute("tags") ?? string.Empty };
        }

        if (current is null)
        {
            return null;
        }

        if (reader.LocalName == "id")
        {
            current.Specification = reader.GetAttribute("specification") ?? string.Empty;
            current.Clause = reader.GetAttribute("clause") ?? string.Empty;
            current.Test = reader.GetAttribute("testNumber") ?? string.Empty;
        }
        else if (reader.LocalName == "description" && current.Description.Length == 0)
        {
            current.Description = string.Join(' ', reader.ReadElementContentAsString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            found.Add(current);
        }

        return current;
    }

    /// <summary>Orders rules by specification, then clause number, then test number.</summary>
    /// <param name="x">The first rule.</param>
    /// <param name="y">The second rule.</param>
    /// <returns>The order.</returns>
    private static int Compare(RuleRow x, RuleRow y)
    {
        var order = string.CompareOrdinal(x.Specification, y.Specification);
        if (order != 0)
        {
            return order;
        }

        order = CompareClause(x.Clause, y.Clause);
        if (order != 0)
        {
            return order;
        }

        order = CompareNumber(x.Test, y.Test);
        return order != 0 ? order : string.CompareOrdinal(x.Description, y.Description);
    }

    /// <summary>Orders dotted clause numbers part by part.</summary>
    /// <param name="x">The first clause.</param>
    /// <param name="y">The second clause.</param>
    /// <returns>The order.</returns>
    private static int CompareClause(string x, string y)
    {
        var left = x.Split('.');
        var right = y.Split('.');
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var order = CompareNumber(left[index], right[index]);
            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    /// <summary>Orders two parts as numbers when both are numbers, otherwise as text.</summary>
    /// <param name="x">The first part.</param>
    /// <param name="y">The second part.</param>
    /// <returns>The order.</returns>
    private static int CompareNumber(string x, string y) =>
        int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var a) && int.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out var b)
            ? a.CompareTo(b)
            : string.CompareOrdinal(x, y);

    /// <summary>One rule and the profiles that hold it.</summary>
    private sealed class RuleRow
    {
        /// <summary>Gets or sets the specification the rule's clause belongs to.</summary>
        internal string Specification { get; set; } = string.Empty;

        /// <summary>Gets or sets the clause.</summary>
        internal string Clause { get; set; } = string.Empty;

        /// <summary>Gets or sets the test number.</summary>
        internal string Test { get; set; } = string.Empty;

        /// <summary>Gets or sets the profile's tags for the rule.</summary>
        internal string Tags { get; set; } = string.Empty;

        /// <summary>Gets or sets the rule's text.</summary>
        internal string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the bit of each profile that holds the rule.</summary>
        internal int Profiles { get; set; }
    }
}
