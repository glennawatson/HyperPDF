// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Compares PDF objects by content, ignoring whether they are direct or indirect and how streams are compressed, as
/// ISO 19005-2 6.2.4.4 asks for Separation spaces. Numbers compare by value; streams by their decoded data and their
/// dictionaries without <c>/Length</c>, <c>/Filter</c> and <c>/DecodeParms</c>.
/// </summary>
internal static class PdfAEquivalence
{
    /// <summary>The deepest the comparison goes, to end cycles.</summary>
    private const int MaxDepth = 32;

    /// <summary>Determines whether two values are equivalent.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AreEquivalent(PdfAColourState state, PdfValue left, PdfValue right) => AreEquivalent(state, left, right, 0);

    /// <summary>Determines whether two values are equivalent.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool AreEquivalent(PdfAColourState state, PdfValue left, PdfValue right, int depth)
    {
        if (depth >= MaxDepth)
        {
            // A cycle compares as equal, as veraPDF treats a pair it is already comparing.
            return true;
        }

        left = StoreReading.Resolve(state.Store, left);
        right = StoreReading.Resolve(state.Store, right);
        if (left.IsNumber || right.IsNumber)
        {
            // Numbers compare by exact value, whether written as integers or reals.
            return left.IsNumber && right.IsNumber && left.AsNumber().CompareTo(right.AsNumber()) == 0;
        }

        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            PdfKind.Array => ArraysEquivalent(state, left.AsArray()!, right.AsArray()!, depth),
            PdfKind.Dictionary => DictionariesEquivalent(state, left.AsDictionary()!, right.AsDictionary()!, depth, false),
            PdfKind.Stream => StreamsEquivalent(state, left.AsStream()!, right.AsStream()!, depth),
            PdfKind.String => left.AsStringBytes().SequenceEqual(right.AsStringBytes()),
            _ => left == right,
        };
    }

    /// <summary>Compares arrays element by element.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they are equivalent.</returns>
    private static bool ArraysEquivalent(PdfAColourState state, PdfArray left, PdfArray right, int depth)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!AreEquivalent(state, left.GetRaw(i), right.GetRaw(i), depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares dictionaries by their entries with non-null values.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="left">The first dictionary.</param>
    /// <param name="right">The second dictionary.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="isStream">Whether they are stream dictionaries, whose length and filter entries are ignored.</param>
    /// <returns><see langword="true"/> when they are equivalent.</returns>
    private static bool DictionariesEquivalent(PdfAColourState state, PdfDictionary left, PdfDictionary right, int depth, bool isStream) =>
        CoveredBy(state, left, right, depth, isStream) && CoveredBy(state, right, left, depth, isStream);

    /// <summary>Checks that every entry of one dictionary has an equivalent entry in another.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="source">The dictionary whose entries are checked.</param>
    /// <param name="other">The dictionary they are looked up in.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="isStream">Whether they are stream dictionaries.</param>
    /// <returns><see langword="true"/> when every entry matches.</returns>
    private static bool CoveredBy(PdfAColourState state, PdfDictionary source, PdfDictionary other, int depth, bool isStream)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (isStream && key.ToKnownName() is KnownName.Length or KnownName.Filter or KnownName.DecodeParms)
            {
                continue;
            }

            var value = source.GetValueAt(i);
            if (StoreReading.Resolve(state.Store, value).IsNull)
            {
                continue;
            }

            if (!AreEquivalent(state, value, other.GetRaw(key), depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares streams by decoded data and dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="left">The first stream.</param>
    /// <param name="right">The second stream.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they are equivalent.</returns>
    private static bool StreamsEquivalent(PdfAColourState state, PdfStream left, PdfStream right, int depth)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        var first = default(PooledBuffer);
        var second = default(PooledBuffer);
        try
        {
            var token = state.Context.CancellationToken;
            var leftDecoded = PdfAStreams.TryDecode(left, ref first, token);
            var rightDecoded = PdfAStreams.TryDecode(right, ref second, token);
            return leftDecoded && rightDecoded && first.WrittenSpan.SequenceEqual(second.WrittenSpan)
                && DictionariesEquivalent(state, left.Dictionary, right.Dictionary, depth, true);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }
}
