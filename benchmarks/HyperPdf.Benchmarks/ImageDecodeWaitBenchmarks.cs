// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures completed and contended synchronous image decode waits.</summary>
public class ImageDecodeWaitBenchmarks : IThreadPoolWorkItem
{
    /// <summary>A decode that finished before its waiter arrived.</summary>
    private readonly ImageDecode _completed = new();

    /// <summary>The current decode, published before its owner is signalled.</summary>
    private ImageDecode? _pending;

    /// <summary>Gets or sets the duration of work performed by the decode owner.</summary>
    [Params(100, 10_000)]
    public int DecodeMicroseconds { get; set; }

    /// <summary>Completes the uncontended fixture before measurement.</summary>
    [GlobalSetup]
    public void Setup() => _completed.Complete();

    /// <summary>Returns immediately when the decode has completed.</summary>
    [Benchmark]
    public void Completed() => _completed.Wait(CancellationToken.None);

    /// <summary>Waits for a separate owner doing a short decode, including the real per-decode coordination object.</summary>
    [Benchmark]
    public void Contended()
    {
        var pending = new ImageDecode();
        Volatile.Write(ref _pending, pending);
        _ = ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
        pending.Wait(CancellationToken.None);
    }

    /// <inheritdoc/>
    void IThreadPoolWorkItem.Execute()
    {
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start).TotalMicroseconds < DecodeMicroseconds)
        {
            Thread.SpinWait(1);
        }

        Volatile.Read(ref _pending)!.Complete();
    }
}
