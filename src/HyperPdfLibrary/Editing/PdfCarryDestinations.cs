// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using ByteArrayComparer = HyperPdfLibrary.Objects.ByteArrayComparer;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Retargets the destinations of copied annotations and outline entries. An explicit destination is kept when its page was
/// copied and dropped when not. A named destination that leads to a copied page moves to the target's <c>/Names /Dests</c>
/// name tree, renamed when the target already has the name; its users then name the copy. Names always become strings,
/// which only the name tree can resolve.
/// </summary>
[DebuggerDisplay("PdfCarryDestinations: {_added.Count} named")]
internal sealed class PdfCarryDestinations
{
    /// <summary>The most actions read in one chain, so a looping or branching <c>/Next</c> chain ends.</summary>
    private const int MaxChainedActions = 256;

    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>The new name of each source name that was carried; an empty array for one that was dropped.</summary>
    private readonly Dictionary<byte[], byte[]> _names = [with(ByteArrayComparer.Instance)];

    /// <summary>The named destinations carried, with their new names.</summary>
    private readonly List<NameTreeEntry> _added = [];

    /// <summary>The target's destination names, plus those carried; made on first use.</summary>
    private HashSet<string>? _used;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryDestinations"/> class.</summary>
    /// <param name="context">The context.</param>
    internal PdfCarryDestinations(PdfCarryContext context) => _context = context;

    /// <summary>Retargets the destination, action and additional actions of a dictionary the caller owns.</summary>
    /// <param name="holder">A copy of an annotation or outline entry, changed in place.</param>
    /// <returns><see langword="true"/> when it had any of them.</returns>
    internal bool Fix(PdfDictionary holder)
    {
        var touched = false;
        if (holder.ContainsKey(KnownName.Dest))
        {
            holder.Set(KnownName.Dest, Rewrite(holder.GetRaw(KnownName.Dest)));
            touched = true;
        }

        if (holder.ContainsKey(KnownName.A))
        {
            holder.Set(KnownName.A, RewriteAction(holder.GetRaw(KnownName.A)));
            touched = true;
        }

        if (holder.ContainsKey(KnownName.AA))
        {
            holder.Set(KnownName.AA, RewriteAdditional(holder.GetRaw(KnownName.AA)));
            touched = true;
        }

        return touched;
    }

    /// <summary>Retargets a destination.</summary>
    /// <param name="raw">The destination as stored in the source: an array, a name or a string.</param>
    /// <returns>The value to copy, or null when the destination does not lead to a copied page.</returns>
    internal PdfValue Rewrite(PdfValue raw)
    {
        var value = StoreReading.Resolve(_context.Source, raw);
        return value.Kind switch
        {
            PdfKind.Array => IsLive(value.AsArray()!) ? raw : default,
            PdfKind.Name => RewriteName(_context.Source.Names.GetSpelling(value.AsName()).ToArray()),
            PdfKind.String => RewriteName(value.AsStringBytes().ToArray()),
            _ => default,
        };
    }

    /// <summary>
    /// Retargets an action and the actions that follow it in its <c>/Next</c> chain (ISO 32000-2 12.6.2): a go-to action
    /// follows its destination, any other action is kept, and a chained go-to action that leads nowhere is removed.
    /// </summary>
    /// <param name="raw">The action as stored in the source.</param>
    /// <returns>The value to copy, or null for a go-to action that does not lead to a copied page.</returns>
    internal PdfValue RewriteAction(PdfValue raw)
    {
        var budget = MaxChainedActions;
        return RewriteAction(raw, ref budget);
    }

    /// <summary>Adds the carried named destinations to the target's name tree.</summary>
    internal void Finish()
    {
        if (_added.Count == 0)
        {
            return;
        }

        var names = _context.Names;
        var merged = PdfNameTreeMerge.Merge(_context.TargetStore, names.GetDictionary(KnownName.Dests), _added);
        _context.SetEntry(names, KnownName.Dests, merged);
    }

    /// <summary>Uses one unit of a chain's budget.</summary>
    /// <param name="budget">The chained actions still allowed.</param>
    /// <returns><see langword="false"/> when none was left.</returns>
    private static bool TakeBudget(ref int budget)
    {
        if (budget <= 0)
        {
            return false;
        }

        budget--;
        return true;
    }

    /// <summary>Retargets an action and its chain.</summary>
    /// <param name="raw">The action as stored in the source.</param>
    /// <param name="budget">The chained actions still allowed; a chain that exceeds it loses the rest.</param>
    /// <returns>The value to copy, or null for a go-to action that does not lead to a copied page.</returns>
    private PdfValue RewriteAction(PdfValue raw, ref int budget)
    {
        if (StoreReading.Resolve(_context.Source, raw).AsDictionary() is not { } action)
        {
            return raw;
        }

        PdfDictionary? copy = null;
        if (action.IsName(KnownName.S, KnownName.GoTo))
        {
            var destination = Rewrite(action.GetRaw(KnownName.D));
            if (destination.IsNull)
            {
                return default;
            }

            copy = action.Clone();
            copy.Set(KnownName.D, destination);
        }

        if (action.ContainsKey(KnownName.Next))
        {
            var next = action.GetRaw(KnownName.Next);
            var rewritten = TakeBudget(ref budget) ? RewriteChain(next, ref budget) : default;
            if (rewritten != next)
            {
                copy ??= action.Clone();
                _ = copy.Remove(KnownName.Next);
                if (!rewritten.IsNull)
                {
                    copy.Set(KnownName.Next, rewritten);
                }
            }
        }

        return copy is null ? raw : PdfValue.FromDictionary(copy);
    }

    /// <summary>Retargets what an action's <c>/Next</c> names: one action or an array of them.</summary>
    /// <param name="next">The entry as stored in the source.</param>
    /// <param name="budget">The chained actions still allowed.</param>
    /// <returns>The entry itself when nothing changed, the value to copy otherwise, or null when no action is left.</returns>
    private PdfValue RewriteChain(PdfValue next, ref int budget)
    {
        if (StoreReading.Resolve(_context.Source, next).AsArray() is not { } list)
        {
            return RewriteAction(next, ref budget);
        }

        var kept = new PdfArray(list.Owner, list.Count);
        var changed = false;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list.GetRaw(i);
            var rewritten = TakeBudget(ref budget) ? RewriteAction(entry, ref budget) : default;
            changed |= rewritten != entry;
            if (!rewritten.IsNull)
            {
                kept.Add(rewritten);
            }
        }

        if (!changed)
        {
            return next;
        }

        return kept.Count == 0 ? default : PdfValue.FromArray(kept);
    }

    /// <summary>Determines whether a destination array names a page that is being copied.</summary>
    /// <param name="destination">The array.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private bool IsLive(PdfArray destination)
    {
        var page = destination.GetRaw(0);
        return page.IsReference && _context.IsCopiedPage(page.AsReference().Number);
    }

    /// <summary>Retargets an additional-actions dictionary, dropping the actions that lead nowhere.</summary>
    /// <param name="raw">The dictionary as stored in the source.</param>
    /// <returns>The value to copy.</returns>
    private PdfValue RewriteAdditional(PdfValue raw)
    {
        if (StoreReading.Resolve(_context.Source, raw).AsDictionary() is not { } triggers)
        {
            return raw;
        }

        var copy = new PdfDictionary(triggers.Owner, triggers.Count);
        for (var i = 0; i < triggers.Count; i++)
        {
            var action = RewriteAction(triggers.GetValueAt(i));
            if (!action.IsNull)
            {
                copy.Add(triggers.GetKeyAt(i), action);
            }
        }

        return PdfValue.FromDictionary(copy);
    }

    /// <summary>Carries a named destination.</summary>
    /// <param name="key">The name's bytes.</param>
    /// <returns>The destination's new name as a string, or null when it does not lead to a copied page.</returns>
    private PdfValue RewriteName(byte[] key)
    {
        if (_names.TryGetValue(key, out var known))
        {
            return known.Length == 0 ? default : PdfValue.FromString(known);
        }

        var destination = FindSource(key);
        var target = destination.AsDictionary() is { } wrapper ? wrapper.Get(KnownName.D) : destination;
        if (target.AsArray() is not { } array || !IsLive(array))
        {
            _names[key] = [];
            return default;
        }

        var name = PdfCarryText.Unique(key, GetUsed());
        _names[key] = name;
        _added.Add(new(PdfValue.FromString(name), _context.Sink.Import(destination)));
        return PdfValue.FromString(name);
    }

    /// <summary>Finds a named destination in the source: the name tree first, then the catalog's dictionary.</summary>
    /// <param name="key">The name's bytes.</param>
    /// <returns>The destination array or dictionary, or null.</returns>
    private PdfValue FindSource(byte[] key)
    {
        var source = _context.Source;
        var catalog = source.Catalog;
        var found = StoreReading.Resolve(source, NameTree.Find(catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.Dests), key));
        return found.IsNull && catalog.GetDictionary(KnownName.Dests) is { } dests ? dests.Get(source.Names.Intern(key)) : found;
    }

    /// <summary>Gets the destination names the target uses, in its name tree and its catalog dictionary.</summary>
    /// <returns>The names, by <see cref="PdfCarryText.Key"/>; new names are added to it as they are chosen.</returns>
    private HashSet<string> GetUsed()
    {
        if (_used is not null)
        {
            return _used;
        }

        var catalog = _context.Catalog;
        _used = PdfNameTreeMerge.GetNames(catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.Dests));
        if (catalog.GetDictionary(KnownName.Dests) is { } legacy && _context.TargetStore is { } store)
        {
            for (var i = 0; i < legacy.Count; i++)
            {
                _ = _used.Add(PdfCarryText.Key(store.Names.GetSpelling(legacy.GetKeyAt(i))));
            }
        }

        return _used;
    }
}
