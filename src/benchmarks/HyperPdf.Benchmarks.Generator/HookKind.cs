// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks.Generator;

/// <summary>The four set-up and clean-up hooks of a benchmark class.</summary>
internal enum HookKind
{
    /// <summary>Runs once before a benchmark.</summary>
    GlobalSetup = 0,

    /// <summary>Runs once after a benchmark.</summary>
    GlobalCleanup = 1,

    /// <summary>Runs before each measured call.</summary>
    IterationSetup = 2,

    /// <summary>Runs after each measured call.</summary>
    IterationCleanup = 3,
}
