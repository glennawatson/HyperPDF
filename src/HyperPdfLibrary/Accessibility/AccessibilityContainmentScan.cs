// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Reports tags that sit inside a parent the hierarchy table of ISO/TS 32005:2023 clause 7.2 does not allow. A tree that uses
/// only PDF 1.7 elements is exempt (clause 5.2), so the check runs only when some tag is in the PDF 2.0 namespace.
/// </summary>
internal static class AccessibilityContainmentScan
{
    /// <summary>Checks every tag against its parent's row of the table.</summary>
    /// <param name="tree">The structure tree, or <see langword="null"/> when there is none.</param>
    /// <param name="findings">Receives a finding for each tag in a place the table does not allow.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Run(PdfStructureTree? tree, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        if (tree is null || tree.ElementCount == 0)
        {
            return;
        }

        var stack = ArrayPool<PdfStructureElement>.Shared.Rent(tree.ElementCount + 1);
        try
        {
            if (UsesPdf20Namespace(tree, stack, cancellationToken))
            {
                Report(tree, stack, findings, cancellationToken);
            }
        }
        finally
        {
            ArrayPool<PdfStructureElement>.Shared.Return(stack, clearArray: true);
        }
    }

    /// <summary>Determines whether any tag is in the PDF 2.0 namespace.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="stack">Working storage for the elements still to visit.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool UsesPdf20Namespace(PdfStructureTree tree, PdfStructureElement[] stack, CancellationToken cancellationToken)
    {
        var count = Seed(tree, stack);
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count--;
            var element = stack[count];
            if (string.Equals(element.Namespace, PdfStructureTypes.Pdf20Namespace, StringComparison.Ordinal))
            {
                return true;
            }

            count = PushKids(element, stack, count);
        }

        return false;
    }

    /// <summary>Checks every tag against its parent.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="stack">Working storage for the elements still to visit.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    private static void Report(PdfStructureTree tree, PdfStructureElement[] stack, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        var count = Seed(tree, stack);
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count--;
            var element = stack[count];
            Check(element, findings);
            count = PushKids(element, stack, count);
        }
    }

    /// <summary>Puts the top-level elements on the stack.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="stack">The stack.</param>
    /// <returns>How many elements it holds.</returns>
    private static int Seed(PdfStructureTree tree, PdfStructureElement[] stack)
    {
        var count = 0;
        foreach (var root in tree.Roots)
        {
            stack[count] = root;
            count++;
        }

        return count;
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

    /// <summary>
    /// Reports a tag that is in the wrong namespace for its type, in a file that mixes namespaces (ISO/TS 32005 clauses 5.3 and 5.4): a type both
    /// namespaces define belongs in the PDF 2.0 namespace, and a type only that namespace defines has to say so. Only a tag spelled with a standard
    /// type name is judged; a custom type that a role map sends to a standard one is not.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckNamespaceUse(PdfStructureElement element, AccessibilityFindings findings)
    {
        if (element.Namespace is not (null or PdfStructureTypes.Pdf17Uri) || !string.Equals(element.RawType, element.MappedType, StringComparison.Ordinal))
        {
            return;
        }

        if (!PdfStructureTypes.IsStandardIn(element.Type, PdfStructureTypes.Pdf20Flag))
        {
            return;
        }

        var code = PdfStructureTypes.IsStandardIn(element.Type, PdfStructureTypes.Pdf17Flag) ? PdfAccessibilityCode.SharedTypeNotInPdf20Namespace : PdfAccessibilityCode.Pdf20TypeWithoutNamespace;
        AccessibilityFindingCollection.Add(findings, code, element.PageIndex, element.Id);
    }

    /// <summary>Reports a tag that holds a second child of a type the table allows at most once (occurrence 0..1, ISO/TS 32005 Table 5).</summary>
    /// <param name="element">The parent tag.</param>
    /// <param name="findings">Receives a finding for each repeated type.</param>
    private static void CheckOccurrence(PdfStructureElement element, AccessibilityFindings findings)
    {
        var parent = StructureContainment.NameOf(element);
        var seen = OnceChild.None;
        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is not { } child)
            {
                continue;
            }

            var flag = StructureContainment.AtMostOnce(parent, StructureContainment.NameOf(child));
            if (flag == OnceChild.None)
            {
                continue;
            }

            if ((seen & flag) != 0)
            {
                AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.StructureOccurrenceViolation, child.PageIndex, child.Id);
            }

            seen |= flag;
        }
    }

    /// <summary>Reports an element whose parent does not allow it.</summary>
    /// <param name="element">The element.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void Check(PdfStructureElement element, AccessibilityFindings findings)
    {
        CheckNamespaceUse(element, findings);
        CheckOccurrence(element, findings);
        if (element.Parent is not { } parent || StructureContainment.NameOf(element) is not { } child || StructureContainment.NameOf(parent) is not { } parentName
            || !StructureContainment.Knows(parentName) || StructureContainment.Allows(parentName, child))
        {
            return;
        }

        AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.StructureContainmentViolation, element.PageIndex, element.Id);
    }
}
