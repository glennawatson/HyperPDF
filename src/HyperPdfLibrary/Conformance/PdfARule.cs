// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A PDF/A rule HyperPdf checks, one for each distinct check in the veraPDF validation profiles. Data/PdfARules.tsv
/// places each rule in the levels that apply it. The summaries quote the profiles' descriptions (© veraPDF Consortium,
/// CC BY 4.0). Both files are generated together from the profiles; keep the numbers in step.
/// </summary>
internal enum PdfARule
{
    /// <summary>For those CMaps that are embedded, the integer value of the WMode entry in the CMap dictionary shall be identical to the WMode value in the embedded CMap stream.</summary>
    CMapWModeMatchesDictionary = 0,

    /// <summary>Maximum value of a CID (character identifier) is 65,535.</summary>
    CMapMaximumCid = 1,

    /// <summary>The ActualText entry shall not contain any PUA values.</summary>
    ActualTextPrivateUse = 2,

    /// <summary>Maximum capacity of an array (in elements) is 8191.</summary>
    ArraySizePdfA1 = 3,

    /// <summary>
    /// The size of any of the page boundaries described in ISO 32000-1:2008, 14.11.2 shall not be less than 3 units in either direction, nor shall it be greater than 14 400 units in
    /// either direction.
    /// </summary>
    BoundingBoxSize = 4,

    /// <summary>Only blend modes that are specified in ISO 32000-1:2008 shall be used for the value of the BM key in an extended graphic state dictionary.</summary>
    BlendModeName = 5,

    /// <summary>Maximum capacity of a dictionary (in entries) is 4095.</summary>
    DictionarySizePdfA1 = 6,

    /// <summary>
    /// In a linearized PDF, if the ID keyword is present in both the first page trailer dictionary and the last trailer dictionary, the value to both instances of the ID keyword shall be
    /// identical.
    /// </summary>
    LinearizedFirstIdMatchesLast = 7,

    /// <summary>
    /// The file trailer dictionary shall contain the ID keyword. The file trailer referred to is either the last trailer dictionary in a PDF file, as described in PDF Reference 3.4.4 and
    /// 3.4.5, or the first page trailer in a linearized PDF file, as described in PDF Reference F.2.
    /// </summary>
    FileIdentifierPdfA1 = 8,

    /// <summary>The document catalog dictionary shall include a MarkInfo dictionary with a Marked entry in it, whose value shall be true.</summary>
    MarkInfoMarked = 9,

    /// <summary>A document's Catalog shall not contain the NeedsRendering key.</summary>
    NeedsRendering = 10,

    /// <summary>The document catalog shall not contain the Requirements key.</summary>
    Requirements = 11,

    /// <summary>A file's name dictionary, as defined in PDF Reference 3.6.3, shall not contain the EmbeddedFiles key.</summary>
    EmbeddedFilesForbidden = 12,

    /// <summary>A PDF/A-4f conforming file shall contain an EmbeddedFiles key in the name dictionary of the document catalog dictionary.</summary>
    EmbeddedFilesRequired = 13,

    /// <summary>The Info key shall not be present in the trailer dictionary of PDF/A-4 conforming files unless there exists a PieceInfo entry in the document catalog dictionary.</summary>
    InfoNeedsPieceInfo = 14,

    /// <summary>
    /// The file header line shall be immediately followed by a comment consisting of a % character followed by at least four characters, each of whose encoded byte values shall have a
    /// decimal value greater than 127.
    /// </summary>
    HeaderBinaryComment = 15,

    /// <summary>
    /// The % character of the file header shall occur at byte offset 0 of the file. The first line of a PDF file is a header identifying the version of the PDF specification to which the
    /// file conforms.
    /// </summary>
    HeaderPdfA1 = 16,

    /// <summary>The file header shall begin at byte zero and shall consist of "%PDF-1.n" followed by a single EOL marker, where 'n' is a single digit number between 0 (30h) and 7 (37h).</summary>
    HeaderPdf17 = 17,

    /// <summary>The file header shall begin at byte zero and shall consist of "%PDF-2.n" followed by a single EOL marker, where 'n' is a single digit number between 0 (30h) and 9 (39h).</summary>
    HeaderPdf20 = 18,

    /// <summary>The document catalog dictionary shall not contain a key with the name OCProperties.</summary>
    OptionalContentForbidden = 19,

    /// <summary>The file trailer dictionary shall contain the ID keyword whose value shall be File Identifiers as defined in ISO 32000-1:2008, 14.4.</summary>
    FileIdentifier = 20,

    /// <summary>Maximum number of indirect objects in a PDF file is 8,388,607.</summary>
    IndirectObjectCount = 21,

    /// <summary>No data shall follow the last end-of-file marker except a single optional end-of-line marker.</summary>
    DataAfterEndOfFile = 22,

    /// <summary>The file specification dictionary shall contain key AFRelationship of type Name identifying the relationship between the embedded file and the content of the document.</summary>
    FileSpecificationRelationshipRequired = 23,

    /// <summary>A file specification dictionary, as defined in PDF 3.10.2, shall not contain the EF key.</summary>
    FileSpecificationEmbeddedFileForbidden = 24,

    /// <summary>The file specification dictionary for an embedded file shall contain the F and UF keys.</summary>
    FileSpecificationNames = 25,

    /// <summary>
    /// Each embedded file’s file specification dictionary shall contain an AFRelationship key (ISO 32000-2, 7.11.3) that describes how this embedded file relates to the content of the
    /// PDF.
    /// </summary>
    FileSpecificationRelationship = 26,

    /// <summary>
    /// The additional information provided for associated files as well as the usage requirements for associated files indicate the relationship between the embedded file and the PDF
    /// document or the part of the PDF document with which it is associated.
    /// </summary>
    FileSpecificationAssociated = 27,

    /// <summary>The LZWDecode filter shall not be permitted.</summary>
    FilterLzwForbidden = 28,

    /// <summary>
    /// All standard stream filters listed in ISO 32000-1:2008, 7.4, Table 6 may be used, with the exception of LZWDecode. In addition, the Crypt filter shall not be used unless the value
    /// of the Name key in the decode parameters dictionary is Identity. Filters that are not listed in ISO 32000-1:2008, 7.4, Table 6 shall not be used.
    /// </summary>
    FilterAllowed = 29,

    /// <summary>The LZWDecode filter shall not be permitted.</summary>
    InlineFilterLzwForbidden = 30,

    /// <summary>
    /// The value of the F key in the Inline Image dictionary shall not be LZW, LZWDecode, Crypt, a value not listed in ISO 32000-1:2008, Table 6, or an array containing any such value.
    /// </summary>
    InlineFilterAllowed = 31,

    /// <summary>
    /// The object number and generation number shall be separated by a single white-space character. The generation number and obj keyword shall be separated by a single white-space
    /// character. The object number and endobj keyword shall each be preceded by an EOL marker. The obj and endobj keywords shall each be followed by an EOL marker.
    /// </summary>
    IndirectObjectSpacing = 32,

    /// <summary>
    /// The value of Author entry from the document information dictionary, if present, and its analogous XMP property "dc:creator" shall be equivalent. dc:creator shall contain exactly
    /// one entry.
    /// </summary>
    InfoAuthorMatchesXmp = 33,

    /// <summary>The value of Creator entry from the document information dictionary, if present, and its analogous XMP property "xmp:CreatorTool" shall be equivalent.</summary>
    InfoCreatorMatchesXmp = 34,

    /// <summary>The value of Keywords entry from the document information dictionary, if present, and its analogous XMP property "pdf:Keywords" shall be equivalent.</summary>
    InfoKeywordsMatchXmp = 35,

    /// <summary>The value of Producer entry from the document information dictionary, if present, and its analogous XMP property "pdf:Producer" shall be equivalent.</summary>
    InfoProducerMatchesXmp = 36,

    /// <summary>The value of Subject entry from the document information dictionary, if present, and its analogous XMP property "dc:description['x-default']" shall be equivalent.</summary>
    InfoSubjectMatchesXmp = 37,

    /// <summary>The value of Title entry from the document information dictionary, if present, and its analogous XMP property "dc:title['x-default']" shall be equivalent.</summary>
    InfoTitleMatchesXmp = 38,

    /// <summary>The value of CreationDate entry from the document information dictionary, if present, and its analogous XMP property "xmp:CreateDate" shall be equivalent.</summary>
    InfoCreationDateMatchesXmp = 39,

    /// <summary>The value of ModDate entry from the document information dictionary, if present, and its analogous XMP property "xmp:ModifyDate" shall be equivalent.</summary>
    InfoModificationDateMatchesXmp = 40,

    /// <summary>If a document information dictionary is present, it shall only contain a ModDate entry.</summary>
    InfoOnlyModDate = 41,

    /// <summary>Largest Integer value is 2,147,483,647. Smallest integer value is -2,147,483,648.</summary>
    IntegerRange = 42,

    /// <summary>
    /// If the Lang entry is present in the document's Catalog dictionary or in a structure element dictionary or property list, its value shall be a language identifier as described in
    /// ISO 32000-1:2008, 14.9.2. A language identifier shall either be the empty text string, to indicate that the language is unknown, or a Language-Tag as defined in RFC 3066, Tags for
    /// the Identification of Languages.
    /// </summary>
    LanguageTagPdfA2 = 43,

    /// <summary>
    /// If the Lang entry is present in the document catalog dictionary or in a structure element dictionary or property list, its value shall be a language identifier as defined by RFC
    /// 1766, Tags for the Identification of Languages, as described in PDF Reference 9.8.1.
    /// </summary>
    LanguageTagPdfA1 = 44,

    /// <summary>Maximum length of a name (in bytes) is 127.</summary>
    NameLength = 45,

    /// <summary>A conforming file shall not contain any real number outside the range of +/-3.403 x 10^38.</summary>
    RealRange = 46,

    /// <summary>Absolute real value must be less than or equal to 32767.0.</summary>
    RealRangePdfA1 = 47,

    /// <summary>A conforming file shall not contain any real number closer to zero than +/-1.175 x 10^(-38).</summary>
    RealMagnitude = 48,

    /// <summary>
    /// Where a rendering intent is specified, its value shall be one of the four values defined in PDF Reference RelativeColorimetric, AbsoluteColorimetric, Perceptual or Saturation.
    /// </summary>
    RenderingIntentName = 49,

    /// <summary>A stream object dictionary shall not contain the F, FFilter, or FDecodeParms keys.</summary>
    StreamExternalFile = 50,

    /// <summary>
    /// The value of the Length key specified in the stream dictionary shall match the number of bytes in the file following the LINE FEED character after the stream keyword and preceding
    /// the EOL marker before the endstream keyword.
    /// </summary>
    StreamLength = 51,

    /// <summary>
    /// The stream keyword shall be followed either by a CARRIAGE RETURN (0Dh) and LINE FEED (0Ah) character sequence or by a single LINE FEED character. The endstream keyword shall be
    /// preceded by an EOL marker.
    /// </summary>
    StreamKeywordLineEnds = 52,

    /// <summary>All non-white-space characters in hexadecimal strings shall be in the range 0 to 9, A to F or a to f.</summary>
    HexStringCharacters = 53,

    /// <summary>Hexadecimal strings shall contain an even number of non-white-space characters.</summary>
    HexStringEvenDigits = 54,

    /// <summary>A conforming file shall not contain any string longer than 32767 bytes.</summary>
    StringLength = 55,

    /// <summary>Maximum length of a string (in bytes) is 65535.</summary>
    StringLengthPdfA1 = 56,

    /// <summary>The keyword Encrypt shall not be used in the trailer dictionary.</summary>
    Encryption = 57,

    /// <summary>
    /// Font names, names of colourants in Separation and DeviceN colour spaces, and structure type names, after expansion of character sequences escaped with a NUMBER SIGN (23h), if any,
    /// shall be valid UTF-8 character sequences.
    /// </summary>
    NameUtf8 = 58,

    /// <summary>In a cross reference subsection header the starting object number and the range shall be separated by a single SPACE character (20h).</summary>
    XrefSubsectionHeader = 59,

    /// <summary>The xref keyword and the cross reference subsection header shall be separated by a single EOL marker.</summary>
    XrefLineEnds = 60,

    /// <summary>
    /// The MIME type of an embedded file, or a subset of a file, shall be specified using the Subtype key of the file specification dictionary. If the MIME type is not known, the
    /// "application/octet-stream" shall be used.
    /// </summary>
    EmbeddedFileMimeType = 61,

    /// <summary>
    /// A file specification dictionary, as defined in ISO 32000-1:2008, 7.11.3, may contain the EF key, provided that the embedded file is compliant with either ISO 19005-1 or this part
    /// of ISO 19005.
    /// </summary>
    EmbeddedFilePdfA12 = 62,

    /// <summary>All of the embedded files shall be compliant with ISO 19005-1, 19005-2 or 19005-4.</summary>
    EmbeddedFilePdfA124 = 63,

    /// <summary>Field 'namespaceURI' of the PDF/A Schema value type in the PDF/A extension schema shall be present and shall have type URI and namespace prefix 'pdfaSchema'.</summary>
    ExtensionSchemaNamespaceUri = 64,

    /// <summary>Field 'prefix' of the PDF/A Schema value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaSchema'.</summary>
    ExtensionSchemaPrefix = 65,

    /// <summary>Field 'property' of the PDF/A Schema value type in the PDF/A extension schema shall have type Seq Property and namespace prefix 'pdfaSchema'.</summary>
    ExtensionSchemaProperties = 66,

    /// <summary>Field 'schema' of the PDF/A Schema value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaSchema'.</summary>
    ExtensionSchemaName = 67,

    /// <summary>Field 'valueType' of the PDF/A Schema value type in the PDF/A extension schema shall have type Seq ValueType and namespace prefix 'pdfaSchema'.</summary>
    ExtensionSchemaValueTypes = 68,

    /// <summary>Field 'description' of the PDF/A Field value type in the PDF/A extension schema shall have type Text and namespace prefix 'pdfaField'.</summary>
    ExtensionFieldDescription = 69,

    /// <summary>Field 'name' of the PDF/A Field value type in the PDF/A extension schema shall have type Text and namespace prefix 'pdfaField'.</summary>
    ExtensionFieldName = 70,

    /// <summary>
    /// Field 'valueType' of the PDF/A Field value type in the PDF/A extension schema shall have type Text and namespace prefix 'pdfaField'. Value of valueType property shall be defined.
    /// </summary>
    ExtensionFieldValueType = 71,

    /// <summary>Extension schema descriptions shall be specified using the PDF/A extension schema description schema defined in this clause.</summary>
    ExtensionUndefinedFields = 72,

    /// <summary>
    /// Field 'category' of the PDF/A Property value type in the PDF/A extension schema shall be present and shall have type Text, value external or internal and namespace prefix
    /// 'pdfaProperty'.
    /// </summary>
    ExtensionPropertyCategory = 73,

    /// <summary>Field 'description' of the PDF/A Property value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaProperty'.</summary>
    ExtensionPropertyDescription = 74,

    /// <summary>Field 'name' of the PDF/A Property value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaProperty'.</summary>
    ExtensionPropertyName = 75,

    /// <summary>
    /// Field 'valueType' of the PDF/A Property value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaProperty'. Value of valueType
    /// property shall be defined.
    /// </summary>
    ExtensionPropertyValueType = 76,

    /// <summary>Field 'description' of the PDF/A ValueType value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaType'.</summary>
    ExtensionValueTypeDescription = 77,

    /// <summary>Field 'field' of the PDF/A ValueType value type in the PDF/A extension schema shall have type Seq Field and namespace prefix 'pdfaType'.</summary>
    ExtensionValueTypeFields = 78,

    /// <summary>Field 'namespaceURI' of the PDF/A ValueType value type in the PDF/A extension schema shall be present and shall have type URI and namespace prefix 'pdfaType'.</summary>
    ExtensionValueTypeNamespaceUri = 79,

    /// <summary>Field 'prefix' of the PDF/A ValueType value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaType'.</summary>
    ExtensionValueTypePrefix = 80,

    /// <summary>Field 'type' of the PDF/A ValueType value type in the PDF/A extension schema shall be present and shall have type Text and namespace prefix 'pdfaType'.</summary>
    ExtensionValueTypeName = 81,

    /// <summary>
    /// The extension schema container schema uses the namespace URI "http://www.aiim.org/pdfa/ns/extension/". The required schema namespace prefix is pdfaExtension. pdfaExtension:schemas
    /// - Bag Schema - Description of extension schemas.
    /// </summary>
    ExtensionContainer = 82,

    /// <summary>
    /// A PDF/A-2 compliant document shall not contain a reference to the .notdef glyph from any of the text showing operators, regardless of text rendering mode, in any content stream.
    /// </summary>
    GlyphNotdef = 83,

    /// <summary>Embedded font programs shall define all font glyphs referenced for rendering with conforming file.</summary>
    GlyphPresent = 84,

    /// <summary>
    /// For every font embedded in a conforming file and used for rendering, the glyph width information in the font dictionary and in the embedded font program shall be consistent.
    /// </summary>
    GlyphWidth = 85,

    /// <summary>
    /// The font dictionary shall include a ToUnicode entry whose value is a CMap stream object that maps character codes to Unicode values, as described in PDF Reference 5.9, unless the
    /// font meets any of the following three conditions: (*) fonts that use the predefined encodings MacRomanEncoding, MacExpertEncoding or WinAnsiEncoding, or that use the predefined
    /// Identity-H or Identity-V CMaps; (*) Type 1 fonts whose character names are taken from the Adobe standard Latin character set or the set of named characters in the Symbol font, as
    /// defined in PDF Reference Appendix D; (*) Type 0 fonts whose descendant CIDFont uses the Adobe-GB1, Adobe-CNS1, Adobe-Japan1 or Adobe-Korea1 character collections.
    /// </summary>
    GlyphToUnicode = 86,

    /// <summary>The Unicode values specified in the ToUnicode CMap shall all be greater than zero (0), but not equal to either U+FEFF or U+FFFE.</summary>
    GlyphToUnicodeValues = 87,

    /// <summary>
    /// For any character, regardless of its rendering mode, that is mapped to a code or codes in the Unicode Private Use Area (PUA), an ActualText entry as described in ISO 32000-1:2008,
    /// 14.9.4 shall be present for this character or a sequence of characters of which such a character is a part.
    /// </summary>
    GlyphPrivateUseActualText = 88,

    /// <summary>All ICCBased colour spaces shall be embedded as ICC profile streams as described in PDF Reference 4.5.</summary>
    IccInputProfilePdfA1 = 89,

    /// <summary>The profile that forms the stream of an ICCBased colour space shall conform to ICC.1:1998-09, ICC.1:2001-12, ICC.1:2003-09 or ISO 15076-1.</summary>
    IccInputProfile = 90,

    /// <summary>
    /// A PDF/A-1 OutputIntent is an OutputIntent dictionary, as defined by PDF Reference 9.10.4, that is included in the file's OutputIntents array and has GTS_PDFA1 as the value of its S
    /// key and a valid ICC profile stream as the value its DestOutputProfile key.
    /// </summary>
    IccOutputProfilePdfA1 = 91,

    /// <summary>
    /// The profile stream that is the value of the DestOutputProfile key shall either be an output profile (Device Class = "prtr") or a monitor profile (Device Class = "mntr"). The
    /// profiles shall have a colour space of either "GRAY", "RGB", or "CMYK".
    /// </summary>
    IccOutputProfile = 92,

    /// <summary>
    /// All ICCBased colour spaces shall be embedded as ICC profile streams as described in PDF Reference 4.5. The number of color components in the color space described by the ICC
    /// profile data must match the number of components actually in the ICC profile. As of PDF 1.4, N must be 1, 3, or 4.
    /// </summary>
    IccProfileComponents = 93,

    /// <summary>The bit-depth of the JPEG2000 data shall have a value in the range 1 to 38. All colour channels in the JPEG2000 data shall have the same bit-depth.</summary>
    JpxBitDepth = 94,

    /// <summary>JPEG2000 enumerated colour space 19 (CIEJab) shall not be used.</summary>
    JpxEnumeratedColorSpace = 95,

    /// <summary>
    /// The value of the METH entry in its 'colr' box shall be 0x01, 0x02 or 0x03. A conforming reader shall use only that colour space and shall ignore all other colour space
    /// specifications.
    /// </summary>
    JpxColorMethod = 96,

    /// <summary>
    /// If the number of colour space specifications in the JPEG2000 data is greater than 1, there shall be exactly one colour space specification that has the value 0x01 in the APPROX
    /// field.
    /// </summary>
    JpxColorSpecifications = 97,

    /// <summary>The number of colour channels in the JPEG2000 data shall be 1, 3 or 4.</summary>
    JpxChannels = 98,

    /// <summary>The PDF/A version and conformance level of a file shall be specified using the PDF/A Identification extension schema.</summary>
    CatalogXmpIdentification = 99,

    /// <summary>A content stream shall not contain any operators not defined in PDF Reference even if such operators are bracketed by the BX/EX compatibility operators.</summary>
    UndefinedOperator = 100,

    /// <summary>Maximum depth of graphics state nesting by q and Q operators is 28.</summary>
    GraphicsStateNesting = 101,

    /// <summary>OutputIntents array shall contain at most one PDF/A output intent.</summary>
    OutputIntentCount = 102,

    /// <summary>
    /// If a file's OutputIntents array contains more than one entry, then all entries that contain a DestOutputProfile key shall have as the value of that key the same indirect object,
    /// which shall be a valid ICC profile stream.
    /// </summary>
    OutputIntentSameProfile = 103,

    /// <summary>The Subtype entry in a 3D stream dictionary (ISO 32000-2:2020, 13.6.3) shall have a value which is either U3D or PRC as described in Annex B.</summary>
    ThreeDStreamSubtype = 104,

    /// <summary>The NeedAppearances flag of the interactive form dictionary shall either not be present or shall be false.</summary>
    NeedAppearances = 105,

    /// <summary>
    /// The document's interactive form dictionary that forms the value of the AcroForm key in the document's Catalog of a PDF/A-2 file, if present, shall not contain the XFA key.
    /// </summary>
    XfaForm = 106,

    /// <summary>
    /// The Launch, Sound, Movie, ResetForm, ImportData, Hide, SetOCGState, Rendition, Trans, GoTo3DView and JavaScript actions shall not be permitted. Additionally, the deprecated
    /// set-state and no-op actions shall not be permitted.
    /// </summary>
    ActionTypePdfA2 = 107,

    /// <summary>
    /// The Launch, Sound, Movie, ResetForm, ImportData, Hide, Rendition, Trans, SetOCGState and GoTo3DView actions shall not be permitted. Additionally, the obsoleted set-state and no-op
    /// actions shall not be permitted.
    /// </summary>
    ActionTypePdfA4 = 108,

    /// <summary>
    /// The Launch, Sound, Movie, ResetForm, ImportData, Hide, Rendition and Trans actions shall not be permitted. Additionally, the deprecated set-state and no-op actions shall not be
    /// permitted. The SetOCGState and GoTo3DView actions shall only be permitted in a PDF/A-4e compliant file as described in Annex B.
    /// </summary>
    ActionTypePdfA4E = 109,

    /// <summary>
    /// The Launch, Sound, Movie, ResetForm, ImportData and JavaScript actions shall not be permitted. Additionally, the deprecated set-state and no-op actions shall not be permitted. The
    /// Hide action shall not be permitted (Corrigendum 2).
    /// </summary>
    ActionTypePdfA1 = 110,

    /// <summary>
    /// If a document catalog dictionary or page dictionary or an annotation dictionary (other than a Widget annotation dictionary) include an AA entry, its value (which is an
    /// additional-actions dictionary) shall only contain keys from the following list: E, X, D, U, Fo, and Bl.
    /// </summary>
    AdditionalActionsTriggers = 111,

    /// <summary>
    /// An annotation dictionary shall not contain the C array or the IC array unless the colour space of the DestOutputProfile in the PDF/A-1 OutputIntent dictionary, defined in 6.2.2, is
    /// RGB.
    /// </summary>
    AnnotationColorNeedsRgbIntent = 112,

    /// <summary>
    /// All annotation dictionaries shall contain the F key, except for annotation dictionaries whose Subtype key has a value of Popup or those whose Rect key’s value is an array where the
    /// value at index 0 is equal to the value at index 2 and the value at index 1 is equal to the value at index 3.
    /// </summary>
    AnnotationFlagsPresentPdfA4 = 113,

    /// <summary>
    /// Every annotation (including those whose Subtype value is Widget, as used for form fields), except for the two cases listed below, shall have at least one appearance dictionary: -
    /// annotations where the value of the Rect key consists of an array where value 1 is equal to value 3 and value 2 is equal to value 4; - annotations whose Subtype value is Popup or
    /// Link.
    /// </summary>
    AnnotationAppearancePdfA2 = 114,

    /// <summary>
    /// Every annotation (including those whose Subtype value is Widget, as used for form fields), except for the two cases listed below, shall have at least one appearance dictionary:
    /// Annotations where the value of the Rect key consists of an array where the value at index 1 is equal to the value at index 3 and the value at index 2 is equal to the value at index
    /// 4; - annotations whose Subtype value is Popup, Link or Projection.
    /// </summary>
    AnnotationAppearancePdfA4 = 115,

    /// <summary>
    /// If an annotation dictionary's Subtype key has value other than Widget, or if FT key associated with Widget annotation has value other than Btn, the value of the N key shall be an
    /// appearance stream.
    /// </summary>
    AnnotationNormalAppearanceStream = 116,

    /// <summary>If an annotation dictionary's Subtype key has a value of Widget and its FT key has a value of Btn, the value of the N key shall be an appearance subdictionary.</summary>
    AnnotationButtonAppearanceDictionary = 117,

    /// <summary>For all annotation dictionaries containing an AP key, the appearance dictionary that it defines as its value shall contain only the N key.</summary>
    AnnotationOnlyNormalAppearance = 118,

    /// <summary>An annotation dictionary shall not contain the CA key with a value other than 1.0.</summary>
    AnnotationOpacity = 119,

    /// <summary>An annotation dictionary shall contain the F key. The F key’s Print flag bit shall be set to 1 and its Hidden, Invisible and NoView flag bits shall be set to 0.</summary>
    AnnotationFlagsPdfA1 = 120,

    /// <summary>If present, the F key's Print flag bit shall be set to 1 and its Hidden, Invisible, ToggleNoView, and NoView flag bits shall be set to 0.</summary>
    AnnotationFlags = 121,

    /// <summary>Except for annotation dictionaries whose Subtype value is Popup, all annotation dictionaries shall contain the F key.</summary>
    AnnotationFlagsPresent = 122,

    /// <summary>Annotation types not defined in PDF Reference shall not be permitted. Additionally, the FileAttachment, Sound and Movie types shall not be permitted.</summary>
    AnnotationTypePdfA1 = 123,

    /// <summary>Annotation types not defined in ISO 32000-1 shall not be permitted. Additionally, the 3D, Sound, Screen and Movie types shall not be permitted.</summary>
    AnnotationTypePdfA2 = 124,

    /// <summary>
    /// Annotation types not defined in ISO 32000-2:2020, 12.5.6.1, Table 171 shall not be permitted. Additionally, the Sound, Screen, Movie, 3D and RichMedia types shall not be permitted.
    /// The FileAttachment type shall only be permitted in a PDF/A-4f compliant file as described in Annex A.
    /// </summary>
    AnnotationTypePdfA4F = 125,

    /// <summary>
    /// Annotation types not defined in ISO 32000-2:2020, 12.5.6.1, Table 171 shall not be permitted. Additionally, the Sound, Screen, Movie and FileAttachment types shall not be
    /// permitted. 3D and RichMedia types shall only be permitted in a PDF/A-4e compliant file as described in Annex B.
    /// </summary>
    AnnotationTypePdfA4E = 126,

    /// <summary>
    /// Annotation types not defined in ISO 32000-2:2020, 12.5.6.1, Table 171 shall not be permitted. Additionally, the Sound, Screen, Movie, 3D, RichMedia and FileAttachment types shall
    /// not be permitted.
    /// </summary>
    AnnotationTypePdfA4 = 127,

    /// <summary>
    /// ISO 32000-1:2008, 9.7.4, Table 117 requires that all embedded Type 2 CIDFonts in the CIDFont dictionary shall contain a CIDToGIDMap entry that shall be a stream mapping from CIDs
    /// to glyph indices or the name Identity, as described in ISO 32000-1:2008, 9.7.4, Table 117.
    /// </summary>
    CidFontToGidMap = 128,

    /// <summary>
    /// For all Type 2 CIDFonts that are used for rendering, the CIDFont dictionary shall contain a CIDToGIDMap entry that shall be a stream mapping from CIDs to glyph indices or the name
    /// Identity, as described in PDF Reference Table 5.13.
    /// </summary>
    CidFontToGidMapPdfA1 = 129,

    /// <summary>
    /// If the FontDescriptor dictionary of an embedded CID font contains a CIDSet stream, then it shall identify all CIDs which are present in the font program, regardless of whether a
    /// CID in the font is referenced or used by the PDF or not.
    /// </summary>
    CidSetComplete = 130,

    /// <summary>
    /// For all CIDFont subsets referenced within a conforming file, the font descriptor dictionary shall include a CIDSet stream identifying which CIDs are present in the embedded CIDFont
    /// file, as described in PDF Reference Table 5.20.
    /// </summary>
    CidSetCompletePdfA1 = 131,

    /// <summary>All CMaps used within a PDF/A-2 file, except those listed in ISO 32000-1:2008, 9.7.5.2, Table 118, shall be embedded in that file as described in ISO 32000-1:2008, 9.7.5.</summary>
    CMapNamePredefined = 132,

    /// <summary>All CMaps used within a conforming file, except Identity-H and Identity-V, shall be embedded in that file as described in PDF Reference 5.6.4.</summary>
    CMapNamePdfA1 = 133,

    /// <summary>
    /// A content stream that references other objects, such as images and fonts that are necessary to fully render or process the stream, shall have an explicitly associated Resources
    /// dictionary as described in ISO 32000-1:2008, 7.8.3.
    /// </summary>
    ContentInheritedResources = 134,

    /// <summary>
    /// A content stream's named resources shall be defined by a resource dictionary, which shall enumerate the named resources needed by the operators in the content stream and the names
    /// by which they can be referred to.
    /// </summary>
    ContentUndefinedResources = 135,

    /// <summary>
    /// DeviceCMYK shall only be used if a device independent DefaultCMYK colour space has been set when the DeviceCMYK colour space is used or if the current transparency blending space,
    /// when the DeviceCMYK colour space is used, is a device independent CMYK-based colour space or the current PDF/A OutputIntent, when the DeviceCMYK colour space is used, contains a
    /// 'CMYK' destination profile.
    /// </summary>
    DeviceCmykPdfA4 = 136,

    /// <summary>DeviceCMYK may be used only if the file has a PDF/A-1 OutputIntent that uses a CMYK colour space.</summary>
    DeviceCmyk = 137,

    /// <summary>If an uncalibrated colour space is used in a file then that file shall contain a PDF/A-1 OutputIntent, as defined in 6.2.2.</summary>
    DeviceGray = 138,

    /// <summary>
    /// DeviceGray shall only be used if a device independent DefaultGray colour space has been set when the DeviceGray colour space is used, or if a PDF/A OutputIntent is in effect.
    /// </summary>
    DeviceGrayPdfA4 = 139,

    /// <summary>For any spot colour used in a DeviceN or NChannel colour space, an entry in the Colorants dictionary shall be present.</summary>
    DeviceNColorants = 140,

    /// <summary>A conforming file shall not contain a DeviceN colour space with more than 32 colourants.</summary>
    DeviceNComponents = 141,

    /// <summary>Maximum number of DeviceN components is 8.</summary>
    DeviceNComponentsPdfA1 = 142,

    /// <summary>
    /// DeviceRGB shall only be used if a device independent DefaultRGB colour space has been set when the DeviceRGB colour space is used or if the current transparency blending space,
    /// when the DeviceRGB colour space is used, is a device independent RGB-based colour space or the current PDF/A OutputIntent, when the DeviceRGB colour space is used, contains an 'RGB
    /// ' destination profile.
    /// </summary>
    DeviceRgbPdfA4 = 143,

    /// <summary>DeviceRGB may be used only if the file has a PDF/A-1 OutputIntent that uses an RGB colour space.</summary>
    DeviceRgb = 144,

    /// <summary>
    /// If the Version key is present in the document catalog dictionary, the first character in its value shall be a 2 (32h) and the second character of its value shall be a PERIOD (2Eh)
    /// (decimal point). The third character shall be a decimal digit. The number of characters of the value of the Version key shall be exactly 3.
    /// </summary>
    VersionPdf20 = 145,

    /// <summary>The document catalog dictionary shall not include an AA entry for an additional-actions dictionary.</summary>
    DocumentAdditionalActions = 146,

    /// <summary>There shall be no AlternatePresentations entry in the document's name dictionary.</summary>
    AlternatePresentations = 147,

    /// <summary>
    /// The document catalog dictionary of a conforming file shall contain the Metadata key. The metadata stream dictionary shall contain entry Type with value /Metadata and entry Subtype
    /// with value /XML.
    /// </summary>
    CatalogMetadata = 148,

    /// <summary>
    /// The logical structure of the conforming file shall be described by a structure hierarchy rooted in the StructTreeRoot entry of the document catalog dictionary, as described in PDF
    /// Reference 9.6.
    /// </summary>
    StructureTreeRoot = 149,

    /// <summary>Xref streams shall not be used.</summary>
    XrefStreamForbidden = 150,

    /// <summary>If a CA key is present in an ExtGState object, its value shall be 1.0.</summary>
    ExtGStateStrokeAlpha = 151,

    /// <summary>If a ca key is present in an ExtGState object, its value shall be 1.0.</summary>
    ExtGStateFillAlpha = 152,

    /// <summary>If a BM key is present in an ExtGState object, its value shall be Normal or Compatible.</summary>
    ExtGStateBlendModePdfA1 = 153,

    /// <summary>A graphics state parameter dictionary shall not contain the HTO key.</summary>
    ExtGStateHalftoneOrigin = 154,

    /// <summary>An ExtGState dictionary shall not contain the HTP key.</summary>
    ExtGStateHalftonePhase = 155,

    /// <summary>If an SMask key appears in an ExtGState dictionary, its value shall be None.</summary>
    ExtGStateSoftMask = 156,

    /// <summary>An ExtGState dictionary shall not contain the TR key.</summary>
    ExtGStateTransfer = 157,

    /// <summary>An ExtGState dictionary shall not contain the TR2 key with a value other than Default.</summary>
    ExtGStateTransfer2 = 158,

    /// <summary>Property "amd" of the PDF/A Identification Schema shall have namespace prefix "pdfaid".</summary>
    IdentificationAmendmentPrefix = 159,

    /// <summary>A Level A conforming file shall specify the value of "pdfaid:conformance" as A.</summary>
    IdentificationConformanceA = 160,

    /// <summary>A Level A conforming file shall specify the value of "pdfaid:conformance" as A. A Level B conforming file shall specify the value of "pdfaid:conformance" as B.</summary>
    IdentificationConformancePdfA1 = 161,

    /// <summary>
    /// A Level A conforming file shall specify the value of "pdfaid:conformance" as A. A Level B conforming file shall specify the value of "pdfaid:conformance" as B. A Level U conforming
    /// file shall specify the value of "pdfaid:conformance" as U.
    /// </summary>
    IdentificationConformancePdfA2 = 162,

    /// <summary>A PDF/A-4e conforming file (as described in Annex B) shall specify the value of "pdfaid:conformance" as E.</summary>
    IdentificationConformanceE = 163,

    /// <summary>A PDF/A-4f conforming file (as described in Annex A) shall specify the value of "pdfaid:conformance" as F.</summary>
    IdentificationConformanceF = 164,

    /// <summary>
    /// A Level A conforming file shall specify the value of "pdfaid:conformance" as A. A Level B conforming file shall specify the value of "pdfaid:conformance" as B. A Level U conforming
    /// file shall specify the value of "pdfaid:conformance" as U.
    /// </summary>
    IdentificationConformanceU = 165,

    /// <summary>A file that does not conform to either PDF/A-4e or PDF/A-4f shall not provide any "pdfaid:conformance".</summary>
    IdentificationConformanceNone = 166,

    /// <summary>Property "conformance" of the PDF/A Identification Schema shall have namespace prefix "pdfaid".</summary>
    IdentificationConformancePrefix = 167,

    /// <summary>Property "corr" of the PDF/A Identification Schema shall have namespace prefix "pdfaid".</summary>
    IdentificationCorrigendumPrefix = 168,

    /// <summary>The value of "pdfaid:part" shall be the part number of ISO 19005 to which the file conforms.</summary>
    IdentificationPart1 = 169,

    /// <summary>The value of "pdfaid:part" shall be the part number of ISO 19005 to which the file conforms.</summary>
    IdentificationPart2 = 170,

    /// <summary>The value of "pdfaid:part" shall be the part number of ISO 19005 to which the file conforms.</summary>
    IdentificationPart3 = 171,

    /// <summary>The value of "pdfaid:part" shall be the part number of ISO 19005 to which the file conforms.</summary>
    IdentificationPart4 = 172,

    /// <summary>Property "part" of the PDF/A Identification Schema shall have namespace prefix "pdfaid".</summary>
    IdentificationPartPrefix = 173,

    /// <summary>The value of "pdfaid:rev" shall be "2020".</summary>
    IdentificationRevision = 174,

    /// <summary>Property "rev" of the PDF/A Identification Schema shall have namespace prefix "pdfaid".</summary>
    IdentificationRevisionPrefix = 175,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. Subtype - name - (Required) The type of font; must be "Type1" for Type 1
    /// fonts, "MMType1" for multiple master fonts, "TrueType" for TrueType fonts "Type3" for Type 3 fonts, "Type0" for Type 0 fonts and "CIDFontType0" or "CIDFontType2" for CID fonts.
    /// </summary>
    FontSubtype = 176,

    /// <summary>
    /// The font programs for all fonts used within a conforming file shall be embedded within that file, as defined in PDF Reference 5.8, except when the fonts are used exclusively with
    /// text rendering mode 3.
    /// </summary>
    FontEmbedded = 177,

    /// <summary>All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. BaseFont - name - (Required) The PostScript name of the font.</summary>
    FontName = 178,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. Type - name - (Required) The type of PDF object that this dictionary
    /// describes; must be Font for a font dictionary.
    /// </summary>
    FontType = 179,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. The subtype is the value of the Subtype key, if present, in the font file
    /// stream dictionary. The only valid values of this key in PDF 1.4 are Type1C - Type 1–equivalent font program represented in the Compact Font Format (CFF) and CIDFontType0C - Type 0
    /// CIDFont program represented in the Compact Font Format (CFF).
    /// </summary>
    FontFileSubtypePdfA1 = 180,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. The subtype is the value of the Subtype key, if present, in the font file
    /// stream dictionary. The only valid values of this key in PDF 1.7 are Type1C - Type 1–equivalent font program represented in the Compact Font Format (CFF), CIDFontType0C - Type 0
    /// CIDFont program represented in the Compact Font Format (CFF) and OpenType - OpenType® font program, as described in the OpenType Specification v.1.4.
    /// </summary>
    FontFileSubtype = 181,

    /// <summary>A Field dictionary shall not include an AA entry for an additional-actions dictionary.</summary>
    FormFieldAdditionalActions = 182,

    /// <summary>
    /// A Group object with an S key with a value of Transparency shall not be included in a form XObject. A Group object with an S key with a value of Transparency shall not be included
    /// in a page dictionary.
    /// </summary>
    TransparencyGroupPdfA1 = 183,

    /// <summary>Halftones in a conforming PDF/A-2 file shall not contain a HalftoneName key.</summary>
    HalftoneName = 184,

    /// <summary>All halftones in a conforming PDF/A-2 file shall have the value 1 or 5 for the HalftoneType key.</summary>
    HalftoneType = 185,

    /// <summary>The TransferFunction key in a halftone dictionary shall be used only as required by ISO 32000-1.</summary>
    HalftoneTransfer = 186,

    /// <summary>
    /// An ICCBased colour space shall not be used where the profile is a CMYK destination profile and is identical to that in the current PDF/A OutputIntent or the current transparency
    /// blending colorspace.
    /// </summary>
    IccCmykOverprintProfile = 187,

    /// <summary>
    /// Overprint mode (as set by the OPM value in an ExtGState dictionary) shall not be one (1) when an ICCBased CMYK colour space is used for stroke and overprinting for stroke is set to
    /// true, or when ICCBased CMYK colour space is used for fill and overprinting for fill is set to true, or both.
    /// </summary>
    IccCmykOverprintMode = 188,

    /// <summary>Any inline image dictionary shall contain a L key whose value is the length of the data as it appears in the stream, before applying the filters, if any.</summary>
    InlineImageLength = 189,

    /// <summary>If an image mask dictionary contains the BitsPerComponent key, its value shall be 1.</summary>
    MaskBitsPerComponent = 190,

    /// <summary>The Metadata object stream dictionary in the document’s catalog shall not contain the Filter key.</summary>
    MetadataFilterPdfA1 = 191,

    /// <summary>Named actions other than NextPage, PrevPage, FirstPage, and LastPage shall not be permitted.</summary>
    NamedAction = 192,

    /// <summary>The AS key shall not appear in any optional content configuration dictionary.</summary>
    OptionalContentAutoState = 193,

    /// <summary>
    /// Each optional content configuration dictionary that forms the value of the D key, or that is an element in the array that forms the value of the Configs key in the OCProperties
    /// dictionary, shall contain the Name key.
    /// </summary>
    OptionalContentNamePdfA4 = 194,

    /// <summary>
    /// Each optional content configuration dictionary that forms the value of the D key, or that is an element in the array that forms the value of the Configs key in the OCProperties
    /// dictionary, shall contain the Name key.
    /// </summary>
    OptionalContentName = 195,

    /// <summary>
    /// If an optional content configuration dictionary contains the Order key, the array which is the value of this Order key shall contain references to all OCGs in the conforming file.
    /// </summary>
    OptionalContentOrder = 196,

    /// <summary>
    /// Each optional content configuration dictionary shall contain the Name key, whose value shall be unique amongst all optional content configuration dictionaries within the PDF/A-2
    /// file.
    /// </summary>
    OptionalContentDuplicateName = 197,

    /// <summary>In addition, the DestOutputProfileRef key, as defined in ISO 15930-7:2010, Annex A, shall not be present in any PDF/X OutputIntent.</summary>
    OutputIntentProfileReferencePdfX = 198,

    /// <summary>The DestOutputProfileRef key, as defined in ISO 32000-2:2020, 14.11.5, Table 401, shall not be present in any output intent dictionary.</summary>
    OutputIntentProfileReference = 199,

    /// <summary>The Page dictionary shall not include an AA entry for an additional-actions dictionary.</summary>
    PageAdditionalActions = 200,

    /// <summary>There shall be no PresSteps entry in any Page dictionary.</summary>
    PagePresentationSteps = 201,

    /// <summary>
    /// If the document does not contain a PDF/A output intent, then all pages that contain transparency shall either have a page-level PDF/A output intent or the page dictionary shall
    /// include the Group key, and the attribute dictionary that forms the value of that Group key shall include a CS entry whose value shall be used as the default blending colour space.
    /// </summary>
    PageTransparencyColorSpacePdfA4 = 202,

    /// <summary>
    /// If the document does not contain a PDF/A OutputIntent, then all Page objects that contain transparency shall include the Group key, and the attribute dictionary that forms the
    /// value of that Group key shall include a CS entry whose value shall be used as the default blending colour space.
    /// </summary>
    PageTransparencyColorSpace = 203,

    /// <summary>No key other than DocMDP shall be present in a permissions dictionary (ISO 32000-2:2020, 12.8.6, Table 263).</summary>
    PermissionsPdfA4 = 204,

    /// <summary>No keys other than UR3 and DocMDP shall be present in a permissions dictionary (ISO 32000-1:2008, 12.8.4, Table 258).</summary>
    Permissions = 205,

    /// <summary>A CMap shall not reference any other CMap except those listed in ISO 32000-1:2008, 9.7.5.2, Table 118.</summary>
    CMapReferencedName = 206,

    /// <summary>
    /// All Separation arrays within a single PDF/A-2 file (including those in Colorants dictionaries) that have the same name shall have the same tintTransform and alternateSpace. In
    /// evaluating equivalence, the PDF objects shall be compared, rather than the computational result of the use of those PDF objects. Compression and whether or not an object is direct
    /// or indirect shall be ignored.
    /// </summary>
    SeparationConsistent = 207,

    /// <summary>
    /// If DocMDP is present, then the Signature References dictionary (ISO 32000-1:2008, 12.8.1, Table 253) shall not contain the keys DigestLocation, DigestMethod, and DigestValue.
    /// </summary>
    SignatureReferenceDigest = 208,

    /// <summary>When computing the digest for the file, it shall be computed over the entire file, including the signature dictionary but excluding the PDF Signature itself.</summary>
    SignatureByteRange = 209,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. Widths - array - (Required except for the standard 14 fonts; indirect
    /// reference preferred) An array of (LastChar − FirstChar + 1) widths.
    /// </summary>
    SimpleFontWidths = 210,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. FirstChar - integer - (Required except for the standard 14 fonts) The
    /// first character code defined in the font's Widths array.
    /// </summary>
    SimpleFontFirstChar = 211,

    /// <summary>
    /// All fonts used in a conforming file shall conform to the font specifications defined in PDF Reference 5.5. LastChar - integer - (Required except for the standard 14 fonts) The last
    /// character code defined in the font's Widths array.
    /// </summary>
    SimpleFontLastChar = 212,

    /// <summary>All symbolic TrueType fonts shall not specify an Encoding entry in the font dictionary.</summary>
    TrueTypeSymbolicEncoding = 213,

    /// <summary>
    /// All non-symbolic TrueType fonts shall have either MacRomanEncoding or WinAnsiEncoding as the value for the Encoding key in the Font dictionary or as the value for the BaseEncoding
    /// key in the dictionary that is the value of the Encoding key in the Font dictionary. In addition, all non-symbolic TrueType fonts shall not define a Differences array unless all of
    /// the glyph names in the Differences array are listed in the Adobe Glyph List and, if the font program is embedded, it shall contain at least the Microsoft Unicode (3,1 – Platform ID
    /// = 3, Encoding ID = 1) encoding in the 'cmap' table.
    /// </summary>
    TrueTypeEncoding = 214,

    /// <summary>
    /// All non-symbolic TrueType fonts shall specify MacRomanEncoding or WinAnsiEncoding, either as the value of the Encoding entry in the font dictionary or as the value of the
    /// BaseEncoding entry in the dictionary that is the value of the Encoding entry in the font dictionary. If the value of the Encoding entry is a dictionary, it shall not contain a
    /// Differences entry.
    /// </summary>
    TrueTypeEncodingPdfA1 = 215,

    /// <summary>
    /// For any given composite (Type 0) font within a conforming file, the CIDSystemInfo entry in its CIDFont dictionary and its Encoding dictionary shall have the following relationship:
    /// - If the Encoding key in the Type 0 font dictionary is Identity-H or Identity-V, any values of Registry, Ordering, and Supplement may be used in the CIDSystemInfo entry of the
    /// CIDFont. - Otherwise, the corresponding Registry and Ordering strings in both CIDSystemInfo dictionaries shall be identical, and the value of the Supplement key in the
    /// CIDSystemInfo dictionary of the CIDFont shall be less than or equal to the Supplement key in the CIDSystemInfo dictionary of the CMap.
    /// </summary>
    Type0CMapOrderingPdfA1 = 216,

    /// <summary>
    /// For any given composite (Type 0) font referenced within a conforming file, the CIDSystemInfo entries of its CIDFont and CMap dictionaries shall be compatible. In other words, the
    /// Registry and Ordering strings of the CIDSystemInfo dictionaries for that font shall be identical, unless the value of the Encoding key in the font dictionary is Identity-H or
    /// Identity-V.
    /// </summary>
    Type0CMapOrdering = 217,

    /// <summary>
    /// If the FontDescriptor dictionary of an embedded Type 1 font contains a CharSet string, then it shall list the character names of all glyphs present in the font program, regardless
    /// of whether a glyph in the font is referenced or used by the PDF or not.
    /// </summary>
    CharSetComplete = 218,

    /// <summary>
    /// For all Type 1 font subsets referenced within a conforming file, the font descriptor dictionary shall include a CharSet string listing the character names defined in the font
    /// subset, as described in PDF Reference Table 5.18.
    /// </summary>
    CharSetCompletePdfA1 = 219,

    /// <summary>Every form field shall have an appearance dictionary associated with the field's data.</summary>
    WidgetAppearance = 220,

    /// <summary>Interactive form fields shall not perform actions of any type.</summary>
    WidgetAction = 221,

    /// <summary>A Widget annotation dictionary shall not contain the A or AA keys.</summary>
    WidgetActions = 222,

    /// <summary>A Widget annotation dictionary shall not include an AA entry for an additional-actions dictionary.</summary>
    WidgetAdditionalActions = 223,

    /// <summary>A form XObject dictionary shall not contain the Subtype2 key with a value of PS or the PS key.</summary>
    FormPostScriptPdfA1 = 224,

    /// <summary>A form XObject dictionary shall not contain any of the following: - the OPI key; - the Subtype2 key with a value of PS; - the PS key.</summary>
    FormPostScript = 225,

    /// <summary>A form XObject dictionary shall not contain an OPI key.</summary>
    FormOpi = 226,

    /// <summary>A conforming file shall not contain any reference XObjects.</summary>
    FormReference = 227,

    /// <summary>If an Image dictionary contains the Interpolate key, its value shall be false.</summary>
    ImageInterpolate = 228,

    /// <summary>An Image dictionary shall not contain the Alternates key.</summary>
    ImageAlternates = 229,

    /// <summary>An Image dictionary shall not contain the OPI key.</summary>
    ImageOpi = 230,

    /// <summary>If an Image dictionary contains the BitsPerComponent key, its value shall be 1, 2, 4 or 8.</summary>
    ImageBitsPerComponent = 231,

    /// <summary>If an Image dictionary contains the BitsPerComponent key, its value shall be 1, 2, 4, 8 or 16.</summary>
    ImageBitsPerComponentPdfA1 = 232,

    /// <summary>A conforming file shall not contain any PostScript XObjects.</summary>
    XObjectPostScript = 233,

    /// <summary>An XObject dictionary (Image or Form) shall not contain the OPI key.</summary>
    XObjectOpiPdfA1 = 234,

    /// <summary>An XObject dictionary shall not contain the SMask key.</summary>
    XObjectSoftMaskPdfA1 = 235,

    /// <summary>
    /// The PDF Signature (a DER-encoded PKCS#7 binary data object) shall be placed into the Contents entry of the signature dictionary. The PKCS#7 object shall conform to the PKCS#7
    /// specification in RFC 2315. At minimum, there shall only be a single signer (e.g. a single "SignerInfo" structure) in the PDF Signature.
    /// </summary>
    SignatureSignerCount = 236,

    /// <summary>
    /// The PDF Signature (a DER-encoded PKCS#7 binary data object) shall be placed into the Contents entry of the signature dictionary. The PKCS#7 object shall conform to the PKCS#7
    /// specification in RFC 2315. At minimum, it shall include the signer's X.509 signing certificate.
    /// </summary>
    SignatureCertificate = 237,

    /// <summary>A circular mapping shall not exist.</summary>
    StructureCircularMapping = 238,

    /// <summary>
    /// All non-standard structure types shall be mapped to the nearest functionally equivalent standard type, as defined in PDF Reference 9.7.4, in the role map dictionary of the
    /// structure tree root.
    /// </summary>
    StructureStandardType = 239,

    /// <summary>Standard tags shall not be remapped to a non-standard type.</summary>
    StructureRemappedStandardType = 240,

    /// <summary>
    /// The 'cmap' subtable in the embedded font program for a symbolic TrueType font shall contain either the Microsoft Symbol (3,0 – Platform ID=3, Encoding ID=0) or the Mac Roman (1,0 –
    /// Platform ID=1, Encoding ID=0) encoding.
    /// </summary>
    TrueTypeSymbolicCmapPdfA4 = 241,

    /// <summary>Font programs' "cmap" tables for all symbolic TrueType fonts shall contain exactly one encoding.</summary>
    TrueTypeSymbolicCmapPdfA1 = 242,

    /// <summary>
    /// The 'cmap' table in the embedded font program for a symbolic TrueType font shall contain either exactly one encoding or it shall contain, at least, the Microsoft Symbol (3,0 -
    /// Platform ID=3, Encoding ID=0) encoding.
    /// </summary>
    TrueTypeSymbolicCmap = 243,

    /// <summary>
    /// For all non-symbolic TrueType fonts used for rendering, the embedded TrueType font program shall contain one or several non-symbolic cmap entries such that all necessary glyph
    /// lookups can be carried out.
    /// </summary>
    TrueTypeCmap = 244,

    /// <summary>
    /// For all non-symbolic TrueType fonts used for rendering, the embedded TrueType font program shall contain at least Microsoft Unicode (3,1 – Platform ID=3, Encoding ID=1), or
    /// Macintosh Roman (1,0 – Platform ID=1, Encoding ID=0) 'cmap' subtable that all necessary glyph lookups are able to be carried out.
    /// </summary>
    TrueTypeCmapPdfA4 = 245,

    /// <summary>All metadata streams present in the PDF shall conform to the XMP Specification. The XMP package must be encoded as UTF-8.</summary>
    XmpEncodingUtf8 = 246,

    /// <summary>The bytes attribute shall not be used in the header of an XMP packet.</summary>
    XmpPacketBytes = 247,

    /// <summary>The encoding attribute shall not be used in the header of an XMP packet.</summary>
    XmpPacketEncoding = 248,

    /// <summary>The metadata stream shall conform to XMP Specification and well formed PDFAExtension Schema for all extensions.</summary>
    XmpSerialization = 249,

    /// <summary>Properties specified in XMP form shall use either the predefined schemas defined in XMP Specification, or extension schemas that comply with XMP Specification.</summary>
    XmpPropertyDefinedPdfA1 = 250,

    /// <summary>
    /// All properties specified in XMP form shall use either the predefined schemas defined in the XMP Specification, ISO 19005-1 or this part of ISO 19005, or any extension schemas that
    /// comply with 6.6.2.3.2.
    /// </summary>
    XmpPropertyDefined = 251,

    /// <summary>Properties specified in XMP form shall use either the predefined schemas defined in XMP Specification, or extension schemas that comply with XMP Specification.</summary>
    XmpPropertyValueType = 252,
}
