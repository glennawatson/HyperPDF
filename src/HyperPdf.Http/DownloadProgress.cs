// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Http;

/// <summary>Turns the bytes of one download, among several, into a fraction done of all of them, from 0 to 1.</summary>
/// <param name="Sink">Receives the fraction done.</param>
/// <param name="Before">The bytes received by the downloads before this one.</param>
/// <param name="Total">The bytes expected in all of them.</param>
internal readonly record struct DownloadProgress(IProgress<double> Sink, long Before, long Total)
{
    /// <summary>Reports the fraction done once this download has received some bytes.</summary>
    /// <param name="received">The bytes this download has received so far.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Report(long received) => Sink.Report(Math.Min(1D, (double)(Before + received) / Math.Max(1L, Total)));

    /// <summary>Reports that everything is done.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Complete() => Sink.Report(1D);
}
