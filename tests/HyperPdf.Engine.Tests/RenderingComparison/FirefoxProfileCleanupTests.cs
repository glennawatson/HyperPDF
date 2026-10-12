// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Verifies bounded cleanup while a browser profile is temporarily locked.</summary>
public sealed class FirefoxProfileCleanupTests
{
    /// <summary>The expected failure message when profile files remain locked.</summary>
    private const string LockFailureMessage = "Profile remains locked.";

    /// <summary>The number of simulated lock failures before deletion succeeds.</summary>
    private const int TransientFailures = 2;

    /// <summary>The cleanup deadline used by the fast retry test.</summary>
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The retry interval used by the fast retry test.</summary>
    private static readonly TimeSpan TestPollInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>The short deadline used to verify a profile that remains locked.</summary>
    private static readonly TimeSpan LockedProfileTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>Verifies deletion retries until the simulated browser releases the profile.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_RetriesUntilProfileIsReleased()
    {
        var profile = CreateProfile();
        var attempts = 0;

        try
        {
            await FirefoxProfileCleanup.DeleteAsync(profile, DeleteAfterTransientLocks, TestTimeout, TestPollInterval, CancellationToken.None);
            await Assert.That(attempts).IsEqualTo(TransientFailures + 1);
            await Assert.That(Directory.Exists(profile)).IsFalse();
        }
        finally
        {
            DeleteIfPresent(profile);
        }

        void DeleteAfterTransientLocks(string path, bool recursive)
        {
            attempts++;
            if (attempts <= TransientFailures)
            {
                throw new IOException("The profile is still in use.");
            }

            Directory.Delete(path, recursive);
        }
    }

    /// <summary>Verifies an initially canceled request does not attempt deletion.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_InitiallyCanceled_DoesNotDeleteProfile()
    {
        var profile = CreateProfile();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var attempts = 0;

        try
        {
            await Assert.That(async () => await FirefoxProfileCleanup.DeleteAsync(
                profile,
                (path, recursive) =>
                {
                    attempts++;
                    Directory.Delete(path, recursive);
                },
                TestTimeout,
                TestPollInterval,
                cancellation.Token)).Throws<OperationCanceledException>();
            await Assert.That(attempts).IsEqualTo(0);
            await Assert.That(Directory.Exists(profile)).IsTrue();
        }
        finally
        {
            DeleteIfPresent(profile);
        }
    }

    /// <summary>Verifies a profile that stays locked reports the I/O failure at the deadline.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_PermanentLock_ReportsIoFailureAtDeadline()
    {
        var profile = CreateProfile();
        var reportedLockFailure = false;

        try
        {
            try
            {
                await FirefoxProfileCleanup.DeleteAsync(
                    profile,
                    static (_, _) => throw new IOException(LockFailureMessage),
                    LockedProfileTimeout,
                    TestPollInterval,
                    CancellationToken.None);
            }
            catch (IOException exception)
            {
                reportedLockFailure = exception.InnerException is IOException innerException
                    && innerException.Message == LockFailureMessage;
            }

            await Assert.That(reportedLockFailure).IsTrue();
            await Assert.That(Directory.Exists(profile)).IsTrue();
        }
        finally
        {
            DeleteIfPresent(profile);
        }
    }

    /// <summary>Verifies cleanup succeeds when the profile is already absent.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_AlreadyAbsentProfile_Completes()
    {
        var profile = Path.Combine(Path.GetTempPath(), $"pvl-firefox-cleanup-{Guid.NewGuid():N}");

        await FirefoxProfileCleanup.DeleteAsync(profile, CancellationToken.None);

        await Assert.That(Directory.Exists(profile)).IsFalse();
    }

    /// <summary>Verifies caller cancellation wins over a concurrent deletion I/O failure.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_CanceledDuringIoFailure_PreservesCallerCancellation()
    {
        var profile = CreateProfile();
        using var cancellation = new CancellationTokenSource();

        try
        {
            await Assert.That(async () => await FirefoxProfileCleanup.DeleteAsync(
                profile,
                (_, _) =>
                {
                    cancellation.Cancel();
                    throw new IOException(LockFailureMessage);
                },
                TestTimeout,
                TestPollInterval,
                cancellation.Token)).Throws<OperationCanceledException>();
        }
        finally
        {
            DeleteIfPresent(profile);
        }
    }

    /// <summary>Verifies unauthorized deletion failures are propagated without retry.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_UnauthorizedFailure_PropagatesImmediately()
    {
        var profile = CreateProfile();
        var attempts = 0;

        try
        {
            await Assert.That(async () => await FirefoxProfileCleanup.DeleteAsync(
                profile,
                (_, _) =>
                {
                    attempts++;
                    throw new UnauthorizedAccessException("Profile cannot be deleted.");
                },
                TestTimeout,
                TestPollInterval,
                CancellationToken.None)).Throws<UnauthorizedAccessException>();
            await Assert.That(attempts).IsEqualTo(1);
        }
        finally
        {
            DeleteIfPresent(profile);
        }
    }

    /// <summary>Verifies non-I/O deletion failures are propagated without retry.</summary>
    /// <returns>A task representing the test.</returns>
    [Test]
    public async Task DeleteAsync_NonIoFailure_PropagatesImmediately()
    {
        var profile = CreateProfile();
        var attempts = 0;

        try
        {
            await Assert.That(async () => await FirefoxProfileCleanup.DeleteAsync(
                profile,
                (_, _) =>
                {
                    attempts++;
                    throw new InvalidOperationException("Deletion failed unexpectedly.");
                },
                TestTimeout,
                TestPollInterval,
                CancellationToken.None)).Throws<InvalidOperationException>();
            await Assert.That(attempts).IsEqualTo(1);
        }
        finally
        {
            DeleteIfPresent(profile);
        }
    }

    /// <summary>Creates an isolated temporary profile directory.</summary>
    /// <returns>The profile path.</returns>
    private static string CreateProfile()
    {
        var profile = Path.Combine(Path.GetTempPath(), $"pvl-firefox-cleanup-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(profile);
        return profile;
    }

    /// <summary>Deletes a temporary profile when a test leaves it behind.</summary>
    /// <param name="profile">The profile path.</param>
    private static void DeleteIfPresent(string profile)
    {
        if (Directory.Exists(profile))
        {
            Directory.Delete(profile, true);
        }
    }
}
