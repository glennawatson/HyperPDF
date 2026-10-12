// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Optimizing;

/// <summary>Something an optimisation left alone, and why.</summary>
/// <param name="Area">The kind of change that was skipped.</param>
/// <param name="Reason">Why it was left alone, in plain words.</param>
[DebuggerDisplay("OptimizeSkip: {Area} {Reason}")]
public sealed record OptimizeSkip(OptimizeArea Area, string Reason);
