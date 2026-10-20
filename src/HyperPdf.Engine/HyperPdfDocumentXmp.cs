// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;

namespace HyperPdf.Engine;

/// <summary>Lists every property of the catalog's XMP packet.</summary>
internal static class HyperPdfDocumentXmp
{
    /// <summary>The separator between the items of an array property.</summary>
    private const string ItemSeparator = "; ";

    /// <summary>The prefixes of the schemas most files use, by address, so a property reads as "dc:title" instead of an address.</summary>
    private static readonly KeyValuePair<string, string>[] Prefixes =
    [
        new(XmpMetadata.DublinCoreNamespace, "dc"),
        new(XmpMetadata.XmpNamespace, "xmp"),
        new(XmpMetadata.PdfNamespace, "pdf"),
        new(XmpMetadata.PdfAIdNamespace, "pdfaid"),
        new(XmpMetadata.PdfUaIdNamespace, "pdfuaid"),
        new(XmpMetadata.XmpMediaManagementNamespace, "xmpMM"),
        new(XmpMetadata.XmpRightsNamespace, "xmpRights"),
        new(XmpMetadata.PdfXNamespace, "pdfx"),
        new(XmpMetadata.PdfDeclarationsNamespace, "pdfd"),
        new(XmpMetadata.PdfAExtensionNamespace, "pdfaExtension"),
        new(XmpMetadata.PdfASchemaNamespace, "pdfaSchema"),
        new(XmpMetadata.PdfAPropertyNamespace, "pdfaProperty"),
    ];

    /// <summary>Gets the properties of the catalog's XMP packet.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The properties in name order; empty when the document has none or the packet cannot be read.</returns>
    internal static IReadOnlyList<DocumentXmpProperty> GetProperties(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (!access.IsActive || self.IsDisposed)
        {
            return [];
        }

        try
        {
            if (PdfDocumentMetadata.GetXmp(self.Document) is not { } xmp)
            {
                return [];
            }

            var result = new List<DocumentXmpProperty>(xmp.Properties.Count);
            foreach (var (key, values) in xmp.Properties)
            {
                result.Add(new(Name(key), string.Join(ItemSeparator, values)));
            }

            result.Sort(static (first, second) => string.CompareOrdinal(first.Name, second.Name));
            return result;
        }
        catch (PdfException)
        {
            // A damaged packet only means the properties cannot be listed.
            return [];
        }
    }

    /// <summary>Turns a property key such as "{http://purl.org/dc/elements/1.1/}title" into "dc:title" where the schema has a common prefix.</summary>
    /// <param name="key">The property key.</param>
    /// <returns>The name.</returns>
    internal static string Name(string key)
    {
        var close = key.IndexOf('}', StringComparison.Ordinal);
        if (key.Length < 2 || key[0] != '{' || close < 0)
        {
            return key;
        }

        var address = key.AsSpan(1, close - 1);
        foreach (var (known, prefix) in Prefixes)
        {
            if (address.SequenceEqual(known))
            {
                return $"{prefix}:{key.AsSpan(close + 1)}";
            }
        }

        return key;
    }
}
