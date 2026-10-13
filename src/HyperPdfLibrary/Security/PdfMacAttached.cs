// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Checks a PDF MAC attached to a signature (ISO/TS 32004:2024 6.5.2 and B.2.3): the trailer's <c>/AuthCode</c> points at the
/// signature dictionary, whose container carries the token as an unsigned attribute. The token's data digest covers the
/// signature's byte range and its signature digest covers the signature value.
/// </summary>
internal static class PdfMacAttached
{
    /// <summary>Checks a revision's attached PDF MAC.</summary>
    /// <param name="context">The revision and its <c>/AuthCode</c>.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfMacRevisionResult Check(PdfMacCheckContext context, CancellationToken cancellationToken)
    {
        var reference = context.AuthCode.GetRaw(context.Names.SigObjRef);
        if (context.AuthCode.ContainsKey(KnownName.ByteRange) || context.AuthCode.ContainsKey(context.Names.Mac) || !reference.IsReference
            || StoreReading.Resolve(context.Raw, reference).AsDictionary() is not { } signature)
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.BadLayout);
        }

        var contents = signature.GetStringBytes(KnownName.Contents);
        var range = ReadRange(signature.GetArray(KnownName.ByteRange));
        var check = range is null ? default : PdfByteRangeChecker.Check(range, contents.Length, context.File, context.Revisions);
        if (range is null || !check.IsValid || check.RevisionIndex != context.Index)
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.BadLayout);
        }

        return context.CoversRevision(check.SignedEnd)
            ? Verify(context, contents, range, cancellationToken)
            : Result(context, PdfMacStatus.Invalid, PdfMacMessages.Stale);
    }

    /// <summary>Finds the token in the signature and checks it.</summary>
    /// <param name="context">The revision.</param>
    /// <param name="contents">The signature's <c>/Contents</c>.</param>
    /// <param name="range">The signature's byte range.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    private static PdfMacRevisionResult Verify(PdfMacCheckContext context, ReadOnlySpan<byte> contents, long[] range, CancellationToken cancellationToken)
    {
        var lookup = PdfMacSignature.Find(contents, out var tokenRange, out var signatureRange);
        if (lookup != PdfMacSignatureLookup.Found)
        {
            return lookup == PdfMacSignatureLookup.NotFound
                ? Result(context, PdfMacStatus.Missing, PdfMacMessages.NoSignatureMac)
                : Result(context, PdfMacStatus.Invalid, PdfMacMessages.ForToken(PdfMacTokenStatus.Malformed, CmsDigestAlgorithm.Unknown));
        }

        var token = contents[tokenRange];
        var verified = PdfMacTokenReader.Verify(token, context.Security.FileKey, context.Salt);
        if (verified.Status != PdfMacTokenStatus.Valid)
        {
            return Result(context, PdfMacMessages.ForTokenStatus(verified.Status), PdfMacMessages.ForToken(verified.Status, verified.Digest));
        }

        if (!verified.HasSignatureDigest)
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.NoSignatureDigest);
        }

        Span<byte> digest = stackalloc byte[CmsDigests.MaxLength];
        var length = PdfMacDigest.Hash(context.File, verified.Digest, range, digest, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(digest[..length], token[verified.DataDigest]))
        {
            return Result(context, PdfMacStatus.Invalid, PdfMacMessages.BytesChanged);
        }

        length = PdfMacDigest.Hash(verified.Digest, contents[signatureRange], digest);
        return CryptographicOperations.FixedTimeEquals(digest[..length], token[verified.SignatureDigest])
            ? Result(context, PdfMacStatus.Valid, PdfMacMessages.Matches)
            : Result(context, PdfMacStatus.Invalid, PdfMacMessages.SignatureChanged);
    }

    /// <summary>Reads a signature's byte range as integers.</summary>
    /// <param name="array">The /ByteRange array, or <see langword="null"/>.</param>
    /// <returns>The numbers, or <see langword="null"/> when the array is missing or holds anything but integers.</returns>
    private static long[]? ReadRange(PdfArray? array)
    {
        if (array is null)
        {
            return null;
        }

        var range = new long[array.Count];
        for (var i = 0; i < range.Length; i++)
        {
            var item = array.Get(i);
            if (item.Kind != PdfKind.Integer)
            {
                return null;
            }

            range[i] = item.AsInteger();
        }

        return range;
    }

    /// <summary>Creates a result for the revision.</summary>
    /// <param name="context">The revision.</param>
    /// <param name="status">The status.</param>
    /// <param name="message">The sentence.</param>
    /// <returns>The result.</returns>
    private static PdfMacRevisionResult Result(PdfMacCheckContext context, PdfMacStatus status, string message) =>
        new(context.Index, status, PdfMacLocation.AttachedToSignature, message);
}
