// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document and page actions.</summary>
public static class PdfDocumentActions
{
    /// <summary>The most actions read in one chain.</summary>
    private const int MaxActionChain = 1000;

    /// <summary>The subtype of an action synthesised from a catalog <c>/OpenAction</c> destination.</summary>
    private const string GoToSubtype = "GoTo";

    /// <summary>Reads an action with its <c>/Next</c> chain. The chain is cut at a loop and after <see cref="MaxActionChain"/> actions.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action and the actions that follow it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public static PdfActionNode ReadActionNode(PdfDocument document, PdfDictionary action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var budget = PdfDocumentActions.MaxActionChain;
        return PdfDocumentActions.ReadActionNode(document, action, visited, 0, ref budget);
    }

    /// <summary>Gets the action run when the document opens (the catalog's <c>/OpenAction</c>), which may be a destination.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The action, or <see langword="null"/> when there is none.</returns>
    public static PdfActionNode? GetOpenAction(PdfDocument document)
    {
        var open = document.Catalog.Get(KnownName.OpenAction);
        if (open.AsDictionary() is { } action)
        {
            return PdfDocumentActions.ReadActionNode(document, action);
        }

        return !open.IsNull
        && PdfDocumentNavigation.ResolveDestination(
        document,
        open) is { } destination ? new PdfActionNode(
        PdfDocumentActions.GoToSubtype,
        new GoToAction(destination),
        [],
        new(document.Objects)) : null;
    }

    /// <summary>Gets the document's additional actions (the catalog's <c>/AA</c>): will close, will save, did save, will print and did print.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The triggers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfTrigger[] GetTriggers(PdfDocument document) => PdfDocumentActions.GetTriggers(document, document.Catalog);

    /// <summary>Gets a page's additional actions (<c>/AA</c>): opened (O) and closed (C).</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The triggers.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfTrigger[] GetTriggers(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return PdfDocumentActions.GetTriggers(document, page.Dictionary);
    }

    /// <summary>Gets the additional actions of any dictionary: an annotation, a form field or the catalog.</summary>
    /// <param name="document">The document.</param>
    /// <param name="owner">The dictionary holding an <c>/AA</c> entry.</param>
    /// <returns>The triggers, in the dictionary's order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public static PdfTrigger[] GetTriggers(PdfDocument document, PdfDictionary owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.GetDictionary(KnownName.AA) is not { } actions)
        {
            return [];
        }

        var triggers = new List<PdfTrigger>(actions.Count);
        for (var i = 0; i < actions.Count; i++)
        {
            if (StoreReading.Resolve(document.Objects, actions.GetValueAt(i)).AsDictionary() is { } action)
            {
                triggers.Add(new(document.Objects.Names.GetString(actions.GetKeyAt(i)), PdfDocumentActions.ReadActionNode(document, action)));
            }
        }

        return [.. triggers];
    }

    /// <summary>Gets the document-level scripts of the <c>/Names /JavaScript</c> name tree. The text is data; nothing runs.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The scripts in name tree order.</returns>
    public static PdfNamedScript[] GetDocumentScripts(PdfDocument document)
    {
        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(document.Catalog.GetDictionary(KnownName.Names)?.Dict("JavaScript"), entries);
        var scripts = new List<PdfNamedScript>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Value.AsDictionary()?.Get(KnownName.JS) is { } code && PdfDocumentFileSpecs.ReadScript(code) is { } script)
            {
                scripts.Add(new(PdfText.Decode(entry.Key.AsStringBytes()), script));
            }
        }

        return [.. scripts];
    }

    /// <summary>Reads an action and the actions after it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <param name="visited">The actions already read, to stop loops.</param>
    /// <param name="depth">The depth in the chain.</param>
    /// <param name="budget">The actions still allowed.</param>
    /// <returns>The node.</returns>
    internal static PdfActionNode ReadActionNode(PdfDocument document, PdfDictionary action, HashSet<PdfDictionary> visited, int depth, ref int budget)
    {
        _ = visited.Add(action);
        budget--;
        var subtype = action.NameText("S") ?? string.Empty;
        return new(subtype, PdfDocumentNavigation.ReadAction(document, action), PdfDocumentActions.ReadNext(document, action, visited, depth, ref budget), action);
    }

    /// <summary>Reads the <c>/Next</c> entry: one action or an array of them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <param name="visited">The actions already read.</param>
    /// <param name="depth">The depth of <paramref name="action"/>.</param>
    /// <param name="budget">The actions still allowed.</param>
    /// <returns>The following actions.</returns>
    private static PdfActionNode[] ReadNext(PdfDocument document, PdfDictionary action, HashSet<PdfDictionary> visited, int depth, ref int budget)
    {
        var next = action.Get(KnownName.Next);
        if (depth + 1 >= PdfLimits.MaxNesting || next.IsNull)
        {
            return [];
        }

        if (next.AsArray() is { } array)
        {
            var nodes = new List<PdfActionNode>(Math.Min(array.Count, budget));
            for (var i = 0; i < array.Count && budget > 0; i++)
            {
                PdfDocumentActions.AddNext(document, nodes, array.GetDictionary(i), visited, depth, ref budget);
            }

            return [.. nodes];
        }

        return budget > 0 && next.AsDictionary() is { } single && !visited.Contains(single)
            ? [PdfDocumentActions.ReadActionNode(document, single, visited, depth + 1, ref budget)]
            : [];
    }

    /// <summary>Adds a next action unless it was read already.</summary>
    /// <param name="document">The document.</param>
    /// <param name="nodes">The result list.</param>
    /// <param name="next">The action dictionary, or null.</param>
    /// <param name="visited">The actions already read.</param>
    /// <param name="depth">The depth of the action it follows.</param>
    /// <param name="budget">The actions still allowed.</param>
    private static void AddNext(PdfDocument document, List<PdfActionNode> nodes, PdfDictionary? next, HashSet<PdfDictionary> visited, int depth, ref int budget)
    {
        if (next is not null && !visited.Contains(next))
        {
            nodes.Add(PdfDocumentActions.ReadActionNode(document, next, visited, depth + 1, ref budget));
        }
    }
}
