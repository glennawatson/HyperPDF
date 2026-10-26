// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests;

/// <summary>
/// Gives the test process enough ready pool threads before any test starts. Many tests block a thread while they wait, and the
/// pool adds a thread only about once a second beyond its minimum, so on a Windows runner a continuation that cancels or
/// completes a test's wait queued for ten seconds or more and the test missed its limit.
/// </summary>
internal static class TestThreadPool
{
    /// <summary>The ready pool threads per processor.</summary>
    private const int ThreadsPerProcessor = 16;

    /// <summary>Raises the pool's minimum thread counts for the test session.</summary>
    [Before(TestSession)]
    public static void Prepare()
    {
        ThreadPool.GetMinThreads(out var workers, out var completionPorts);
        var wanted = Environment.ProcessorCount * ThreadsPerProcessor;
        _ = ThreadPool.SetMinThreads(Math.Max(workers, wanted), completionPorts);
    }
}
