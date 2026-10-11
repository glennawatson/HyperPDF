// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads outlines and resolves document destinations.</summary>
public static class PdfDocumentNavigation
{
    /// <summary>The most outline entries read, guarding against huge or looping outlines.</summary>
    internal const int MaxOutlineEntries = 20_000;

    /// <summary>The deepest outline nesting read.</summary>
    internal const int MaxOutlineDepth = 64;

    /// <summary>The position of the left coordinate in an /XYZ, /FitV or /FitR destination.</summary>
    private const int LeftSlot = 2;

    /// <summary>The position of the top coordinate in an /XYZ destination.</summary>
    private const int XyzTopSlot = 3;

    /// <summary>The position of the zoom in an /XYZ destination.</summary>
    private const int XyzZoomSlot = 4;

    /// <summary>The position of the top coordinate in a /FitR destination.</summary>
    private const int FitRTopSlot = 5;

    /// <summary>Gets the outline (bookmarks), loading its entries first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The root entries.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var load = document.Catalog.GetDictionary(KnownName.Outlines) is { } root ? PdfPrefetcher.PrefetchAsync(
        document.Objects,
        root,
        PdfPrefetchKind.Outline,
        cancellationToken) : ValueTask.CompletedTask;
        return load.IsCompletedSuccessfully ? PdfDocumentNavigation.OutlineReady(document, cancellationToken) : PdfDocumentNavigation.OutlineAfterAsync(document, load, cancellationToken);
    }

    /// <summary>Gets the outline (bookmarks).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The root entries.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfOutlineItem> GetOutline(PdfDocument document) =>
        ReadOutlinePrepared(document, CancellationToken.None);

    /// <summary>Resolves a destination: an explicit array, or a name looked up in the document's named destinations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="value">The destination value.</param>
    /// <returns>The destination, or <see langword="null"/> when it leads nowhere.</returns>
    public static PdfDestination? ResolveDestination(PdfDocument document, PdfValue value)
    {
        value = StoreReading.Resolve(document.Objects, value);
        if (value.Kind is PdfKind.Name or PdfKind.String)
        {
            value = PdfDocumentNavigation.FindNamedDestination(document, value);
        }

        if (value.AsDictionary() is { } wrapped)
        {
            value = wrapped.Get(KnownName.D);
        }

        return value.AsArray() is { } array ? PdfDocumentNavigation.ReadExplicitDestination(document, array) : null;
    }

    /// <summary>Reads an action dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action; an <see cref="UnsupportedAction"/> for names the library does not read and for navigation actions that lead nowhere.</returns>
    public static PdfAction ReadAction(PdfDocument document, PdfDictionary action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var navigation = PdfDocumentNavigation.ReadNavigationAction(document, action);
        return navigation.HasAction ? navigation : PdfDocumentActionTypes.ReadExtendedAction(document, action, action.NameText("S") ?? string.Empty);
    }

    /// <summary>Looks up a named destination in the /Names /Dests name tree and then the catalog's /Dests dictionary, as PDFium does.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The name or string.</param>
    /// <returns>The destination value, or null.</returns>
    internal static PdfValue FindNamedDestination(PdfDocument document, PdfValue name)
    {
        var bytes = name.Kind == PdfKind.Name ? document.Objects.Names.GetSpelling(name.AsName()) : name.AsStringBytes();
        var found = StoreReading.Resolve(document.Objects, NameTree.Find(document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.Dests), bytes));
        if (!found.IsNull)
        {
            return found;
        }

        return document.Catalog.GetDictionary(KnownName.Dests) is { } dests ? dests.Get(document.Objects.Names.Intern(bytes)) : default;
    }

    /// <summary>Gets the outline while checking cancellation during its first walk.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The root entries.</returns>
    private static PdfOutlineItem[] ReadOutlinePrepared(PdfDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outline = Volatile.Read(ref document.State.Outline);
        if (outline is not null)
        {
            return outline;
        }

        var budget = PdfDocumentNavigation.MaxOutlineEntries;
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var first = document.Catalog.GetDictionary(KnownName.Outlines)?.GetDictionary(KnownName.First);
        outline = [.. PdfDocumentNavigation.ReadOutlineLevel(document, first, 0, visited, ref budget, cancellationToken)];
        Volatile.Write(ref document.State.Outline, outline);
        return outline;
    }

    /// <summary>Reads the outline once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed outline, or a faulted task.</returns>
    private static ValueTask<IReadOnlyList<PdfOutlineItem>> OutlineReady(PdfDocument document, CancellationToken cancellationToken)
    {
        try
        {
            return new(PdfDocumentNavigation.ReadOutlinePrepared(document, cancellationToken));
        }
        catch (Exception ex) when (PdfDocumentAsyncTasks.IsTaskFault(ex))
        {
            return ValueTask.FromException<IReadOnlyList<PdfOutlineItem>>(ex);
        }
    }

    /// <summary>Waits for a load, then reads the outline.</summary>
    /// <param name="document">The document.</param>
    /// <param name="load">The load.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The outline.</returns>
    private static async ValueTask<IReadOnlyList<PdfOutlineItem>> OutlineAfterAsync(PdfDocument document, ValueTask load, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return PdfDocumentNavigation.ReadOutlinePrepared(document, cancellationToken);
    }

    /// <summary>Reads an optional coordinate; null means "unchanged".</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="index">The position.</param>
    /// <returns>The coordinate, or <see langword="null"/>.</returns>
    private static float? Optional(PdfArray array, int index)
    {
        var value = array.Get(index);
        return value.IsNumber ? value.AsSingle() : null;
    }

    /// <summary>Reads the viewer-facing action kinds.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action; no action (a null <c>Value</c>) for other kinds or when it leads nowhere.</returns>
    private static PdfAction ReadNavigationAction(PdfDocument document, PdfDictionary action) => action.GetName(KnownName.S).ToKnownName() switch
    {
        KnownName.GoTo => PdfDocumentNavigation.ReadGoTo(document, action),
        KnownName.URI => PdfDocumentFileSpecs.ReadUri(document, action),
        KnownName.GoToR => PdfDocumentNavigation.ReadRemoteGoTo(document, action),
        KnownName.Launch => PdfDocumentFileSpecs.ReadLaunch(document, action),
        KnownName.GoToE => new EmbeddedGoToAction(action.GetDictionary(KnownName.T)?.GetText(KnownName.N)),
        KnownName.Named => new NamedAction(document.Objects.Names.GetString(action.GetName(KnownName.N))),
        KnownName.JavaScript => PdfDocumentFileSpecs.ReadScript(action.Get(KnownName.JS)) is { } script ? new JavaScriptAction(script) : default(PdfAction),
        _ => default,
    };

    /// <summary>Reads one level of the outline and, recursively, the levels below it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="first">The first entry at this level.</param>
    /// <param name="depth">The level's depth.</param>
    /// <param name="visited">The entries already read, to stop at loops.</param>
    /// <param name="budget">The entries still allowed.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The entries.</returns>
    private static List<PdfOutlineItem> ReadOutlineLevel(PdfDocument document, PdfDictionary? first, int depth, HashSet<PdfDictionary> visited, ref int budget, CancellationToken cancellationToken)
    {
        var items = new List<PdfOutlineItem>();
        for (var entry = first; entry is not null && budget > 0 && depth < PdfDocumentNavigation.MaxOutlineDepth && visited.Add(entry); entry = entry.GetDictionary(KnownName.Next))
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget--;
            var children = PdfDocumentNavigation.ReadOutlineLevel(document, entry.GetDictionary(KnownName.First), depth + 1, visited, ref budget, cancellationToken);
            items.Add(new(entry.GetText(KnownName.Title) ?? string.Empty, PdfDocumentNavigation.ReadOutlineTarget(document, entry), [.. children], entry.GetInteger(KnownName.Count) > 0));
        }

        return items;
    }

    /// <summary>Reads where an outline entry leads: its /Dest, or else its /A action.</summary>
    /// <param name="document">The document.</param>
    /// <param name="entry">The outline entry.</param>
    /// <returns>The action.</returns>
    private static PdfAction ReadOutlineTarget(PdfDocument document, PdfDictionary entry)
    {
        if (PdfDocumentNavigation.ResolveDestination(document, entry.Get(KnownName.Dest)) is { } destination)
        {
            return new GoToAction(destination);
        }

        return entry.GetDictionary(KnownName.A) is { } action ? PdfDocumentNavigation.ReadAction(document, action) : default;
    }

    /// <summary>Reads an explicit destination array: a page then a fit type and its coordinates.</summary>
    /// <param name="document">The document.</param>
    /// <param name="array">The destination array.</param>
    /// <returns>The destination, or <see langword="null"/> when its page is not in the document.</returns>
    private static PdfDestination? ReadExplicitDestination(PdfDocument document, PdfArray array)
    {
        var page = array.GetRaw(0);
        var index = page.IsReference ? PdfDocumentPages.GetPageIndex(document, page.AsReference()) : page.AsInt32(-1);
        return (uint)index >= (uint)document.PageCount ? null : array.GetName(1).ToKnownName() switch
        {
            KnownName.XYZ =>
        new(
        index,
        PdfDocumentNavigation.Optional(
        array,
        PdfDocumentNavigation.LeftSlot),
        PdfDocumentNavigation.Optional(
        array,
        PdfDocumentNavigation.XyzTopSlot),
        PdfDocumentNavigation.Optional(
        array,
        PdfDocumentNavigation.XyzZoomSlot)),

            KnownName.FitH or KnownName.FitBH => new(index, null, PdfDocumentNavigation.Optional(array, PdfDocumentNavigation.LeftSlot), null),
            KnownName.FitV or KnownName.FitBV => new(index, PdfDocumentNavigation.Optional(array, PdfDocumentNavigation.LeftSlot), null, null),
            KnownName.FitR => new(index, PdfDocumentNavigation.Optional(array, PdfDocumentNavigation.LeftSlot), PdfDocumentNavigation.Optional(array, PdfDocumentNavigation.FitRTopSlot), null),
            _ => new(index, null, null, null),
        };
    }

    /// <summary>Reads a go-to action.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action, or no action when the destination is not in the document.</returns>
    private static PdfAction ReadGoTo(
        PdfDocument document,
        PdfDictionary action) =>
        PdfDocumentNavigation.ResolveDestination(
        document,
        action.Get(KnownName.D)) is { } destination ? new GoToAction(destination) : default(PdfAction);

    /// <summary>Reads a remote go-to action: the other file and a page number in it, or page zero.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action, or no action when the file is missing.</returns>
    private static PdfAction ReadRemoteGoTo(PdfDocument document, PdfDictionary action)
    {
        if (PdfDocumentFileSpecs.ReadFileSpec(document, action.Get(KnownName.F), out var isUrl) is not { } file)
        {
            return default;
        }

        if (isUrl)
        {
            return new UriAction(file);
        }

        var destination = StoreReading.Resolve(document.Objects, action.Get(KnownName.D));
        if (destination.Kind is PdfKind.Name or PdfKind.String)
        {
            var named = destination.Kind == PdfKind.Name ? document.Objects.Names.GetString(destination.AsName()) : PdfText.Decode(destination.AsStringBytes());
            return new RemoteGoToAction(PdfDocumentFileSpecs.ToPlatformPath(file), 0) { NamedDestination = named, };
        }

        var page = destination.AsArray()?.Get(0) ?? default;
        return new RemoteGoToAction(PdfDocumentFileSpecs.ToPlatformPath(file), Math.Max(0, page.AsInt32(0)));
    }
}
