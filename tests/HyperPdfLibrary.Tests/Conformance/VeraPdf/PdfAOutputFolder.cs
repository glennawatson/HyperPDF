// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// A folder for the files a validation test writes. With <see cref="OutputVariable"/> set the files are kept there, so CI
/// can upload them; otherwise they go to a temporary folder that is deleted afterwards.
/// </summary>
[DebuggerDisplay("PdfAOutputFolder: {Path}")]
internal sealed class PdfAOutputFolder : IDisposable
{
    /// <summary>The variable naming a folder that keeps the written files.</summary>
    internal const string OutputVariable = "PDFVIEWERLITE_PDFA_OUTPUT";

    /// <summary>Whether the folder is deleted on dispose.</summary>
    private readonly bool _temporary;

    /// <summary>Initializes a new instance of the <see cref="PdfAOutputFolder"/> class.</summary>
    /// <param name="name">The test's folder name.</param>
    internal PdfAOutputFolder(string name)
    {
        var kept = Environment.GetEnvironmentVariable(OutputVariable);
        _temporary = string.IsNullOrWhiteSpace(kept);
        Path = System.IO.Path.Combine(_temporary ? System.IO.Path.GetTempPath() : kept!, _temporary ? $"pdfa-{name}-{Guid.NewGuid():N}" : name);
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the folder.</summary>
    internal string Path { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_temporary && Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}
