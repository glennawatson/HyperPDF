// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Waits for browser processes to release a profile before removing it.</summary>
internal static class FirefoxProfileCleanup
{
    /// <summary>The maximum time to wait for profile handles to close.</summary>
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The interval between checks for released profile handles.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Removes a Firefox profile after its files become available.</summary>
    /// <param name="profile">The temporary profile directory.</param>
    /// <param name="cancellationToken">Cancels cleanup.</param>
    /// <returns>A task representing profile cleanup.</returns>
    /// <exception cref="IOException">A browser still holds profile files at the cleanup deadline.</exception>
    internal static Task DeleteAsync(string profile, CancellationToken cancellationToken) =>
        DeleteAsync(profile, Directory.Delete, CleanupTimeout, PollInterval, cancellationToken);

    /// <summary>Retries profile deletion while the supplied delete operation reports a transient lock.</summary>
    /// <param name="profile">The temporary profile directory.</param>
    /// <param name="delete">The directory deletion operation.</param>
    /// <param name="timeout">The maximum retry duration.</param>
    /// <param name="pollInterval">The wait between deletion attempts.</param>
    /// <param name="cancellationToken">Cancels cleanup.</param>
    /// <returns>A task representing profile cleanup.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The timeout or poll interval is not positive.</exception>
    /// <exception cref="IOException">The profile remains locked at the cleanup deadline.</exception>
    internal static async Task DeleteAsync(
        string profile,
        Action<string, bool> delete,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

        cancellationToken.ThrowIfCancellationRequested();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        using var timer = new PeriodicTimer(pollInterval);
        IOException? lastFailure = null;
        while (Directory.Exists(profile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            lastFailure = TryDelete(profile, delete, cancellationToken, bounded.Token) ?? lastFailure;

            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(profile))
            {
                return;
            }

            try
            {
                _ = await timer.WaitForNextTickAsync(bounded.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (Directory.Exists(profile))
                {
                    throw DeadlineException((Exception?)lastFailure ?? exception);
                }

                return;
            }
        }
    }

    /// <summary>Attempts profile deletion and retains transient lock failures for the deadline report.</summary>
    /// <param name="profile">The temporary profile directory.</param>
    /// <param name="delete">The directory deletion operation.</param>
    /// <param name="cancellationToken">Cancels cleanup.</param>
    /// <param name="boundedToken">Cancels cleanup at its internal deadline.</param>
    /// <returns>The transient I/O failure, or <see langword="null"/> when deletion succeeds.</returns>
    private static IOException? TryDelete(
        string profile,
        Action<string, bool> delete,
        CancellationToken cancellationToken,
        CancellationToken boundedToken)
    {
        try
        {
            delete(profile, true);
            return null;
        }
        catch (IOException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (boundedToken.IsCancellationRequested)
            {
                throw DeadlineException(exception);
            }

            return exception;
        }
    }

    /// <summary>Creates the error reported when the profile still exists at the cleanup deadline.</summary>
    /// <param name="cause">The last deletion failure or internal timeout.</param>
    /// <returns>The cleanup deadline failure.</returns>
    private static IOException DeadlineException(Exception cause) =>
        new("Firefox still holds files in its temporary profile after the cleanup deadline.", cause);
}
