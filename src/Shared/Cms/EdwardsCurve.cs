// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Cms;

/// <summary>
/// The parameters of an Edwards curve that EdDSA (RFC 8032) uses: the curve <c>a*x^2 + y^2 = 1 + d*x^2*y^2</c> over the
/// prime field, the base point and its order, the encoded key size, and how the challenge is hashed. Field values are
/// limbs in Montgomery form; the order is plain limbs.
/// </summary>
/// <param name="Field">The prime field.</param>
/// <param name="A">The curve coefficient a.</param>
/// <param name="D">The curve coefficient d.</param>
/// <param name="Order">The order of the base point.</param>
/// <param name="BasePoint">The base point, as X, Y and Z elements.</param>
/// <param name="KeyLength">The encoded key and half-signature size in bytes.</param>
/// <param name="UsesShake">Whether the challenge hash is SHAKE256 (Ed448) rather than SHA-512 (Ed25519).</param>
[DebuggerDisplay("EdwardsCurve: {KeyLength}-byte keys")]
internal sealed record EdwardsCurve(EdwardsField Field, ulong[] A, ulong[] D, ulong[] Order, ulong[] BasePoint, int KeyLength, bool UsesShake)
{
    /// <summary>The number two: Fermat's inverse exponent is the prime minus two.</summary>
    private const int Two = 2;

    /// <summary>The Ed25519 key size in bytes (RFC 8032, 5.1).</summary>
    private const int Ed25519KeyLength = 32;

    /// <summary>The Ed448 key size in bytes (RFC 8032, 5.2).</summary>
    private const int Ed448KeyLength = 57;

    /// <summary>The Ed25519 field prime is 2^255 - 19.</summary>
    private const int Ed25519PrimeBits = 255;

    /// <summary>The Ed25519 prime's offset from the power of two.</summary>
    private const int Ed25519PrimeOffset = 19;

    /// <summary>The numerator of Ed25519's d, -121665 / 121666.</summary>
    private const int Ed25519DNumerator = 121_665;

    /// <summary>The denominator of Ed25519's d.</summary>
    private const int Ed25519DDenominator = 121_666;

    /// <summary>The Ed25519 base point's y is 4/5.</summary>
    private const int Ed25519BaseYNumerator = 4;

    /// <summary>The denominator of the Ed25519 base point's y.</summary>
    private const int Ed25519BaseYDenominator = 5;

    /// <summary>The Ed25519 group order minus 2^252 (RFC 8032, 5.1).</summary>
    private const string Ed25519OrderOffset = "27742317777372353535851937790883648493";

    /// <summary>The bit that Ed25519's group order is a power of two plus an offset: 2^252.</summary>
    private const int Ed25519OrderBits = 252;

    /// <summary>The Ed448 field prime is 2^448 - 2^224 - 1.</summary>
    private const int Ed448PrimeBits = 448;

    /// <summary>The middle power of the Ed448 prime.</summary>
    private const int Ed448PrimeMiddleBits = 224;

    /// <summary>The magnitude of the Ed448 coefficient d, which is negative (RFC 8032, 5.2).</summary>
    private const int Ed448D = 39_081;

    /// <summary>The power of two the Ed448 group order is an offset below.</summary>
    private const int Ed448OrderBits = 446;

    /// <summary>The amount the Ed448 group order lies below that power of two (RFC 8032, 5.2).</summary>
    private const string Ed448OrderOffset = "13818066809895115352007386748515426880336692474882178609894547503885";

    /// <summary>The Ed448 base point's x (RFC 8032, 5.2).</summary>
    private const string Ed448BaseX = "224580040295924300187604334099896036246789641632564134246125461686950415467406032909029192869357953282578032075146446173674602635247710";

    /// <summary>The Ed448 base point's y (RFC 8032, 5.2).</summary>
    private const string Ed448BaseY = "298819210078481492676017930443930673437544040154080242095928241372331506189835876003536878655418784733982303233503462500531545062832660";

    /// <summary>Gets the Ed25519 curve of RFC 8032, 5.1.</summary>
    internal static EdwardsCurve Ed25519 { get; } = CreateEd25519();

    /// <summary>Gets the Ed448 curve of RFC 8032, 5.2.</summary>
    internal static EdwardsCurve Ed448 { get; } = CreateEd448();

    /// <summary>Builds Ed25519 from its defining numbers.</summary>
    /// <returns>The curve.</returns>
    private static EdwardsCurve CreateEd25519()
    {
        var prime = (BigInteger.One << Ed25519PrimeBits) - Ed25519PrimeOffset;
        var field = EdwardsField.Create(prime);
        var d = (prime - Ed25519DNumerator) * Invert(Ed25519DDenominator, prime) % prime;
        var order = (BigInteger.One << Ed25519OrderBits) + BigInteger.Parse(Ed25519OrderOffset, CultureInfo.InvariantCulture);
        var baseY = Ed25519BaseYNumerator * Invert(Ed25519BaseYDenominator, prime) % prime;
        var minusOne = EdwardsField.ToMontgomeryLimbs(prime - 1, prime);
        var basePoint = new ulong[EdwardsPoint.Length(field.Limbs)];
        var curve = new EdwardsCurve(field, minusOne, EdwardsField.ToMontgomeryLimbs(d, prime), EdwardsField.ToLimbs(order, field.Limbs), basePoint, Ed25519KeyLength, false);

        // The base point's x is the even root for y = 4/5 (RFC 8032, 5.1).
        Span<ulong> point = basePoint;
        var y = EdwardsField.ToMontgomeryLimbs(baseY, prime);
        _ = EdwardsArithmetic.TryRecoverX(curve, y, false, EdwardsPoint.X(point, field.Limbs));
        y.CopyTo(EdwardsPoint.Y(point, field.Limbs));
        field.One.CopyTo(EdwardsPoint.Z(point, field.Limbs));
        return curve;
    }

    /// <summary>Builds Ed448 from its defining numbers.</summary>
    /// <returns>The curve.</returns>
    private static EdwardsCurve CreateEd448()
    {
        var prime = (BigInteger.One << Ed448PrimeBits) - (BigInteger.One << Ed448PrimeMiddleBits) - 1;
        var field = EdwardsField.Create(prime);
        var order = (BigInteger.One << Ed448OrderBits) - BigInteger.Parse(Ed448OrderOffset, CultureInfo.InvariantCulture);
        var basePoint = new ulong[EdwardsPoint.Length(field.Limbs)];
        Span<ulong> point = basePoint;
        EdwardsField.ToMontgomeryLimbs(BigInteger.Parse(Ed448BaseX, CultureInfo.InvariantCulture), prime).CopyTo(EdwardsPoint.X(point, field.Limbs));
        EdwardsField.ToMontgomeryLimbs(BigInteger.Parse(Ed448BaseY, CultureInfo.InvariantCulture), prime).CopyTo(EdwardsPoint.Y(point, field.Limbs));
        field.One.CopyTo(EdwardsPoint.Z(point, field.Limbs));
        var d = EdwardsField.ToMontgomeryLimbs(prime - Ed448D, prime);
        return new(field, field.One, d, EdwardsField.ToLimbs(order, field.Limbs), basePoint, Ed448KeyLength, true);
    }

    /// <summary>Inverts a number in a prime field.</summary>
    /// <param name="value">The number.</param>
    /// <param name="prime">The prime.</param>
    /// <returns>The inverse.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BigInteger Invert(int value, BigInteger prime) => BigInteger.ModPow(value, prime - Two, prime);
}
