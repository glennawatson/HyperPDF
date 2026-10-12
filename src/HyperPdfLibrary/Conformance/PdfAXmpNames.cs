// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The XML and XMP namespaces and names the PDF/A metadata rules use.</summary>
internal static class PdfAXmpNames
{
    /// <summary>The RDF namespace.</summary>
    internal const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The XML namespace.</summary>
    internal const string Xml = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The namespace of the <c>x:xmpmeta</c> wrapper.</summary>
    internal const string Meta = "adobe:ns:meta/";

    /// <summary>The Dublin Core namespace.</summary>
    internal const string DublinCore = "http://purl.org/dc/elements/1.1/";

    /// <summary>The older Dublin Core namespace, read as <see cref="DublinCore"/>.</summary>
    internal const string DublinCoreDeprecated = "http://purl.org/dc/1.1/";

    /// <summary>The XMP basic namespace.</summary>
    internal const string XmpBasic = "http://ns.adobe.com/xap/1.0/";

    /// <summary>The Adobe PDF namespace.</summary>
    internal const string AdobePdf = "http://ns.adobe.com/pdf/1.3/";

    /// <summary>The PDF/A identification namespace.</summary>
    internal const string PdfAId = "http://www.aiim.org/pdfa/ns/id/";

    /// <summary>The PDF/A extension schema container namespace.</summary>
    internal const string PdfAExtension = "http://www.aiim.org/pdfa/ns/extension/";

    /// <summary>The PDF/A schema value type namespace.</summary>
    internal const string PdfASchema = "http://www.aiim.org/pdfa/ns/schema#";

    /// <summary>The PDF/A property value type namespace.</summary>
    internal const string PdfAProperty = "http://www.aiim.org/pdfa/ns/property#";

    /// <summary>The PDF/A value type namespace.</summary>
    internal const string PdfAType = "http://www.aiim.org/pdfa/ns/type#";

    /// <summary>The PDF/A field value type namespace.</summary>
    internal const string PdfAField = "http://www.aiim.org/pdfa/ns/field#";

    /// <summary>The target of the XMP packet wrapper instruction.</summary>
    internal const string PacketInstruction = "xpacket";

    /// <summary>The language of the default item of a language alternative.</summary>
    internal const string DefaultLanguage = "x-default";

    /// <summary>The name of the <c>rdf:type</c> qualifier.</summary>
    internal const string TypeQualifier = "rdf:type";
}
