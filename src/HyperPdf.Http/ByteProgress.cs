// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Http;

/// <summary>Turns bytes received across several downloads into a fraction done from 0 to 1.</summary>
[DebuggerDisplay("ByteProgress: {_done} of {_total}")]
internal sealed class ByteProgress
{
    /// <summary>The bytes expected in total, at least 1.</summary>
    private readonly long _total;

    /// <summary>Receives the fraction done.</summary>
    private readonly IProgress<double> _sink;

    /// <summary>The bytes received so far.</summary>
    private long _done;

    /// <summary>Initializes a new instance of the <see cref="ByteProgress"/> class.</summary>
    /// <param name="total">The bytes expected in total.</param>
    /// <param name="sink">Receives the fraction done.</param>
    internal ByteProgress(long total, IProgress<double> sink)
    {
        _total = Math.Max(1L, total);
        _sink = sink;
    }

    /// <summary>Counts bytes received and reports the new fraction.</summary>
    /// <param name="bytes">The bytes received.</param>
    internal void Add(int bytes)
    {
        _done += bytes;
        _sink.Report(Math.Min(1D, (double)_done / _total));
    }

    /// <summary>Reports that everything is done.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Complete() => _sink.Report(1D);
}
