// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The XMP properties and value types PDF/A accepts without an extension schema, read once from the embedded
/// <c>Data/XmpSchemas.tsv</c>: the 2004 XMP schemas for PDF/A-1 and the 2005 ones for PDF/A-2 and PDF/A-3.
/// </summary>
internal static class PdfAXmpSchemaTable
{
    /// <summary>The manifest name of the table.</summary>
    private const string ResourceName = "HyperPdfLibrary.Conformance.XmpSchemas.tsv";

    /// <summary>The columns of a property line.</summary>
    private const int PropertyColumns = 5;

    /// <summary>The columns of a structure field line.</summary>
    private const int FieldColumns = 6;

    /// <summary>The column holding the field type of a structure field line.</summary>
    private const int FieldTypeColumn = 5;

    /// <summary>The column holding the field name of a structure field line, or the type of a property line.</summary>
    private const int FourthColumn = 4;

    /// <summary>The column holding the owner's namespace or type name.</summary>
    private const int OwnerColumn = 2;

    /// <summary>The column holding a property name or a field namespace.</summary>
    private const int ThirdColumn = 3;

    /// <summary>The table's lines.</summary>
    private static readonly PdfAXmpSchemaRows Rows = Load();

    /// <summary>The PDF/A-1 predefined schemas.</summary>
    private static readonly PdfAXmpSchemaDefinition PdfA1 = CreatePredefined(true);

    /// <summary>The PDF/A-2 and PDF/A-3 predefined schemas.</summary>
    private static readonly PdfAXmpSchemaDefinition PdfA2 = CreatePredefined(false);

    /// <summary>Gets the value types every context knows: the simple types, dates, URIs and language alternatives.</summary>
    internal static PdfAXmpTypeSet BaseTypes { get; } = CreateBaseTypes();

    /// <summary>Gets the predefined schemas of a part.</summary>
    /// <param name="pdfA1">Whether PDF/A-1, rather than PDF/A-2 or PDF/A-3, is checked.</param>
    /// <returns>The schemas.</returns>
    internal static PdfAXmpSchemaDefinition Predefined(bool pdfA1) => pdfA1 ? PdfA1 : PdfA2;

    /// <summary>Creates a new set of the value types a part predefines, for an extension schema to add to.</summary>
    /// <param name="pdfA1">Whether PDF/A-1, rather than PDF/A-2 or PDF/A-3, is checked.</param>
    /// <returns>The set.</returns>
    internal static PdfAXmpTypeSet CreatePredefinedTypes(bool pdfA1)
    {
        var types = CreateBaseTypes();
        types.Types["gpscoordinate"] = PdfAXmpType.Matching(pdfA1 ? PdfAXmpPatterns.GpsCoordinate2004() : PdfAXmpPatterns.GpsCoordinate2005());
        types.Types["locale"] = PdfAXmpType.AnyText;
        foreach (var field in Rows.Fields)
        {
            if (!field.AppliesTo(pdfA1))
            {
                continue;
            }

            ref var structure = ref CollectionsMarshal.GetValueRefOrAddDefault(types.Types, PdfAXmpTypeChecks.Simplify(field.Owner), out var exists);
            if (!exists)
            {
                structure = new(PdfAXmpTypeKind.Structure, null, field.FieldNamespace, [with(StringComparer.Ordinal)], types);
            }

            structure!.Fields![field.Name] = field.Type;
        }

        return types;
    }

    /// <summary>Creates the value types every context knows.</summary>
    /// <returns>The set.</returns>
    private static PdfAXmpTypeSet CreateBaseTypes()
    {
        var types = new PdfAXmpTypeSet();
        types.Types["date"] = new(PdfAXmpTypeKind.Date, null, null, null, null);
        types.Types["lang alt"] = new(PdfAXmpTypeKind.LanguageAlternative, null, null, null, null);
        types.Types["xpath"] = new(PdfAXmpTypeKind.XPath, null, null, null, null);
        types.Types["boolean"] = PdfAXmpType.Matching(PdfAXmpPatterns.Boolean());
        types.Types["integer"] = PdfAXmpType.Matching(PdfAXmpPatterns.Integer());
        types.Types["real"] = PdfAXmpType.Matching(PdfAXmpPatterns.Real());
        types.Types["mimetype"] = PdfAXmpType.Matching(PdfAXmpPatterns.MimeType());
        foreach (var name in (string[])["uri", "url", "propername", "text", "agentname", "rational", "renditionclass"])
        {
            types.Types[name] = PdfAXmpType.AnyText;
        }

        return types;
    }

    /// <summary>Creates a part's predefined schemas.</summary>
    /// <param name="pdfA1">Whether PDF/A-1, rather than PDF/A-2 or PDF/A-3, is checked.</param>
    /// <returns>The schemas.</returns>
    private static PdfAXmpSchemaDefinition CreatePredefined(bool pdfA1)
    {
        var definition = new PdfAXmpSchemaDefinition(CreatePredefinedTypes(pdfA1));
        foreach (var property in Rows.Properties)
        {
            if (property.AppliesTo(pdfA1))
            {
                definition.Register(new(property.Owner, property.Name), property.Type);
            }
        }

        return definition;
    }

    /// <summary>Reads the embedded table.</summary>
    /// <returns>The lines.</returns>
    /// <exception cref="InvalidDataException">The table is missing or malformed.</exception>
    private static PdfAXmpSchemaRows Load()
    {
        using var stream = typeof(PdfAXmpSchemaTable).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The XMP schema table is missing from the library.");
        using var reader = new StreamReader(stream);
        var properties = new List<PdfAXmpSchemaRow>();
        var fields = new List<PdfAXmpSchemaRow>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var columns = line.Split('\t');
            var inPdfA1 = columns[1].Contains('1', StringComparison.Ordinal);
            var inPdfA2 = columns[1].Contains('2', StringComparison.Ordinal);
            if (columns[0] == "P" && columns.Length == PropertyColumns)
            {
                properties.Add(new(inPdfA1, inPdfA2, columns[OwnerColumn], string.Empty, columns[ThirdColumn], columns[FourthColumn]));
            }
            else if (columns[0] == "T" && columns.Length == FieldColumns)
            {
                fields.Add(new(inPdfA1, inPdfA2, columns[OwnerColumn], columns[ThirdColumn], columns[FourthColumn], columns[FieldTypeColumn]));
            }
            else
            {
                throw new InvalidDataException("The XMP schema table is malformed.");
            }
        }

        return new([.. properties], [.. fields]);
    }
}
