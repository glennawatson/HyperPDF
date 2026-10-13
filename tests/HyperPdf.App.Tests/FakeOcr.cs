// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Services;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;

namespace HyperPdf.App.Tests;

/// <summary>
/// A text recogniser setup that never touches the network or the person's own folders. English "ships" in a test
/// folder, other languages are found only in the test's pack folder, downloads write placeholder packs there, and the
/// recogniser reads one word on each scanned page with a chosen confidence.
/// </summary>
[DebuggerDisplay("FakeOcr: Downloads={Downloads}")]
internal sealed class FakeOcr : IOcrEngine
{
    /// <summary>The confidence of a clearly read word.</summary>
    internal const float Clear = 95;

    /// <summary>The confidence of a word that probably is not in the chosen language.</summary>
    internal const float Poor = 30;

    /// <summary>The left edge of the word read, in points.</summary>
    private const float WordLeft = 72;

    /// <summary>The top edge of the word read, in points.</summary>
    private const float WordTop = 72;

    /// <summary>The right edge of the word read, in points.</summary>
    private const float WordRight = 144;

    /// <summary>The bottom edge of the word read, in points.</summary>
    private const float WordBottom = 90;

    /// <summary>The longest the recogniser waits at the gate; its call cannot be awaited.</summary>
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    /// <summary>Whether the Tesseract library is reported as installed.</summary>
    private readonly bool _engineInstalled;

    /// <summary>Initializes a new instance of the <see cref="FakeOcr"/> class.</summary>
    /// <param name="engineInstalled">Whether the Tesseract library is reported as installed.</param>
    internal FakeOcr(bool engineInstalled) => _engineInstalled = engineInstalled;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public OcrEngineStatus Status => OcrEngineStatus.Ready;

    /// <summary>Gets or sets a gate the recogniser waits at until it is completed; <see langword="null"/> reads at once.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that the recogniser is waiting at the gate.</summary>
    internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets or sets the confidence of the word read on each scanned page.</summary>
    internal float Confidence { get; set; } = Clear;

    /// <summary>Gets the number of downloads asked for.</summary>
    internal int Downloads { get; private set; }

    /// <summary>Gets the language settings the recogniser was made for.</summary>
    internal List<string> Created { get; } = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        // The test owns the fake.
    }

    /// <inheritdoc/>
    public void Recognize(ReadOnlySpan<byte> image, int width, int height, float pixelsPerPoint, List<OcrWord> output)
    {
        if (Gate is { } gate)
        {
            _ = Reached.TrySetResult();

            // The call is synchronous, so the hold on a pool thread polls the release with a bound.
            _ = SpinWait.SpinUntil(() => gate.Task.IsCompleted, HoldLimit);
        }

        output.Add(new("Hello", PageRect.FromEdges(WordLeft, WordTop, WordRight, WordBottom), Confidence));
    }

    /// <summary>Creates a setup using this fake, with English shipped in <c>bundled</c> beside the pack folder.</summary>
    /// <param name="directory">The pack folder.</param>
    /// <returns>The setup.</returns>
    internal OcrSetup CreateSetup(string directory)
    {
        var bundled = Path.Combine(Path.GetDirectoryName(directory)!, "bundled");
        _ = Directory.CreateDirectory(bundled);
        File.WriteAllText(Path.Combine(bundled, "eng.traineddata"), "placeholder");
        return new(directory, bundled, () => _engineInstalled, (languages, packs) => FindInFolders(languages, packs, bundled), CreateEngine, DownloadAsync);
    }

    /// <summary>Writes a placeholder of the right size and recorded hash for each pack, as a finished download would.</summary>
    /// <param name="packs">The packs.</param>
    /// <param name="directory">The pack folder.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A task.</returns>
    internal Task DownloadAsync(IReadOnlyList<OcrLanguagePack> packs, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        Downloads++;
        _ = Directory.CreateDirectory(directory);
        foreach (var pack in packs)
        {
            var path = Path.Combine(directory, pack.FileName);
            using (var file = File.Create(path))
            {
                file.SetLength(pack.Bytes);
            }

            File.WriteAllText($"{path}.sha256", pack.Sha256);
        }

        progress.Report(1);
        return Task.CompletedTask;
    }

    /// <summary>Finds one folder holding every language: the pack folder first, then the shipped folder.</summary>
    /// <param name="languages">The language setting.</param>
    /// <param name="packs">The pack folder.</param>
    /// <param name="bundled">The shipped folder.</param>
    /// <returns>The folder, or <see langword="null"/>.</returns>
    private static string? FindInFolders(string languages, string packs, string bundled)
    {
        if (HasAll(languages, packs))
        {
            return packs;
        }

        return HasAll(languages, bundled) ? bundled : null;
    }

    /// <summary>Determines whether a folder holds every language in a setting.</summary>
    /// <param name="languages">The language setting.</param>
    /// <param name="directory">The folder.</param>
    /// <returns><see langword="true"/> when all are there.</returns>
    private static bool HasAll(string languages, string directory)
    {
        foreach (var code in OcrLanguageCatalog.Parse(languages))
        {
            if (!File.Exists(Path.Combine(directory, $"{code}.traineddata")))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Records the recogniser asked for and returns this fake.</summary>
    /// <param name="languages">The language setting.</param>
    /// <param name="directory">The pack folder.</param>
    /// <returns>This fake.</returns>
    private FakeOcr CreateEngine(string languages, string directory)
    {
        Created.Add(languages);
        return this;
    }
}
