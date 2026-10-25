// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests bounded parsing of deeply nested PDF values.</summary>
public sealed class PdfParserTests
{
    /// <summary>The nesting used to exceed the parser's stack budget.</summary>
    private const int HostileDepth = 10_000;

    /// <summary>The object containing the deeply nested dictionary.</summary>
    private const int NestedObject = 3;

    /// <summary>The cross-reference size of the raw file.</summary>
    private const int TableSize = 4;

    /// <summary>Nested values are skipped without losing a following dictionary entry.</summary>
    /// <param name="dictionary">Whether the nested containers are dictionaries rather than arrays.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ParseValue_DeepContainers_PreservesFollowingEntry(bool dictionary)
    {
        var bytes = NestedValue(dictionary);
        var parsed = Parse(bytes);

        await Assert.That(parsed.GetInteger(KnownName.Count)).IsEqualTo(1);
        await Assert.That(parsed.ContainsKey(KnownName.A)).IsTrue();
    }

    /// <summary>Deeply nested indirect dictionaries report the truncation to the caller.</summary>
    /// <param name="dictionary">Whether the nested containers are dictionaries rather than arrays.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ParseValue_DeepContainers_ReportsRecursionLimit(bool dictionary)
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var store = StoreOpening.OpenWith(
            new RawPdf()
                .Object(1, "<< /Type /Catalog /Pages 2 0 R >>")
                .Object(NestedObject - 1, "<< /Type /Pages /Kids [] /Count 0 >>")
                .Object(NestedObject, Encoding.UTF8.GetString(NestedValue(dictionary)))
                .Table(TableSize, "/Root 1 0 R").ToArray(),
            new() { Diagnostics = diagnostics.Add });

        _ = StoreReading.GetDictionary(store, new(NestedObject, 0));

        await Assert.That(diagnostics.Exists(static item => item.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Builds raw PDF syntax with ten thousand nested containers.</summary>
    /// <param name="dictionary">Whether to use dictionaries.</param>
    /// <returns>The syntax bytes.</returns>
    private static byte[] NestedValue(bool dictionary)
    {
        var text = new StringBuilder("<< /A ");
        for (var level = 0; level < HostileDepth; level++)
        {
            _ = text.Append(dictionary ? "<< /A " : "[");
        }

        _ = text.Append('1');
        for (var level = 0; level < HostileDepth; level++)
        {
            _ = text.Append(dictionary ? " >>" : "]");
        }

        return Encoding.UTF8.GetBytes(text.Append(" /Count 1 >>").ToString());
    }

    /// <summary>Parses a raw dictionary without resolving its values.</summary>
    /// <param name="bytes">The syntax.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Parse(byte[] bytes)
    {
        var parser = new PdfParser(bytes, 0, null, new());
        return parser.ParseValue().AsDictionary()!;
    }
}
