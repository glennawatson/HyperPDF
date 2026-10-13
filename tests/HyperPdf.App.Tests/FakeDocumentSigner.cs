// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.App.Tests;

/// <summary>A document signer that returns a fixed file and notes, and can be held up or made to fail.</summary>
internal sealed class FakeDocumentSigner : IDocumentSigner
{
    /// <summary>The bytes the fake returns.</summary>
    private static readonly byte[] Output = "%PDF-1.7 fake signed copy"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets the names of the calls received, in order.</summary>
    internal List<string> Calls { get; } = [];

    /// <summary>Gets or sets whether validation data was asked to include a record for each signature.</summary>
    internal bool? IncludedVri { get; set; }

    /// <summary>Gets or sets the notes every outcome carries.</summary>
    internal IReadOnlyList<string> Notes { get; set; } = [];

    /// <summary>Gets the managed thread the last run started on.</summary>
    internal int ThreadId { get; private set; }

    /// <summary>Gets or sets the prepared signature <see cref="PrepareAsync"/> returns; <see langword="null"/> makes it fail.</summary>
    internal IPendingSignature? Pending { get; set; }

    /// <summary>Gets or sets what the run throws, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a gate the run waits on before it finishes; <see langword="null"/> runs straight through.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that a run has started and is waiting at the gate.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public SigningPreview Preview(in SigningRequest request) => throw new NotSupportedException("The fake does not preview.");

    /// <inheritdoc/>
    public ValueTask<SigningOutcome> SignAsync(X509Certificate2 certificate, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken) =>
        RunAsync(nameof(SignAsync), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SigningOutcome> SignAsync(ISignatureProvider provider, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken) =>
        RunAsync(nameof(SignAsync), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IPendingSignature> PrepareAsync(SignerIdentity identity, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken) =>
        Pending is { } pending ? ValueTask.FromResult(pending) : throw Failure ?? new NotSupportedException("The fake does not prepare.");

    /// <inheritdoc/>
    public ValueTask<SigningOutcome> CompleteAsync(IPendingSignature pending, ReadOnlyMemory<byte> signature, CancellationToken cancellationToken) =>
        RunAsync(nameof(CompleteAsync), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SigningOutcome> CompleteWithContainerAsync(IPendingSignature pending, ReadOnlyMemory<byte> container, CancellationToken cancellationToken) =>
        RunAsync(nameof(CompleteWithContainerAsync), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SigningOutcome> AddValidationDataAsync(ISignatureRevocationSource source, bool includeVri, CancellationToken cancellationToken)
    {
        IncludedVri = includeVri;
        return RunAsync(nameof(AddValidationDataAsync), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SigningOutcome> AddDocumentTimestampAsync(ISignatureTimestamper timestamper, SigningDigest digest, CancellationToken cancellationToken) =>
        RunAsync(nameof(AddDocumentTimestampAsync), cancellationToken);

    /// <summary>Records a call, waits at the gate, fails on request, and returns the fixed file.</summary>
    /// <param name="name">The name of the call.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The outcome.</returns>
    private async ValueTask<SigningOutcome> RunAsync(string name, CancellationToken cancellationToken)
    {
        Calls.Add(name);
        ThreadId = Environment.CurrentManagedThreadId;
        _ = Started.TrySetResult();
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Failure is { } failure ? throw failure : new SigningOutcome(Output, Notes);
    }
}
