// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>
/// Opens thousands of damaged documents and reads everything from each. Only <see cref="PdfException"/> may escape, and
/// each mutant must finish in time. Set HYPERPDF_FUZZ_ITERATIONS for more mutants per seed and
/// HYPERPDF_FUZZ_SEED to change the random seed.
/// </summary>
public sealed class FuzzTests
{
    /// <summary>The environment variable holding the mutants per seed document.</summary>
    private const string IterationsVariable = "HYPERPDF_FUZZ_ITERATIONS";

    /// <summary>The environment variable holding the random seed.</summary>
    private const string SeedVariable = "HYPERPDF_FUZZ_SEED";

    /// <summary>The mutants per seed document when the environment does not say.</summary>
    private const int DefaultIterations = 120;

    /// <summary>The random seed when the environment does not say.</summary>
    private const int DefaultSeed = 20_260_501;

    /// <summary>The fraction of mutants that must open: at least one in this many.</summary>
    private const int OpenedShare = 4;

    /// <summary>The multiplier of the name hash.</summary>
    private const int HashMultiplier = 31;

    /// <summary>Damaged copies of generated documents open and read, or fail with a <see cref="PdfException"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedGeneratedDocumentsNeverCrashOrHang()
    {
        var failures = new List<string>();
        var tally = new Tally();
        var iterations = ReadNumber(IterationsVariable, DefaultIterations);
        foreach (var seed in RobustnessSeeds.Create())
        {
            await FuzzAsync(seed.Name, seed.Bytes, iterations, failures, tally);
        }

        await Assert.That(failures).IsEmpty();

        // Damage that always stops the open would test nothing past it; some mutants must survive to be read.
        await Assert.That(tally.Opened).IsGreaterThan(tally.Total / OpenedShare);
    }

    /// <summary>Reads a positive number from an environment variable.</summary>
    /// <param name="name">The variable.</param>
    /// <param name="fallback">The value when the variable is missing or not a positive number.</param>
    /// <returns>The number.</returns>
    private static int ReadNumber(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;

    /// <summary>Hashes a name the same way in every process, unlike <see cref="string.GetHashCode()"/>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The hash.</returns>
    private static int StableHash(string name)
    {
        var hash = 0;
        foreach (var character in name)
        {
            hash = unchecked((hash * HashMultiplier) + character);
        }

        return hash;
    }

    /// <summary>Mutates one document repeatedly and records every mutant that fails.</summary>
    /// <param name="name">The document name.</param>
    /// <param name="source">The document.</param>
    /// <param name="iterations">The number of mutants.</param>
    /// <param name="failures">Receives a description of each failure.</param>
    /// <param name="tally">Counts the mutants.</param>
    /// <returns>A task.</returns>
    private static async Task FuzzAsync(string name, byte[] source, int iterations, List<string> failures, Tally tally)
    {
        var random = new SeededRandom(ReadNumber(SeedVariable, DefaultSeed) ^ StableHash(name));
        for (var i = 0; i < iterations; i++)
        {
            var log = new StringBuilder();
            var mutant = PdfMutator.Mutate(source, random, log);
            var result = await MutantRunner.RunAsync(mutant);
            tally.Total++;
            tally.Opened += result.Opened ? 1 : 0;
            failures.AddRange(await DescribeAsync(result.Problem, name, i, log, mutant));
        }
    }

    /// <summary>Saves a failing mutant and describes the failure.</summary>
    /// <param name="problem">The problem, or <see langword="null"/> when the mutant behaved.</param>
    /// <param name="name">The document name.</param>
    /// <param name="index">The mutant's number.</param>
    /// <param name="log">The mutations applied.</param>
    /// <param name="mutant">The damaged file.</param>
    /// <returns>No lines when the mutant behaved; otherwise one line.</returns>
    private static async Task<string[]> DescribeAsync(string? problem, string name, int index, StringBuilder log, byte[] mutant)
    {
        if (problem is null)
        {
            return [];
        }

        var saved = Path.Combine(Path.GetTempPath(), string.Create(CultureInfo.InvariantCulture, $"hyperpdf-fuzz-{name}-{index}.pdf"));
        await File.WriteAllBytesAsync(saved, mutant);
        return [string.Create(CultureInfo.InvariantCulture, $"{name} #{index} (mutations {log}) saved to {saved}: {problem}")];
    }

    /// <summary>Counts how many mutants opened, so a fuzzer that rejects everything is noticed.</summary>
    private sealed class Tally
    {
        /// <summary>Gets or sets the number of mutants that opened and were read in full.</summary>
        public int Opened { get; set; }

        /// <summary>Gets or sets the number of mutants tried.</summary>
        public int Total { get; set; }
    }
}
