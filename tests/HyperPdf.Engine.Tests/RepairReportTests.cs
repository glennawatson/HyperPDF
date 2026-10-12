// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests that the engine tells the viewer a file was repaired, in plain words.</summary>
public sealed class RepairReportTests
{
    /// <summary>A damaged file reports its repairs in plain words, and a clean one reports none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsRepairsInPlainWords()
    {
        var damaged = Path.Combine(Path.GetTempPath(), $"hyperpdf-repair-{Guid.NewGuid():N}.pdf");
        var plain = TestPdf.WriteTempFile(1);
        var bytes = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(TestPdf.Create(1)).Replace("startxref", "startxxxx", StringComparison.Ordinal));
        await File.WriteAllBytesAsync(damaged, bytes);
        try
        {
            using var document = new HyperPdfEngine().Open(damaged, null);
            var report = DocumentFeature<IRepairReport>.Cast(document)!;
            var notes = report.GetRepairs();

            await Assert.That(report.WasRepaired).IsTrue();
            await Assert.That(notes.Count).IsGreaterThan(0);
            await Assert.That(RepairWarnings.Describe(notes)).Contains("index");

            using var clean = new HyperPdfEngine().Open(plain, null);
            await Assert.That(DocumentFeature<IRepairReport>.Cast(clean)!.WasRepaired).IsFalse();
            await Assert.That(DocumentFeature<IRepairReport>.Cast(clean)!.GetRepairs().Count).IsEqualTo(0);
        }
        finally
        {
            File.Delete(damaged);
            File.Delete(plain);
        }
    }
}
