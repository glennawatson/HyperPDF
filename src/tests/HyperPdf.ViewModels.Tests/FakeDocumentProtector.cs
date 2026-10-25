// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Protection;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A protector that records its calls and returns what a test sets, so the windows can be tested without an engine.</summary>
internal sealed class FakeDocumentProtector : IDocumentProtector
{
    /// <summary>The bytes the fake writes when the copy is made.</summary>
    private static readonly byte[] Output = "%PDF-2.0 fake protected copy"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets or sets the status the fake reports.</summary>
    internal ProtectionStatus Status { get; set; } = new(false, true, false);

    /// <summary>Gets or sets the reason the copy is refused; <see langword="null"/> makes it.</summary>
    internal string? Refusal { get; set; }

    /// <summary>Gets or sets what the run throws, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a gate the run waits on before it finishes; <see langword="null"/> runs straight through.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that the run has started and is waiting at the gate.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets how many times a copy was asked for.</summary>
    internal int Calls { get; private set; }

    /// <summary>Gets the last protect request, or <see langword="null"/>.</summary>
    internal ProtectionRequest? LastRequest { get; private set; }

    /// <summary>Gets the owner password of the last removal, or <see langword="null"/>.</summary>
    internal string? LastOwnerPassword { get; private set; }

    /// <inheritdoc/>
    public ProtectionStatus GetStatus() => Status;

    /// <inheritdoc/>
    public Task<ProtectionResult> ProtectAsync(ProtectionRequest request, Stream destination, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return RunAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ProtectionResult> RemoveProtectionAsync(string? ownerPassword, Stream destination, CancellationToken cancellationToken)
    {
        LastOwnerPassword = ownerPassword;
        return RunAsync(destination, cancellationToken);
    }

    /// <summary>Waits at the gate, then writes the copy or refuses.</summary>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The result.</returns>
    private async Task<ProtectionResult> RunAsync(Stream destination, CancellationToken cancellationToken)
    {
        Calls++;
        _ = Started.TrySetResult();
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }

        if (Refusal is { } refusal)
        {
            return new(refusal, []);
        }

        await destination.WriteAsync(Output, cancellationToken).ConfigureAwait(false);
        return new(null, ["Screen readers can still read this file."]);
    }
}
