// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Walks the structure tree once, in logical order, and reports what the element types alone show: types that mean
/// nothing, heading order, figures, tables and lists. It also notes which element owns each annotation, so the page
/// checks can tell whether an annotation is tagged.
/// </summary>
[DebuggerDisplay("AccessibilityStructureScan: {_elementCount} elements")]
internal sealed class AccessibilityStructureScan
{
    /// <summary>The most role map steps followed when looking for a loop.</summary>
    private const int MaxRoleMapSteps = 32;

    /// <summary>The longest type spelling checked against the standard types.</summary>
    private const int MaxTypeSpelling = 64;

    /// <summary>Set in a table's flags when it has a header cell.</summary>
    private const int HasHeaderCell = 1;

    /// <summary>Set in a table's flags when a header cell states its scope.</summary>
    private const int HasScope = 2;

    /// <summary>Set in a table's flags when a cell lists its headers.</summary>
    private const int HasHeadersList = 4;

    /// <summary>The findings.</summary>
    private readonly AccessibilityFindings _findings;

    /// <summary>The structure tree.</summary>
    private readonly PdfStructureTree _tree;

    /// <summary>The role map, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _roleMap;

    /// <summary>The claimed PDF/UA part, or <see langword="null"/>.</summary>
    private readonly int? _part;

    /// <summary>The cancellation token.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>What the namespace role maps do to each type in each explicitly provided namespace.</summary>
    private readonly Dictionary<NamespaceType, NamespaceMapProblem> _namespaceMaps = [];

    /// <summary>What the root role map does to each raw type, by spelling.</summary>
    private readonly Dictionary<string, RoleMapFacts> _roleMapFacts = [with(StringComparer.Ordinal)];

    /// <summary>The elements that refer to each object, by object number.</summary>
    private readonly Dictionary<int, PdfStructureElement> _owners = [];

    /// <summary>The pages tables start on; -1 stands for a table whose page is unknown.</summary>
    private readonly HashSet<int> _tablePages = [];

    /// <summary>The number of elements visited.</summary>
    private int _elementCount;

    /// <summary>The level of the last heading, or zero before the first.</summary>
    private int _lastHeading;

    /// <summary>The number of level 1 headings seen.</summary>
    private int _levelOneCount;

    /// <summary>Initializes a new instance of the <see cref="AccessibilityStructureScan"/> class.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellation">The cancellation token.</param>
    internal AccessibilityStructureScan(PdfStructureTree tree, int? part, AccessibilityFindings findings, CancellationToken cancellation)
    {
        _tree = tree;
        _part = part;
        _findings = findings;
        _cancellation = cancellation;
        _roleMap = tree.Root.GetDictionary(KnownName.RoleMap);
    }

    /// <summary>Gets a value indicating whether the tree uses the PDF 2.0 structure namespace.</summary>
    internal bool UsesPdf20Namespace { get; private set; }

    /// <summary>Determines whether a page may hold part of a table, so its reading nodes are worth building.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns><see langword="true"/> when a table starts on the page, or starts on a page the file does not name.</returns>
    internal bool MayHoldTable(int pageIndex) => _tablePages.Contains(pageIndex) || _tablePages.Contains(-1);

    /// <summary>Finds the element that refers to an annotation or other object.</summary>
    /// <param name="id">The object's id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    internal PdfStructureElement? FindOwner(PdfObjectId id) => id.IsValid && _owners.TryGetValue(id.Number, out var owner) ? owner : null;

    /// <summary>Walks the whole tree.</summary>
    internal void Run()
    {
        CheckRoots();
        foreach (var root in _tree.Roots)
        {
            Visit(root);
        }
    }

    /// <summary>Reads what a table's cells state, without entering tables nested inside it.</summary>
    /// <param name="element">The table or one of its descendants.</param>
    /// <param name="isRoot">Whether the element is the table itself.</param>
    /// <returns>The flags: header cell, scope and headers list.</returns>
    private static int CollectTableFlags(PdfStructureElement element, bool isRoot)
    {
        if (!isRoot && element.Type == PdfStructureType.Table)
        {
            return 0;
        }

        var flags = 0;
        if (element.Type == PdfStructureType.TableHeaderCell)
        {
            flags |= HasHeaderCell;
            flags |= element.Attributes.Scope != PdfTableScope.None ? HasScope : 0;
        }

        flags |= element.Attributes.Headers.Length > 0 ? HasHeadersList : 0;
        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                flags |= CollectTableFlags(child, false);
            }
        }

        return flags;
    }

    /// <summary>Determines whether a spelling is a standard structure type.</summary>
    /// <param name="type">The type's spelling.</param>
    /// <param name="namespaces">The standard namespaces to look in: <see cref="PdfStructureTypes.Pdf17Flag"/>, <see cref="PdfStructureTypes.Pdf20Flag"/> or both.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsStandard(string type, int namespaces)
    {
        if (type.Length > MaxTypeSpelling)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[MaxTypeSpelling];
        return Encoding.Latin1.GetBytes(type, bytes) == type.Length
            && PdfStructureTypes.IsStandardIn(PdfStructureTypes.Find(bytes[..type.Length]), namespaces);
    }

    /// <summary>Visits an element, then its kids.</summary>
    /// <param name="element">The element.</param>
    private void Visit(PdfStructureElement element)
    {
        _cancellation.ThrowIfCancellationRequested();
        _elementCount++;
        UsesPdf20Namespace |= element.Namespace == PdfStructureTypes.Pdf20Uri;
        CheckRoleMap(element);
        CheckLanguage(element);
        CheckPart2Type(element);
        CheckType(element);
        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                Visit(child);
            }
            else if (kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid)
            {
                _ = _owners.TryAdd(kid.Object.Number, element);
            }
        }
    }

    /// <summary>Runs the check that belongs to an element's type.</summary>
    /// <param name="element">The element.</param>
    private void CheckType(PdfStructureElement element)
    {
        switch (element.Type)
        {
            case PdfStructureType.Figure:
            {
                CheckFigure(element);
                break;
            }

            case PdfStructureType.Table:
            {
                CheckTable(element);
                break;
            }

            case PdfStructureType.ListItem:
            {
                CheckListItem(element);
                break;
            }

            case >= PdfStructureType.Heading1 and <= PdfStructureType.Heading6:
            {
                CheckHeading(element);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>
    /// Reports what an element's type and the role maps make of it: a loop in the role map (ISO 14289-1 7.1; ISO 14289-2 8.2.4),
    /// a standard type the role map remaps (ISO 14289-1 7.1), a type that reaches no standard type, or a namespace role map that
    /// maps a type within its own namespace (ISO 14289-2 8.2.4).
    /// </summary>
    /// <param name="element">The element.</param>
    private void CheckRoleMap(PdfStructureElement element)
    {
        var problem = ReadNamespaceProblem(element);
        var cycles = problem == NamespaceMapProblem.Cycle;
        if (element.Namespace is null && _roleMap is not null)
        {
            var facts = GetRoleMapFacts(element.RawType);
            cycles |= facts.Cycles;
            if (facts.RemapsStandard)
            {
                _findings.Add(PdfAccessibilityCode.StandardTypeRemapped, element.PageIndex, element.Id);
            }
        }

        if (cycles)
        {
            _findings.Add(PdfAccessibilityCode.RoleMapCycle, element.PageIndex, element.Id);
        }

        if (problem == NamespaceMapProblem.SameNamespace)
        {
            _findings.Add(PdfAccessibilityCode.RoleMapSameNamespace, element.PageIndex, element.Id);
        }

        if (!cycles)
        {
            CheckMapped(element);
        }
    }

    /// <summary>Reports a type that reaches no standard type.</summary>
    /// <param name="element">The element.</param>
    private void CheckMapped(PdfStructureElement element)
    {
        // A standard type the role map remaps is still a standard type, so only the remapping is reported.
        if (element.Type == PdfStructureType.Unknown && !element.ResolvesToMathMl && !(element.Namespace is null && IsStandard(element.RawType, PdfStructureTypes.AnyStandardFlag)))
        {
            _findings.Add(PdfAccessibilityCode.UnmappedStructureType, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports an element's own <c>/Lang</c> when it is not a language identifier (ISO 14289-1 7.2; ISO 14289-2 8.4.4).</summary>
    /// <param name="element">The element.</param>
    private void CheckLanguage(PdfStructureElement element)
    {
        if (element.Dictionary.GetText(KnownName.Lang) is { } language && !AccessibilityLanguageTags.IsValid(language))
        {
            _findings.Add(PdfAccessibilityCode.LanguageInvalid, element.PageIndex, element.Id);
        }
    }

    /// <summary>Follows the namespace role maps from an element's type, once for each namespace and type.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The problem found.</returns>
    private NamespaceMapProblem ReadNamespaceProblem(PdfStructureElement element)
    {
        if (element.Namespace is null || element.Dictionary.Dict("NS") is not { } space)
        {
            return NamespaceMapProblem.None;
        }

        ref var problem = ref CollectionsMarshal.GetValueRefOrAddDefault(_namespaceMaps, new(space, element.RawType), out var exists);
        if (!exists)
        {
            problem = AccessibilityNamespaceMap.Read(space, element.RawType);
        }

        return problem;
    }

    /// <summary>Reads what the root role map does to a type, once for each spelling.</summary>
    /// <param name="type">The type's spelling.</param>
    /// <returns>The facts.</returns>
    private RoleMapFacts GetRoleMapFacts(string type)
    {
        ref var facts = ref CollectionsMarshal.GetValueRefOrAddDefault(_roleMapFacts, type, out var exists);
        if (!exists)
        {
            facts = ReadRoleMapFacts(type);
        }

        return facts;
    }

    /// <summary>Follows the root role map from a type.</summary>
    /// <param name="type">The type's spelling.</param>
    /// <returns>Whether the map loops back on a type already seen, and whether it remaps a standard type.</returns>
    private RoleMapFacts ReadRoleMapFacts(string type)
    {
        var map = _roleMap!;
        var first = map.NameText(type);
        if (first is null)
        {
            return default;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { type };
        var current = first;
        var loops = false;
        for (var step = 0; step < MaxRoleMapSteps && current is not null && !loops; step++)
        {
            loops = !seen.Add(current);
            current = map.NameText(current);
        }

        var remaps = !string.Equals(first, type, StringComparison.Ordinal) && IsStandard(type, PdfStructureTypes.Pdf17Flag);
        return new(loops, remaps);
    }

    /// <summary>Checks the top of the tree for PDF/UA-2: a single Document element in the PDF 2.0 namespace (ISO 14289-2 8.2.5.2).</summary>
    private void CheckRoots()
    {
        if (_part != PdfUaClaim.Part2)
        {
            return;
        }

        var roots = _tree.Roots;
        var valid = roots.Count == 1 && roots[0].Type == PdfStructureType.Document && roots[0].Namespace == PdfStructureTypes.Pdf20Uri;
        if (!valid)
        {
            _findings.Add(PdfAccessibilityCode.DocumentRootInvalid);
        }
    }

    /// <summary>Reports the types PDF/UA-2 does not allow: <c>H</c> (ISO 14289-2 8.2.5.12) and an unmapped <c>Note</c> (8.2.5.14).</summary>
    /// <param name="element">The element.</param>
    private void CheckPart2Type(PdfStructureElement element)
    {
        if (_part != PdfUaClaim.Part2)
        {
            return;
        }

        if (element.Type == PdfStructureType.Heading)
        {
            _findings.Add(PdfAccessibilityCode.HeadingTypeNotAllowed, element.PageIndex, element.Id);
        }
        else if (element.Type == PdfStructureType.Note)
        {
            _findings.Add(PdfAccessibilityCode.NoteTypeNotAllowed, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a figure with no description.</summary>
    /// <param name="element">The figure.</param>
    private void CheckFigure(PdfStructureElement element)
    {
        // An empty /ActualText still replaces the figure's content with nothing, which ISO 14289 accepts as a description.
        if (element.AlternateText is null && element.ActualText is null && !element.Dictionary.Has("ActualText"))
        {
            _findings.Add(PdfAccessibilityCode.FigureNoDescription, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a list item outside a list.</summary>
    /// <param name="element">The list item.</param>
    private void CheckListItem(PdfStructureElement element)
    {
        if (element.Parent is not { Type: PdfStructureType.List })
        {
            _findings.Add(PdfAccessibilityCode.ListItemOutsideList, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a heading that skips a level, and a second level 1 heading.</summary>
    /// <param name="element">The heading.</param>
    private void CheckHeading(PdfStructureElement element)
    {
        var level = element.HeadingLevel;
        if (level > _lastHeading + 1)
        {
            _findings.Add(PdfAccessibilityCode.HeadingLevelSkipped, element.PageIndex, element.Id);
        }

        if (level == 1)
        {
            _levelOneCount++;
            if (_levelOneCount > 1 && _part != PdfUaClaim.Part2)
            {
                _findings.Add(PdfAccessibilityCode.MultipleLevelOneHeadings, element.PageIndex, element.Id);
            }
        }

        _lastHeading = level;
    }

    /// <summary>Reports a table with no header cells, or with header cells that say nothing about what they head.</summary>
    /// <param name="table">The table.</param>
    private void CheckTable(PdfStructureElement table)
    {
        _ = _tablePages.Add(table.PageIndex);
        var flags = CollectTableFlags(table, true);
        if ((flags & HasHeaderCell) == 0)
        {
            _findings.Add(PdfAccessibilityCode.TableNoHeaderCells, table.PageIndex, table.Id);
        }
        else if ((flags & (HasScope | HasHeadersList)) == 0)
        {
            _findings.Add(PdfAccessibilityCode.TableHeaderNoScope, table.PageIndex, table.Id);
        }
    }

    /// <summary>A structure type in a namespace dictionary.</summary>
    /// <param name="Space">The namespace dictionary.</param>
    /// <param name="Type">The type's spelling.</param>
    private readonly record struct NamespaceType(PdfDictionary Space, string Type);

    /// <summary>What the root role map does to one type.</summary>
    /// <param name="Cycles">Whether following the map comes back to a type already seen, which includes a type mapped to itself.</param>
    /// <param name="RemapsStandard">Whether a standard type is mapped to a different type.</param>
    private readonly record struct RoleMapFacts(bool Cycles, bool RemapsStandard);
}
