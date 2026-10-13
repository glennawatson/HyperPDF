// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Security.Cryptography;

namespace HyperPdf.Cms;

/// <summary>
/// Checks Ed25519 and Ed448 signatures as RFC 8032, 5.1.7 and 5.2.7 specify (pure EdDSA with an empty context), which the
/// platform's cryptography libraries do not offer to .NET. It only verifies: it never handles a private key.
/// </summary>
internal static class EdDsa
{
    /// <summary>The signature is two key-sized halves: R then S.</summary>
    private const int SignatureHalves = 2;

    /// <summary>The scalars the verification holds: S and the challenge.</summary>
    private const int Scalars = 2;

    /// <summary>The points the verification holds: A, R, both sides of the check and the product k * A.</summary>
    private const int Points = 5;

    /// <summary>The limbs of the verification workspace, sized for the largest field.</summary>
    private const int Workspace = (Scalars + (Points * EdwardsPoint.Elements)) * EdwardsField.MaxLimbs;

    /// <summary>The longest encoded point, an Ed448 key.</summary>
    private const int MaxEncodedLength = 57;

    /// <summary>The bit of the last key byte that holds x's sign.</summary>
    private const byte SignMask = 0x80;

    /// <summary>The SHA-512 length, which is the Ed25519 challenge hash length.</summary>
    private const int Sha512Length = 64;

    /// <summary>The Ed448 challenge hash length, 2 * 57 bytes.</summary>
    private const int Shake256ChallengeLength = 114;

    /// <summary>Gets the Ed448 domain separation prefix, <c>"SigEd448" || 0 || 0</c> (RFC 8032, 2).</summary>
    private static ReadOnlySpan<byte> Dom4 => "SigEd448\0\0"u8;

    /// <summary>Verifies a signature.</summary>
    /// <param name="curve">Ed25519 or Ed448.</param>
    /// <param name="publicKey">The encoded public key.</param>
    /// <param name="message">The signed message.</param>
    /// <param name="signature">The encoded signature: R then S.</param>
    /// <returns><see langword="true"/> when the signature is good.</returns>
    internal static bool Verify(EdwardsCurve curve, ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(curve);
        var length = curve.KeyLength;
        if (publicKey.Length != length || signature.Length != SignatureHalves * length || (curve.UsesShake && !Shake256.IsSupported))
        {
            return false;
        }

        var limbs = curve.Field.Limbs;
        var point = EdwardsPoint.Length(limbs);

        // The scalars S and k, the points A and R, the two sides of the check, and the product k * A.
        Span<ulong> workspace = stackalloc ulong[Workspace];
        workspace.Clear();
        var s = workspace[..limbs];
        var k = workspace[limbs..(limbs + limbs)];
        var points = workspace[(limbs + limbs)..];
        var a = points[..point];
        var r = points[point..(point + point)];
        var left = points[(point + point)..(point + point + point)];
        var right = points[(point + point + point)..(point + point + point + point)];
        var product = points[(point + point + point + point)..(point + point + point + point + point)];
        var encodedR = signature[..length];
        if (!EdwardsLimbs.TryRead(signature[length..], s) || EdwardsLimbs.Compare(s, curve.Order) >= 0 || !TryDecode(curve, publicKey, a) || !TryDecode(curve, encodedR, r))
        {
            return false;
        }

        Challenge(curve, encodedR, publicKey, message, k);
        EdwardsArithmetic.Multiply(curve, curve.BasePoint, s, left);
        EdwardsArithmetic.Multiply(curve, a, k, product);
        EdwardsArithmetic.Add(curve, r, product, right);
        return EdwardsArithmetic.Equal(curve, left, right);
    }

    /// <summary>Decodes a point (RFC 8032, 5.1.3 and 5.2.3): y in little-endian with x's sign in the top bit.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="encoded">The encoded point.</param>
    /// <param name="point">Receives the point, with its coordinates in Montgomery form.</param>
    /// <returns><see langword="true"/> when the encoding is canonical and the point is on the curve.</returns>
    internal static bool TryDecode(EdwardsCurve curve, ReadOnlySpan<byte> encoded, Span<ulong> point)
    {
        if (encoded.Length > MaxEncodedLength)
        {
            return false;
        }

        var field = curve.Field;
        var limbs = field.Limbs;
        Span<byte> copy = stackalloc byte[MaxEncodedLength];
        copy = copy[..encoded.Length];
        encoded.CopyTo(copy);
        var odd = (copy[^1] & SignMask) != 0;
        copy[^1] &= unchecked((byte)~SignMask);

        // y must be below the prime, which also holds it inside the limbs.
        Span<ulong> plain = stackalloc ulong[EdwardsField.MaxLimbs];
        plain = plain[..limbs];
        if (!EdwardsLimbs.TryRead(copy, plain) || EdwardsLimbs.Compare(plain, field.Prime) >= 0)
        {
            return false;
        }

        var y = EdwardsPoint.Y(point, limbs);
        EdwardsFieldMath.Multiply(field, plain, field.RSquared, y);
        if (!EdwardsArithmetic.TryRecoverX(curve, y, odd, EdwardsPoint.X(point, limbs)))
        {
            return false;
        }

        field.One.CopyTo(EdwardsPoint.Z(point, limbs));
        return true;
    }

    /// <summary>Computes the challenge k = hash(dom || R || A || M) mod L.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="r">The encoded R.</param>
    /// <param name="a">The encoded public key.</param>
    /// <param name="message">The message.</param>
    /// <param name="challenge">Receives the challenge.</param>
    private static void Challenge(EdwardsCurve curve, ReadOnlySpan<byte> r, ReadOnlySpan<byte> a, ReadOnlySpan<byte> message, Span<ulong> challenge)
    {
        var prefix = curve.UsesShake ? Dom4.Length : 0;
        var total = prefix + r.Length + a.Length + message.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(total);
        try
        {
            var input = buffer.AsSpan(0, total);
            Dom4[..prefix].CopyTo(input);
            r.CopyTo(input[prefix..]);
            a.CopyTo(input[(prefix + r.Length)..]);
            message.CopyTo(input[(prefix + r.Length + a.Length)..]);
            Span<byte> hash = stackalloc byte[Shake256ChallengeLength];
            var digest = hash[..(curve.UsesShake ? Shake256ChallengeLength : Sha512Length)];
            if (curve.UsesShake)
            {
                Shake256.HashData(input, digest);
            }
            else
            {
                _ = SHA512.HashData(input, digest);
            }

            EdwardsLimbs.Reduce(digest, curve.Order, challenge);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
