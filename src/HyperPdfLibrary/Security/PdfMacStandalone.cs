// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Checks a standalone PDF MAC (ISO/TS 32004:2024 5.2.3 and B.2.2): the trailer's <c>/AuthCode</c> holds the token as a
/// hexadecimal string, the byte range covers the revision except that string, and the token's data digest matches.
/// </summary>
internal static class PdfMacStandalone
{
    /// <summary>The numbers in the byte range: zero, the first length, the second start and the second length.</summary>
    private const int RangeNumbers = 4;

    /// <summary>The characters a hexadecimal token string adds around its digits: the brackets.</summary>
    private const int Brackets = 2;

    /// <summary>The index of the first length in the byte range.</summary>
    private const int FirstLengthIndex = 1;

    /// <summary>The index of the second start in the byte range.</summary>
    private const int SecondStartIndex = 2;

    /// <summary>The index of the second length in the byte range.</summary>
    private const int SecondLengthIndex = 3;

    /// <summary>Checks a revision's standalone PDF MAC.</summary>
    /// <param name="context">The revision and its <c>/AuthCode</c>.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfMacRevisionResult Check(PdfMacCheckContext context, CancellationToken cancellationToken)
    {
        var range = ReadRange(context);
        if (range is null || context.AuthCode.ContainsKey(context.Names.SigObjRef))
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.BadLayout);
        }

        var end = range[SecondStartIndex] + range[SecondLengthIndex];
        if (end > context.File.Length)
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.BadLayout);
        }

        if (!context.CoversRevision(end))
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.Stale);
        }

        // The /MAC entry must be the very bytes in the gap; the gap is what the digest leaves out.
        var token = ReadToken(context, range[FirstLengthIndex], range[SecondStartIndex]);
        return token is not null && CryptographicOperations.FixedTimeEquals(context.AuthCode.GetStringBytes(context.Names.Mac), token)
            ? Verify(context, token, range, cancellationToken)
            : Result(context, PdfMacStatus.Invalid, PdfMacMessages.BadLayout);
    }

    /// <summary>Checks the token and its digest against the covered bytes.</summary>
    /// <param name="context">The revision.</param>
    /// <param name="token">The token.</param>
    /// <param name="range">The byte range.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    private static PdfMacRevisionResult Verify(PdfMacCheckContext context, byte[] token, long[] range, CancellationToken cancellationToken)
    {
        var verified = PdfMacTokenReader.Verify(token, context.Security.FileKey, context.Salt);
        if (verified.Status != PdfMacTokenStatus.Valid)
        {
            return Result(context, PdfMacMessages.ForTokenStatus(verified.Status), PdfMacMessages.ForToken(verified.Status, verified.Digest));
        }

        if (verified.HasSignatureDigest)
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.UnexpectedSignatureDigest);
        }

        // The four numbers are the offset and length of two ranges, which is the layout the digest takes.
        Span<byte> digest = stackalloc byte[CmsDigests.MaxLength];
        var length = PdfMacDigest.Hash(context.File, verified.Digest, range, digest, cancellationToken);
        return CryptographicOperations.FixedTimeEquals(digest[..length], token.AsSpan(verified.DataDigest))
            ? Result(context, PdfMacStatus.Valid, PdfMacMessages.Matches)
            : Result(context, PdfMacStatus.Invalid, PdfMacMessages.BytesChanged);
    }

    /// <summary>Reads the byte range: four non-negative integers, the first zero, the second start after the first length.</summary>
    /// <param name="context">The revision.</param>
    /// <returns>The four numbers, or <see langword="null"/> when the entry is missing or not as the standard requires.</returns>
    private static long[]? ReadRange(PdfMacCheckContext context)
    {
        var array = context.AuthCode.GetRaw(KnownName.ByteRange).AsArray();
        if (array is not { Count: RangeNumbers })
        {
            return null;
        }

        var range = new long[RangeNumbers];
        for (var i = 0; i < range.Length; i++)
        {
            var item = array.Get(i);
            range[i] = item.Kind == PdfKind.Integer ? item.AsInteger() : -1;
            if (range[i] < 0)
            {
                return null;
            }
        }

        return range[0] == 0 && range[SecondStartIndex] > range[FirstLengthIndex] ? range : null;
    }

    /// <summary>
    /// Reads the token from the gap in the byte range: the hexadecimal string with its brackets, digits only, and no
    /// white space or padding (ISO/TS 32004 Table 6, the <c>/MAC</c> entry).
    /// </summary>
    /// <param name="context">The revision.</param>
    /// <param name="start">The gap's first byte.</param>
    /// <param name="next">The first byte after the gap.</param>
    /// <returns>The token, or <see langword="null"/> when the gap is not such a string.</returns>
    private static byte[]? ReadToken(PdfMacCheckContext context, long start, long next)
    {
        var digits = next - start - Brackets;
        if (digits is <= 0 or > (long)PdfLimits.MaxMacTokenLength * PdfMacHex.DigitsPerByte)
        {
            return null;
        }

        using var lease = context.File.Lease(start, (int)(next - start));
        var gap = lease.Span;
        var token = new byte[digits / PdfMacHex.DigitsPerByte];
        return gap is [(byte)'<', .., (byte)'>'] && PdfMacHex.TryDecode(gap[1..^1], token) ? token : null;
    }

    /// <summary>Creates a result for the revision.</summary>
    /// <param name="context">The revision.</param>
    /// <param name="status">The status.</param>
    /// <param name="message">The sentence.</param>
    /// <returns>The result.</returns>
    private static PdfMacRevisionResult Result(PdfMacCheckContext context, PdfMacStatus status, string message) =>
        new(context.Index, status, PdfMacLocation.Standalone, message);
}
