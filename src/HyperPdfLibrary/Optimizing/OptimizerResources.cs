// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Reusable optimiser state owned by one registered drawing service or document service instance.</summary>
[DebuggerDisplay("OptimizerResources")]
internal sealed class OptimizerResources
{
    /// <summary>The idle optimisation sessions.</summary>
    private ReusableSlots<OptimizeSession>? _sessions;

    /// <summary>The idle resource scanners.</summary>
    private ReusableSlots<ContentUsageScanner>? _scanners;

    /// <summary>The idle duplicate finders.</summary>
    private ReusableSlots<DuplicateFinder>? _duplicates;

    /// <summary>The idle font plans.</summary>
    private ReusableSlots<FontPlanState>? _fonts;

    /// <summary>The idle font-usage devices.</summary>
    private ReusableSlots<FontUsageDevice>? _fontDevices;

    /// <summary>The idle reachable-object graphs.</summary>
    private ReusableSlots<OptimizerGraph>? _graphs;

    /// <summary>The idle file writers.</summary>
    private ReusableSlots<OptimizedFileWriter>? _writers;

    /// <summary>The idle write plans.</summary>
    private ReusableSlots<WritePlan>? _plans;

    /// <summary>The idle tagging state.</summary>
    private ReusableSlots<TaggerState>? _taggers;

    /// <summary>Gets the sessions available to this service.</summary>
    internal ReusableSlots<OptimizeSession> Sessions => Get(ref _sessions);

    /// <summary>Gets the scanners available to this service.</summary>
    internal ReusableSlots<ContentUsageScanner> Scanners => Get(ref _scanners);

    /// <summary>Gets the duplicate finders available to this service.</summary>
    internal ReusableSlots<DuplicateFinder> Duplicates => Get(ref _duplicates);

    /// <summary>Gets the font plans available to this service.</summary>
    internal ReusableSlots<FontPlanState> Fonts => Get(ref _fonts);

    /// <summary>Gets the font-usage devices available to this service.</summary>
    internal ReusableSlots<FontUsageDevice> FontDevices => Get(ref _fontDevices);

    /// <summary>Gets the object graphs available to this service.</summary>
    internal ReusableSlots<OptimizerGraph> Graphs => Get(ref _graphs);

    /// <summary>Gets the writers available to this service.</summary>
    internal ReusableSlots<OptimizedFileWriter> Writers => Get(ref _writers);

    /// <summary>Gets the write plans available to this service.</summary>
    internal ReusableSlots<WritePlan> Plans => Get(ref _plans);

    /// <summary>Gets the tagging state available to this service.</summary>
    internal ReusableSlots<TaggerState> Taggers => Get(ref _taggers);

    /// <summary>Publishes a pool when its operation is first used.</summary>
    /// <typeparam name="T">The reusable state.</typeparam>
    /// <param name="pool">The service-owned pool field.</param>
    /// <returns>The shared pool for this service instance.</returns>
    private static ReusableSlots<T> Get<T>(ref ReusableSlots<T>? pool)
        where T : class
    {
        var existing = Volatile.Read(ref pool);
        if (existing is not null)
        {
            return existing;
        }

        var created = new ReusableSlots<T>();
        return Interlocked.CompareExchange(ref pool, created, null) ?? created;
    }
}
