// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Stores predefined CMaps by name.</summary>
[DebuggerDisplay("{Cache.Count} predefined CMaps")]
internal sealed class PredefinedCMapState
{
    /// <summary>Gets the gate for cache lookup and recursive usecmap loading.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets the parsed maps.</summary>
    internal Dictionary<string, CompositeCMap> Cache { get; } = [with(StringComparer.Ordinal)];
}
