// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks count queries on a retained accessibility report.</summary>
public sealed class PdfAccessibilityReportTests
{
    /// <summary>The count for the first finding code.</summary>
    private const int FirstCount = 3;

    /// <summary>The count for the second finding code.</summary>
    private const int SecondCount = 2;

    /// <summary>How many times the measured loop runs; the least allocation of a round is judged.</summary>
    private const int Rounds = 5;

    /// <summary>The number of synchronous count-query repetitions.</summary>
    private const int Repetitions = 1000;

    /// <summary>Count queries preserve totals and do not create collection enumerators.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CountReads_DoNotAllocate()
    {
        var report = Report([
            new(PdfAccessibilityCode.NoLanguage, FirstCount),
            new(PdfAccessibilityCode.NoMetadata, SecondCount),
        ]);

        // Run the loop once unmeasured so tiered compilation has promoted the code; its call counting would otherwise allocate inside the measured loop.
        for (var index = 0; index < Repetitions; index++)
        {
            _ = ReadCounts(report);
        }

        // A round can still catch the runtime's own tier-up work, so the least a round allocates is what the reads cost.
        var sum = 0;
        var allocated = long.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            sum = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < Repetitions; index++)
            {
                sum += ReadCounts(report);
            }

            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        await Assert.That(sum).IsEqualTo(Repetitions * (FirstCount + FirstCount + SecondCount));
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>An empty report has no findings for any requested code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CountReads_EmptyReport_ReturnZero()
    {
        var report = Report([]);
        await Assert.That(report.TotalCount).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoLanguage)).IsEqualTo(0);
    }

    /// <summary>Reads the total, a present code and an absent code without async test machinery.</summary>
    /// <param name="report">The report to query.</param>
    /// <returns>The sum of the queried counts.</returns>
    private static int ReadCounts(PdfAccessibilityReport report) =>
        report.TotalCount + report.GetCount(PdfAccessibilityCode.NoLanguage) + report.GetCount(PdfAccessibilityCode.NoTitle);

    /// <summary>Creates a report with independently supplied counts.</summary>
    /// <param name="counts">The counts to expose.</param>
    /// <returns>The report.</returns>
    private static PdfAccessibilityReport Report(PdfAccessibilityCount[] counts) =>
        new(default, false, false, null, false, false, false, [], [], counts);
}
