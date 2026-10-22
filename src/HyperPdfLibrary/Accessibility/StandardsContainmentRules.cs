// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Reads the parent-child rules of veraPDF's tagged PDF profiles (ISO 32000 Annex L and ISO/TS 32005 Table 5) and finds the pairs a
/// structure tree breaks. A rule is checked when <see cref="StructureContainment"/>, the table HyperPdf checks against, forbids
/// the same pair or limits it to one in the same way. A rule that applies only to a parent used as a grouping or non-grouping element
/// is not checked, because the table does not know how a parent is used.
/// </summary>
internal static class StandardsContainmentRules
{
    /// <summary>The words before the child in a rule that forbids a pair, in ISO/TS 32005 Table 5.</summary>
    private const string TableForbids = " shall not contain ";

    /// <summary>The words before the child in a rule that limits a pair to one, in ISO/TS 32005 Table 5.</summary>
    private const string TableAtMostOne = " shall contain at most one ";

    /// <summary>The words after the parent in a rule that forbids a pair, in Annex L.</summary>
    private const string AnnexForbids = " elem should not contain ";

    /// <summary>The words after the parent in a rule that limits a pair to one, in Annex L.</summary>
    private const string AnnexAtMostOne = " elem should contain zero or one ";

    /// <summary>The word after the child in an Annex L rule.</summary>
    private const string AnnexKid = " kid";

    /// <summary>The structure type that holds the document's top-level elements.</summary>
    private const string TreeRoot = "StructTreeRoot";

    /// <summary>Reads a rule's text as a parent-child rule.</summary>
    /// <param name="description">The rule's description.</param>
    /// <param name="rule">Receives the rule.</param>
    /// <returns><see langword="true"/> when the text is a parent-child rule.</returns>
    internal static bool TryParse(string description, out StandardsContainmentRule rule)
    {
        rule = default;
        return TryParseTable(description, TableForbids, StandardsContainmentKind.Forbidden, out rule)
            || TryParseTable(description, TableAtMostOne, StandardsContainmentKind.AtMostOnce, out rule)
            || TryParseAnnex(description, AnnexForbids, StandardsContainmentKind.Forbidden, out rule)
            || TryParseAnnex(description, AnnexAtMostOne, StandardsContainmentKind.AtMostOnce, out rule);
    }

    /// <summary>Determines whether HyperPdf's table decides a rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when a document that breaks the rule is always reported.</returns>
    internal static bool IsChecked(in StandardsContainmentRule rule)
    {
        if (rule.Conditional || rule.Parent == TreeRoot || !StructureContainment.Knows(rule.Parent))
        {
            return false;
        }

        return rule.Kind == StandardsContainmentKind.Forbidden
            ? !StructureContainment.Allows(rule.Parent, rule.Child)
            : StructureContainment.AtMostOnce(rule.Parent, rule.Child) != OnceChild.None;
    }

    /// <summary>Finds the parent-child pairs a tree breaks.</summary>
    /// <param name="tree">The structure tree, or <see langword="null"/> when there is none.</param>
    /// <param name="violations">Receives <see langword="true"/> at the number of each pair that is broken; it holds one flag for each pair the rules name.</param>
    /// <param name="annexL">
    /// Whether to read the tree as the Annex L profiles do: they look through the grouping types <c>Part</c> and <c>Div</c> to
    /// their children and never fault those two types themselves, where Table 5 faults them like any other.
    /// </param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Collect(PdfStructureTree? tree, Span<bool> violations, bool annexL, CancellationToken cancellationToken)
    {
        if (tree is null || tree.ElementCount == 0)
        {
            return;
        }

        var stack = ArrayPool<PdfStructureElement>.Shared.Rent(tree.ElementCount + 1);
        var holders = annexL ? ArrayPool<PdfStructureElement>.Shared.Rent(tree.ElementCount + 1) : [];
        try
        {
            // veraPDF applies the pair rules to a tree of PDF 1.7 elements as well, so the exemption the accessibility report gives it is not made here.
            var count = Seed(tree, stack);
            while (count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count--;
                var element = stack[count];
                CollectElement(element, violations, annexL, holders);
                count = PushKids(element, stack, count);
            }
        }
        finally
        {
            ArrayPool<PdfStructureElement>.Shared.Return(stack, clearArray: true);
            if (annexL)
            {
                ArrayPool<PdfStructureElement>.Shared.Return(holders, clearArray: true);
            }
        }
    }

    /// <summary>Reads a rule written in the style of ISO/TS 32005 Table 5, such as <c>&lt;H&gt; shall not contain &lt;Part&gt;</c>.</summary>
    /// <param name="description">The rule's description.</param>
    /// <param name="words">The words between the parent and the child.</param>
    /// <param name="kind">The kind of rule the words state.</param>
    /// <param name="rule">Receives the rule.</param>
    /// <returns><see langword="true"/> when the text has that form.</returns>
    private static bool TryParseTable(string description, string words, StandardsContainmentKind kind, out StandardsContainmentRule rule)
    {
        rule = default;
        var index = description.IndexOf(words, StringComparison.Ordinal);
        if (index <= 0)
        {
            return false;
        }

        var subject = description[..index];
        var comma = subject.IndexOf(',', StringComparison.Ordinal);
        var parent = Unwrap(comma < 0 ? subject : subject[..comma]);
        var child = Unwrap(description[(index + words.Length)..]);
        if (!IsElementType(parent) || !IsElementType(child))
        {
            return false;
        }

        rule = new(kind, parent, child, comma >= 0);
        return true;
    }

    /// <summary>Reads a rule written in the style of Annex L, such as <c>RB elem should not contain Part kid</c>.</summary>
    /// <param name="description">The rule's description.</param>
    /// <param name="words">The words between the parent and the child.</param>
    /// <param name="kind">The kind of rule the words state.</param>
    /// <param name="rule">Receives the rule.</param>
    /// <returns><see langword="true"/> when the text has that form.</returns>
    private static bool TryParseAnnex(string description, string words, StandardsContainmentKind kind, out StandardsContainmentRule rule)
    {
        rule = default;
        var index = description.IndexOf(words, StringComparison.Ordinal);
        if (index <= 0 || !description.EndsWith(AnnexKid, StringComparison.Ordinal))
        {
            return false;
        }

        var child = description[(index + words.Length)..^AnnexKid.Length];
        if (!IsElementType(child) || !IsElementType(description[..index]))
        {
            return false;
        }

        rule = new(kind, description[..index], child, false);
        return true;
    }

    /// <summary>Determines whether Annex L looks through a type to its children.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for <c>Part</c> and <c>Div</c>.</returns>
    private static bool IsTransparent(string type) => type is "Part" or "Div";

    /// <summary>Determines whether text names a structure element type: a rule that talks of content items or artifacts is not a parent-child rule of element types.</summary>
    /// <param name="name">The text.</param>
    /// <returns><see langword="true"/> when it is a single word that is not <c>Artifact</c>.</returns>
    private static bool IsElementType(string name) => name.Length > 0 && !name.Contains(' ', StringComparison.Ordinal) && name != "Artifact";

    /// <summary>Removes the angle brackets around a type name.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Unwrap(string text) => text.Trim().Trim('<', '>');

    /// <summary>Records the pairs one element breaks with its children.</summary>
    /// <param name="element">The element.</param>
    /// <param name="violations">Receives the flags.</param>
    /// <param name="annexL">Whether to look through <c>Part</c> and <c>Div</c> children to their own children.</param>
    /// <param name="holders">Working storage for the transparent children still to read; it holds as many elements as the tree has.</param>
    private static void CollectElement(PdfStructureElement element, Span<bool> violations, bool annexL, PdfStructureElement[] holders)
    {
        var parent = StructureContainment.NameOf(element);
        var seen = OnceChild.None;
        var pending = 0;
        var holder = element;
        var more = true;
        while (more)
        {
            foreach (var kid in holder.KidSpan)
            {
                if (kid.Element is not { } child || StructureContainment.NameOf(child) is not { } childName)
                {
                    continue;
                }

                if (annexL && IsTransparent(childName))
                {
                    holders[pending] = child;
                    pending++;
                    continue;
                }

                Judge(parent, childName, violations, ref seen);
            }

            more = pending > 0;
            if (!more)
            {
                continue;
            }

            pending--;
            holder = holders[pending];
        }
    }

    /// <summary>Records the pair a parent and a child break, if any.</summary>
    /// <param name="parent">The parent's table spelling, or <see langword="null"/>.</param>
    /// <param name="child">The child's table spelling.</param>
    /// <param name="violations">Receives the flags.</param>
    /// <param name="seen">The limited children the parent already holds.</param>
    private static void Judge(string? parent, string child, Span<bool> violations, ref OnceChild seen)
    {
        if (parent is not null && StructureContainment.Knows(parent) && !StructureContainment.Allows(parent, child))
        {
            Flag(new(StandardsContainmentKind.Forbidden, parent, child), violations);
        }

        var flag = StructureContainment.AtMostOnce(parent, child);
        if (flag != OnceChild.None && (seen & flag) != 0 && parent is not null)
        {
            Flag(new(StandardsContainmentKind.AtMostOnce, parent, child), violations);
        }

        seen |= flag;
    }

    /// <summary>Marks a pair as broken when some rule names it.</summary>
    /// <param name="key">The pair.</param>
    /// <param name="violations">Receives the flag.</param>
    private static void Flag(in StandardsPairKey key, Span<bool> violations)
    {
        if (StandardsRulePlans.TryGetPairId(in key, out var id))
        {
            violations[id] = true;
        }
    }

    /// <summary>Puts the top-level elements on the stack.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="stack">The stack.</param>
    /// <returns>How many elements it holds.</returns>
    private static int Seed(PdfStructureTree tree, PdfStructureElement[] stack)
    {
        var roots = tree.Roots;
        for (var index = 0; index < roots.Count; index++)
        {
            stack[index] = roots[index];
        }

        return roots.Count;
    }

    /// <summary>Puts an element's child elements on the stack.</summary>
    /// <param name="element">The element.</param>
    /// <param name="stack">The stack.</param>
    /// <param name="count">How many elements the stack holds.</param>
    /// <returns>How many it holds afterwards.</returns>
    private static int PushKids(PdfStructureElement element, PdfStructureElement[] stack, int count)
    {
        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is not { } child)
            {
                continue;
            }

            stack[count] = child;
            count++;
        }

        return count;
    }
}
