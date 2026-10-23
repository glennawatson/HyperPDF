// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Exporting;

namespace HyperPdf.App.Tests;

/// <summary>A page exporter that records its calls and returns what a test sets, so the window can be tested without an engine.</summary>
internal sealed class FakePageFileExporter : IPageFileExporter
{
    /// <summary>The bytes the fake writes for each file.</summary>
    private static readonly byte[] Output = "fake export"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets the pages of each call, in order.</summary>
    internal List<int[]> Calls { get; } = [];

    /// <summary>Gets the options of the last call.</summary>
    internal PageFileExportOptions? LastOptions { get; private set; }

    /// <summary>Gets or sets what each call returns.</summary>
    internal PageFileExportResult Result { get; set; } = PageFileExportResult.Exact();

    /// <summary>Gets or sets what the run throws, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <inheritdoc/>
    public async ValueTask<PageFileExportResult> ExportAsync(IReadOnlyList<int> pages, PageFileFormat format, PageFileExportOptions options, Stream destination, CancellationToken cancellationToken)
    {
        Calls.Add([.. pages]);
        LastOptions = options;
        if (Failure is { } failure)
        {
            throw failure;
        }

        if (Result.Written)
        {
            await destination.WriteAsync(Output, cancellationToken).ConfigureAwait(false);
        }

        return Result;
    }
}
