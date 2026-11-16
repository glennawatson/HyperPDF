// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Output;

/// <summary>Writes a copy of the open document to a temporary file and moves it into place only when the copy was made.</summary>
internal static class CopyFile
{
    /// <summary>The bytes copied at a time.</summary>
    private const int ChunkSize = 81_920;

    /// <summary>Writes the copy on the pool, then moves it over <paramref name="path"/> when it was made.</summary>
    /// <typeparam name="TResult">What the library reports about the copy.</typeparam>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="suffix">The ending of the temporary file.</param>
    /// <param name="write">Writes the copy to the stream it is given; this is the PDF library's async call, the one task here.</param>
    /// <param name="wasMade">Whether the result means a copy was made, so the file is kept.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>
    /// A cold signal giving the result on the interface thread. Nothing is left at <paramref name="path"/> when the copy was
    /// refused, failed or stopped, and unsubscribing deletes the temporary file.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<TResult> Write<TResult>(
        string path,
        string suffix,
        Func<Stream, CancellationToken, Task<TResult>> write,
        Func<TResult, bool> wasMade,
        CancellationToken cancellationToken) =>
        Signal.Using(
            () => new TemporaryCopy(path + suffix),
            copy =>

                // The library reads and rewrites the whole document before its first await, so the write starts on the
                // pool to keep the window responsive; the library cannot hop threads itself.
                Signal.FromAsync(token => write(copy.Stream, token), cancellationToken)
                    .SubscribeOn(TaskPoolSequencer.Default)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Do(result => Finish(copy, path, wasMade(result)))
                    .Catch<TResult, Exception>(error => Discard<TResult>(copy, error)));

    /// <summary>Writes the copy with plain file I/O on the pool, then moves it over <paramref name="path"/> when it was made.</summary>
    /// <typeparam name="TResult">What the write reports about the copy.</typeparam>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="suffix">The ending of the temporary file.</param>
    /// <param name="write">Writes the copy to the stream it is given, stopping soon after its token is cancelled.</param>
    /// <param name="wasMade">Whether the result means a copy was made, so the file is kept.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A cold signal giving the result on the interface thread; unsubscribing stops the write and deletes the temporary file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<TResult> WriteBlocking<TResult>(
        string path,
        string suffix,
        Func<Stream, CancellationToken, TResult> write,
        Func<TResult, bool> wasMade,
        CancellationToken cancellationToken) =>
        Signal.Using(
            () => new TemporaryCopy(path + suffix),
            copy => Signal.Using(
                    () => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
                    linked => Signal.Start(() => write(copy.Stream, linked.Token), TaskPoolSequencer.Default))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Do(result => Finish(copy, path, wasMade(result)))
                .Catch<TResult, Exception>(error => Discard<TResult>(copy, error)));

    /// <summary>Copies one stream to another in chunks, stopping when the token is cancelled.</summary>
    /// <param name="input">What to read.</param>
    /// <param name="output">Where to write.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    /// <returns><see langword="true"/> once everything is copied.</returns>
    internal static bool CopyChunks(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Moves the temporary file over the destination when the copy was made, and deletes it otherwise.</summary>
    /// <param name="copy">The temporary file.</param>
    /// <param name="path">The destination.</param>
    /// <param name="made">Whether the copy was made.</param>
    private static void Finish(TemporaryCopy copy, string path, bool made)
    {
        if (made)
        {
            copy.MoveTo(path);
            return;
        }

        copy.Dispose();
    }

    /// <summary>Deletes the temporary file at once when the write failed or was stopped.</summary>
    /// <typeparam name="TResult">What the library reports about the copy.</typeparam>
    /// <param name="copy">The temporary file.</param>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that fails with the same error.</returns>
    private static IObservable<TResult> Discard<TResult>(TemporaryCopy copy, Exception error)
    {
        copy.Dispose();
        return Signal.Throw<TResult>(error);
    }
}
