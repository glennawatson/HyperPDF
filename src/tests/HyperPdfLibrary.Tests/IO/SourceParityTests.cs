// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Robustness;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>
/// Checks that a document reads, repairs and saves the same whether its bytes are held in memory, read in windows,
/// read through a stream with a one-page cache, mapped, or read through a file handle.
/// </summary>
public sealed class SourceParityTests
{
    /// <summary>The damaged copies compared per seed document.</summary>
    private const int MutantsPerSeed = 24;

    /// <summary>The random seed of the damaged copies.</summary>
    private const int MutantSeed = 20_261_009;

    /// <summary>The integers in the large array, which makes an object far larger than the first parse window.</summary>
    private const int LargeArrayCount = 20_000;

    /// <summary>The length of the large literal string.</summary>
    private const int LargeStringLength = 40_000;

    /// <summary>The length of the stream whose /Length is wrong.</summary>
    private const int LongStreamLength = 150_000;

    /// <summary>The value written into the edited object.</summary>
    private const int EditedValue = 42;

    /// <summary>The object number of the large array.</summary>
    private const int ArrayObject = 4;

    /// <summary>The object number of the stream whose /Length is wrong.</summary>
    private const int StreamObject = 5;

    /// <summary>The pages of the saved document.</summary>
    private const int SavedPages = 2;

    /// <summary>Every seed document reads the same through every source.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedsReadTheSameThroughEverySource()
    {
        var failures = new List<string>();
        var directory = SourceOpener.CreateDirectory();
        try
        {
            foreach (var seed in RobustnessSeeds.Create())
            {
                failures.AddRange(Compare(seed.Name, seed.Bytes, directory));
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Damaged documents give the same result, or the same failure, through windows as from memory.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDocumentsReadTheSameThroughWindows()
    {
        var failures = new List<string>();
        var directory = SourceOpener.CreateDirectory();
        try
        {
            foreach (var seed in RobustnessSeeds.Create())
            {
                var random = new SeededRandom(MutantSeed ^ seed.Name.Length);
                for (var i = 0; i < MutantsPerSeed; i++)
                {
                    var mutant = PdfMutator.Mutate(seed.Bytes, random, new());
                    failures.AddRange(Compare(string.Create(CultureInfo.InvariantCulture, $"{seed.Name} #{i}"), mutant, directory));
                }
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Objects larger than the first window, and a stream whose /Length is wrong, read the same through every source.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeObjectsReadAcrossWindows()
    {
        var file = CreateLargeObjects();
        var directory = SourceOpener.CreateDirectory();
        try
        {
            await Assert.That(Compare("large", file, directory)).IsEmpty();
            foreach (var kind in SourceOpener.Kinds)
            {
                using var document = SourceOpener.Open(kind, file, PdfOpenOptions.Default, directory);
                var array = StoreReading.GetObject(document.Objects, new(ArrayObject, 0)).AsArray()!;
                var stream = StoreReading.GetObject(document.Objects, new(StreamObject, 0)).AsStream()!;
                await Assert.That(array.Count).IsEqualTo(LargeArrayCount);
                await Assert.That(stream.RawLength).IsEqualTo(LongStreamLength);
                await Assert.That(PdfDocumentMetadata.GetInfo(document).Title!.Length).IsEqualTo(LargeStringLength);
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>An incremental save copies the original through every source to the same bytes, with and without edits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSavesMatchAcrossSources()
    {
        var file = TestPdf.Create(SavedPages);
        var directory = SourceOpener.CreateDirectory();
        try
        {
            var expectedPlain = Save(SourceOpener.Kinds[0], file, directory, false);
            var expectedEdited = Save(SourceOpener.Kinds[0], file, directory, true);
            foreach (var kind in SourceOpener.Kinds)
            {
                await Assert.That(Save(kind, file, directory, false)).IsEquivalentTo(expectedPlain);
                await Assert.That(Save(kind, file, directory, true)).IsEquivalentTo(expectedEdited);
                await Assert.That(await SaveAsync(kind, file, directory)).IsEquivalentTo(expectedEdited);
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>A signed then updated file validates the same through every source: byte range, digest, signature, revisions and changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignaturesValidateTheSameThroughEverySource()
    {
        var file = SignatureSamples.AddAnnotation(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty));
        var directory = SourceOpener.CreateDirectory();
        try
        {
            var expected = Validation(SourceOpener.Kinds[0], file, directory);
            foreach (var kind in SourceOpener.Kinds)
            {
                await Assert.That(Validation(kind, file, directory)).IsEqualTo(expected);
            }

            await Assert.That(expected).Contains("digest=True signature=True");
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>Validates a file's signatures and describes the result.</summary>
    /// <param name="kind">The source kind.</param>
    /// <param name="file">The file.</param>
    /// <param name="directory">A temporary directory.</param>
    /// <returns>The description.</returns>
    private static string Validation(string kind, byte[] file, string directory)
    {
        using var document = SourceOpener.Open(kind, file, PdfOpenOptions.Default, directory);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"revisions={PdfDocumentSignatureValidation.GetRevisions(document).Count};");
        foreach (var report in PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] }))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"range={report.ByteRange.Status} covers={report.CoversWholeDocument} modified={report.ModifiedAfterSigning} ");
            _ = text.Append(CultureInfo.InvariantCulture, $"changes={report.Changes.Length} kinds={report.ChangeKinds} digest={report.DigestValid} signature={report.SignatureValid};");
        }

        return text.ToString();
    }

    /// <summary>Reads a document through every source and lists the sources that differ from memory.</summary>
    /// <param name="name">The document name.</param>
    /// <param name="file">The file.</param>
    /// <param name="directory">A temporary directory.</param>
    /// <returns>A line for each source that read differently.</returns>
    private static List<string> Compare(string name, byte[] file, string directory)
    {
        var failures = new List<string>();
        var expected = Outcome(SourceOpener.Kinds[0], file, directory);
        foreach (var kind in SourceOpener.Kinds)
        {
            var actual = Outcome(kind, file, directory);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{name} via {kind}: expected [{expected}] got [{actual}]"));
            }
        }

        return failures;
    }

    /// <summary>Opens and reads a document, describing what was read or how it failed.</summary>
    /// <param name="kind">The source kind.</param>
    /// <param name="file">The file.</param>
    /// <param name="directory">A temporary directory.</param>
    /// <returns>The description.</returns>
    private static string Outcome(string kind, byte[] file, string directory)
    {
        try
        {
            using var document = SourceOpener.Open(kind, file, PdfOpenOptions.Default, directory);
            return DocumentExerciser.Read(document);
        }
        catch (PdfException ex)
        {
            return $"PdfException {ex.Error}";
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }

    /// <summary>Saves a document incrementally, optionally after replacing an object.</summary>
    /// <param name="kind">The source kind.</param>
    /// <param name="file">The file.</param>
    /// <param name="directory">A temporary directory.</param>
    /// <param name="edit">Whether to replace an object first.</param>
    /// <returns>The saved bytes.</returns>
    private static byte[] Save(string kind, byte[] file, string directory, bool edit)
    {
        using var document = SourceOpener.Open(kind, file, PdfOpenOptions.Default, directory);
        if (edit)
        {
            _ = StoreEditing.Add(document.Objects, PdfValue.FromInteger(EditedValue));
        }

        using var output = new MemoryStream();
        PdfIncrementalWriter.Save(document.Objects, output);
        var viaStream = output.ToArray();
        var viaBuffer = PdfIncrementalWriter.Save(document.Objects);
        return viaStream.AsSpan().SequenceEqual(viaBuffer) ? viaStream : [];
    }

    /// <summary>Saves a document asynchronously after adding an object.</summary>
    /// <param name="kind">The source kind.</param>
    /// <param name="file">The file.</param>
    /// <param name="directory">A temporary directory.</param>
    /// <returns>The saved bytes.</returns>
    private static async Task<byte[]> SaveAsync(string kind, byte[] file, string directory)
    {
        using var document = SourceOpener.Open(kind, file, PdfOpenOptions.Default, directory);
        _ = StoreEditing.Add(document.Objects, PdfValue.FromInteger(EditedValue));
        await using var output = new MemoryStream();
        await PdfIncrementalWriter.SaveAsync(document.Objects, output, CancellationToken.None);
        return output.ToArray();
    }

    /// <summary>Builds a document with a large array, a long title and a stream whose /Length is short.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateLargeObjects()
    {
        var array = new StringBuilder("[");
        for (var i = 0; i < LargeArrayCount; i++)
        {
            _ = array.Append(CultureInfo.InvariantCulture, $"{i} ");
        }

        _ = array.Append(']');
        var title = new string('t', LargeStringLength);
        var data = new string('d', LongStreamLength);
        var file = MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 5 0 R >>",
        array.ToString(),
        string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Length 7 >>\nstream\n{data}\nendstream"),
        string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Title ({title}) >>"));

        // MiniPdf writes no /Info; the trailer follows the table, so adding it there moves no offsets.
        var text = Encoding.Latin1.GetString(file).Replace("/Root 1 0 R >>", "/Root 1 0 R /Info 6 0 R >>", StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(text);
    }
}
