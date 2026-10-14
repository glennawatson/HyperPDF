// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// Chooses the symbol that stands for each symbol component. In lossless matching a component uses an earlier one with
/// exactly its bitmap. In lossy matching it also uses an earlier one whose bitmap is similar enough: the pixels black in
/// both are at least a set share of the pixels black in either, and every black pixel of each is within one pixel of a black pixel of the other.
/// </summary>
[DebuggerDisplay("Jbig2SymbolMatcher: {ExemplarCount} symbols")]
internal sealed class Jbig2SymbolMatcher : IDisposable
{
    /// <summary>The most earlier symbols compared for one component in one size bucket.</summary>
    private const int ChainLimit = 24;

    /// <summary>The smallest hash table.</summary>
    private const int MinimumTable = 16;

    /// <summary>The number of size buckets around a size that lossy matching looks in, along one axis.</summary>
    private const int SizeSpread = 1;

    /// <summary>The factor by which the hash tables exceed the symbol count.</summary>
    private const int TableFactor = 2;

    /// <summary>The multiplier that mixes a width into a size key.</summary>
    private const int WidthMix = unchecked((int)0x9E3779B1);

    /// <summary>The number of components between checks of the cancellation token.</summary>
    private const int CancellationInterval = 256;

    /// <summary>The symbol component standing for each component, or -1 for a component that is not a symbol.</summary>
    private readonly int[] _exemplarOf;

    /// <summary>The components that are symbols in the dictionary, in the order they first appeared.</summary>
    private readonly int[] _exemplars;

    /// <summary>The first symbol of each exact-shape bucket, or -1.</summary>
    private readonly int[] _exactHead;

    /// <summary>The next symbol in the exact-shape bucket of each component.</summary>
    private readonly int[] _exactNext;

    /// <summary>The first symbol of each size bucket, or -1.</summary>
    private readonly int[] _sizeHead;

    /// <summary>The next symbol in the size bucket of each component.</summary>
    private readonly int[] _sizeNext;

    /// <summary>
    /// For a component that is a near copy of its symbol, the first component with exactly its bitmap; -1 for a component
    /// whose bitmap is its symbol's. Only filled when variants are tracked.
    /// </summary>
    private readonly int[] _variantOf;

    /// <summary>For the first component of each variant, how many components have exactly its bitmap.</summary>
    private readonly int[] _variantCount;

    /// <summary>The first variant of each bucket, or -1.</summary>
    private readonly int[] _variantHead;

    /// <summary>The next variant in the bucket of each component.</summary>
    private readonly int[] _variantNext;

    /// <summary>The bucket mask of the hash tables.</summary>
    private readonly int _mask;

    /// <summary>Whether near copies of a symbol are grouped.</summary>
    private bool _trackVariants;

    /// <summary>Initializes a new instance of the <see cref="Jbig2SymbolMatcher"/> class.</summary>
    /// <param name="components">The components to match.</param>
    private Jbig2SymbolMatcher(Jbig2Components components)
    {
        var count = components.Count;
        var table = MinimumTable;
        while (table < count * TableFactor)
        {
            table <<= 1;
        }

        _mask = table - 1;
        _exemplarOf = ArrayPool<int>.Shared.Rent(count);
        _exemplars = ArrayPool<int>.Shared.Rent(count);
        _exactNext = ArrayPool<int>.Shared.Rent(count);
        _sizeNext = ArrayPool<int>.Shared.Rent(count);
        _exactHead = ArrayPool<int>.Shared.Rent(table);
        _sizeHead = ArrayPool<int>.Shared.Rent(table);
        _variantOf = ArrayPool<int>.Shared.Rent(count);
        _variantCount = ArrayPool<int>.Shared.Rent(count);
        _variantNext = ArrayPool<int>.Shared.Rent(count);
        _variantHead = ArrayPool<int>.Shared.Rent(table);
        _exactHead.AsSpan(0, table).Fill(-1);
        _sizeHead.AsSpan(0, table).Fill(-1);
        _variantHead.AsSpan(0, table).Fill(-1);
        _variantOf.AsSpan(0, count).Fill(-1);
        _variantCount.AsSpan(0, count).Clear();
    }

    /// <summary>Gets the number of distinct symbols.</summary>
    internal int ExemplarCount { get; private set; }

    /// <summary>Returns the rented arrays.</summary>
    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_exemplarOf);
        ArrayPool<int>.Shared.Return(_exemplars);
        ArrayPool<int>.Shared.Return(_exactNext);
        ArrayPool<int>.Shared.Return(_sizeNext);
        ArrayPool<int>.Shared.Return(_exactHead);
        ArrayPool<int>.Shared.Return(_sizeHead);
        ArrayPool<int>.Shared.Return(_variantOf);
        ArrayPool<int>.Shared.Return(_variantCount);
        ArrayPool<int>.Shared.Return(_variantNext);
        ArrayPool<int>.Shared.Return(_variantHead);
    }

    /// <summary>Matches every symbol component to a symbol.</summary>
    /// <param name="components">The components.</param>
    /// <param name="similarity">The least similarity for sharing a symbol, or 1 for exact matching only.</param>
    /// <param name="trackVariants">Whether near copies of a symbol are grouped by exact bitmap, so the repeated ones can be coded once.</param>
    /// <param name="cancellationToken">Cancels the match.</param>
    /// <returns>The matches.</returns>
    internal static Jbig2SymbolMatcher Match(Jbig2Components components, double similarity, bool trackVariants, CancellationToken cancellationToken)
    {
        var matcher = new Jbig2SymbolMatcher(components) { _trackVariants = trackVariants };
        try
        {
            matcher.Run(components, similarity, cancellationToken);
            return matcher;
        }
        catch
        {
            matcher.Dispose();
            throw;
        }
    }

    /// <summary>Gets the symbol component that stands for a component.</summary>
    /// <param name="component">The component.</param>
    /// <returns>The symbol component, which is the component itself when it is the first of its shape.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ExemplarOf(int component) => _exemplarOf[component];

    /// <summary>Gets a symbol component by order of first appearance.</summary>
    /// <param name="index">The symbol, below <see cref="ExemplarCount"/>.</param>
    /// <returns>The component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Exemplar(int index) => _exemplars[index];

    /// <summary>Gets the first component with exactly a near copy's bitmap.</summary>
    /// <param name="component">The component.</param>
    /// <returns>The first component with its bitmap, or -1 when its bitmap is its symbol's.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int VariantOf(int component) => _variantOf[component];

    /// <summary>Gets how many components have exactly a variant's bitmap.</summary>
    /// <param name="variant">The first component of the variant.</param>
    /// <returns>The count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int VariantCount(int variant) => _variantCount[variant];

    /// <summary>Gets the largest row length of any symbol, plus one.</summary>
    /// <param name="components">The components.</param>
    /// <returns>The bytes.</returns>
    private static int MaximumStride(Jbig2Components components)
    {
        var widest = 0;
        for (var i = 0; i < components.Count; i++)
        {
            widest = components.IsSymbol(i) ? Math.Max(widest, components.Width(i)) : widest;
        }

        return Jbig2Bits.Stride(widest) + 1;
    }

    /// <summary>Gets the hash of a symbol's size and bitmap.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <returns>The hash.</returns>
    private static int ShapeHash(Jbig2Components components, int component)
    {
        var hash = default(HashCode);
        hash.Add(components.Width(component));
        hash.Add(components.Height(component));
        hash.AddBytes(components.Rows(component));
        return hash.ToHashCode();
    }

    /// <summary>Gets the bucket key of a size.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The key.</returns>
    private static int SizeKey(int width, int height) => unchecked((width * WidthMix) ^ height);

    /// <summary>Determines whether two symbol components have exactly the same size and bitmap.</summary>
    /// <param name="components">The components.</param>
    /// <param name="first">The first component.</param>
    /// <param name="second">The second component.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool SameBitmap(Jbig2Components components, int first, int second) =>
        components.Width(first) == components.Width(second)
        && components.Height(first) == components.Height(second)
        && components.Rows(first).SequenceEqual(components.Rows(second));

    /// <summary>Gets a bitmap view of a symbol component.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <returns>The view.</returns>
    private static Jbig2BitmapView View(Jbig2Components components, int component) =>
        new(components.Rows(component), components.Width(component), components.Height(component));

    /// <summary>Determines whether two symbol components are similar enough to share a symbol.</summary>
    /// <param name="components">The components.</param>
    /// <param name="first">The component.</param>
    /// <param name="second">The earlier symbol.</param>
    /// <param name="similarity">The least similarity.</param>
    /// <param name="scratch">Working space for the covering test.</param>
    /// <returns><see langword="true"/> when they can share.</returns>
    private static bool IsSimilar(Jbig2Components components, int first, int second, double similarity, Span<byte> scratch)
    {
        long inkFirst = components.Ink(first);
        long inkSecond = components.Ink(second);

        // The pixels black in only one bitmap are at least the difference of the counts, which bounds the similarity from above.
        if (Math.Min(inkFirst, inkSecond) < similarity * Math.Max(inkFirst, inkSecond))
        {
            return false;
        }

        var a = View(components, first);
        var b = View(components, second);
        var both = Jbig2ShapeComparer.Intersection(a, b);
        return both >= similarity * (inkFirst + inkSecond - both) && Jbig2ShapeComparer.IsCovered(a, b, scratch) && Jbig2ShapeComparer.IsCovered(b, a, scratch);
    }

    /// <summary>Matches the components one by one in reading order.</summary>
    /// <param name="components">The components.</param>
    /// <param name="similarity">The least similarity for lossy sharing.</param>
    /// <param name="cancellationToken">Cancels the match.</param>
    private void Run(Jbig2Components components, double similarity, CancellationToken cancellationToken)
    {
        var lossy = similarity < 1.0;
        var scratch = ArrayPool<byte>.Shared.Rent(MaximumStride(components));
        try
        {
            for (var component = 0; component < components.Count; component++)
            {
                if (component % CancellationInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                _exemplarOf[component] = components.IsSymbol(component) ? Choose(components, component, lossy ? similarity : 1.0, scratch) : -1;
                TrackVariant(components, component);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    /// <summary>Finds an earlier near copy with exactly the component's bitmap and symbol.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <param name="bucket">The component's variant bucket.</param>
    /// <returns>The first component of the variant, or -1.</returns>
    private int FindVariant(Jbig2Components components, int component, int bucket)
    {
        for (var other = _variantHead[bucket]; other >= 0; other = _variantNext[other])
        {
            if (_exemplarOf[other] == _exemplarOf[component] && SameBitmap(components, other, component))
            {
                return other;
            }
        }

        return -1;
    }

    /// <summary>Groups a component that shares another's symbol by its exact bitmap, unless its bitmap is the symbol's own.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    private void TrackVariant(Jbig2Components components, int component)
    {
        var exemplar = _exemplarOf[component];
        if (!_trackVariants || exemplar < 0 || exemplar == component || SameBitmap(components, exemplar, component))
        {
            return;
        }

        var bucket = ShapeHash(components, component) & _mask;
        var found = FindVariant(components, component, bucket);
        if (found >= 0)
        {
            _variantOf[component] = found;
            _variantCount[found]++;
            return;
        }

        _variantOf[component] = component;
        _variantCount[component] = 1;
        _variantNext[component] = _variantHead[bucket];
        _variantHead[bucket] = component;
    }

    /// <summary>Finds the symbol that stands for a component, adding the component as a new symbol when none does.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <param name="similarity">The least similarity, 1 for exact matching.</param>
    /// <param name="scratch">Working space for the covering test.</param>
    /// <returns>The symbol component.</returns>
    private int Choose(Jbig2Components components, int component, double similarity, Span<byte> scratch)
    {
        var match = FindExact(components, component);
        if (match < 0 && similarity < 1.0)
        {
            match = FindSimilar(components, component, similarity, scratch);
        }

        if (match >= 0)
        {
            return match;
        }

        Add(components, component);
        return component;
    }

    /// <summary>Finds an earlier symbol with exactly the component's bitmap.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <returns>The symbol component, or -1.</returns>
    private int FindExact(Jbig2Components components, int component)
    {
        var rows = components.Rows(component);
        for (var other = _exactHead[ShapeHash(components, component) & _mask]; other >= 0; other = _exactNext[other])
        {
            if (components.Width(other) == components.Width(component) && components.Height(other) == components.Height(component) && components.Rows(other).SequenceEqual(rows))
            {
                return other;
            }
        }

        return -1;
    }

    /// <summary>Finds an earlier symbol similar enough to the component to share.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <param name="similarity">The least similarity.</param>
    /// <param name="scratch">Working space for the covering test.</param>
    /// <returns>The symbol component, or -1.</returns>
    private int FindSimilar(Jbig2Components components, int component, double similarity, Span<byte> scratch)
    {
        var width = components.Width(component);
        var height = components.Height(component);
        for (var dh = -SizeSpread; dh <= SizeSpread; dh++)
        {
            for (var dw = -SizeSpread; dw <= SizeSpread; dw++)
            {
                var found = FindInBucket(components, component, new(width + dw, height + dh), similarity, scratch);
                if (found >= 0)
                {
                    return found;
                }
            }
        }

        return -1;
    }

    /// <summary>Looks through the symbols of one size for one similar to the component.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    /// <param name="size">The size of the symbols to look through.</param>
    /// <param name="similarity">The least similarity.</param>
    /// <param name="scratch">Working space for the covering test.</param>
    /// <returns>The symbol component, or -1.</returns>
    private int FindInBucket(Jbig2Components components, int component, Jbig2Size size, double similarity, Span<byte> scratch)
    {
        var seen = 0;
        for (var other = _sizeHead[SizeKey(size.Width, size.Height) & _mask]; other >= 0 && seen < ChainLimit; other = _sizeNext[other])
        {
            if (components.Width(other) != size.Width || components.Height(other) != size.Height)
            {
                continue;
            }

            seen++;
            if (IsSimilar(components, component, other, similarity, scratch))
            {
                return other;
            }
        }

        return -1;
    }

    /// <summary>Adds a component as a new symbol.</summary>
    /// <param name="components">The components.</param>
    /// <param name="component">The component.</param>
    private void Add(Jbig2Components components, int component)
    {
        var exact = ShapeHash(components, component) & _mask;
        _exactNext[component] = _exactHead[exact];
        _exactHead[exact] = component;
        var size = SizeKey(components.Width(component), components.Height(component)) & _mask;
        _sizeNext[component] = _sizeHead[size];
        _sizeHead[size] = component;
        _exemplars[ExemplarCount] = component;
        ExemplarCount++;
    }
}
