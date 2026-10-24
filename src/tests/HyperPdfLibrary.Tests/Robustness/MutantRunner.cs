// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Opens a damaged file on a worker thread, reads everything from it and reports a crash or hang.</summary>
internal static class MutantRunner
{
    /// <summary>The processor count for the unscaled parsing budget.</summary>
    private const int BudgetProcessors = 16;

    /// <summary>The parsing budget on a machine with enough processors for the parallel tests.</summary>
    private const int ParsingSeconds = 10;

    /// <summary>The hard bound, including a worker that ignores cancellation.</summary>
    private const int HardSeconds = 20;

    /// <summary>Opens and reads one file. Only <see cref="PdfException"/> is an acceptable failure.</summary>
    /// <param name="file">The damaged file.</param>
    /// <returns>Whether the file opened, and a description of the problem or <see langword="null"/> when it behaved.</returns>
    internal static Task<MutantResult> RunAsync(byte[] file) =>
        RunAsync(file, GetLimits(Environment.ProcessorCount), Exercise);

    /// <summary>Runs a file worker with explicit deadlines, including workers that ignore cancellation.</summary>
    /// <param name="file">The damaged file.</param>
    /// <param name="limits">The parsing and hard deadlines.</param>
    /// <param name="exercise">The file worker.</param>
    /// <returns>The worker result, or a hang diagnostic.</returns>
    internal static async Task<MutantResult> RunAsync(byte[] file, MutantLimits limits, Func<byte[], CancellationToken, MutantResult> exercise)
    {
        try
        {
            // Other robustness tests keep readers busy until disposal. A dedicated worker cannot queue behind them.
            return await Task.Factory.StartNew(
                static state =>
                {
                    var worker = (Worker)state!;
                    return ExerciseTimed(worker.File, worker.Limit, worker.Exercise);
                },
                new Worker(file, limits.Parsing, exercise),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).WaitAsync(limits.Hard);
        }
        catch (TimeoutException)
        {
            return new(false, "hung past the hard limit");
        }
    }

    /// <summary>Scales bounded deadlines by the processors available to the test process.</summary>
    /// <param name="processors">The available processor count.</param>
    /// <returns>The parsing and hard deadlines.</returns>
    internal static MutantLimits GetLimits(int processors)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processors);
        var scale = ((BudgetProcessors - 1) / Math.Min(processors, BudgetProcessors)) + 1;
        return new(TimeSpan.FromSeconds(ParsingSeconds * scale), TimeSpan.FromSeconds(HardSeconds * scale));
    }

    /// <summary>Starts the parsing deadline when a worker begins processing the file.</summary>
    /// <param name="bytes">The damaged file.</param>
    /// <param name="limit">The parsing deadline.</param>
    /// <param name="exercise">The file worker.</param>
    /// <returns>The parsing result.</returns>
    private static MutantResult ExerciseTimed(byte[] bytes, TimeSpan limit, Func<byte[], CancellationToken, MutantResult> exercise)
    {
        using var timeout = new CancellationTokenSource(limit);
        return exercise(bytes, timeout.Token);
    }

    /// <summary>Opens a file and reads everything from it.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="token">Cancelled when the file has run too long.</param>
    /// <returns>Whether the file opened, and a description of the problem or <see langword="null"/>.</returns>
    private static MutantResult Exercise(byte[] bytes, CancellationToken token)
    {
        try
        {
            using var document = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { CancellationToken = token });
            _ = DocumentExerciser.Read(document);
            return new(true, null);
        }
        catch (PdfException)
        {
            return new(false, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    /// <summary>Inputs for the dedicated worker.</summary>
    /// <param name="File">The damaged file.</param>
    /// <param name="Limit">The parsing deadline.</param>
    /// <param name="Exercise">The file worker.</param>
    private sealed record Worker(byte[] File, TimeSpan Limit, Func<byte[], CancellationToken, MutantResult> Exercise);
}
