// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Render.Skia;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks built engine assemblies for backend and engine references.</summary>
public sealed class EngineIndependenceTests
{
    /// <summary>The name part that marks a PDFium assembly or native library.</summary>
    private const string PdfiumName = "pdfium";

    /// <summary>The name that marks the SkiaSharp assembly or native library.</summary>
    private const string SkiaSharpName = "SkiaSharp";

    /// <summary>The adapter assembly references no PDFium assembly and imports no PDFium library.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HyperPdfAdapterHasNoPdfiumReference() => await Assert.That(ReferencesContaining(typeof(HyperPdfDocument).Assembly.Location, PdfiumName)).IsEmpty();

    /// <summary>The managed engine assembly references no PDFium assembly and imports no PDFium library.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LibraryHasNoPdfiumReference() => await Assert.That(ReferencesContaining(typeof(PdfDocument).Assembly.Location, PdfiumName)).IsEmpty();

    /// <summary>The managed engine assembly has no SkiaSharp assembly or native-module reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LibraryHasNoSkiaSharpReference() => await Assert.That(ReferencesContaining(typeof(PdfDocument).Assembly.Location, SkiaSharpName)).IsEmpty();

    /// <summary>The Skia drawing backend has no PDFium assembly or native-module reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkiaBackendHasNoPdfiumReference() => await Assert.That(ReferencesContaining(typeof(SkiaRenderBackend).Assembly.Location, PdfiumName)).IsEmpty();

    /// <summary>The rendering tests import native PDFium directly without a managed PDFium adapter.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderingTestsImportNativePdfiumOnly()
    {
        var references = ReferencesContaining(typeof(EngineIndependenceTests).Assembly.Location, PdfiumName);
        await Assert.That(references).Contains("pdfium");
        await Assert.That(references).DoesNotContain("HyperPdf.Pdfium");
    }

    /// <summary>Lists assembly references and module imports that contain a name fragment.</summary>
    /// <param name="path">The assembly file.</param>
    /// <param name="fragment">The name fragment.</param>
    /// <returns>The names found.</returns>
    private static List<string> ReferencesContaining(string path, string fragment)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var found = new List<string>();
        foreach (var handle in metadata.AssemblyReferences)
        {
            AddIfContained(found, metadata.GetString(metadata.GetAssemblyReference(handle).Name), fragment);
        }

        for (var row = 1; row <= metadata.GetTableRowCount(TableIndex.ModuleRef); row++)
        {
            AddIfContained(found, metadata.GetString(metadata.GetModuleReference(MetadataTokens.ModuleReferenceHandle(row)).Name), fragment);
        }

        return found;
    }

    /// <summary>Adds a name when it contains the fragment.</summary>
    /// <param name="found">The names found.</param>
    /// <param name="name">The name.</param>
    /// <param name="fragment">The name fragment.</param>
    private static void AddIfContained(List<string> found, string name, string fragment)
    {
        if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
        {
            found.Add(name);
        }
    }
}
