// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The symbols of the dictionaries being written, in dictionary order: by height, then width, which is how a symbol dictionary
/// must list them (T.88 section 6.5.5). The base symbols come first and go in a plain dictionary. Repeated variants, near copies
/// of a base symbol that occur more than once with the same bitmap, follow and go in a second dictionary coded as refinements of
/// base symbols. A dictionary of one symbol gets a blank 1 by 1 symbol first, because a one-symbol text region would use
/// zero-bit symbol IDs, which decoders do not agree on. The value holds rented arrays; copies share them and only the
/// owner that built it disposes. The default value holds no symbols.
/// </summary>
[DebuggerDisplay("Jbig2EncoderSymbols: {BaseCount} base, {Count} in all")]
internal readonly record struct Jbig2EncoderSymbols : IDisposable
{
    /// <summary>The fewest symbols a dictionary gets, so a text region never needs zero-bit symbol IDs.</summary>
    private const int MinimumSymbols = 2;

    /// <summary>The fewest times a near copy must occur before it is worth a place in the refined dictionary.</summary>
    private const int RepeatedVariant = 2;

    /// <summary>The bits that hold a symbol's width in a sort key.</summary>
    private const int WidthBits = 20;

    /// <summary>The bits that hold a symbol's number in a sort key.</summary>
    private const int NumberBits = 24;

    /// <summary>The blank symbol added to a one-symbol dictionary.</summary>
    private static readonly byte[] BlankRows = [0];

    /// <summary>The component of each symbol, or -1 for the blank symbol.</summary>
    private readonly int[] _component;

    /// <summary>The base symbol each variant refines, or -1 for a base symbol.</summary>
    private readonly int[] _reference;

    /// <summary>The dictionary position of each symbol component, indexed by component number.</summary>
    private readonly int[] _rankOfComponent;

    /// <summary>The components the symbols were built from.</summary>
    private readonly Jbig2Components _components;

    /// <summary>The symbol choices for the components.</summary>
    private readonly Jbig2SymbolMatcher _matcher;

    /// <summary>Initializes a new instance of the <see cref="Jbig2EncoderSymbols"/> struct.</summary>
    /// <param name="components">The components.</param>
    /// <param name="matcher">The matches.</param>
    private Jbig2EncoderSymbols(in Jbig2Components components, in Jbig2SymbolMatcher matcher)
    {
        _components = components;
        _matcher = matcher;
        var distinct = matcher.ExemplarCount;
        var variants = CountVariants(components, matcher);
        BaseCount = distinct == 1 && variants == 0 ? MinimumSymbols : distinct;
        Count = BaseCount + variants;
        _component = ArrayPool<int>.Shared.Rent(Count);
        _reference = ArrayPool<int>.Shared.Rent(Count);
        _rankOfComponent = ArrayPool<int>.Shared.Rent(components.Count);
        _reference.AsSpan(0, Count).Fill(-1);
        var keys = ArrayPool<long>.Shared.Rent(Math.Max(distinct, variants));
        try
        {
            OrderBase(keys);
            OrderVariants(keys);
        }
        finally
        {
            ArrayPool<long>.Shared.Return(keys);
        }
    }

    /// <summary>Gets the number of symbols in the base dictionary.</summary>
    internal int BaseCount { get; }

    /// <summary>Gets the number of symbols in both dictionaries.</summary>
    internal int Count { get; }

    /// <summary>Returns the rented arrays.</summary>
    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_component);
        ArrayPool<int>.Shared.Return(_reference);
        ArrayPool<int>.Shared.Return(_rankOfComponent);
    }

    /// <summary>Lists the symbols a matcher chose in dictionary order.</summary>
    /// <param name="components">The components.</param>
    /// <param name="matcher">The matches.</param>
    /// <returns>The symbols.</returns>
    internal static Jbig2EncoderSymbols Build(in Jbig2Components components, in Jbig2SymbolMatcher matcher) => new(components, matcher);

    /// <summary>Gets the width of a symbol.</summary>
    /// <param name="rank">The symbol's dictionary position.</param>
    /// <returns>The width in pixels.</returns>
    internal int Width(int rank) => _component[rank] < 0 ? 1 : _components.Width(_component[rank]);

    /// <summary>Gets the height of a symbol.</summary>
    /// <param name="rank">The symbol's dictionary position.</param>
    /// <returns>The height in pixels.</returns>
    internal int Height(int rank) => _component[rank] < 0 ? 1 : _components.Height(_component[rank]);

    /// <summary>Gets the packed rows of a symbol.</summary>
    /// <param name="rank">The symbol's dictionary position.</param>
    /// <returns>The rows, 1 for black.</returns>
    internal ReadOnlySpan<byte> Rows(int rank) => _component[rank] < 0 ? BlankRows : _components.Rows(_component[rank]);

    /// <summary>Gets the base symbol a variant is coded as a refinement of.</summary>
    /// <param name="rank">The variant's dictionary position.</param>
    /// <returns>The base symbol's position, or -1 for a base symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ReferenceOf(int rank) => _reference[rank];

    /// <summary>Gets the symbol that stands for a component, and whether the component must be refined against it.</summary>
    /// <param name="component">The symbol component.</param>
    /// <param name="refined">Receives whether the instance is only a near copy of the symbol and is coded as a refinement.</param>
    /// <returns>The dictionary position of the symbol to place, or the one to refine.</returns>
    internal int RankOfComponent(int component, out bool refined)
    {
        var variant = _matcher.VariantOf(component);
        if (variant >= 0 && _matcher.VariantCount(variant) >= RepeatedVariant)
        {
            refined = false;
            return _rankOfComponent[variant];
        }

        refined = variant >= 0;
        return _rankOfComponent[_matcher.ExemplarOf(component)];
    }

    /// <summary>Counts the variants that occur often enough for the refined dictionary.</summary>
    /// <param name="components">The components.</param>
    /// <param name="matcher">The matches.</param>
    /// <returns>The count.</returns>
    private static int CountVariants(in Jbig2Components components, in Jbig2SymbolMatcher matcher)
    {
        var count = 0;
        for (var i = 0; i < components.Count; i++)
        {
            count += components.IsSymbol(i) && matcher.VariantOf(i) == i && matcher.VariantCount(i) >= RepeatedVariant ? 1 : 0;
        }

        return count;
    }

    /// <summary>Builds the sort key of a symbol: height, then width, then its number.</summary>
    /// <param name="component">The component.</param>
    /// <param name="number">The symbol's number among those sorted.</param>
    /// <returns>The key.</returns>
    private long Key(int component, int number) =>
        ((long)_components.Height(component) << (WidthBits + NumberBits)) | ((long)_components.Width(component) << NumberBits) | (uint)number;

    /// <summary>Sorts the matcher's base symbols into dictionary order and records each one's position.</summary>
    /// <param name="keys">Working space for the sort keys.</param>
    private void OrderBase(long[] keys)
    {
        var distinct = _matcher.ExemplarCount;
        var offset = BaseCount - distinct;
        var sorted = _component.AsSpan(offset, distinct);
        for (var i = 0; i < distinct; i++)
        {
            sorted[i] = _matcher.Exemplar(i);
            keys[i] = Key(sorted[i], i);
        }

        keys.AsSpan(0, distinct).Sort(sorted);
        if (offset > 0)
        {
            _component[0] = -1;
        }

        for (var i = 0; i < distinct; i++)
        {
            _rankOfComponent[_component[offset + i]] = offset + i;
        }
    }

    /// <summary>Sorts the repeated variants into dictionary order after the base symbols.</summary>
    /// <param name="keys">Working space for the sort keys.</param>
    private void OrderVariants(long[] keys)
    {
        var count = Count - BaseCount;
        if (count == 0)
        {
            return;
        }

        var sorted = _component.AsSpan(BaseCount, count);
        var number = 0;
        for (var i = 0; i < _components.Count; i++)
        {
            if (!_components.IsSymbol(i) || _matcher.VariantOf(i) != i || _matcher.VariantCount(i) < RepeatedVariant)
            {
                continue;
            }

            sorted[number] = i;
            keys[number] = Key(i, number);
            number++;
        }

        keys.AsSpan(0, count).Sort(sorted);
        for (var i = 0; i < count; i++)
        {
            var rank = BaseCount + i;
            _rankOfComponent[sorted[i]] = rank;
            _reference[rank] = _rankOfComponent[_matcher.ExemplarOf(sorted[i])];
        }
    }
}
