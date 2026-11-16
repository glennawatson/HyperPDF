// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Reactive;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Http;

/// <summary>Downloads the files of a batch one after another, reporting one fraction done across all of them.</summary>
internal static class DownloadSequence
{
    /// <summary>Downloads each file in turn; the next starts only once the one before has finished.</summary>
    /// <typeparam name="TItem">The catalogue entry type.</typeparam>
    /// <param name="missing">The files to download.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="download">Starts the download of one file, given where its bytes count in the whole.</param>
    /// <returns>A cold signal giving one value once every file is in place; unsubscribing stops the download in progress and keeps files already finished.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<RxVoid> Run<TItem>(DownloadBatch<TItem> missing, IProgress<double> progress, Func<TItem, DownloadProgress, IObservable<long>> download) =>
        Signal.Defer(() =>
        {
            var tally = new DownloadTally();
            return LazySequence.Concat(Steps(missing, progress, download, tally))
                .Count()
                .Select(_ => Complete(progress, tally, missing.Bytes));
        });

    /// <summary>Makes the download of each file, one at a time as the caller moves along.</summary>
    /// <typeparam name="TItem">The catalogue entry type.</typeparam>
    /// <param name="missing">The files to download.</param>
    /// <param name="progress">Receives the fraction done.</param>
    /// <param name="download">Starts the download of one file.</param>
    /// <param name="tally">The bytes received so far.</param>
    /// <returns>A cold signal for each file.</returns>
    private static IEnumerable<IObservable<long>> Steps<TItem>(DownloadBatch<TItem> missing, IProgress<double> progress, Func<TItem, DownloadProgress, IObservable<long>> download, DownloadTally tally)
    {
        for (var i = 0; i < missing.Count; i++)
        {
            yield return download(missing.Items[i], new(progress, tally.Done, missing.Bytes)).Do(bytes => tally.Done += bytes);
        }
    }

    /// <summary>Reports that everything is done.</summary>
    /// <param name="progress">Receives the fraction done.</param>
    /// <param name="tally">The bytes received.</param>
    /// <param name="total">The bytes expected.</param>
    /// <returns>The completion value.</returns>
    private static RxVoid Complete(IProgress<double> progress, DownloadTally tally, long total)
    {
        new DownloadProgress(progress, tally.Done, total).Complete();
        return RxVoid.Default;
    }
}
