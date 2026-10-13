// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;

namespace HyperPdf.Cms;

/// <summary>
/// The prime field of an Edwards curve and the constants its fixed-size arithmetic needs. Numbers are little-endian
/// arrays of 64-bit limbs, and products use Montgomery multiplication (Montgomery, "Modular multiplication without
/// trial division", 1985; Handbook of Applied Cryptography, algorithm 14.36), so every field operation works in place
/// on caller-supplied storage. The constants are made once with <see cref="BigInteger"/>.
/// </summary>
/// <param name="Limbs">How many 64-bit limbs a field element takes.</param>
/// <param name="Prime">The prime.</param>
/// <param name="Inverse">The negated inverse of the prime's lowest limb modulo 2^64.</param>
/// <param name="RSquared">2^(128 * limbs) modulo the prime, which moves a number into Montgomery form.</param>
/// <param name="One">The number one in Montgomery form.</param>
/// <param name="InverseExponent">The prime minus two, the exponent of Fermat's inverse.</param>
/// <param name="RootExponent">(p + 1) / 4 for a prime that is 3 mod 4, (p + 3) / 8 for one that is 5 mod 8 (RFC 8032, 5.1.3 and 5.2.3).</param>
/// <param name="RootOfMinusOne">A square root of -1 in Montgomery form for a prime that is 5 mod 8; empty for one that is 3 mod 4.</param>
[DebuggerDisplay("EdwardsField: {Limbs} limbs")]
internal sealed record EdwardsField(int Limbs, ulong[] Prime, ulong Inverse, ulong[] RSquared, ulong[] One, ulong[] InverseExponent, ulong[] RootExponent, ulong[] RootOfMinusOne)
{
    /// <summary>The most limbs a field element takes: Ed448's 448-bit prime.</summary>
    internal const int MaxLimbs = 7;

    /// <summary>The bits in a limb.</summary>
    private const int LimbBits = 64;

    /// <summary>The bytes in a limb.</summary>
    private const int LimbBytes = 8;

    /// <summary>Newton steps that lift an inverse correct to three bits to all 64.</summary>
    private const int NewtonSteps = 5;

    /// <summary>The modulus that tells a 3 mod 4 prime.</summary>
    private const int FourModulus = 4;

    /// <summary>The modulus that tells a 5 mod 8 prime.</summary>
    private const int EightModulus = 8;

    /// <summary>The residue of a prime that is 3 mod 4.</summary>
    private const int ThreeModFour = 3;

    /// <summary>The number two: Fermat's inverse exponent is the prime minus two, and 2 is a non-residue to root -1 from.</summary>
    private const int Two = 2;

    /// <summary>Makes the field of a prime.</summary>
    /// <param name="prime">The odd prime, below 2^448.</param>
    /// <returns>The field.</returns>
    internal static EdwardsField Create(BigInteger prime)
    {
        var limbs = (int)((prime.GetBitLength() + LimbBits - 1) / LimbBits);
        var radix = BigInteger.One << (LimbBits * limbs);
        var one = radix % prime;
        var primeLimbs = ToLimbs(prime, limbs);
        var threeModFour = (int)(prime % FourModulus) == ThreeModFour;
        var rootExponent = threeModFour ? (prime + 1) / FourModulus : (prime + ThreeModFour) / EightModulus;
        var rootOfMinusOne = threeModFour ? [] : ToLimbs(BigInteger.ModPow(Two, (prime - 1) / FourModulus, prime) * radix % prime, limbs);
        return new(limbs, primeLimbs, NegatedInverse(primeLimbs[0]), ToLimbs(one * one % prime, limbs), ToLimbs(one, limbs), ToLimbs(prime - Two, limbs), ToLimbs(rootExponent, limbs), rootOfMinusOne);
    }

    /// <summary>Moves a number into Montgomery form.</summary>
    /// <param name="value">The number.</param>
    /// <param name="prime">The field prime.</param>
    /// <returns>The limbs of the number times 2^(64 * limbs) modulo the prime.</returns>
    internal static ulong[] ToMontgomeryLimbs(BigInteger value, BigInteger prime)
    {
        var limbs = (int)((prime.GetBitLength() + LimbBits - 1) / LimbBits);
        return ToLimbs(value % prime * (BigInteger.One << (LimbBits * limbs)) % prime, limbs);
    }

    /// <summary>Splits a non-negative number into limbs.</summary>
    /// <param name="value">The number.</param>
    /// <param name="limbs">How many limbs to make.</param>
    /// <returns>The limbs, lowest first.</returns>
    internal static ulong[] ToLimbs(BigInteger value, int limbs)
    {
        var bytes = new byte[limbs * LimbBytes];
        _ = value.TryWriteBytes(bytes, out _, true);
        var result = new ulong[limbs];
        for (var i = 0; i < limbs; i++)
        {
            result[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(i * LimbBytes));
        }

        return result;
    }

    /// <summary>Works out -1 / lowest limb modulo 2^64 with Newton's method; the limb of an odd prime is odd.</summary>
    /// <param name="lowest">The prime's lowest limb.</param>
    /// <returns>The negated inverse.</returns>
    private static ulong NegatedInverse(ulong lowest)
    {
        // An odd number is its own inverse modulo 8, then each step doubles the correct bits.
        var inverse = lowest;
        for (var i = 0; i < NewtonSteps; i++)
        {
            inverse = unchecked(inverse * (Two - (lowest * inverse)));
        }

        return unchecked(0UL - inverse);
    }
}
