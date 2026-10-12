// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on the file's structure and syntax: the header, trailer and cross-reference sections, the bytes of every
/// object and stream, and the implementation limits (ISO 19005-1, 6.1; ISO 19005-2, 6.1; ISO 19005-4, 6.1).
/// </summary>
internal static class PdfAFileStructureRules
{
    /// <summary>The context of failures in the file as a whole.</summary>
    internal const string FileContext = "file";

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.HeaderBinaryComment,
        PdfARule.HeaderPdfA1,
        PdfARule.HeaderPdf17,
        PdfARule.HeaderPdf20,
        PdfARule.DataAfterEndOfFile,
        PdfARule.XrefLineEnds,
        PdfARule.XrefSubsectionHeader,
        PdfARule.XrefStreamForbidden,
        PdfARule.FileIdentifierPdfA1,
        PdfARule.FileIdentifier,
        PdfARule.LinearizedFirstIdMatchesLast,
        PdfARule.IndirectObjectCount,
        PdfARule.IndirectObjectSpacing,
        PdfARule.IntegerRange,
        PdfARule.RealRange,
        PdfARule.RealRangePdfA1,
        PdfARule.RealMagnitude,
        PdfARule.NameLength,
        PdfARule.StringLength,
        PdfARule.StringLengthPdfA1,
        PdfARule.HexStringCharacters,
        PdfARule.HexStringEvenDigits,
        PdfARule.ArraySizePdfA1,
        PdfARule.DictionarySizePdfA1,
        PdfARule.StreamExternalFile,
        PdfARule.StreamLength,
        PdfARule.StreamKeywordLineEnds,
        PdfARule.FilterLzwForbidden,
        PdfARule.FilterAllowed,
        PdfARule.InlineFilterLzwForbidden,
        PdfARule.InlineFilterAllowed,
        PdfARule.MarkInfoMarked,
        PdfARule.StructureTreeRoot,
        PdfARule.OptionalContentForbidden,
        PdfARule.EmbeddedFilesForbidden,
        PdfARule.EmbeddedFilesRequired,
        PdfARule.AlternatePresentations,
        PdfARule.LanguageTagPdfA1,
        PdfARule.LanguageTagPdfA2,
        PdfARule.FileSpecificationRelationshipRequired,
        PdfARule.FileSpecificationEmbeddedFileForbidden,
        PdfARule.FileSpecificationNames,
        PdfARule.FileSpecificationRelationship,
        PdfARule.FileSpecificationAssociated,
        PdfARule.EmbeddedFileMimeType,
        PdfARule.EmbeddedFilePdfA12,
        PdfARule.EmbeddedFilePdfA124,
        PdfARule.Permissions,
        PdfARule.PermissionsPdfA4,
        PdfARule.SignatureReferenceDigest,
        PdfARule.SignatureByteRange,
        PdfARule.SignatureSignerCount,
        PdfARule.SignatureCertificate,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        using var scan = new PdfAFileScan(context);
        PdfAFileHeader.Check(scan);
        var layout = PdfAXrefSections.Read(scan);
        PdfAXrefSections.CheckMainTrailer(scan, layout);
        PdfAFileIdentifiers.Check(scan, layout);
        PdfAFileObjects.Check(scan);
        PdfASyntaxStreams.Check(scan);
        PdfACatalogStructureRules.Check(context);
        PdfAEmbeddedFileRules.Check(context);
        PdfASignatureRules.Check(scan, layout);
    }
}
