// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks structure types and records annotation owners and table pages in one logical walk.</summary>
internal static class AccessibilityStructureScan
{
    /// <summary>The most role map steps followed when looking for a loop.</summary>
    private const int MaxRoleMapSteps = 32;

    /// <summary>Set in a table's flags when it has a header cell.</summary>
    private const int HasHeaderCell = 1;

    /// <summary>Set in a table's flags when a header cell states its scope.</summary>
    private const int HasScope = 2;

    /// <summary>Set in a table's flags when a cell lists its headers.</summary>
    private const int HasHeadersList = 4;

    /// <summary>Walks the whole tree.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    internal static void Run(ref AccessibilityStructureState state)
    {
        CheckRoots(ref state);
        var roots = state.Tree.Roots;
        for (var index = 0; index < roots.Count; index++)
        {
            Visit(ref state, roots[index]);
        }

        if (state.NoteIdentifiers is not null)
        {
            AccessibilityNoteIdentifiers.Check(state);
        }
    }

    /// <summary>Clears and returns the walk's lazily leased storage.</summary>
    /// <param name="state">The completed or interrupted walk.</param>
    internal static void Release(in AccessibilityStructureState state)
    {
        if (state.NoteIdentifiers is { } identifiers)
        {
            identifiers.Clear();
            _ = state.Pools.Slots<Dictionary<PdfValue, int>>().Offer(identifiers);
        }

        if (state.NamespaceMaps is { } namespaces)
        {
            namespaces.Clear();
            _ = state.Pools.Slots<Dictionary<NamespaceType, NamespaceMapProblem>>().Offer(namespaces);
        }

        if (state.RoleMapFacts is { } facts)
        {
            facts.Clear();
            _ = state.Pools.Slots<Dictionary<PdfName, RoleMapFacts>>().Offer(facts);
        }

        if (state.Owners is { } owners)
        {
            owners.Clear();
            _ = state.Pools.Slots<Dictionary<int, PdfStructureElement>>().Offer(owners);
        }

        if (state.TablePages is not { } pages)
        {
            return;
        }

        pages.Clear();
        _ = state.Pools.Slots<HashSet<int>>().Offer(pages);
    }

    /// <summary>Reports a PDF/UA-1 formula without an alternate text string.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The mathematical expression.</param>
    internal static void CheckFormula(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        // ISO 14289-1 7.7 requires Alt separately from replacement text; an empty string remains a string.
        if (element.Dictionary.Get(KnownName.Alt).Kind != PdfKind.String)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.FormulaNoAlternative, element.PageIndex, element.Id);
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
        var kids = element.Kids;
        for (var index = 0; index < kids.Count; index++)
        {
            var kid = kids[index];
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                flags |= CollectTableFlags(child, false);
            }
        }

        return flags;
    }

    /// <summary>Determines whether a spelling is a standard structure type.</summary>
    /// <param name="dictionary">The dictionary whose owner interns the type name.</param>
    /// <param name="type">The type's spelling.</param>
    /// <param name="namespaces">The standard namespaces to look in: <see cref="PdfStructureTypes.Pdf17Flag"/>, <see cref="PdfStructureTypes.Pdf20Flag"/> or both.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsStandard(PdfDictionary dictionary, PdfName type, int namespaces) =>
        dictionary.Owner is { } owner
        && PdfStructureTypes.IsStandardIn(PdfStructureTypes.Find(owner.Names.GetSpelling(type)), namespaces);

    /// <summary>Visits an element, then its kids.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    private static void Visit(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        state.Cancellation.ThrowIfCancellationRequested();
        state = state with
        {
            UsesPdf20Namespace = state.UsesPdf20Namespace || (element.Namespace == PdfStructureTypes.Pdf20Uri)
        };
        CheckRoleMap(ref state, element);
        CheckLanguage(ref state, element);
        CheckPart2Type(ref state, element);
        CheckType(ref state, element);
        var kids = element.Kids;
        for (var index = 0; index < kids.Count; index++)
        {
            var kid = kids[index];
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                Visit(ref state, child);
            }
            else if (kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid)
            {
                if (state.Owners is null)
                {
                    state = state with
                    {
                        Owners = state.Pools.Slots<Dictionary<int, PdfStructureElement>>().Take() ?? []
                    };
                }

                _ = state.Owners.TryAdd(kid.Object.Number, element);
            }
        }
    }

    /// <summary>Runs the check that belongs to an element's type.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    private static void CheckType(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        switch (element.Type)
        {
            case PdfStructureType.Figure:
                {
                    CheckFigure(ref state, element);
                    break;
                }

            case PdfStructureType.Formula when state.Part == PdfUaClaim.Part1:
                {
                    CheckFormula(ref state, element);
                    break;
                }

            case PdfStructureType.Note when state.Part == PdfUaClaim.Part1:
                {
                    if (state.NoteIdentifiers is null)
                    {
                        state = state with { NoteIdentifiers = AccessibilityNoteIdentifiers.Rent(state.Pools) };
                    }

                    break;
                }

            case PdfStructureType.Table:
                {
                    CheckTable(ref state, element);
                    break;
                }

            case PdfStructureType.ListItem:
                {
                    CheckListItem(ref state, element);
                    break;
                }

            // ISO 14289-2 8.2.5.12 requires semantic heading levels, not the part 1 sequential-level rule.
            case >= PdfStructureType.Heading1 and <= PdfStructureType.Heading6 when state.Part != PdfUaClaim.Part2:
            case PdfStructureType.NumberedHeading when state.Part != PdfUaClaim.Part2:
                {
                    CheckHeading(ref state, element);
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
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    private static void CheckRoleMap(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        var problem = ReadNamespaceProblem(ref state, element);
        var cycles = problem == NamespaceMapProblem.Cycle;
        if (element.Namespace is null && state.RoleMap is not null)
        {
            var facts = GetRoleMapFacts(ref state, element.RawType);
            cycles |= facts.Cycles;
            if (facts.RemapsStandard)
            {
                AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.StandardTypeRemapped, element.PageIndex, element.Id);
            }
        }

        if (cycles)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.RoleMapCycle, element.PageIndex, element.Id);
        }

        if (problem == NamespaceMapProblem.SameNamespace)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.RoleMapSameNamespace, element.PageIndex, element.Id);
        }

        if (!cycles)
        {
            CheckMapped(ref state, element);
        }
    }

    /// <summary>Reports a type that reaches no standard type.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    private static void CheckMapped(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        // Remapped standard types have their own finding, rather than an unmapped-type finding.
        if (element.Type == PdfStructureType.Unknown && !element.ResolvesToMathMl
        && !(element.Namespace is null
        && IsStandard(element.Dictionary, element.Dictionary.GetName(KnownName.S), PdfStructureTypes.AnyStandardFlag)))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.UnmappedStructureType, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports an element's own <c>/Lang</c> when it is not a language identifier (ISO 14289-1 7.2; ISO 14289-2 8.4.4).</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    private static void CheckLanguage(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        if (element.Dictionary.GetText(KnownName.Lang) is { } language && !AccessibilityLanguageTags.IsValid(language))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.LanguageInvalid, element.PageIndex, element.Id);
        }
    }

    /// <summary>Follows the namespace role maps from an element's type, once for each namespace and type.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    /// <returns>The problem found.</returns>
    private static NamespaceMapProblem ReadNamespaceProblem(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        if (element.Namespace is null || element.Dictionary.Dict("NS") is not { } space || space.Dict("RoleMapNS") is not { } roleMap
        || roleMap.Array(element.RawType) is not { Count: > 1 })
        {
            return NamespaceMapProblem.None;
        }

        if (state.NamespaceMaps is null)
        {
            state = state with
            {
                NamespaceMaps = state.Pools.Slots<Dictionary<NamespaceType, NamespaceMapProblem>>().Take() ?? []
            };
        }

        ref var problem = ref CollectionsMarshal.GetValueRefOrAddDefault(state.NamespaceMaps, new(space, space.Key(element.RawType)), out var exists);
        if (!exists)
        {
            problem = AccessibilityNamespaceMap.Read(space, element.RawType, state.Cancellation);
        }

        return problem;
    }

    /// <summary>Reads what the root role map does to a type, once for each spelling.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="spelling">The structure type's raw spelling.</param>
    /// <returns>The facts.</returns>
    private static RoleMapFacts GetRoleMapFacts(ref AccessibilityStructureState state, string spelling)
    {
        var map = state.RoleMap!;
        var type = map.Key(spelling);
        var first = map.GetName(type);
        if (first.IsNone)
        {
            return default;
        }

        if (state.RoleMapFacts is null)
        {
            state = state with
            {
                RoleMapFacts = state.Pools.Slots<Dictionary<PdfName, RoleMapFacts>>().Take() ?? []
            };
        }

        ref var facts = ref CollectionsMarshal.GetValueRefOrAddDefault(state.RoleMapFacts, type, out var exists);
        if (!exists)
        {
            facts = ReadRoleMapFacts(map, type, first, state.Cancellation);
        }

        return facts;
    }

    /// <summary>Follows the root role map from a type.</summary>
    /// <param name="map">The root role map.</param>
    /// <param name="type">The type's spelling.</param>
    /// <param name="first">The first mapped name, known to be present.</param>
    /// <param name="cancellation">Cancels the bounded role-map walk.</param>
    /// <returns>Whether the map loops back on a type already seen, and whether it remaps a standard type.</returns>
    private static RoleMapFacts ReadRoleMapFacts(PdfDictionary map, PdfName type, PdfName first, CancellationToken cancellation)
    {
        Span<PdfName> seen = stackalloc PdfName[MaxRoleMapSteps + 1];
        seen[0] = type;
        var current = first;
        var loops = false;
        for (var step = 0; step < MaxRoleMapSteps && !current.IsNone && !loops; step++)
        {
            cancellation.ThrowIfCancellationRequested();
            loops = seen[..(step + 1)].Contains(current);
            seen[step + 1] = current;
            current = map.GetName(current);
        }

        var remaps = first != type && IsStandard(map, type, PdfStructureTypes.Pdf17Flag);
        return new(loops, remaps);
    }

    /// <summary>Checks the top of the tree for PDF/UA-2: a single Document element in the PDF 2.0 namespace (ISO 14289-2 8.2.5.2).</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    private static void CheckRoots(ref AccessibilityStructureState state)
    {
        if (state.Part != PdfUaClaim.Part2)
        {
            return;
        }

        var roots = state.Tree.Roots;

        // The reading tree drops malformed and repeated children during recovery; count the raw /K as well.
        var children = state.Tree.Root.GetArray(KnownName.K);
        var valid = (children is null || children.Count == 1) && roots.Count == 1 && roots[0].Type == PdfStructureType.Document
        && roots[0].Namespace == PdfStructureTypes.Pdf20Uri;
        if (!valid)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.DocumentRootInvalid);
        }
    }

    /// <summary>Reports the types PDF/UA-2 does not allow: <c>H</c> (ISO 14289-2 8.2.5.12) and an unmapped <c>Note</c> (8.2.5.14).</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The element.</param>
    private static void CheckPart2Type(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        if (state.Part != PdfUaClaim.Part2)
        {
            return;
        }

        if (element.Type == PdfStructureType.Heading)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.HeadingTypeNotAllowed, element.PageIndex, element.Id);
        }
        else if (element.Type == PdfStructureType.Note)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.NoteTypeNotAllowed, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a figure with no description.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The figure.</param>
    private static void CheckFigure(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        // An empty /ActualText still replaces the figure's content with nothing, which ISO 14289 accepts as a description.
        if (element.AlternateText is null && element.ActualText is null && !element.Dictionary.Has("ActualText"))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.FigureNoDescription, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a list item outside a list.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The list item.</param>
    private static void CheckListItem(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        if (element.Parent is not { Type: PdfStructureType.List })
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.ListItemOutsideList, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a heading that skips a level.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="element">The heading.</param>
    private static void CheckHeading(ref AccessibilityStructureState state, PdfStructureElement element)
    {
        var name = NumberedHeadings.GetName(element);
        if (NumberedHeadings.SkipsLevel(state.LastHeading, name))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.HeadingLevelSkipped, element.PageIndex, element.Id);
        }

        state = state with
        {
            LastHeading = name
        };
    }

    /// <summary>Reports a table with no header cells, or with header cells that say nothing about what they head.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="table">The table.</param>
    private static void CheckTable(ref AccessibilityStructureState state, PdfStructureElement table)
    {
        if (state.TablePages is null)
        {
            state = state with
            {
                TablePages = state.Pools.Slots<HashSet<int>>().Take() ?? []
            };
        }

        _ = state.TablePages.Add(table.PageIndex);
        var flags = CollectTableFlags(table, true);
        if ((flags & HasHeaderCell) == 0)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.TableNoHeaderCells, table.PageIndex, table.Id);
        }
        else if ((flags & (HasScope | HasHeadersList)) == 0)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.TableHeaderNoScope, table.PageIndex, table.Id);
        }
    }

    /// <summary>A structure type interned by its namespace dictionary's owner.</summary>
    /// <param name="Space">The namespace dictionary.</param>
    /// <param name="Type">The interned structure type.</param>
    internal readonly record struct NamespaceType(PdfDictionary Space, PdfName Type);

    /// <summary>Whether a root role map loops or remaps a standard structure type.</summary>
    /// <param name="Cycles">Whether the chain revisits a type.</param>
    /// <param name="RemapsStandard">Whether a standard type maps to a different type.</param>
    internal readonly record struct RoleMapFacts(bool Cycles, bool RemapsStandard);
}
