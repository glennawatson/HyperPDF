// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Services;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>A second launch with files hands them to the first launch and ends, while a launch with no files starts a viewer of its own.</summary>
[NotInParallel]
public sealed class SingleInstanceTests
{
    /// <summary>The longest the test waits for the files to arrive, in seconds.</summary>
    private const double TimeoutSeconds = 10;

    /// <summary>The wait between looks for the files, in milliseconds.</summary>
    private const int LookMilliseconds = 10;

    /// <summary>The characters of a new GUID that make a test pipe name unique.</summary>
    private const int GuidCharacters = 12;

    /// <summary>The length of a user name that is too long to go into a pipe name.</summary>
    private const int LongUserNameLength = 80;

    /// <summary>The longest pipe name the tests allow, so the path stays under the macOS limit.</summary>
    private const int MaxPipeNameLength = 40;

    /// <summary>The file the second launch names.</summary>
    private const string SecondFile = "second.pdf";

    /// <summary>The first launch keeps the pipe, the second hands over its file and ends, and the first gets the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondLaunchHandsItsFileToTheFirst()
    {
        var name = TestPipeName();
        var received = new List<string>();
        using var first = SingleInstance.Start([], name);
        using var follow = first!.Requests.SubscribeSafe(received.Add, ErrorTrace.Write);
        var second = SingleInstance.Start([SecondFile], name);
        var started = Stopwatch.GetTimestamp();
        while (received.Count == 0 && Stopwatch.GetElapsedTime(started).TotalSeconds < TimeoutSeconds)
        {
            await Task.Delay(LookMilliseconds);
        }

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNull();
        await Assert.That(received.Count).IsEqualTo(1);
        await Assert.That(Path.GetFileName(received[0])).IsEqualTo(SecondFile);
        await Assert.That(Path.IsPathRooted(received[0])).IsTrue();
    }

    /// <summary>A launch with files, while no viewer runs, becomes the viewer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LaunchWithFilesBecomesTheViewerWhenNoneRuns()
    {
        using var only = SingleInstance.Start([SecondFile], TestPipeName());
        await Assert.That(only).IsNotNull();
    }

    /// <summary>A long user name is shortened so the pipe path fits a macOS socket, and the same name always gives the same pipe.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongUserNamesGiveShortStablePipeNames()
    {
        var longName = new string('a', LongUserNameLength);

        await Assert.That(SingleInstance.NameFor("glenn")).EndsWith("glenn");
        await Assert.That(SingleInstance.NameFor(longName).Length).IsLessThanOrEqualTo(MaxPipeNameLength);
        await Assert.That(SingleInstance.NameFor(longName)).IsEqualTo(SingleInstance.NameFor(longName));
        await Assert.That(SingleInstance.NameFor(longName)).IsNotEqualTo(SingleInstance.NameFor($"{longName}b"));
    }

    /// <summary>Makes a pipe name for one test, short enough for a macOS socket path.</summary>
    /// <returns>The name.</returns>
    private static string TestPipeName() => $"HyperPdfTest-{Guid.NewGuid().ToString("N")[..GuidCharacters]}";
}
