// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Engine;

namespace HyperPdf.Benchmarks;

/// <summary>A document opened from bytes written to a temporary file, deleted when disposed.</summary>
[DebuggerDisplay("BenchmarkPdfDocument: {Document.FilePath}")]
public sealed class BenchmarkPdfDocument : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="BenchmarkPdfDocument"/> class.</summary>
    /// <param name="bytes">The PDF.</param>
    public BenchmarkPdfDocument(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-ocrbench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        Document = new HyperPdfEngine().Open(path, null);
    }

    /// <summary>Gets the document.</summary>
    public IDocument Document { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        var path = Document.FilePath;
        Document.Dispose();
        File.Delete(path);
    }
}
