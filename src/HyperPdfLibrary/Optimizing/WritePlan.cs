// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>How the optimised file is written: rewritten, appended to as an incremental update, or copied unchanged.</summary>
[DebuggerDisplay("WritePlan: {Mode}")]
internal sealed class WritePlan : IDisposable
{
    /// <summary>The bytes copied at a time when the file is copied unchanged.</summary>
    private const int CopyChunk = 1 << 16;

    /// <summary>The registered service that owns this plan's reusable state.</summary>
    private readonly OptimizerResources _resources;

    /// <summary>Receives the check of the written file's PDF/A claim.</summary>
    private readonly PdfSaveReport _save = new();

    /// <summary>The rewriting writer, when the file is rewritten.</summary>
    private OptimizedFileWriter? _writer;

    /// <summary>The changed store, when an incremental update is appended.</summary>
    private PdfObjectStore? _store;

    /// <summary>The original file, when it is copied unchanged.</summary>
    private PdfByteSource? _original;

    /// <summary>The stream object reused for validation, with its rented array released after each write.</summary>
    private PooledWriteStream? _validationBuffer;

    /// <summary>1 once the plan has been given back, so a second dispose does not offer it twice.</summary>
    private int _returned;

    /// <summary>Initializes a new instance of the <see cref="WritePlan"/> class.</summary>
    /// <param name="resources">The registered service's reusable state.</param>
    private WritePlan(OptimizerResources resources) => _resources = resources;

    /// <summary>Gets how the file is written.</summary>
    internal PdfOptimizeMode Mode { get; private set; }

    /// <summary>Gets the check of the written file's PDF/A claim, once written; <see langword="null"/> when it claims none.</summary>
    internal PdfAClaimCheck? Claim => _save.Claim;

    /// <summary>Gets the check of the written file's PDF/UA-1 claim, once written; <see langword="null"/> when it claims none.</summary>
    internal PdfUaClaimCheck? UaClaim => _save.UaClaim;

    /// <summary>Gives the plan and its writer back to their owning service.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        _writer?.Return();
        _validationBuffer?.Dispose();
        _writer = null;
        _store = null;
        _original = null;
        _save.Reset();
        _save.UnmetUaClaim = default;
        _ = _resources.Plans.Offer(this);
    }

    /// <summary>Creates a plan that rewrites the file.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The plan.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static WritePlan Rewrite(OptimizedFileWriter writer) => Rent(PdfOptimizeMode.Rewritten, writer, null, null, writer.Resources);

    /// <summary>Creates a plan that appends a store's changes as an incremental update.</summary>
    /// <param name="store">The changed store.</param>
    /// <param name="resources">The registered service's reusable state.</param>
    /// <returns>The plan.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static WritePlan Incremental(PdfObjectStore store, OptimizerResources resources) => Rent(PdfOptimizeMode.Incremental, null, store, null, resources);

    /// <summary>Creates a plan that copies the file unchanged.</summary>
    /// <param name="original">The file.</param>
    /// <param name="resources">The registered service's reusable state.</param>
    /// <returns>The plan.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static WritePlan Copy(PdfByteSource original, OptimizerResources resources) => Rent(PdfOptimizeMode.Copied, null, null, original, resources);

    /// <summary>Sets what the write does when a PDF/UA-1 claim cannot be kept.</summary>
    /// <param name="action">The action.</param>
    internal void SetUnmetUaClaim(PdfUaUnmetClaimAction action) => _save.UnmetUaClaim = action;

    /// <summary>Writes the file.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The bytes written.</returns>
    internal long WriteTo(Stream destination, CancellationToken cancellationToken)
    {
        if (_writer is not null)
        {
            if (!IsClaimed(_writer.Store))
            {
                return _writer.WriteTo(destination, cancellationToken);
            }

            using var rewritten = PrepareBuffer();
            _ = _writer.WriteTo(rewritten, cancellationToken);
            var file = CheckRewrite(rewritten.ToArray(), cancellationToken);
            destination.Write(file);
            return file.Length;
        }

        using var counter = new CountingStream(destination);
        if (_store is not null)
        {
            PdfIncrementalWriter.Save(_store, counter, _save);
            return counter.Written;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(CopyChunk);
        try
        {
            for (long offset = 0; offset < _original!.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = _original.Read(offset, buffer.AsSpan(0, CopyChunk));
                if (read <= 0)
                {
                    break;
                }

                counter.Write(buffer, 0, read);
                offset += read;
            }

            return counter.Written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Writes the file asynchronously.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The bytes written.</returns>
    internal async Task<long> WriteToAsync(Stream destination, CancellationToken cancellationToken)
    {
        if (_writer is not null)
        {
            if (!IsClaimed(_writer.Store))
            {
                return await _writer.WriteToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            var buffer = PrepareBuffer();
            await using (buffer.ConfigureAwait(false))
            {
                _ = await _writer.WriteToAsync(buffer, cancellationToken).ConfigureAwait(false);
                var file = await CheckRewriteAsync(buffer.ToArray(), cancellationToken).ConfigureAwait(false);
                await destination.WriteAsync(file, cancellationToken).ConfigureAwait(false);
                return file.Length;
            }
        }

        var counter = new CountingStream(destination);
        await using (counter.ConfigureAwait(false))
        {
            if (_store is not null)
            {
                await PdfIncrementalWriter.SaveAsync(_store, counter, _save, cancellationToken).ConfigureAwait(false);
                return counter.Written;
            }

            await CopyAsync(counter, cancellationToken).ConfigureAwait(false);
            return counter.Written;
        }
    }

    /// <summary>Takes an idle plan, or makes one.</summary>
    /// <param name="mode">How the file is written.</param>
    /// <param name="writer">The rewriting writer, or <see langword="null"/>.</param>
    /// <param name="store">The changed store, or <see langword="null"/>.</param>
    /// <param name="original">The original file, or <see langword="null"/>.</param>
    /// <param name="resources">The registered service's reusable state.</param>
    /// <returns>The plan.</returns>
    private static WritePlan Rent(PdfOptimizeMode mode, OptimizedFileWriter? writer, PdfObjectStore? store, PdfByteSource? original, OptimizerResources resources)
    {
        var plan = resources.Plans.Take() ?? new(resources);
        _ = Interlocked.Exchange(ref plan._returned, 0);
        plan.Mode = mode;
        plan._writer = writer;
        plan._store = store;
        plan._original = original;
        return plan;
    }

    /// <summary>Determines whether a file claims a level this plan checks: a PDF/A level or PDF/UA-1.</summary>
    /// <param name="store">The objects.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsClaimed(PdfObjectStore store) => PdfAClaimGuard.IsClaimed(store) || PdfUaClaimGuard.IsClaimed(store);

    /// <summary>Prepares empty temporary storage for validating the rewritten file.</summary>
    /// <returns>The stream, which the write disposes after validation.</returns>
    private PooledWriteStream PrepareBuffer()
    {
        if (_validationBuffer is null)
        {
            _validationBuffer = new();
        }
        else
        {
            _validationBuffer.Reset();
        }

        return _validationBuffer;
    }

    /// <summary>
    /// Checks a rewritten file against the PDF/A level it claims, with font data already on this computer. A file that
    /// breaks a rule is replaced by its repaired copy; when it cannot be repaired, it gets an incremental update that
    /// removes the claim, so the optimiser never writes a false claim and its layout is kept.
    /// </summary>
    /// <param name="file">The rewritten file.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The file to write.</returns>
    private byte[] CheckRewrite(byte[] file, CancellationToken cancellationToken)
    {
        var kept = KeepUaClaim(file, cancellationToken);
        using var written = PdfDocumentReader.Open(kept, null);
        return Settle(written, kept, PdfAClaimGuard.CheckOutput(written.Objects, kept, cancellationToken));
    }

    /// <summary>Checks a rewritten file against the PDF/A level it claims, downloading font data a repair needs.</summary>
    /// <param name="file">The rewritten file.</param>
    /// <param name="cancellationToken">Stops the check and the downloads.</param>
    /// <returns>The file to write.</returns>
    private async Task<byte[]> CheckRewriteAsync(byte[] file, CancellationToken cancellationToken)
    {
        var kept = KeepUaClaim(file, cancellationToken);
        using var written = PdfDocumentReader.Open(kept, null);
        var outcome = await PdfAClaimGuard.CheckOutputAsync(written.Objects, kept, cancellationToken).ConfigureAwait(false);
        return Settle(written, kept, outcome);
    }

    /// <summary>
    /// Checks a rewritten file against the PDF/UA-1 claim it makes. What a repair fixes, and a removed claim, are appended
    /// to the file as an update, so the optimiser's layout is kept. A claim that cannot be kept stops the write with the reason,
    /// or is removed when the options say so. A file kept encrypted behind an open password cannot be read back, so it is
    /// left unchecked.
    /// </summary>
    /// <param name="file">The rewritten file.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The file to write.</returns>
    /// <exception cref="PdfException">The claim cannot be kept and the options did not allow removing it.</exception>
    private byte[] KeepUaClaim(byte[] file, CancellationToken cancellationToken)
    {
        if (!PdfUaClaimGuard.IsClaimed(_writer!.Store))
        {
            return file;
        }

        PdfDocument written;
        try
        {
            written = PdfDocumentReader.Open(file, null);
        }
        catch (PdfException exception) when (exception.Error == PdfError.Password)
        {
            return file;
        }

        using (written)
        {
            PdfUaClaimGuard.Apply(written.Objects, true, _save, cancellationToken);
            return StoreEditing.HasChanges(written.Objects) ? PdfIncrementalWriter.Save(written.Objects) : file;
        }
    }

    /// <summary>Records the claim check and picks the file to write.</summary>
    /// <param name="written">The rewritten file, opened.</param>
    /// <param name="file">The rewritten file.</param>
    /// <param name="outcome">The check, or <see langword="null"/> when the file claims no level.</param>
    /// <returns>The file to write.</returns>
    private byte[] Settle(PdfDocument written, byte[] file, PdfAClaimOutcome? outcome)
    {
        _save.Claim = outcome?.Check;
        return outcome switch
        {
            { Repaired: { } repaired } => repaired,
            { Check.Removed: true } => PdfIncrementalWriter.Save(written.Objects),
            _ => file,
        };
    }

    /// <summary>Copies the original file asynchronously.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    /// <returns>A task that completes when the file is copied.</returns>
    private async Task CopyAsync(Stream destination, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyChunk);
        try
        {
            for (long offset = 0; offset < _original!.Length;)
            {
                var read = _original.Read(offset, buffer.AsSpan(0, CopyChunk));
                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                offset += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
