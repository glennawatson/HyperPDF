// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Cms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Builds the file for a signature and finishes it: the update, the byte range, the signature container and the checks of the result.</summary>
internal static class PdfSigningEngine
{
    /// <summary>The most times the file is built when the finished signature is larger than the room reserved for it.</summary>
    internal const int MaxAttempts = 3;

    /// <summary>Loads the whole file before the synchronous work starts, when the document is read from a source that needs asynchronous reads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the file is loaded.</returns>
    internal static async ValueTask PrefetchAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var source = document.Objects.Source;
        if (source.NeedsPrefetch)
        {
            await source.PrefetchAsync(0, source.Length, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Plans a signature and throws when it is refused.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="identity">The signer.</param>
    /// <returns>The plan and its notes.</returns>
    /// <exception cref="PdfSigningRefusedException">Something forbids the signature; the reasons are in the exception.</exception>
    internal static PdfSigningPlanResult PlanOrThrow(PdfDocument document, PdfSigningOptions options, PdfSignerIdentity identity)
    {
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        ArgumentNullException.ThrowIfNull(options);
        var planned = PdfSigningPlanner.Plan(document, options, identity);
        return planned.Problems.Count > 0 ? throw new PdfSigningRefusedException(planned.Problems) : planned;
    }

    /// <summary>Builds the file for a signature on a private copy of the document, for a signature built once.</summary>
    /// <param name="document">The document.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="ellipticCurve">Whether the signing key is an elliptic-curve key.</param>
    /// <param name="options">The options.</param>
    /// <param name="notes">What the user should know.</param>
    /// <param name="capacity">The room to reserve for the signature value, in bytes.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The prepared file.</returns>
    /// <exception cref="PdfSigningRefusedException">The page cannot take the signature.</exception>
    /// <exception cref="PdfException">The update cannot be written.</exception>
    internal static PdfPreparedSignature Build(
        PdfDocument document,
        PdfSigningPlan plan,
        bool ellipticCurve,
        PdfSigningOptions options,
        IReadOnlyList<string> notes,
        int capacity,
        CancellationToken cancellationToken)
    {
        using var workspace = PdfSigningWorkspace.Open(document);
        return PdfSigningEngine.Build(workspace, plan, ellipticCurve, options, notes, capacity, cancellationToken);
    }

    /// <summary>Builds the file for a signature: the update with room reserved for the signature value and the byte range patched in.</summary>
    /// <param name="workspace">The private copy of the document; the attempts of one signature share it.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="ellipticCurve">Whether the signing key is an elliptic-curve key.</param>
    /// <param name="options">The options.</param>
    /// <param name="notes">What the user should know.</param>
    /// <param name="capacity">The room to reserve for the signature value, in bytes.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The prepared file.</returns>
    /// <exception cref="PdfSigningRefusedException">The page cannot take the signature.</exception>
    /// <exception cref="PdfException">The update cannot be written.</exception>
    internal static PdfPreparedSignature Build(
        PdfSigningWorkspace workspace,
        PdfSigningPlan plan,
        bool ellipticCurve,
        PdfSigningOptions options,
        IReadOnlyList<string> notes,
        int capacity,
        CancellationToken cancellationToken)
    {
        // The update is built on the private copy and rolled back when the attempt ends, ready for the next attempt.
        var working = workspace.Document;
        var store = working.Objects;
        var workingPlan = OnCopy(working, plan);
        using var attempt = workspace.BeginAttempt("Sign");
        var contents = workspace.Reserve(capacity, cancellationToken);
        var created = PdfSignatureObjects.Create(working, attempt.Transaction, workingPlan, contents);
        PdfSigningDeclarations.DeclareFor(working, plan.Format, plan.Digest, ellipticCurve, options.Timestamp is not null);
        var keys = PdfSigningMac.IsNeeded(store) ? PdfSigningMac.Keys(store) : null;
        var authCode = keys is null ? null : PdfSigningMac.AuthCode(store, created.Signature);
        var bytes = PdfIncrementalWriter.BuildSigningFile(store, authCode, out var updateStart, cancellationToken);
        return new(PdfSigningFileWriter.Prepare(bytes, updateStart, capacity, cancellationToken), plan, keys, options, notes, workspace.ScratchPools, cancellationToken);
    }

    /// <summary>Gets the room to reserve for the signature value.</summary>
    /// <param name="identity">The signer.</param>
    /// <param name="options">The options.</param>
    /// <param name="withMac">Whether a PDF MAC token is attached.</param>
    /// <returns>The size in bytes.</returns>
    internal static int Capacity(PdfSignerIdentity identity, PdfSigningOptions options, bool withMac)
    {
        var signer = PdfSignerCertificate.Read(identity.Certificates[0]);
        return PdfContentsSizing.Estimate(identity.Certificates, signer, PdfContentsSizing.TimestampSize(options.Timestamp), withMac, options.Headroom);
    }

    /// <summary>Takes the digest of the file's signed bytes and builds the signed attributes the signer signs.</summary>
    /// <param name="prepared">The prepared file.</param>
    /// <param name="identity">The signer.</param>
    /// <returns>The container's makeup, the digest and the signed attributes.</returns>
    internal static PdfSignedAttributes Attributes(PdfPreparedSignature prepared, PdfSignerIdentity identity)
    {
        var plan = prepared.Plan;
        var digest = PdfSigningDigests.ToCms(plan.Digest);
        var spec = new PdfCmsSpec(digest, identity.Algorithm, plan.Format, identity.Certificates as byte[][] ?? [.. identity.Certificates], plan.Time);
        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        var length = PdfSigningFileWriter.HashSigned(prepared.File, digest, hash, prepared.ScratchPools, prepared.CancellationToken);
        var documentDigest = hash[..length].ToArray();
        return new(spec, documentDigest, PdfCmsAttributes.Build(spec, documentDigest));
    }

    /// <summary>Gets a timestamp token over a signature value.</summary>
    /// <param name="client">The client, or <see langword="null"/> for no timestamp.</param>
    /// <param name="digest">The digest of the timestamp request.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The DER token, or an empty array for no timestamp.</returns>
    internal static async ValueTask<byte[]> StampAsync(IPdfTimestampClient? client, PdfDigestMethod digest, byte[] signature, CancellationToken cancellationToken) =>
        client is null ? [] : await client.TimestampAsync(HashOf(digest, signature), digest, cancellationToken).ConfigureAwait(false);

    /// <summary>Hashes some bytes.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="data">The bytes.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="NotSupportedException">The digest cannot be computed here.</exception>
    internal static byte[] HashOf(PdfDigestMethod digest, ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        var length = CmsDigests.Hash(PdfSigningDigests.ToCms(digest), data, hash);
        return length == 0 ? throw new NotSupportedException($"This computer cannot compute the {PdfSigningDigests.DisplayName(digest)} digest.") : hash[..length].ToArray();
    }

    /// <summary>Writes the container into the prepared file and checks the result.</summary>
    /// <param name="prepared">The prepared file.</param>
    /// <param name="container">The DER container.</param>
    /// <param name="attempts">The times the file was built.</param>
    /// <param name="needed">Receives the size the container needs when it does not fit.</param>
    /// <param name="cancellationToken">Cancels before writing the signature.</param>
    /// <returns>The result, or <see langword="null"/> when the container does not fit the space reserved for it.</returns>
    internal static PdfSigningResult? TryFinish(PdfPreparedSignature prepared, byte[] container, int attempts, out int needed, CancellationToken cancellationToken)
    {
        needed = container.Length;
        if (!PdfSigningFileWriter.TryWriteContents(prepared.File, container, cancellationToken))
        {
            return null;
        }

        var plan = prepared.Plan;
        var notes = prepared.Notes;
        if (prepared.Options.VerifyOutput)
        {
            // Reading the file back only has something to add in rare cases, so the notes are copied only then.
            List<string>? added = null;
            PdfSigningVerification.Check(prepared.File, plan, ref added);
            notes = added is null ? notes : [.. notes, .. added];
        }

        // The container is in the file, so the keys that made its MAC token are not needed again.
        prepared.Mac?.Wipe();
        return new(prepared.File.File, plan.FieldName, plan.Format, plan.Digest, prepared.File.Capacity, container.Length, attempts, notes);
    }

    /// <summary>Gets the larger room to reserve after a finished signature did not fit.</summary>
    /// <param name="needed">The size the signature needs.</param>
    /// <param name="headroom">The extra bytes to add.</param>
    /// <returns>The new size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Grow(int needed, int headroom) => PdfContentsSizing.RoundUp((long)needed + Math.Max(headroom, 0));

    /// <summary>Makes the exception for a signature that did not fit even after the file was built again with more room.</summary>
    /// <param name="needed">The size the signature needs.</param>
    /// <param name="reserved">The room reserved.</param>
    /// <returns>The exception.</returns>
    internal static PdfException NoRoom(int needed, int reserved) =>
        new(PdfError.Format, $"The signature needs {needed} bytes but {reserved} were reserved for it. Raise the headroom and sign again.");

    /// <summary>Finds the plan's field in a copy of the document, because the plan holds the field's dictionaries from the document it was made on.</summary>
    /// <param name="working">The copy.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The plan with the copy's field, or the plan itself when it adds a new field.</returns>
    /// <exception cref="PdfSigningRefusedException">The field is no longer in the document.</exception>
    private static PdfSigningPlan OnCopy(PdfDocument working, PdfSigningPlan plan) =>
        plan.Field is not { } field
            ? plan
            : plan with { Field = PdfSigningFields.Find(working.Objects, field.Name) ?? throw new PdfSigningRefusedException($"The document has no signature field named \"{field.Name}\".") };
}
