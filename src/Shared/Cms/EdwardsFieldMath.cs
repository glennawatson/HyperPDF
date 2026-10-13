// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Cms;

/// <summary>
/// Arithmetic in the prime field of an Edwards curve on caller-supplied limbs. Elements are kept in Montgomery form
/// and fully reduced, and every operation may write its result over either input. Nothing here allocates, and nothing
/// runs in constant time, so it must never touch a private key.
/// </summary>
internal static class EdwardsFieldMath
{
    /// <summary>The extra limbs the multiplication accumulator needs beyond a field element.</summary>
    private const int AccumulatorExtra = 2;

    /// <summary>Multiplies two elements (Handbook of Applied Cryptography, algorithm 14.36, finely integrated operand scanning).</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The first element.</param>
    /// <param name="y">The second element.</param>
    /// <param name="result">Receives <paramref name="x"/> times <paramref name="y"/>.</param>
    internal static void Multiply(EdwardsField field, ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y, Span<ulong> result)
    {
        var n = field.Limbs;
        var prime = field.Prime;
        Span<ulong> t = stackalloc ulong[EdwardsField.MaxLimbs + AccumulatorExtra];
        t.Clear();
        for (var i = 0; i < n; i++)
        {
            var carry = AddProducts(x, y[i], t, n);
            var top = unchecked(t[n] + carry);
            t[n + 1] = top < carry ? 1UL : 0UL;
            t[n] = top;
            ReduceStep(prime, field.Inverse, t, n);
        }

        FinishMultiply(prime, t, n, result);
    }

    /// <summary>Adds two elements.</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The first element.</param>
    /// <param name="y">The second element.</param>
    /// <param name="result">Receives the sum.</param>
    internal static void Add(EdwardsField field, ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y, Span<ulong> result)
    {
        var carry = EdwardsLimbs.Add(result, x, y);
        if (carry != 0 || EdwardsLimbs.Compare(result, field.Prime) >= 0)
        {
            _ = EdwardsLimbs.Subtract(result, result, field.Prime);
        }
    }

    /// <summary>Subtracts an element from another.</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The element to subtract from.</param>
    /// <param name="y">The element to subtract.</param>
    /// <param name="result">Receives the difference.</param>
    internal static void Subtract(EdwardsField field, ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y, Span<ulong> result)
    {
        if (EdwardsLimbs.Subtract(result, x, y) != 0)
        {
            _ = EdwardsLimbs.Add(result, result, field.Prime);
        }
    }

    /// <summary>Raises an element to a power by square and multiply, from the exponent's top bit.</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The element.</param>
    /// <param name="exponent">The exponent, as limbs.</param>
    /// <param name="result">Receives the power.</param>
    internal static void Power(EdwardsField field, ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> exponent, Span<ulong> result)
    {
        Span<ulong> accumulator = stackalloc ulong[EdwardsField.MaxLimbs];
        var power = accumulator[..field.Limbs];
        field.One.CopyTo(power);
        for (var bit = EdwardsLimbs.TopBit(exponent); bit >= 0; bit--)
        {
            Multiply(field, power, power, power);
            if (EdwardsLimbs.IsSet(exponent, bit))
            {
                Multiply(field, power, x, power);
            }
        }

        power.CopyTo(result);
    }

    /// <summary>Takes the inverse of an element by Fermat's little theorem; zero has the inverse zero.</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The element.</param>
    /// <param name="result">Receives the inverse.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Invert(EdwardsField field, ReadOnlySpan<ulong> x, Span<ulong> result) => Power(field, x, field.InverseExponent, result);

    /// <summary>Moves a number out of Montgomery form.</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The element.</param>
    /// <param name="result">Receives the plain number.</param>
    internal static void FromMontgomery(EdwardsField field, ReadOnlySpan<ulong> x, Span<ulong> result)
    {
        Span<ulong> one = stackalloc ulong[EdwardsField.MaxLimbs];
        one.Clear();
        one[0] = 1;
        Multiply(field, x, one[..field.Limbs], result);
    }

    /// <summary>Takes a square root, for a prime that is 3 mod 4 (Ed448) or 5 mod 8 (Ed25519) (RFC 8032, 5.1.3 and 5.2.3).</summary>
    /// <param name="field">The field.</param>
    /// <param name="x">The element.</param>
    /// <param name="root">Receives one of its roots.</param>
    /// <returns><see langword="true"/> when the element is a square.</returns>
    internal static bool TrySquareRoot(EdwardsField field, ReadOnlySpan<ulong> x, Span<ulong> root)
    {
        Span<ulong> storage = stackalloc ulong[EdwardsField.MaxLimbs];
        var squared = storage[..field.Limbs];
        Power(field, x, field.RootExponent, root);
        Multiply(field, root, root, squared);
        if (squared.SequenceEqual(x))
        {
            return true;
        }

        if (field.RootOfMinusOne.Length == 0)
        {
            return false;
        }

        // A prime that is 5 mod 8 has a second candidate: the first one times a square root of -1.
        Multiply(field, root, field.RootOfMinusOne, root);
        Multiply(field, root, root, squared);
        return squared.SequenceEqual(x);
    }

    /// <summary>Adds an element times one limb into the accumulator.</summary>
    /// <param name="x">The element.</param>
    /// <param name="multiplier">The limb.</param>
    /// <param name="t">The accumulator.</param>
    /// <param name="n">The limbs in an element.</param>
    /// <returns>The carry out of the element's top limb.</returns>
    private static ulong AddProducts(ReadOnlySpan<ulong> x, ulong multiplier, Span<ulong> t, int n)
    {
        ulong carry = 0;
        for (var j = 0; j < n; j++)
        {
            carry = MultiplyAdd(x[j], multiplier, t[j], carry, out var low);
            t[j] = low;
        }

        return carry;
    }

    /// <summary>Adds the multiple of the prime that clears the accumulator's lowest limb, then shifts the accumulator down a limb.</summary>
    /// <param name="prime">The prime.</param>
    /// <param name="inverse">The negated inverse of the prime's lowest limb.</param>
    /// <param name="t">The accumulator.</param>
    /// <param name="n">The limbs in an element.</param>
    private static void ReduceStep(ReadOnlySpan<ulong> prime, ulong inverse, Span<ulong> t, int n)
    {
        var multiple = unchecked(t[0] * inverse);
        var carry = MultiplyAdd(multiple, prime[0], t[0], 0, out _);
        for (var j = 1; j < n; j++)
        {
            carry = MultiplyAdd(multiple, prime[j], t[j], carry, out var low);
            t[j - 1] = low;
        }

        var sum = unchecked(t[n] + carry);
        t[n - 1] = sum;
        t[n] = unchecked(t[n + 1] + (sum < carry ? 1UL : 0UL));
    }

    /// <summary>Takes the accumulator, below twice the prime, into the field.</summary>
    /// <param name="prime">The prime.</param>
    /// <param name="t">The accumulator.</param>
    /// <param name="n">The limbs in an element.</param>
    /// <param name="result">Receives the reduced element.</param>
    private static void FinishMultiply(ReadOnlySpan<ulong> prime, Span<ulong> t, int n, Span<ulong> result)
    {
        var value = t[..n];
        if (t[n] != 0 || EdwardsLimbs.Compare(value, prime) >= 0)
        {
            _ = EdwardsLimbs.Subtract(value, value, prime);
        }

        value.CopyTo(result);
    }

    /// <summary>Computes <paramref name="a"/> * <paramref name="b"/> + <paramref name="c"/> + <paramref name="d"/> as a 128-bit number.</summary>
    /// <param name="a">The first factor.</param>
    /// <param name="b">The second factor.</param>
    /// <param name="c">The first addend.</param>
    /// <param name="d">The second addend.</param>
    /// <param name="low">Receives the low 64 bits.</param>
    /// <returns>The high 64 bits; the sum never overflows 128 bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MultiplyAdd(ulong a, ulong b, ulong c, ulong d, out ulong low)
    {
        var high = Math.BigMul(a, b, out var product);
        product = unchecked(product + c);
        high += product < c ? 1UL : 0UL;
        product = unchecked(product + d);
        high += product < d ? 1UL : 0UL;
        low = product;
        return high;
    }
}
