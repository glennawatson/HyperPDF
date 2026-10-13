// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Checks the link and count rules of an outline (ISO 32000-2 12.3.3) and describes it as text.</summary>
internal static class OutlineInvariants
{
    /// <summary>The spaces each level is indented by in a description.</summary>
    private const int IndentWidth = 2;

    /// <summary>The deepest level checked.</summary>
    private const int MaxDepth = 64;

    /// <summary>Checks an outline.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The problems; empty when the outline is well formed.</returns>
    internal static List<string> Check(PdfDocument document)
    {
        var problems = new List<string>();
        var root = document.Catalog.GetDictionary(KnownName.Outlines);
        if (root is null)
        {
            return problems;
        }

        var rootId = document.Catalog.GetRaw(KnownName.Outlines).AsReference();
        var total = Level(root, rootId, 0, problems);
        if (root.GetInteger(KnownName.Count) != total)
        {
            problems.Add($"The root count is {root.GetInteger(KnownName.Count)}; the entries that show are {total}.");
        }

        return problems;
    }

    /// <summary>Describes an outline as indented lines of title, open state and target page, for comparing before and after.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description.</returns>
    internal static string Describe(PdfDocument document)
    {
        var text = new StringBuilder();
        Describe(PdfDocumentNavigation.GetOutline(document), 0, text);
        return text.ToString();
    }

    /// <summary>Describes one level.</summary>
    /// <param name="items">The entries.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="text">The text being built.</param>
    private static void Describe(IReadOnlyList<PdfOutlineItem> items, int depth, StringBuilder text)
    {
        foreach (var item in items)
        {
            var page = item.Action.Value is GoToAction go ? go.Destination.PageIndex : -1;
            var state = item.IsOpen ? " (open) p" : " p";
            _ = text.Append(' ', depth * IndentWidth).Append(item.Title).Append(state).Append(page).Append('\n');
            Describe(item.Children, depth + 1, text);
        }
    }

    /// <summary>Checks the entries below a parent.</summary>
    /// <param name="parent">The parent's dictionary.</param>
    /// <param name="parentId">The parent's object.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="problems">The problems found.</param>
    /// <returns>The number of entries that show below the parent when it is open.</returns>
    private static int Level(PdfDictionary parent, PdfObjectId parentId, int depth, List<string> problems)
    {
        var total = 0;
        var previous = default(PdfObjectId);
        var id = parent.GetRaw(KnownName.First).AsReference();
        var first = id;
        while (id.IsValid && depth < MaxDepth)
        {
            var item = StoreReading.GetDictionary(parent.Owner!, id)!;
            CheckLinks(item, id, parentId, previous, problems);
            var below = Level(item, id, depth + 1, problems);
            var count = item.GetInteger(KnownName.Count);
            var expected = count > 0 ? below : -below;
            if (count != expected)
            {
                problems.Add($"Entry {id.Number} has count {count}; it has {below} entries below.");
            }

            total += 1 + Math.Max(0, expected);
            previous = id;
            id = item.GetRaw(KnownName.Next).AsReference();
        }

        if (previous.Number != parent.GetRaw(KnownName.Last).AsReference().Number)
        {
            problems.Add($"Entry {parentId.Number} has /Last {parent.GetRaw(KnownName.Last).AsReference().Number}; the last child is {previous.Number}.");
        }

        if (first.Number != 0 && previous.Number == 0)
        {
            problems.Add("The first child could not be read.");
        }

        return total;
    }

    /// <summary>Checks the links of one entry.</summary>
    /// <param name="item">The entry.</param>
    /// <param name="id">The entry's object.</param>
    /// <param name="parentId">The parent's object.</param>
    /// <param name="previous">The previous sibling, or an invalid id.</param>
    /// <param name="problems">The problems found.</param>
    private static void CheckLinks(PdfDictionary item, PdfObjectId id, PdfObjectId parentId, PdfObjectId previous, List<string> problems)
    {
        if (item.GetRaw(KnownName.Parent).AsReference().Number != parentId.Number)
        {
            problems.Add($"Entry {id.Number} has the wrong /Parent.");
        }

        if (item.GetRaw(KnownName.Prev).AsReference().Number != previous.Number)
        {
            problems.Add($"Entry {id.Number} has the wrong /Prev.");
        }
    }
}
