// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;
using X86Aes = System.Runtime.Intrinsics.X86.Aes;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Encrypts whole blocks with AES in CBC mode and no padding on the CPU's own AES instructions (AES-NI on x86, the AES extension on
/// Arm), so a caller that sets a new key for every run of blocks, such as the iterated password hash of ISO 32000-2 7.6.4.3.4,
/// creates no cipher object and allocates nothing. The key schedule is the one of FIPS 197, clause 5.2; the rounds are FIPS 197,
/// clause 5.1. Where the CPU has no AES instructions, <see cref="IsAccelerated"/> is <see langword="false"/> and callers use the
/// .NET cipher.
/// </summary>
internal static class AesCbcNoPadding
{
    /// <summary>The bytes in an AES block.</summary>
    internal const int BlockLength = 16;

    /// <summary>The bytes in a word of the key schedule.</summary>
    private const int WordLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a word of the key schedule.</summary>
    private const int WordBits = 32;

    /// <summary>The key length of AES-128 in bytes.</summary>
    private const int Aes128KeyLength = 16;

    /// <summary>The key length of AES-192 in bytes.</summary>
    private const int Aes192KeyLength = 24;

    /// <summary>The key length of AES-256 in bytes.</summary>
    private const int Aes256KeyLength = 32;

    /// <summary>The rounds an AES key adds to its length in words (FIPS 197, Table 3).</summary>
    private const int RoundsOverKeyWords = 6;

    /// <summary>The most round keys: AES-256 has fourteen rounds and one more key.</summary>
    private const int MaxRoundKeys = 15;

    /// <summary>The bytes of the largest key schedule.</summary>
    private const int MaxScheduleLength = MaxRoundKeys * BlockLength;

    /// <summary>The position in a 256-bit key's cycle of words that gets a substitution without a rotation.</summary>
    private const int ExtraSubstitutionPosition = 4;

    /// <summary>The high bit of a byte.</summary>
    private const int HighBit = 0x80;

    /// <summary>The reduction of the AES field polynomial x^8 + x^4 + x^3 + x + 1 (FIPS 197, 4.2).</summary>
    private const int FieldReduction = 0x1B;

    /// <summary>Whether the CPU has AES instructions and they give the answers FIPS 197 gives.</summary>
    private static readonly bool Accelerated = (X86Aes.IsSupported || ArmAes.IsSupported) && PassesKnownAnswers();

    /// <summary>Gets a value indicating whether the CPU's AES instructions are used.</summary>
    internal static bool IsAccelerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Accelerated;
    }

    /// <summary>Gets the key of FIPS 197, C.3, which counts up from zero; C.1 uses its first 16 bytes.</summary>
    private static ReadOnlySpan<byte> KnownKey =>
    [
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
    ];

    /// <summary>Gets the plain text of FIPS 197, C.1 and C.3.</summary>
    private static ReadOnlySpan<byte> KnownPlain => [0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

    /// <summary>Gets the cipher text of FIPS 197, C.1: AES-128.</summary>
    private static ReadOnlySpan<byte> KnownCipher128 => [0x69, 0xC4, 0xE0, 0xD8, 0x6A, 0x7B, 0x04, 0x30, 0xD8, 0xCD, 0xB7, 0x80, 0x70, 0xB4, 0xC5, 0x5A];

    /// <summary>Gets the cipher text of FIPS 197, C.3: AES-256.</summary>
    private static ReadOnlySpan<byte> KnownCipher256 => [0x8E, 0xA2, 0xB7, 0xCA, 0x51, 0x67, 0x45, 0xBF, 0xEA, 0xFC, 0x49, 0x90, 0x4B, 0x49, 0x60, 0x89];

    /// <summary>Encrypts whole blocks, when the CPU has AES instructions.</summary>
    /// <param name="key">The key: 16, 24 or 32 bytes.</param>
    /// <param name="iv">The 16-byte initialisation vector.</param>
    /// <param name="plain">The plain text, a whole number of blocks.</param>
    /// <param name="destination">Receives the cipher text; at least as long as the plain text.</param>
    /// <returns><see langword="false"/> when the CPU has no AES instructions or an argument is not the right size; nothing is written then.</returns>
    internal static bool TryEncrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> plain, Span<byte> destination)
    {
        if (!Accelerated || iv.Length != BlockLength || plain.Length % BlockLength != 0 || destination.Length < plain.Length)
        {
            return false;
        }

        if (key.Length is not (Aes128KeyLength or Aes192KeyLength or Aes256KeyLength))
        {
            return false;
        }

        Encrypt(key, iv, plain, destination);
        return true;
    }

    /// <summary>Checks the CPU's instructions against the examples of FIPS 197, Appendix C.</summary>
    /// <returns><see langword="true"/> when AES-128 and AES-256 give the published cipher text.</returns>
    private static bool PassesKnownAnswers()
    {
        Span<byte> cipher = stackalloc byte[BlockLength];
        Span<byte> zero = stackalloc byte[BlockLength];
        zero.Clear();

        // With a zero initialisation vector, one CBC block is the same as one ECB block, which is what the examples show.
        Encrypt(KnownKey[..Aes128KeyLength], zero, KnownPlain, cipher);
        if (!cipher.SequenceEqual(KnownCipher128))
        {
            return false;
        }

        Encrypt(KnownKey, zero, KnownPlain, cipher);
        return cipher.SequenceEqual(KnownCipher256);
    }

    /// <summary>Applies SubWord of FIPS 197, 5.2 to a word, using the CPU's substitution box.</summary>
    /// <param name="word">The four bytes of the word.</param>
    /// <returns>The substituted word.</returns>
    private static uint SubWord(uint word) => X86Aes.IsSupported
        ? X86Aes.KeygenAssist(Vector128.Create(0U, word, 0U, 0U).AsByte(), 0).AsUInt32().GetElement(0)

        // Four equal columns make ShiftRows change nothing, so one round with a zero key is SubBytes.
        : ArmAes.Encrypt(Vector128.Create(word).AsByte(), Vector128<byte>.Zero).AsUInt32().GetElement(0);

    /// <summary>Expands a key into the round keys (FIPS 197, 5.2).</summary>
    /// <param name="key">The key: 16, 24 or 32 bytes.</param>
    /// <param name="schedule">Receives the round keys, one after another; at least 240 bytes.</param>
    /// <returns>The number of rounds.</returns>
    private static int ExpandKey(ReadOnlySpan<byte> key, Span<byte> schedule)
    {
        var keyWords = key.Length / WordLength;
        var rounds = keyWords + RoundsOverKeyWords;
        var totalWords = (rounds + 1) * WordLength;
        key.CopyTo(schedule);
        var rcon = 1;
        for (var i = keyWords; i < totalWords; i++)
        {
            var temp = BitConverter.ToUInt32(schedule.Slice((i - 1) * WordLength, WordLength));
            if (i % keyWords == 0)
            {
                temp = SubWord(RotateWord(temp)) ^ (uint)rcon;
                rcon = DoubleInField(rcon);
            }
            else if (keyWords > RoundsOverKeyWords && i % keyWords == ExtraSubstitutionPosition)
            {
                temp = SubWord(temp);
            }

            var previous = BitConverter.ToUInt32(schedule.Slice((i - keyWords) * WordLength, WordLength));
            _ = BitConverter.TryWriteBytes(schedule.Slice(i * WordLength, WordLength), previous ^ temp);
        }

        return rounds;
    }

    /// <summary>Rotates the bytes of a word one place to the left in memory order (RotWord of FIPS 197, 5.2).</summary>
    /// <param name="word">The word, with its first byte in the low bits.</param>
    /// <returns>The rotated word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateWord(uint word) => (word >> ByteBits) | (word << (WordBits - ByteBits));

    /// <summary>Multiplies a round constant by x in the AES field (FIPS 197, 4.2.1).</summary>
    /// <param name="value">The constant.</param>
    /// <returns>The product.</returns>
    private static int DoubleInField(int value) => (value & HighBit) == 0 ? value << 1 : ((value << 1) ^ FieldReduction) & byte.MaxValue;

    /// <summary>Encrypts blocks in CBC mode with the instructions of the CPU.</summary>
    /// <param name="key">The key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="plain">The plain text.</param>
    /// <param name="destination">Receives the cipher text.</param>
    private static void Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> plain, Span<byte> destination)
    {
        Span<byte> schedule = stackalloc byte[MaxScheduleLength];
        var rounds = ExpandKey(key, schedule);
        Span<Vector128<byte>> roundKeys = stackalloc Vector128<byte>[MaxRoundKeys];
        for (var i = 0; i <= rounds; i++)
        {
            roundKeys[i] = Vector128.Create(schedule.Slice(i * BlockLength, BlockLength));
        }

        var previous = Vector128.Create(iv);
        for (var offset = 0; offset < plain.Length; offset += BlockLength)
        {
            var state = Vector128.Create(plain[offset..]) ^ previous;
            state = X86Aes.IsSupported ? EncryptX86(state, roundKeys, rounds) : EncryptArm(state, roundKeys, rounds);
            state.CopyTo(destination[offset..]);
            previous = state;
        }

        // The key schedule can be turned back into the key, so it is wiped.
        CryptographicOperations.ZeroMemory(schedule);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(roundKeys));
    }

    /// <summary>Encrypts one block with AES-NI.</summary>
    /// <param name="state">The block, already chained.</param>
    /// <param name="roundKeys">The round keys.</param>
    /// <param name="rounds">The number of rounds.</param>
    /// <returns>The cipher text block.</returns>
    private static Vector128<byte> EncryptX86(Vector128<byte> state, ReadOnlySpan<Vector128<byte>> roundKeys, int rounds)
    {
        state ^= roundKeys[0];
        for (var round = 1; round < rounds; round++)
        {
            state = X86Aes.Encrypt(state, roundKeys[round]);
        }

        return X86Aes.EncryptLast(state, roundKeys[rounds]);
    }

    /// <summary>Encrypts one block with the Arm AES extension, whose first step adds the round key.</summary>
    /// <param name="state">The block, already chained.</param>
    /// <param name="roundKeys">The round keys.</param>
    /// <param name="rounds">The number of rounds.</param>
    /// <returns>The cipher text block.</returns>
    private static Vector128<byte> EncryptArm(Vector128<byte> state, ReadOnlySpan<Vector128<byte>> roundKeys, int rounds)
    {
        for (var round = 0; round < rounds - 1; round++)
        {
            state = ArmAes.MixColumns(ArmAes.Encrypt(state, roundKeys[round]));
        }

        return ArmAes.Encrypt(state, roundKeys[rounds - 1]) ^ roundKeys[rounds];
    }
}
