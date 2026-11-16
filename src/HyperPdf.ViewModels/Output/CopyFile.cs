// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Output;

/// <summary>Writes a copy of the open document to a temporary file and moves it into place only when the copy was made.</summary>
internal static class CopyFile
{
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
