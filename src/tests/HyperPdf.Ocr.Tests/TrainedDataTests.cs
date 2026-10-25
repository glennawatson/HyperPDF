// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the table of parts at the start of a language data file.</summary>
public sealed class TrainedDataTests
{
    /// <summary>The number of parts in the test file.</summary>
    private const int PartCount = 4;

    /// <summary>The size of the table: a count and an offset for each part.</summary>
    private const int TableSize = sizeof(int) + (PartCount * sizeof(long));

    /// <summary>The size of the first part.</summary>
    private const int FirstSize = 5;

    /// <summary>The size of the second part.</summary>
    private const int SecondSize = 3;

    /// <summary>An offset past the end of the test file.</summary>
    private const long PastTheEnd = 10_000;

    /// <summary>Each part runs to the start of the next part the file has, and the last runs to the end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartsRunToTheNextPresentPart()
    {
        var file = TrainedData.Open(CreateFile());

        await Assert.That(file.Has(TrainedDataComponent.Unicharset)).IsTrue();
        await Assert.That(file.Has(TrainedDataComponent.LanguageConfig)).IsFalse();
        await Assert.That(file.Get(TrainedDataComponent.Unicharset).Length).IsEqualTo(FirstSize);
        await Assert.That(file.Get(TrainedDataComponent.IntegerTemplates).Length).IsEqualTo(SecondSize);
        await Assert.That(file.Get(TrainedDataComponent.IntegerTemplates).Span[0]).IsEqualTo((byte)'x');
    }

    /// <summary>A part the file does not have cannot be taken.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPartThrows()
    {
        var file = TrainedData.Open(CreateFile());

        await Assert.That(() => file.Get(TrainedDataComponent.Lstm)).Throws<InvalidDataException>();
    }

    /// <summary>A table that points outside the file is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffsetsOutsideTheFileAreRefused()
    {
        var bytes = new ModelBytes().Int(1).Long(PastTheEnd).ToArray();

        await Assert.That(() => TrainedData.Open(bytes)).Throws<InvalidDataException>();
    }

    /// <summary>Data too short for its own table is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortDataIsRefused()
    {
        var bytes = new ModelBytes().Int(PartCount).ToArray();

        await Assert.That(() => TrainedData.Open(bytes)).Throws<InvalidDataException>();
    }

    /// <summary>Makes a file with four parts: two absent and two present, the first of five bytes and the second of three.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateFile()
    {
        var builder = new ModelBytes().Int(PartCount).Long(-1).Long(TableSize).Long(-1).Long(TableSize + FirstSize);
        return builder.Raw("hello"u8.ToArray()).Raw("xyz"u8.ToArray()).ToArray();
    }
}
