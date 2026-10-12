// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on file specifications and embedded files: none in PDF/A-1 (ISO 19005-1, 6.1.11), /F, /UF and
/// /AFRelationship with each embedded file, a MIME type, association through /AF in PDF/A-3 (ISO 19005-3, 6.8), and
/// embedded PDF files that are themselves PDF/A in PDF/A-2 and PDF/A-4 (ISO 19005-2, 6.8; ISO 19005-4, 6.9). The file
/// specifications checked are every dictionary typed /Filespec and every value of the /EmbeddedFiles name tree.
/// </summary>
internal static class PdfAEmbeddedFileRules
{
    /// <summary>The context of failures in file specifications.</summary>
    private const string Where = "file specification";

    /// <summary>The PDF/A part whose embedded files must be PDF/A-1 or PDF/A-2.</summary>
    private const int Part2 = 2;

    /// <summary>The PDF/A part whose embedded files must be PDF/A-1, PDF/A-2 or PDF/A-4.</summary>
    private const int Part4 = 4;

    /// <summary>
    /// The deepest embedded PDF that is validated; one nested more deeply is not proven valid, so it fails the embedded
    /// file rules. This is HyperPDF's own resource limit, not a PDF/A rule: ISO 19005-2 §6.8 and ISO 19005-4 §6.9 set no
    /// nesting limit, and veraPDF sets none either. Each level is a full validation one call deeper, so a crafted file of
    /// PDFs nested thousands deep would otherwise exhaust the stack, which ends the process. Real archives rarely nest
    /// more than one or two levels.
    /// </summary>
    private const int MaxNesting = 32;

    /// <summary>The keys of an /EF dictionary that hold embedded file streams.</summary>
    private static readonly KnownName[] FileKeys = [KnownName.F, KnownName.UF];

    /// <summary>Records the rules the file specifications break.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var specifications = PdfAFileSpecifications.Collect(context);
        if (specifications.Count == 0)
        {
            return;
        }

        var associated = PdfAAssociatedFiles.Collect(context);
        foreach (var (specification, number) in specifications)
        {
            CheckSpecification(context, specification, number >= 0 && associated.Contains(number));
        }
    }

    /// <summary>Checks one file specification and its embedded files.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="specification">The file specification.</param>
    /// <param name="associated">Whether an /AF array associates it.</param>
    private static void CheckSpecification(PdfAValidationContext context, PdfDictionary specification, bool associated)
    {
        var relationship = specification.Get(context.Document.Objects.Names.Intern("AFRelationship"u8)).Kind == PdfKind.Name;
        var embedded = !specification.GetRaw(KnownName.EF).IsNull;
        var named = specification.Get(KnownName.F).Kind == PdfKind.String && specification.Get(KnownName.UF).Kind == PdfKind.String;
        context.Require(relationship, PdfARule.FileSpecificationRelationshipRequired, Where);
        context.Require(!embedded, PdfARule.FileSpecificationEmbeddedFileForbidden, Where);
        context.Require(!embedded || named, PdfARule.FileSpecificationNames, Where);
        context.Require(!embedded || relationship, PdfARule.FileSpecificationRelationship, Where);
        context.Require(associated, PdfARule.FileSpecificationAssociated, Where);
        if (specification.Get(KnownName.EF).AsDictionary() is { } files)
        {
            CheckEmbeddedFiles(context, files);
        }
    }

    /// <summary>Checks the embedded file streams of an /EF dictionary.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="files">The /EF dictionary.</param>
    private static void CheckEmbeddedFiles(PdfAValidationContext context, PdfDictionary files)
    {
        var names = context.Document.Objects.Names;
        Span<PdfName> keys = [FileKeys[0], FileKeys[1], names.Intern("DOS"u8), names.Intern("Mac"u8), names.Intern("Unix"u8)];
        foreach (var key in keys)
        {
            if (files.Get(key).AsStream() is not { } file)
            {
                continue;
            }

            var subtype = file.Dictionary.Get(KnownName.Subtype);
            context.Require(subtype.TryGetName(out var mime) && IsMimeType(names.GetSpelling(mime)), PdfARule.EmbeddedFileMimeType, Where);
            CheckEmbeddedPdf(context, file);
        }
    }

    /// <summary>Checks that an embedded file is a valid PDF/A file of an allowed level, when the level being checked asks.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="file">The embedded file stream.</param>
    private static void CheckEmbeddedPdf(PdfAValidationContext context, PdfStream file)
    {
        // Validating an embedded file is costly, so only the parts whose rules ask for it pay for it.
        if (context.Part is not (Part2 or Part4))
        {
            return;
        }

        var level = EmbeddedLevel(context, file);
        context.Require(level is PdfALevel.PdfA1B or PdfALevel.PdfA2B, PdfARule.EmbeddedFilePdfA12, Where);
        context.Require(level is PdfALevel.PdfA1B or PdfALevel.PdfA2B or PdfALevel.PdfA4, PdfARule.EmbeddedFilePdfA124, Where);
    }

    /// <summary>Gets the level an embedded file claims and passes, as veraPDF reads it.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="file">The embedded file stream.</param>
    /// <returns>The level, or <see langword="null"/> when the file is not a PDF, claims another level or fails it.</returns>
    private static PdfALevel? EmbeddedLevel(PdfAValidationContext context, PdfStream file)
    {
        if (context.Depth >= MaxNesting)
        {
            return null;
        }

        var bytes = file.DecodeToArray(context.CancellationToken);

        // Most attachments are not PDFs; checking for the header the reader looks for avoids a failed open for each.
        if (bytes.AsSpan(0, Math.Min(bytes.Length, StoreOpening.HeaderSearchLength)).IndexOf("%PDF-"u8) < 0)
        {
            return null;
        }

        try
        {
            using var document = PdfDocumentReader.Open(bytes, null);
            if (PdfAValidator.ClaimedLevel(document) is not { } level || level is not (PdfALevel.PdfA1B or PdfALevel.PdfA2B or PdfALevel.PdfA4))
            {
                return null;
            }

            return PdfAValidator.Validate(document, level, context.Depth + 1, context.CancellationToken).Passed ? level : null;
        }
        catch (PdfException)
        {
            return null;
        }
    }

    /// <summary>Determines whether a name is a MIME type: word characters, hyphens, plus signs and points around one slash.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsMimeType(ReadOnlySpan<byte> name)
    {
        var slash = name.IndexOf((byte)'/');
        return slash > 0 && slash < name.Length - 1 && IsMimePart(name[..slash]) && IsMimePart(name[(slash + 1)..]);
    }

    /// <summary>Determines whether a MIME type part holds only word characters, hyphens, plus signs and points.</summary>
    /// <param name="part">The part.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsMimePart(ReadOnlySpan<byte> part)
    {
        foreach (var c in part)
        {
            if (!char.IsAsciiLetterOrDigit((char)c) && c is not ((byte)'_' or (byte)'-' or (byte)'+' or (byte)'.'))
            {
                return false;
            }
        }

        return true;
    }
}
