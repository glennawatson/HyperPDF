// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks Note IDs against every byte-string ID in the structure hierarchy.</summary>
internal static class AccessibilityNoteIdentifiers
{
    /// <summary>Two uses suffice to establish a duplicate identifier.</summary>
    private const int DuplicateCount = 2;

    /// <summary>Leases counts without copying the strings' existing byte buffers.</summary>
    /// <param name="pools">Owns the scan's reusable storage.</param>
    /// <returns>An empty byte-content-keyed dictionary.</returns>
    internal static Dictionary<PdfValue, int> Rent(ScratchPools pools) =>
        pools.Slots<Dictionary<PdfValue, int>>().Take() ?? [with(IdentifierComparer.Instance)];

    /// <summary>Counts all IDs before checking Notes so forward collisions are also found.</summary>
    /// <param name="state">The walk's exclusively owned state.</param>
    internal static void Check(in AccessibilityStructureState state)
    {
        var identifiers = state.NoteIdentifiers!;
        var roots = state.Tree.Roots;
        for (var index = 0; index < roots.Count; index++)
        {
            Count(roots[index], identifiers, state.Cancellation);
        }

        for (var index = 0; index < roots.Count; index++)
        {
            Check(roots[index], state);
        }
    }

    /// <summary>Counts strings by their bytes, not their decoded text.</summary>
    /// <param name="element">The current element.</param>
    /// <param name="identifiers">Receives counts, saturated at two.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    private static void Count(PdfStructureElement element, Dictionary<PdfValue, int> identifiers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = element.Dictionary.Get(KnownName.ID);
        if (id.Kind == PdfKind.String)
        {
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(identifiers, id, out _);
            count = count == 0 ? 1 : DuplicateCount;
        }

        var kids = element.Kids;
        for (var index = 0; index < kids.Count; index++)
        {
            if (kids[index].Element is { } child)
            {
                Count(child, identifiers, cancellationToken);
            }
        }
    }

    /// <summary>Reports each Note whose ID is missing, malformed or repeated anywhere in the tree.</summary>
    /// <param name="element">The current element.</param>
    /// <param name="state">Holds counts and receives findings.</param>
    private static void Check(PdfStructureElement element, in AccessibilityStructureState state)
    {
        state.Cancellation.ThrowIfCancellationRequested();
        if (element.Type == PdfStructureType.Note)
        {
            var id = element.Dictionary.Get(KnownName.ID);
            if (id.Kind != PdfKind.String || state.NoteIdentifiers![id] == DuplicateCount)
            {
                AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.NoteIdentifierInvalid, element.PageIndex, element.Id);
            }
        }

        var kids = element.Kids;
        for (var index = 0; index < kids.Count; index++)
        {
            if (kids[index].Element is { } child)
            {
                Check(child, state);
            }
        }
    }

    /// <summary>Compares stored string slices by byte content without allocating keys.</summary>
    private sealed class IdentifierComparer : IEqualityComparer<PdfValue>
    {
        /// <summary>Gets the immutable byte-content comparer.</summary>
        internal static IdentifierComparer Instance { get; } = new();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(PdfValue x, PdfValue y) => x.AsStringBytes().SequenceEqual(y.AsStringBytes());

        /// <inheritdoc/>
        public int GetHashCode(PdfValue obj)
        {
            var hash = default(HashCode);
            hash.AddBytes(obj.AsStringBytes());
            return hash.ToHashCode();
        }
    }
}
