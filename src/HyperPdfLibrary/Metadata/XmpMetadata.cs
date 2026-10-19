// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Metadata;

/// <summary>
/// An XMP packet read from a <c>/Metadata</c> stream. Every simple property and array item is kept in
/// <see cref="Properties"/>, keyed <c>"{namespace-uri}local-name"</c>; the common ones also have named accessors.
/// </summary>
/// <param name="RawPacket">The decoded packet, exactly as stored.</param>
/// <param name="Properties">The property values by key. Arrays list their items in order; a language alternative lists its default first.</param>
/// <param name="IsWellFormed">Whether the packet parsed as XML to the end. A damaged packet keeps the properties read before the damage.</param>
[DebuggerDisplay("XmpMetadata: {Title}")]
public sealed record XmpMetadata(byte[] RawPacket, ReadOnlyDictionary<string, string[]> Properties, bool IsWellFormed)
{
    /// <summary>The Dublin Core namespace.</summary>
    public static readonly string DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";

    /// <summary>The Adobe PDF namespace.</summary>
    public static readonly string PdfNamespace = "http://ns.adobe.com/pdf/1.3/";

    /// <summary>The XMP basic namespace.</summary>
    public static readonly string XmpNamespace = "http://ns.adobe.com/xap/1.0/";

    /// <summary>The PDF/A identification namespace.</summary>
    public static readonly string PdfAIdNamespace = "http://www.aiim.org/pdfa/ns/id/";

    /// <summary>The PDF/UA identification namespace.</summary>
    public static readonly string PdfUaIdNamespace = "http://www.aiim.org/pdfua/ns/id/";

    /// <summary>The PDF Declarations namespace.</summary>
    public static readonly string PdfDeclarationsNamespace = "http://pdfa.org/declarations/";

    /// <summary>The XMP media management namespace.</summary>
    public static readonly string XmpMediaManagementNamespace = "http://ns.adobe.com/xap/1.0/mm/";

    /// <summary>The XMP rights management namespace.</summary>
    public static readonly string XmpRightsNamespace = "http://ns.adobe.com/xap/1.0/rights/";

    /// <summary>The PDF/X identification namespace.</summary>
    public static readonly string PdfXNamespace = "http://ns.adobe.com/pdfx/1.3/";

    /// <summary>The PDF/A extension schema container namespace.</summary>
    public static readonly string PdfAExtensionNamespace = "http://www.aiim.org/pdfa/ns/extension/";

    /// <summary>The PDF/A extension schema description namespace.</summary>
    public static readonly string PdfASchemaNamespace = "http://www.aiim.org/pdfa/ns/schema#";

    /// <summary>The PDF/A extension property description namespace.</summary>
    public static readonly string PdfAPropertyNamespace = "http://www.aiim.org/pdfa/ns/property#";

    /// <summary>The braces around the namespace in a property key.</summary>
    private const int KeyDelimiters = 2;

    /// <summary>The longest key built on the stack; a longer one is built as a string.</summary>
    private const int StackKeyChars = 256;

    /// <summary>The date shapes that stop before the day, which the general parser reads wrongly or refuses.</summary>
    private static readonly string[] PartialDateFormats = ["yyyy", "yyyy-MM"];

    /// <summary>Gets an empty well-formed packet.</summary>
    public static XmpMetadata Empty { get; } = new([], new XmpPropertyDictionary([with(StringComparer.Ordinal)]), true);

    /// <summary>Gets the title (the default language).</summary>
    public string? Title => GetValue(DublinCoreNamespace, "title");

    /// <summary>Gets the creators (authors).</summary>
    public string[] Creators => GetValues(DublinCoreNamespace, "creator");

    /// <summary>Gets the description (the default language).</summary>
    public string? Description => GetValue(DublinCoreNamespace, "description");

    /// <summary>Gets the subjects (keywords as a list).</summary>
    public string[] Subjects => GetValues(DublinCoreNamespace, "subject");

    /// <summary>Gets the producing application (<c>pdf:Producer</c>).</summary>
    public string? Producer => GetValue(PdfNamespace, nameof(Producer));

    /// <summary>Gets the keywords string (<c>pdf:Keywords</c>).</summary>
    public string? Keywords => GetValue(PdfNamespace, nameof(Keywords));

    /// <summary>Gets the PDF version written in the packet (<c>pdf:PDFVersion</c>).</summary>
    public string? PdfVersion => GetValue(PdfNamespace, "PDFVersion");

    /// <summary>Gets the creating tool (<c>xmp:CreatorTool</c>).</summary>
    public string? CreatorTool => GetValue(XmpNamespace, nameof(CreatorTool));

    /// <summary>Gets the creation date (<c>xmp:CreateDate</c>), or null when missing or not a date.</summary>
    public DateTimeOffset? CreateDate => GetDate(nameof(CreateDate));

    /// <summary>Gets the modification date (<c>xmp:ModifyDate</c>), or null when missing or not a date.</summary>
    public DateTimeOffset? ModifyDate => GetDate(nameof(ModifyDate));

    /// <summary>Gets the date the metadata last changed (<c>xmp:MetadataDate</c>), or null when missing or not a date.</summary>
    public DateTimeOffset? MetadataDate => GetDate(nameof(MetadataDate));

    /// <summary>Gets the PDF/A part (<c>pdfaid:part</c>), or null.</summary>
    public int? PdfAPart => GetInt(PdfAIdNamespace, "part");

    /// <summary>Gets the PDF/A conformance level (<c>pdfaid:conformance</c>), for example "B", or null.</summary>
    public string? PdfAConformance => GetValue(PdfAIdNamespace, "conformance");

    /// <summary>Gets the PDF/A revision year (<c>pdfaid:rev</c>), or null.</summary>
    public int? PdfARevision => GetInt(PdfAIdNamespace, "rev");

    /// <summary>Gets the PDF/UA part (<c>pdfuaid:part</c>), or null.</summary>
    public int? PdfUaPart => GetInt(PdfUaIdNamespace, "part");

    /// <summary>Gets the declarations in this packet only, retaining claim associations without verifying the claimed profiles.</summary>
    public PdfDeclaration[] Declarations { get; init; } = [];

    /// <summary>Gets the trapping state (<c>pdf:Trapped</c>): True, False or Unknown.</summary>
    public string? Trapped => GetValue(PdfNamespace, nameof(Trapped));

    /// <summary>Gets the rights statement (<c>dc:rights</c>, the default language).</summary>
    public string? Rights => GetValue(DublinCoreNamespace, "rights");

    /// <summary>Gets the document identifier (<c>xmpMM:DocumentID</c>).</summary>
    public string? DocumentId => GetValue(XmpMediaManagementNamespace, "DocumentID");

    /// <summary>Gets the instance identifier (<c>xmpMM:InstanceID</c>).</summary>
    public string? InstanceId => GetValue(XmpMediaManagementNamespace, "InstanceID");

    /// <summary>Gets the original document identifier (<c>xmpMM:OriginalDocumentID</c>).</summary>
    public string? OriginalDocumentId => GetValue(XmpMediaManagementNamespace, "OriginalDocumentID");

    /// <summary>Gets the copyright flag (<c>xmpRights:Marked</c>), or null when missing or not a boolean.</summary>
    public bool? RightsMarked => GetBool(XmpRightsNamespace, "Marked");

    /// <summary>Gets the rights owners (<c>xmpRights:Owner</c>).</summary>
    public string[] RightsOwners => GetValues(XmpRightsNamespace, "Owner");

    /// <summary>Gets the usage terms (<c>xmpRights:UsageTerms</c>, the default language).</summary>
    public string? UsageTerms => GetValue(XmpRightsNamespace, nameof(UsageTerms));

    /// <summary>Gets the rights web statement (<c>xmpRights:WebStatement</c>).</summary>
    public string? WebStatement => GetValue(XmpRightsNamespace, nameof(WebStatement));

    /// <summary>Gets the PDF/X version (<c>pdfx:GTS_PDFXVersion</c>), for example "PDF/X-4".</summary>
    public string? PdfXVersion => GetValue(PdfXNamespace, "GTS_PDFXVersion");

    /// <summary>Builds a property key.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="localName">The local name.</param>
    /// <returns>The key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Key(string namespaceUri, string localName) => $"{{{namespaceUri}}}{localName}";

    /// <summary>Gets the first value of a property.</summary>
    /// <param name="namespaceUri">The property's namespace URI.</param>
    /// <param name="localName">The property's local name.</param>
    /// <returns>The value, or null when missing.</returns>
    public string? GetValue(string namespaceUri, string localName) =>
        TryGetProperty(namespaceUri, localName, out var values) && values.Length != 0 ? values[0] : null;

    /// <summary>Gets all values of a property.</summary>
    /// <param name="namespaceUri">The property's namespace URI.</param>
    /// <param name="localName">The property's local name.</param>
    /// <returns>The values; empty when missing.</returns>
    public string[] GetValues(string namespaceUri, string localName) =>
        TryGetProperty(namespaceUri, localName, out var values) ? values : [];

    /// <summary>Looks a property up by its namespace and name. A parsed packet's dictionary is searched with a key built on the stack, so the lookup makes no string.</summary>
    /// <param name="namespaceUri">The property's namespace URI.</param>
    /// <param name="localName">The property's local name.</param>
    /// <param name="values">Receives the values.</param>
    /// <returns><see langword="true"/> when the property exists.</returns>
    private bool TryGetProperty(string namespaceUri, string localName, out string[] values)
    {
        var length = namespaceUri.Length + localName.Length + KeyDelimiters;
        if (length <= StackKeyChars && Properties is XmpPropertyDictionary parsed
            && parsed.Lookup.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup))
        {
            Span<char> key = stackalloc char[length];
            key[0] = '{';
            namespaceUri.CopyTo(key[1..]);
            key[namespaceUri.Length + 1] = '}';
            localName.CopyTo(key[(namespaceUri.Length + KeyDelimiters)..]);
            return lookup.TryGetValue(key, out values!);
        }

        return Properties.TryGetValue(Key(namespaceUri, localName), out values!);
    }

    /// <summary>Reads an integer property.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="localName">The local name.</param>
    /// <returns>The integer, or null.</returns>
    private int? GetInt(string namespaceUri, string localName) =>
        int.TryParse(GetValue(namespaceUri, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Reads a boolean property (<c>True</c> or <c>False</c>).</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="localName">The local name.</param>
    /// <returns>The value, or null.</returns>
    private bool? GetBool(string namespaceUri, string localName) =>
        bool.TryParse(GetValue(namespaceUri, localName), out var value) ? value : null;

    /// <summary>Reads an <c>xmp:</c> date property. XMP dates may be a year, a year and month, a date, or a date with a time.</summary>
    /// <param name="localName">The local name.</param>
    /// <returns>The date, or null.</returns>
    private DateTimeOffset? GetDate(string localName)
    {
        var text = GetValue(XmpNamespace, localName);
        if (DateTimeOffset.TryParseExact(text, PartialDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var partial))
        {
            return partial;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
    }
}
