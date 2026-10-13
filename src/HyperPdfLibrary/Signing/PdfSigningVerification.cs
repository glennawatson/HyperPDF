// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Reads a finished file back and checks the signature just written: its byte range, digest and signature value, and the PDF/A claim.</summary>
internal static class PdfSigningVerification
{
    /// <summary>The most failed PDF/A rules named in a message.</summary>
    private const int MaxRulesNamed = 3;

    /// <summary>How the signature just written is checked: the usual options, which the check only reads, shared by every read-back.</summary>
    private static readonly PdfSignatureValidationOptions ReadBackOptions = PdfSignatureValidationOptions.Default;

    /// <summary>Checks a finished file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="notes">Receives what the user should know; created when there is a first note.</param>
    /// <exception cref="PdfException">The signature that was written does not check out.</exception>
    /// <exception cref="PdfSigningRefusedException">The signature broke the file's PDF/A claim.</exception>
    internal static void Check(PdfSigningFile file, PdfSigningPlan plan, ref List<string>? notes)
    {
        if (!TryOpen(file.File, ref notes, out var written))
        {
            return;
        }

        using (written)
        {
            CheckSignature(written, plan);
            CheckClaim(written, plan, string.Empty, file.File, file.UpdateStart, ref notes);
        }
    }

    /// <summary>Checks that an update that holds no signature left the file's PDF/A claim intact.</summary>
    /// <param name="file">The whole file.</param>
    /// <param name="updateStart">Where the update starts.</param>
    /// <param name="what">What the update adds, for a message.</param>
    /// <param name="notes">Receives what the user should know; created when there is a first note.</param>
    /// <exception cref="PdfSigningRefusedException">The update broke the claim.</exception>
    internal static void CheckUpdate(byte[] file, int updateStart, string what, ref List<string>? notes)
    {
        if (!TryOpen(file, ref notes, out var written))
        {
            return;
        }

        using (written)
        {
            CheckClaim(written, null, what, file, updateStart, ref notes);
        }
    }

    /// <summary>Opens the finished file, unless it is encrypted.</summary>
    /// <param name="file">The file.</param>
    /// <param name="notes">Receives what the user should know.</param>
    /// <param name="written">Receives the document.</param>
    /// <returns><see langword="true"/> when the file was opened.</returns>
    private static bool TryOpen(byte[] file, ref List<string>? notes, out PdfDocument written)
    {
        try
        {
            written = PdfDocumentReader.Open(file, null);
            return true;
        }
        catch (PdfException exception) when (exception.Error is PdfError.Password or PdfError.Certificate)
        {
            (notes ??= []).Add("The file is encrypted, so it was not read back to check what was added.");
            written = null!;
            return false;
        }
    }

    /// <summary>Checks the new signature's byte range, digest and signature value.</summary>
    /// <param name="written">The finished file.</param>
    /// <param name="plan">The plan.</param>
    /// <exception cref="PdfException">The signature does not check out.</exception>
    private static void CheckSignature(PdfDocument written, PdfSigningPlan plan)
    {
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(written, ReadBackOptions);
        PdfSignatureValidationReport? found = null;
        foreach (var report in reports)
        {
            if (string.Equals(report.Signature.FieldName, plan.FieldName, StringComparison.Ordinal))
            {
                found = report;
            }
        }

        if (found is null || !found.ByteRange.IsValid || !found.DigestValid || !found.SignatureValid)
        {
            throw new PdfException(PdfError.Format, "The signature that was just written did not check out when the file was read back, so the file was not signed.");
        }
    }

    /// <summary>Names the first few failed rules for a message.</summary>
    /// <param name="failed">The failed rules.</param>
    /// <returns>The rules, separated by semicolons.</returns>
    private static string NameRules(PdfARuleResult[] failed)
    {
        var parts = new List<string>(MaxRulesNamed);
        for (var i = 0; i < failed.Length && i < MaxRulesNamed; i++)
        {
            parts.Add($"{failed[i].Clause}: {failed[i].Description}");
        }

        return string.Join("; ", parts);
    }

    /// <summary>Checks that an update left the file's PDF/A claim intact.</summary>
    /// <param name="written">The finished file.</param>
    /// <param name="plan">The plan of the signature the update adds, or <see langword="null"/> for an update that holds none.</param>
    /// <param name="what">What an update without a signature adds, for a message.</param>
    /// <param name="file">The whole file.</param>
    /// <param name="updateStart">Where the update starts.</param>
    /// <param name="notes">Receives what the user should know.</param>
    /// <exception cref="PdfSigningRefusedException">The update broke the claim.</exception>
    private static void CheckClaim(PdfDocument written, PdfSigningPlan? plan, string what, byte[] file, int updateStart, ref List<string>? notes)
    {
        if (PdfAValidator.ClaimedLevel(written) is not { } level)
        {
            return;
        }

        var report = PdfAValidator.Validate(written, level);
        if (report.Passed)
        {
            return;
        }

        using var original = PdfDocumentReader.Open(file.AsSpan(0, updateStart).ToArray(), null);
        var before = PdfAValidator.Validate(original, level);
        var named = NameRules(report.Failed);
        if (before.Passed)
        {
            var action = plan is null ? $"Adding {what}" : $"Signing \"{plan.FieldName}\"";
            throw new PdfSigningRefusedException($"{action} would break the file's {level} claim ({named}), so the file was not changed.");
        }

        (notes ??= []).Add($"The file already did not meet the {level} level it claims, and still does not ({named}).");
    }
}
