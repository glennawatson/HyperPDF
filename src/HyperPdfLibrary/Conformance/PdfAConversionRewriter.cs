// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Applies a dictionary fix to every dictionary of every object the document reaches, including dictionaries nested
/// directly inside others. A changed dictionary is copied, never changed in place, and the indirect object holding it is
/// replaced in the working copy.
/// </summary>
internal static class PdfAConversionRewriter
{
    /// <summary>Checks cancellation after each 64 objects.</summary>
    private const int CancellationCheckMask = 63;

    /// <summary>Runs a fix over the document.</summary>
    /// <typeparam name="TState">The fix's state.</typeparam>
    /// <param name="session">The conversion.</param>
    /// <param name="state">The state passed to each call of the fix.</param>
    /// <param name="fix">Returns a changed copy of a dictionary, or <see langword="null"/> to leave it.</param>
    /// <returns>The number of objects changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Rewrite<TState>(PdfAConversionSession session, TState state, Func<PdfAConversionSession, TState, PdfDictionary, PdfDictionary?> fix) =>
        Rewrite(session.Store, new PdfAConversionSessionFix<TState>(session, state, fix), static (run, dictionary) => run.Fix(run.Session, run.State, dictionary), session.CancellationToken);

    /// <summary>Runs a fix over a document's objects.</summary>
    /// <typeparam name="TState">The fix's state.</typeparam>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="state">The state passed to each call of the fix.</param>
    /// <param name="fix">Returns a changed copy of a dictionary, or <see langword="null"/> to leave it.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The number of objects changed.</returns>
    internal static int Rewrite<TState>(PdfObjectStore store, TState state, Func<TState, PdfDictionary, PdfDictionary?> fix, CancellationToken cancellationToken)
    {
        var rewrite = new PdfAConversionRewrite<TState>(state, fix);
        var graph = PdfObjectGraph.Collect(store, false, cancellationToken);
        var changed = 0;
        for (var number = 1; number <= graph.Count; number++)
        {
            if ((number & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var value = graph.GetValue(number);
            var rewritten = RewriteValue(rewrite, value, 0);
            if (IsSame(rewritten, value))
            {
                continue;
            }

            StoreEditing.Replace(store, new(graph.GetOldNumber(number), 0), rewritten);
            changed++;
        }

        RefreshCatalog(store);
        return changed;
    }

    /// <summary>Replaces the catalog object and makes the store answer with the new catalog.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="catalog">The new catalog.</param>
    internal static void ReplaceCatalog(PdfObjectStore store, PdfDictionary catalog)
    {
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (root.IsReference)
        {
            StoreEditing.Replace(store, root.AsReference(), PdfValue.FromDictionary(catalog));
        }
        else
        {
            store.Trailer.Set(KnownName.Root, PdfValue.FromDictionary(catalog));
        }

        store.Catalog = catalog;
    }

    /// <summary>Points the store's catalog at the edited catalog object.</summary>
    /// <param name="store">The working copy's objects.</param>
    private static void RefreshCatalog(PdfObjectStore store)
    {
        if (StoreReading.Resolve(store, store.Trailer.GetRaw(KnownName.Root)).AsDictionary() is { } catalog)
        {
            store.Catalog = catalog;
        }
    }

    /// <summary>Determines whether a rewritten value is the value it came from.</summary>
    /// <param name="rewritten">The rewritten value.</param>
    /// <param name="value">The original.</param>
    /// <returns><see langword="true"/> when nothing changed.</returns>
    private static bool IsSame(PdfValue rewritten, PdfValue value) =>
        ReferenceEquals(rewritten.AsDictionary(), value.AsDictionary()) && ReferenceEquals(rewritten.AsArray(), value.AsArray());

    /// <summary>Rewrites a value and the dictionaries and arrays directly inside it.</summary>
    /// <typeparam name="TState">The fix's state.</typeparam>
    /// <param name="rewrite">The fix and its state.</param>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The value, or a changed copy.</returns>
    private static PdfValue RewriteValue<TState>(PdfAConversionRewrite<TState> rewrite, PdfValue value, int depth) => depth > PdfLimits.MaxNesting
        ? value
        : value.Kind switch
        {
            PdfKind.Dictionary => PdfValue.FromDictionary(RewriteDictionary(rewrite, value.AsDictionary()!, depth)),
            PdfKind.Stream => RewriteStream(rewrite, value.AsStream()!, depth),
            PdfKind.Array => PdfValue.FromArray(RewriteArray(rewrite, value.AsArray()!, depth)),
            _ => value,
        };

    /// <summary>Rewrites a stream's dictionary, keeping its data.</summary>
    /// <typeparam name="TState">The fix's state.</typeparam>
    /// <param name="rewrite">The fix and its state.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The stream, or a copy with the changed dictionary.</returns>
    private static PdfValue RewriteStream<TState>(PdfAConversionRewrite<TState> rewrite, PdfStream stream, int depth)
    {
        var dictionary = RewriteDictionary(rewrite, stream.Dictionary, depth);
        return ReferenceEquals(dictionary, stream.Dictionary) ? PdfValue.FromStream(stream) : PdfValue.FromStream(new(dictionary, stream.CopyRawData()));
    }

    /// <summary>Rewrites a dictionary: its nested values first, then the dictionary itself.</summary>
    /// <typeparam name="TState">The fix's state.</typeparam>
    /// <param name="rewrite">The fix and its state.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The dictionary, or a changed copy.</returns>
    private static PdfDictionary RewriteDictionary<TState>(PdfAConversionRewrite<TState> rewrite, PdfDictionary dictionary, int depth)
    {
        PdfDictionary? copy = null;
        for (var i = 0; i < dictionary.Count; i++)
        {
            var child = dictionary.GetValueAt(i);
            if (child.Kind is not (PdfKind.Dictionary or PdfKind.Array))
            {
                continue;
            }

            var rewritten = RewriteValue(rewrite, child, depth + 1);
            if (IsSame(rewritten, child))
            {
                continue;
            }

            copy ??= dictionary.Clone();
            copy.Set(dictionary.GetKeyAt(i), rewritten);
        }

        var current = copy ?? dictionary;
        return rewrite.Fix(rewrite.State, current) ?? current;
    }

    /// <summary>Rewrites the dictionaries and arrays inside an array.</summary>
    /// <typeparam name="TState">The fix's state.</typeparam>
    /// <param name="rewrite">The fix and its state.</param>
    /// <param name="array">The array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The array, or a changed copy.</returns>
    private static PdfArray RewriteArray<TState>(PdfAConversionRewrite<TState> rewrite, PdfArray array, int depth)
    {
        PdfArray? copy = null;
        for (var i = 0; i < array.Count; i++)
        {
            var child = array.GetRaw(i);
            if (child.Kind is not (PdfKind.Dictionary or PdfKind.Array))
            {
                continue;
            }

            var rewritten = RewriteValue(rewrite, child, depth + 1);
            if (IsSame(rewritten, child))
            {
                continue;
            }

            copy ??= array.Clone();
            copy.SetAt(i, rewritten);
        }

        return copy ?? array;
    }
}
