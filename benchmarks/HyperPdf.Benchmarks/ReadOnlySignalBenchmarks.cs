// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Benchmarks;

/// <summary>Measures subscriptions through the cached read-only signal views published by application services.</summary>
public class ReadOnlySignalBenchmarks
{
    /// <summary>The private notification source.</summary>
    private readonly Signal<RxVoid> _source = new();

    /// <summary>The cached view that prevents callers from publishing notifications.</summary>
    private readonly AsObservableSignal<RxVoid> _readOnly;

    /// <summary>Initializes a new instance of the <see cref="ReadOnlySignalBenchmarks"/> class.</summary>
    public ReadOnlySignalBenchmarks() => _readOnly = new(_source);

    /// <summary>Subscribes directly to the private source and removes the subscription.</summary>
    [Benchmark(Baseline = true)]
    public void SubscribeDirect()
    {
        using var subscription = _source.SubscribeSafe(static _ => { }, static _ => { });
    }

    /// <summary>Subscribes through the cached read-only view and removes the subscription.</summary>
    [Benchmark]
    public void SubscribeReadOnly()
    {
        using var subscription = _readOnly.SubscribeSafe(static _ => { }, static _ => { });
    }

    /// <summary>Creates the view allocated once when an application service publishes a signal.</summary>
    /// <returns>The read-only notification source.</returns>
    [Benchmark]
    public AsObservableSignal<RxVoid> CreateReadOnlyView() => new(_source);
}
