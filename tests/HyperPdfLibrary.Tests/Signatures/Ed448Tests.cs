// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Tests for the Ed448 verification of RFC 8032, 5.2.7. The known-answer signatures were made by OpenSSL from fixed private
/// keys, since Ed448 signing is deterministic. Further signatures are made by OpenSSL when it is on the path, and one is built
/// here from the curve's definition so the group order boundary can be tested.
/// </summary>
public sealed class Ed448Tests
{
    /// <summary>The encoded key and half-signature size (RFC 8032, 5.2).</summary>
    private const int KeyLength = 57;

    /// <summary>The encoded signature size, R then S.</summary>
    private const int SignatureLength = 114;

    /// <summary>The bits of the Ed448 prime's top power of two (RFC 8032, 5.2).</summary>
    private const int PrimeBits = 448;

    /// <summary>The Ed448 prime is 2^448 - 2^224 - 1.</summary>
    private const int PrimeMiddleBits = 224;

    /// <summary>The group order is 2^446 minus an offset (RFC 8032, 5.2).</summary>
    private const int OrderBits = 446;

    /// <summary>The amount the group order lies below 2^446 (RFC 8032, 5.2).</summary>
    private const string OrderOffset = "13818066809895115352007386748515426880336692474882178609894547503885";

    /// <summary>The magnitude of the curve coefficient d, which is negative (RFC 8032, 5.2).</summary>
    private const int DMagnitude = 39_081;

    /// <summary>The base point's x (RFC 8032, 5.2).</summary>
    private const string BaseX = "224580040295924300187604334099896036246789641632564134246125461686950415467406032909029192869357953282578032075146446173674602635247710";

    /// <summary>The base point's y (RFC 8032, 5.2).</summary>
    private const string BaseY = "298819210078481492676017930443930673437544040154080242095928241372331506189835876003536878655418784733982303233503462500531545062832660";

    /// <summary>The bit of the last key byte that holds x's sign.</summary>
    private const byte SignBit = 0x80;

    /// <summary>The challenge hash length, 2 * 57 bytes.</summary>
    private const int ChallengeLength = 114;

    /// <summary>The scalar whose multiple of the base point is the built signature's R.</summary>
    private const int BuiltNonce = 5;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The Ed25519 key size, used to test that a key of the other curve's size is refused.</summary>
    private const int Ed25519KeyLength = 32;

    /// <summary>The Ed25519 signature size.</summary>
    private const int Ed25519SignatureLength = 64;

    /// <summary>The number two: Fermat's inverse exponent is the prime minus two, and the Euler criterion halves the prime less one.</summary>
    private const int Two = 2;

    /// <summary>The first y tried when looking for one that is not on the curve.</summary>
    private const int FirstSearchedY = 2;

    /// <summary>The y at which the search for a y that is not on the curve ends.</summary>
    private const int LastSearchedY = 40;

    /// <summary>The fresh keys OpenSSL makes.</summary>
    private const int FreshKeys = 4;

    /// <summary>A message longer than one signature.</summary>
    private const int LongMessageLength = 1000;

    /// <summary>The bits between flipped bits, so the loops cover every byte position without a thousand verifications per vector.</summary>
    private const int FlipStep = 7;

    /// <summary>The longest tool run.</summary>
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The field prime.</summary>
    private static readonly BigInteger Prime = (BigInteger.One << PrimeBits) - (BigInteger.One << PrimeMiddleBits) - 1;

    /// <summary>The group order L.</summary>
    private static readonly BigInteger Order = (BigInteger.One << OrderBits) - BigInteger.Parse(OrderOffset, CultureInfo.InvariantCulture);

    /// <summary>The curve coefficient d.</summary>
    private static readonly BigInteger D = Prime - DMagnitude;

    /// <summary>The base point.</summary>
    private static readonly CurvePoint Base = new(BigInteger.Parse(BaseX, CultureInfo.InvariantCulture), BigInteger.Parse(BaseY, CultureInfo.InvariantCulture));

    /// <summary>OpenSSL-made signatures of an empty, a one byte and a 100 byte message.</summary>
    private static readonly Vector[] Vectors =
    [
        new(
            Convert.FromHexString("0d3297e7694721cd0566eb289c304af272173a90765e4b979297bdcacd9b24ed5cb83cb5991d6ef289829f3f506a429fba5a69354fe10f6480"),
            Convert.FromHexString(
                "83951f63ed48f58ca8586d026dd810ef3a0a94348acd51f04b0a0c689fca9323e43419a6639349a80fe47a52bbae240a6642d4c5ef8e0f478028253dc061e949"
                + "6ef64301710b09a5d372ea638caf894b5e15798d9e458cba6f6e43aee61cfa0ed80f701e680b76c5c498be9391312fe92600"),
            []),
        new(
            Convert.FromHexString("68d8ad27e54c27521454b37b25f89c126aec2c8b9a4399e98d45603215400faf82cf397282480a7a37da056e9058aadc56ee156780dc20b900"),
            Convert.FromHexString(
                "38894fbdc8096b7029601f74aacb1de899b659767b17ad877aee6d4e18112cbcfd05c8b22ac69352ee0412173a5dc957142f611e1b09e17a00642e3e92816346"
                + "0aacf395b9e1fab3a1f5d94373064588ebae0532f70821d40399377660c1725b4c4c2a52e1589e57b784111b057aad573500"),
            [0x03]),
        new(
            Convert.FromHexString("a82503bd8d0fbc83e5a101f8511a93c45e6167058dbf254d39978139573a7d5033bb4acdf5582c2a8c50e6070c1185b87a684d32952f0ce180"),
            Convert.FromHexString(
                "1e2cc87c9061c31562ab1acde5638e22b180a048d7b40e6f53e50f7797da9ad70feaf551a7f4f53311ac78d013f781c3efc0c56a51a59d22804459d67a4b2b6ff"
                + "db99024f1135ae75b84c7a7e0d8228af9efd4053e5f6ad9e6bda1e19c09a1e4c95fb83025de66ad1f97f165e3d8e2ef2700"),
            [.. Enumerable.Range(0, 100).Select(static i => (byte)i)]),
    ];

    /// <summary>The curve's group order equals the RFC 8032 value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CurveOrderMatchesTheRfc()
    {
        var order = new BigInteger(MemoryMarshal.AsBytes(EdwardsCurve.Ed448.Order.AsSpan()), true);
        await Assert.That(order).IsEqualTo(Order);
    }

    /// <summary>Signatures made by OpenSSL verify.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnownAnswerSignaturesVerify()
    {
        RequireShake();
        foreach (var vector in Vectors)
        {
            await Assert.That(Verify(vector.Key, vector.Message, vector.Signature)).IsTrue();
        }
    }

    /// <summary>A changed bit in the signature, key or message does not verify.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlippedBitsDoNotVerify()
    {
        RequireShake();
        foreach (var vector in Vectors)
        {
            for (var bit = 0; bit < SignatureLength * BitsPerByte; bit += FlipStep)
            {
                await Assert.That(Verify(vector.Key, vector.Message, Flip(vector.Signature, bit))).IsFalse();
            }

            for (var bit = 0; bit < KeyLength * BitsPerByte; bit += FlipStep)
            {
                await Assert.That(Verify(Flip(vector.Key, bit), vector.Message, vector.Signature)).IsFalse();
            }

            for (var bit = 0; bit < vector.Message.Length * BitsPerByte; bit += FlipStep)
            {
                await Assert.That(Verify(vector.Key, Flip(vector.Message, bit), vector.Signature)).IsFalse();
            }

            await Assert.That(Verify(vector.Key, [.. vector.Message, 0], vector.Signature)).IsFalse();
        }
    }

    /// <summary>A key or signature of the wrong length does not verify.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongLengthsDoNotVerify()
    {
        RequireShake();
        var vector = Vectors[1];
        await Assert.That(Verify(vector.Key[..^1], vector.Message, vector.Signature)).IsFalse();
        await Assert.That(Verify([.. vector.Key, 0], vector.Message, vector.Signature)).IsFalse();
        await Assert.That(Verify([], vector.Message, vector.Signature)).IsFalse();
        await Assert.That(Verify(vector.Key, vector.Message, vector.Signature[..^1])).IsFalse();
        await Assert.That(Verify(vector.Key, vector.Message, [.. vector.Signature, 0])).IsFalse();
        await Assert.That(Verify(vector.Key, vector.Message, [])).IsFalse();

        // An Ed25519 sized key and signature.
        await Assert.That(Verify(vector.Key[..Ed25519KeyLength], vector.Message, vector.Signature[..Ed25519SignatureLength])).IsFalse();
    }

    /// <summary>An S of zero, L - 1, L, L + 1 or all ones does not verify; L - 1 and 0 fail the equation, the others the range check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalarsAtTheOrderBoundaryDoNotVerify()
    {
        RequireShake();
        var vector = Vectors[0];
        BigInteger[] scalars = [0, Order - 1, Order, Order + 1, Order + Order, (BigInteger.One << (KeyLength * BitsPerByte)) - 1];
        foreach (var scalar in scalars)
        {
            await Assert.That(Verify(vector.Key, vector.Message, WithScalar(vector.Signature, scalar))).IsFalse();
        }
    }

    /// <summary>The real S plus L is the same group element but not the canonical encoding, so it is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalarPlusTheOrderIsRefused()
    {
        RequireShake();
        foreach (var vector in Vectors)
        {
            var s = new BigInteger(vector.Signature.AsSpan(KeyLength), true);
            await Assert.That(s).IsLessThan(Order);
            await Assert.That(Verify(vector.Key, vector.Message, WithScalar(vector.Signature, s + Order))).IsFalse();
        }
    }

    /// <summary>A signature built from the curve's definition verifies, and the same signature with S + L does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuiltSignatureVerifiesButItsMalleatedScalarDoesNot()
    {
        RequireShake();
        byte[] message = [1, 2, 3];
        var key = Encode(Base.Y, !Base.X.IsEven);
        var encodedR = Encode(Multiply(Base, BuiltNonce));
        var k = Challenge(encodedR, key, message);
        var s = (BuiltNonce + k) % Order;
        var signature = Join(encodedR, ToBytes(s, KeyLength));

        await Assert.That(Verify(key, message, signature)).IsTrue();
        await Assert.That(Verify(key, [.. message, 0], signature)).IsFalse();
        await Assert.That(Verify(key, message, WithScalar(signature, s + Order))).IsFalse();
        await Assert.That(Verify(key, message, WithScalar(signature, (s + 1) % Order))).IsFalse();
    }

    /// <summary>A non-canonical y, or x of zero with the sign bit set, is refused as a key and as R.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonCanonicalPointEncodingsAreRefused()
    {
        var one = Encode(1, false);
        var minusOne = Encode(Prime - 1, false);
        await Assert.That(Decode(one)).IsTrue();
        await Assert.That(Decode(minusOne)).IsTrue();
        await Assert.That(Decode(Encode(Base.Y, !Base.X.IsEven))).IsTrue();

        // y = p + 1 and y = p are y = 1 and y = 0 written with an extra multiple of the prime.
        await Assert.That(Decode(Encode(Prime + 1, false))).IsFalse();
        await Assert.That(Decode(Encode(Prime, false))).IsFalse();
        await Assert.That(Decode(Encode(Prime + 1, true))).IsFalse();
        await Assert.That(Decode(Encode((BigInteger.One << PrimeBits) - 1, false))).IsFalse();

        // x = 0 for y = 1 and y = -1, so a set sign bit has no matching x.
        await Assert.That(Decode(Encode(1, true))).IsFalse();
        await Assert.That(Decode(Encode(Prime - 1, true))).IsFalse();

        // The seven bits of the last byte above y must be clear.
        var junk = one.ToArray();
        junk[KeyLength - 1] = 1;
        await Assert.That(Decode(junk)).IsFalse();

        var vector = Vectors[0];
        await Assert.That(Verify(Encode(Prime + 1, false), vector.Message, vector.Signature)).IsFalse();
        await Assert.That(Verify(vector.Key, vector.Message, Join(Encode(Prime + 1, false), vector.Signature[KeyLength..]))).IsFalse();
        await Assert.That(Verify(vector.Key, vector.Message, Join(Encode(1, true), vector.Signature[KeyLength..]))).IsFalse();
    }

    /// <summary>A y whose x squared is not a square is not a point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task YWithoutAnXIsRefused()
    {
        var found = 0;
        for (var y = FirstSearchedY; y < LastSearchedY; y++)
        {
            var y2 = y * (BigInteger)y % Prime;
            var x2 = Mod(y2 - 1) * Invert(Mod((D * y2) - 1)) % Prime;
            var isSquare = x2.IsZero || BigInteger.ModPow(x2, (Prime - 1) / Two, Prime).IsOne;
            await Assert.That(Decode(Encode(y, false))).IsEqualTo(isSquare);
            found += isSquare ? 0 : 1;
        }

        await Assert.That(found).IsGreaterThan(0);
    }

    /// <summary>Signatures OpenSSL makes for fresh keys and assorted message lengths verify, and tampered ones do not.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenSslSignaturesVerify(CancellationToken cancellationToken)
    {
        RequireShake();
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"hyperpdf-ed448-{Guid.NewGuid():N}")).FullName;
        try
        {
            for (var index = 0; index < FreshKeys; index++)
            {
                var privateKey = Path.Combine(folder, $"key{index}.pem");
                var publicKey = Path.Combine(folder, $"key{index}.der");
                await OpenSslAsync(["genpkey", "-algorithm", "ED448", "-out", privateKey], cancellationToken);
                if (!File.Exists(privateKey))
                {
                    Skip.Test("openssl is not installed or cannot make Ed448 keys.");
                }

                await OpenSslAsync(["pkey", "-in", privateKey, "-pubout", "-outform", "DER", "-out", publicKey], cancellationToken);
                var key = (await File.ReadAllBytesAsync(publicKey, cancellationToken))[^KeyLength..];
                foreach (var length in new[] { 0, 1, KeyLength, SignatureLength, LongMessageLength })
                {
                    var message = RandomNumberGenerator.GetBytes(length);
                    var signature = await SignAsync(folder, privateKey, message, cancellationToken);
                    if (signature.Length == 0)
                    {
                        // An openssl that writes an empty signature file could not sign this input. That is the tool's limit, not the library's:
                        // an empty message is left out, and a tool that cannot sign any message skips the test.
                        if (length != 0)
                        {
                            Skip.Test("This openssl cannot sign Ed448 with raw input (pkeyutl -rawin).");
                        }

                        continue;
                    }

                    await Assert.That(signature.Length).IsEqualTo(SignatureLength);
                    await Assert.That(Verify(key, message, signature)).IsTrue();
                    await Assert.That(Verify(key, message, Flip(signature, length))).IsFalse();
                    await Assert.That(Verify(key, [.. message, 0], signature)).IsFalse();
                    var s = new BigInteger(signature.AsSpan(KeyLength), true);
                    await Assert.That(Verify(key, message, WithScalar(signature, s + Order))).IsFalse();
                }
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Skips the test on a machine whose cryptography library has no SHAKE256, which Ed448 needs.</summary>
    private static void RequireShake()
    {
        if (!Shake256.IsSupported)
        {
            Skip.Test("SHAKE256 is not supported on this machine, so Ed448 cannot be checked.");
        }
    }

    /// <summary>Verifies a signature with the Ed448 curve.</summary>
    /// <param name="key">The public key.</param>
    /// <param name="message">The message.</param>
    /// <param name="signature">The signature.</param>
    /// <returns>Whether it verifies.</returns>
    private static bool Verify(byte[] key, byte[] message, byte[] signature) => EdDsa.Verify(EdwardsCurve.Ed448, key, message, signature);

    /// <summary>Flips one bit in a copy of the bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="bit">The bit position, wrapped to the length.</param>
    /// <returns>The changed copy.</returns>
    private static byte[] Flip(byte[] bytes, int bit)
    {
        var copy = bytes.ToArray();
        if (copy.Length > 0)
        {
            var position = bit % (copy.Length * BitsPerByte);
            copy[position / BitsPerByte] ^= (byte)(1 << (position % BitsPerByte));
        }

        return copy;
    }

    /// <summary>Replaces the S half of a signature.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="scalar">The new S, which must fit in 57 bytes.</param>
    /// <returns>The changed signature.</returns>
    private static byte[] WithScalar(byte[] signature, BigInteger scalar) => Join(signature[..KeyLength], ToBytes(scalar, KeyLength));

    /// <summary>Joins two arrays.</summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    /// <returns>The joined array.</returns>
    private static byte[] Join(byte[] first, byte[] second) => [.. first, .. second];

    /// <summary>Writes a number as little-endian bytes.</summary>
    /// <param name="value">The non-negative number.</param>
    /// <param name="length">The byte count.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidOperationException">The number does not fit.</exception>
    private static byte[] ToBytes(BigInteger value, int length)
    {
        var bytes = new byte[length];
        return value.TryWriteBytes(bytes, out _, true) ? bytes : throw new InvalidOperationException("The number does not fit.");
    }

    /// <summary>Encodes y with the sign bit of x (RFC 8032, 5.2.2).</summary>
    /// <param name="y">The y coordinate, which may be non-canonical.</param>
    /// <param name="sign">The sign bit.</param>
    /// <returns>The encoding.</returns>
    private static byte[] Encode(BigInteger y, bool sign)
    {
        var bytes = ToBytes(y, KeyLength);
        if (sign)
        {
            bytes[KeyLength - 1] |= SignBit;
        }

        return bytes;
    }

    /// <summary>Encodes a point (RFC 8032, 5.2.2).</summary>
    /// <param name="point">The point.</param>
    /// <returns>The encoding.</returns>
    private static byte[] Encode(CurvePoint point) => Encode(point.Y, !point.X.IsEven);

    /// <summary>Decodes a point with the library.</summary>
    /// <param name="encoded">The encoding.</param>
    /// <returns>Whether it is a point.</returns>
    private static bool Decode(byte[] encoded)
    {
        var curve = EdwardsCurve.Ed448;
        return EdDsa.TryDecode(curve, encoded, new ulong[EdwardsPoint.Length(curve.Field.Limbs)]);
    }

    /// <summary>Hashes the challenge k = SHAKE256(dom4 || R || A || M, 114) mod L (RFC 8032, 5.2.6).</summary>
    /// <param name="encodedR">The encoded R.</param>
    /// <param name="key">The encoded key.</param>
    /// <param name="message">The message.</param>
    /// <returns>The challenge.</returns>
    private static BigInteger Challenge(byte[] encodedR, byte[] key, byte[] message)
    {
        byte[] input = [.. "SigEd448\0\0"u8, .. encodedR, .. key, .. message];
        return new BigInteger(Shake256.HashData(input, ChallengeLength), true) % Order;
    }

    /// <summary>Reduces a number into the field.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The non-negative remainder.</returns>
    private static BigInteger Mod(BigInteger value) => ((value % Prime) + Prime) % Prime;

    /// <summary>Inverts a field element.</summary>
    /// <param name="value">The element.</param>
    /// <returns>The inverse.</returns>
    private static BigInteger Invert(BigInteger value) => BigInteger.ModPow(value, Prime - Two, Prime);

    /// <summary>Adds two points with the complete Edwards law, a = 1 (RFC 8032, 5.2.4).</summary>
    /// <param name="p">The first point.</param>
    /// <param name="q">The second point.</param>
    /// <returns>The sum.</returns>
    private static CurvePoint Add(CurvePoint p, CurvePoint q)
    {
        var t = D * p.X % Prime * q.X % Prime * p.Y % Prime * q.Y % Prime;
        var x = Mod((p.X * q.Y) + (q.X * p.Y)) * Invert(Mod(1 + t)) % Prime;
        var y = Mod((p.Y * q.Y) - (p.X * q.X)) * Invert(Mod(1 - t)) % Prime;
        return new(x, y);
    }

    /// <summary>Multiplies a point by a small scalar by repeated addition.</summary>
    /// <param name="point">The point.</param>
    /// <param name="scalar">The scalar, at least one.</param>
    /// <returns>The product.</returns>
    private static CurvePoint Multiply(CurvePoint point, int scalar)
    {
        var result = point;
        for (var i = 1; i < scalar; i++)
        {
            result = Add(result, point);
        }

        return result;
    }

    /// <summary>Signs a message with OpenSSL.</summary>
    /// <param name="folder">The test's folder.</param>
    /// <param name="privateKey">The private key file.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The signature.</returns>
    private static async Task<byte[]> SignAsync(string folder, string privateKey, byte[] message, CancellationToken cancellationToken)
    {
        var input = Path.Combine(folder, "message.bin");
        var output = Path.Combine(folder, "signature.bin");
        await File.WriteAllBytesAsync(input, message, cancellationToken);
        File.Delete(output);
        await OpenSslAsync(["pkeyutl", "-sign", "-inkey", privateKey, "-rawin", "-in", input, "-out", output], cancellationToken);
        await Assert.That(File.Exists(output)).IsTrue().Because("openssl should write the signature.");
        return await File.ReadAllBytesAsync(output, cancellationToken);
    }

    /// <summary>Runs openssl from the path.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task that ends when openssl has; a missing openssl shows as missing output files.</returns>
    private static async Task OpenSslAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("openssl") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ToolTimeout);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return;
            }

            var output = process.StandardOutput.ReadToEndAsync(limit.Token);
            var errors = process.StandardError.ReadToEndAsync(limit.Token);
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            _ = await errors.ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            // openssl is not installed; the caller sees no output file and skips.
        }
    }

    /// <summary>A known-answer signature.</summary>
    /// <param name="Key">The public key.</param>
    /// <param name="Signature">The signature.</param>
    /// <param name="Message">The signed message.</param>
    private sealed record Vector(byte[] Key, byte[] Signature, byte[] Message);

    /// <summary>An affine point of the curve.</summary>
    /// <param name="X">The x coordinate.</param>
    /// <param name="Y">The y coordinate.</param>
    private sealed record CurvePoint(BigInteger X, BigInteger Y);
}
