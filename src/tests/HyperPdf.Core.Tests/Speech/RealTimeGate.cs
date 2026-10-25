// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests.Speech;

/// <summary>
/// Lets one real-voice test run at a time across test processes: the suite runs this project once per target
/// framework at the same time, and two voices synthesizing together on the same processor would make each look slower
/// than real time when neither is. A file opened for exclusive use is the lock, so it works on every platform.
/// </summary>
internal static class RealTimeGate
{
    /// <summary>How long to wait between attempts to take the lock.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(200);

    /// <summary>The longest wait for the lock before timing anyway.</summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(30);

    /// <summary>Gets the lock file.</summary>
    private static string LockPath { get; } = Path.Combine(Path.GetTempPath(), "hyperpdf-realtime-voice.lock");

    /// <summary>Takes the lock, waiting for a timed test in another process to finish.</summary>
    /// <returns>The lock, released when disposed; <see langword="null"/> when it could not be taken in time.</returns>
    internal static async Task<IDisposable?> EnterAsync()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < MaxWait)
        {
            try
            {
                return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                await Task.Delay(Retry);
            }
        }

        return null;
    }
}
