// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Seeded sample data for the filter encoder tests: edge cases and random shapes that stress runs, tables and group boundaries.</summary>
internal static class FilterSamples
{
    /// <summary>The seed of every random sample, so a failure repeats.</summary>
    internal const uint Seed = 20_260_714;

    /// <summary>The longest of the short samples, which cover every length around the group sizes of the filters.</summary>
    private const int ShortLength = 600;

    /// <summary>The length of the long samples.</summary>
    private const int LongLength = 70_000;

    /// <summary>The length of the all-zero and all-ones samples.</summary>
    private const int UniformLength = 100_000;

    /// <summary>The longest run in the run sample.</summary>
    private const int MaxRun = 300;

    /// <summary>The runs in the run sample.</summary>
    private const int RunCount = 400;

    /// <summary>The bytes either side of a run in an edge sample.</summary>
    private const int EdgeGuards = 2;

    /// <summary>The lines in the text sample.</summary>
    private const int TextLines = 300;

    /// <summary>The alphabet sizes of the low-entropy samples, which fill LZW tables deeply.</summary>
    private static readonly int[] Alphabets = [2, 3, 7];

    /// <summary>The run lengths that sit on the edges of RunLengthDecode's 128 limit.</summary>
    private static readonly int[] EdgeRuns = [1, 2, 3, 4, 126, 127, 128, 129, 130, 255, 256, 257, 258, 1000];

    /// <summary>Gets every sample.</summary>
    /// <returns>The samples.</returns>
    internal static List<FilterSample> All()
    {
        var generator = new SampleRandom(Seed);
        var samples = new List<FilterSample>
        {
            new("empty", []),
            new("zeros", new byte[UniformLength]),
            new("ones", Fill(UniformLength, byte.MaxValue)),
            new("ramp", Ramp(LongLength)),
            new("noise", generator.NextBytes(LongLength)),
            new("runs", Runs(generator)),
            new("text", Text()),
        };
        foreach (var alphabet in Alphabets)
        {
            samples.Add(new($"alphabet{alphabet}", Alphabet(generator, alphabet)));
        }

        foreach (var run in EdgeRuns)
        {
            samples.Add(new($"run{run}", Edge(run)));
        }

        for (var length = 1; length <= ShortLength; length++)
        {
            samples.Add(new($"short{length}", generator.NextBytes(length)));
        }

        return samples;
    }

    /// <summary>Gets one sample by name.</summary>
    /// <param name="name">The sample's name.</param>
    /// <returns>The sample.</returns>
    internal static FilterSample Get(string name) => All().Single(sample => sample.Name == name);

    /// <summary>Makes bytes that all have one value.</summary>
    /// <param name="length">The length.</param>
    /// <param name="value">The value.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Fill(int length, byte value)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    /// <summary>Makes bytes that count up and wrap.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Ramp(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)i;
        }

        return bytes;
    }

    /// <summary>Makes bytes drawn from a small alphabet.</summary>
    /// <param name="generator">The generator.</param>
    /// <param name="alphabet">The number of distinct values.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Alphabet(SampleRandom generator, int alphabet)
    {
        var bytes = new byte[LongLength];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)generator.Next(alphabet);
        }

        return bytes;
    }

    /// <summary>Makes runs of random bytes with random lengths.</summary>
    /// <param name="generator">The generator.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Runs(SampleRandom generator)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < RunCount; i++)
        {
            var value = (byte)generator.Next(byte.MaxValue + 1);
            bytes.AddRange(Enumerable.Repeat(value, generator.Next(1, MaxRun)));
        }

        return [.. bytes];
    }

    /// <summary>Makes a run of one length between two different bytes, so the run's edges are covered.</summary>
    /// <param name="run">The run length.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Edge(int run)
    {
        var bytes = new byte[run + EdgeGuards];
        Array.Fill(bytes, (byte)'a', 1, run);
        bytes[0] = (byte)'b';
        bytes[^1] = (byte)'c';
        return bytes;
    }

    /// <summary>Makes repeated page-content text.</summary>
    /// <returns>The bytes.</returns>
    private static byte[] Text()
    {
        var line = "BT /F1 12 Tf 72 712 Td (Hello, filters) Tj ET\n"u8;
        var bytes = new byte[line.Length * TextLines];
        for (var offset = 0; offset < bytes.Length; offset += line.Length)
        {
            line.CopyTo(bytes.AsSpan(offset));
        }

        return bytes;
    }
}
