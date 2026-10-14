// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<HyperPdfLibrary.Tests.TestParallelLimit>]

namespace HyperPdfLibrary.Tests;

/// <summary>
/// Caps how many tests of one test process run at once. TUnit's default is four tests per processor, and CI runs several
/// test processes side by side on a runner with four cores. Many tests spin threads or run browsers, and on Windows that
/// many runnable threads starved tests until their time limits passed.
/// </summary>
internal sealed class TestParallelLimit : IParallelLimit
{
    /// <summary>The tests allowed per processor; tests also wait on I/O, so this is above one.</summary>
    private const int TestsPerProcessor = 2;

    /// <summary>Gets the most tests of this process that run at the same time.</summary>
    public int Limit => Math.Max(1, Environment.ProcessorCount * TestsPerProcessor);
}
