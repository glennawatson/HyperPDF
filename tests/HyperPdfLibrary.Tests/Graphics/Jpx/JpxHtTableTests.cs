// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Checks the decoder's packed CxtVLC lookup tables against the rows of T.814 Annex C, so the generated file cannot drift
/// from the data file it comes from.
/// </summary>
public sealed class JpxHtTableTests
{
    /// <summary>The rows of CxtVLC_table_0 in Annex C.</summary>
    private const int FirstRowRows = 444;

    /// <summary>The rows of CxtVLC_table_1 in Annex C.</summary>
    private const int LaterRowRows = 358;

    /// <summary>The resource containing the checked-in generated source.</summary>
    private const string GeneratedResourceName = "JpxHtTables.cs";

    /// <summary>Annex C lists the expected number of rows in each table.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Test]
    public async Task AnnexCRowCountsMatch()
    {
        var rows = JpxTestHtRows.Load();

        using (Assert.Multiple())
        {
            await Assert.That(rows[0].Length).IsEqualTo(FirstRowRows);
            await Assert.That(rows[1].Length).IsEqualTo(LaterRowRows);
        }
    }

    /// <summary>The compiled tables equal the tables built in memory from the rows.</summary>
    /// <param name="table">0 for the first row of quads, 1 for the others.</param>
    /// <returns>A task that completes when the check is done.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task CompiledTablesMatchTheRows(int table)
    {
        var expected = JpxHtTableBuilder.BuildTable(JpxTestHtRows.Load()[table]);
        var actual = (table == 0 ? JpxHtTables.FirstRow : JpxHtTables.LaterRows).ToArray();

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>The checked-in source file is exactly what the generator writes for the rows.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    /// <exception cref="InvalidOperationException">The assembly does not embed the generated source.</exception>
    [Test]
    public async Task CheckedInFileMatchesTheGenerator()
    {
        // Embed the source because deterministic builds map CallerFilePath to a virtual repository path.
        await using var stream = typeof(JpxHtTableTests).Assembly.GetManifestResourceStream(GeneratedResourceName)
            ?? throw new InvalidOperationException("The generated HT table source is not embedded.");
        using var reader = new StreamReader(stream);
        var checkedIn = (await reader.ReadToEndAsync()).Replace("\r\n", "\n", StringComparison.Ordinal);

        await Assert.That(checkedIn).IsEqualTo(JpxHtTableBuilder.Emit(JpxTestHtRows.Load()));
    }
}
