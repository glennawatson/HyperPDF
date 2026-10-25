// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<HyperPdfLibrary.Tests.TestParallelLimit>]

namespace HyperPdfLibrary.Tests;

/// <summary>
/// Caps how many tests of one test process run at once. The test driver already runs several test processes on CI;
/// rendering, decoding and browser tests also start their own threads. Keep those tests sequential inside each CI
/// process so the process-level and test-level limits do not multiply the runner's CPU budget.
/// </summary>
internal sealed class TestParallelLimit : IParallelLimit
{
    /// <summary>The tests allowed per processor; tests also wait on I/O, so this is above one.</summary>
    private const int TestsPerProcessor = 2;

    /// <summary>Gets the most tests of this process that run at the same time.</summary>
    public int Limit => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
        ? 1
        : Math.Max(1, Environment.ProcessorCount * TestsPerProcessor);
}
