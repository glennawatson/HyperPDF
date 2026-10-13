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

    /// <summary>The namespace of the test-only PDFium reference helpers, with its trailing dot.</summary>
    private const string ReferenceNamespace = "HyperPdf.Engine.Tests.PdfiumReference.";

    /// <summary>The rendering comparison's PDFium helper and its nested native methods.</summary>
    private const string RenderingReferencePrefix = "HyperPdf.Engine.Tests.RenderingComparison.PdfiumReferenceRendering";

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

    /// <summary>
    /// Only the test-only reference helpers declare native PDFium imports: the <c>PdfiumReference</c> folder and the
    /// rendering comparison's <c>PdfiumReferenceRendering</c>. Parity tests reach PDFium through them.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyReferenceHelpersImportPdfium()
    {
        var importers = TypesImporting(typeof(EngineIndependenceTests).Assembly.Location, PdfiumName);

        await Assert.That(importers).IsNotEmpty();
        await Assert.That(importers.Where(static name => !IsReferenceHelper(name))).IsEmpty();
    }

    /// <summary>No HyperPDF assembly beside the tests, product or shared test helper, references PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoOtherHyperPdfAssemblyReferencesPdfium()
    {
        var self = Path.GetFileName(typeof(EngineIndependenceTests).Assembly.Location);
        var checkedAssemblies = 0;
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "HyperPdf*.dll"))
        {
            if (string.Equals(Path.GetFileName(path), self, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            checkedAssemblies++;
            await Assert.That(ReferencesContaining(path, PdfiumName)).IsEmpty().Because(Path.GetFileName(path));
        }

        await Assert.That(checkedAssemblies).IsGreaterThan(0);
    }

    /// <summary>Gets whether a type is one of the reference helpers allowed to import PDFium.</summary>
    /// <param name="name">The type's full metadata name, with <c>+</c> before nested type names.</param>
    /// <returns>True for a reference helper.</returns>
    private static bool IsReferenceHelper(string name) =>
        name.StartsWith(ReferenceNamespace, StringComparison.Ordinal)
        || name.StartsWith(RenderingReferencePrefix, StringComparison.Ordinal);

    /// <summary>Lists the types that declare a method imported from a native module whose name contains a fragment.</summary>
    /// <param name="path">The assembly file.</param>
    /// <param name="fragment">The module name fragment.</param>
    /// <returns>The declaring types' full names.</returns>
    private static HashSet<string> TypesImporting(string path, string fragment)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            var import = method.GetImport();
            if (import.Module.IsNil)
            {
                continue;
            }

            var module = metadata.GetString(metadata.GetModuleReference(import.Module).Name);
            if (module.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                _ = found.Add(TypeName(metadata, method.GetDeclaringType()));
            }
        }

        return found;
    }

    /// <summary>Gets a type's full metadata name, with <c>+</c> before nested type names.</summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="handle">The type.</param>
    /// <returns>The name.</returns>
    private static string TypeName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        var name = metadata.GetString(type.Name);
        var outer = type.GetDeclaringType();
        if (!outer.IsNil)
        {
            return $"{TypeName(metadata, outer)}+{name}";
        }

        var space = metadata.GetString(type.Namespace);
        return space.Length == 0 ? name : $"{space}.{name}";
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
