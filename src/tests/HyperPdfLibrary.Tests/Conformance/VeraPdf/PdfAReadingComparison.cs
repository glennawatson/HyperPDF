// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Compares HyperPdf's reading of a file with veraPDF's: the PDF/A claim against the profile veraPDF picked from it, the
/// page media boxes, the document information entries and the output intents.
/// </summary>
internal static class PdfAReadingComparison
{
    /// <summary>The largest difference allowed between two box coordinates, in points.</summary>
    private const double BoxTolerance = 0.01;

    /// <summary>The text that starts a PDF/A profile name.</summary>
    private const string ProfilePrefix = "PDF/A-";

    /// <summary>Lists where HyperPdf reads a file differently from veraPDF.</summary>
    /// <param name="path">The file.</param>
    /// <param name="job">veraPDF's result for the file, validated with the detected flavour.</param>
    /// <returns>The differences; empty when the readings agree.</returns>
    internal static List<string> Compare(string path, VeraPdfJob job)
    {
        using var document = PdfDocumentReader.Open(path, null);
        var report = PdfDocumentConformance.GetConformance(document);
        var differences = new List<string>();
        var expected = ClaimFromProfile(job.ProfileName);
        if (!string.Equals(report.PdfA?.Label, expected, StringComparison.Ordinal))
        {
            differences.Add($"claim {report.PdfA?.Label ?? "none"}, veraPDF profile {job.ProfileName ?? "none"}");
        }

        ComparePages(document, job.Pages, differences);
        CompareInformation(PdfDocumentMetadata.GetInfo(document), job.Information, differences);
        var intents = report.OutputIntents.Select(static intent => intent.OutputConditionIdentifier ?? string.Empty);
        if (!intents.SequenceEqual(job.OutputIntents, StringComparer.Ordinal))
        {
            differences.Add($"output intents [{string.Join(", ", intents)}], veraPDF [{string.Join(", ", job.OutputIntents)}]");
        }

        return differences;
    }

    /// <summary>Turns a profile name such as "PDF/A-2b validation profile" into a claim label such as 2B.</summary>
    /// <param name="profile">The profile name.</param>
    /// <returns>The label, or <see langword="null"/> for a profile that is not PDF/A.</returns>
    internal static string? ClaimFromProfile(string? profile)
    {
        if (profile is null || !profile.StartsWith(ProfilePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var end = profile.IndexOf(' ', StringComparison.Ordinal);
        return profile[ProfilePrefix.Length..(end < 0 ? profile.Length : end)].ToUpperInvariant();
    }

    /// <summary>Compares the page count and each media box.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">veraPDF's media boxes.</param>
    /// <param name="differences">Receives the differences.</param>
    private static void ComparePages(PdfDocument document, List<VeraPdfPage> pages, List<string> differences)
    {
        if (document.PageCount != pages.Count)
        {
            differences.Add($"{document.PageCount} pages, veraPDF {pages.Count}");
            return;
        }

        for (var i = 0; i < pages.Count; i++)
        {
            var box = PdfDocumentPages.GetPage(document, i).MediaBox;
            var expected = pages[i];
            if (Math.Abs(box.Left - expected.Left) > BoxTolerance || Math.Abs(box.Bottom - expected.Bottom) > BoxTolerance
                || Math.Abs(box.Right - expected.Right) > BoxTolerance || Math.Abs(box.Top - expected.Top) > BoxTolerance)
            {
                differences.Add($"page {i + 1} media box {box}, veraPDF {expected}");
            }
        }
    }

    /// <summary>Compares the document information entries veraPDF read with HyperPdf's.</summary>
    /// <param name="info">HyperPdf's document information.</param>
    /// <param name="entries">veraPDF's entries.</param>
    /// <param name="differences">Receives the differences.</param>
    private static void CompareInformation(PdfDocumentInfo info, Dictionary<string, string> entries, List<string> differences)
    {
        foreach (var (key, value) in entries)
        {
            var same = key switch
            {
                "Title" => IsSameText(info.Title, value),
                "Author" => IsSameText(info.Author, value),
                "Subject" => IsSameText(info.Subject, value),
                "Keywords" => IsSameText(info.Keywords, value),
                "Creator" => IsSameText(info.Creator, value),
                "Producer" => IsSameText(info.Producer, value),
                "CreationDate" => IsSameDate(info.Created, value),
                "ModDate" => IsSameDate(info.Modified, value),
                _ => true,
            };
            if (!same)
            {
                differences.Add($"/Info /{key} differs from veraPDF's '{value}'");
            }
        }
    }

    /// <summary>Compares text, treating blank as missing, as HyperPdf reports it.</summary>
    /// <param name="actual">HyperPdf's text.</param>
    /// <param name="expected">veraPDF's text.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool IsSameText(string? actual, string expected) =>
        string.IsNullOrWhiteSpace(expected) ? actual is null : string.Equals(actual?.Trim(), expected.Trim(), StringComparison.Ordinal);

    /// <summary>Compares a date with veraPDF's ISO 8601 text.</summary>
    /// <param name="actual">HyperPdf's date.</param>
    /// <param name="expected">veraPDF's text.</param>
    /// <returns><see langword="true"/> when they name the same instant.</returns>
    private static bool IsSameDate(DateTimeOffset? actual, string expected) =>
        DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) && actual == date;
}
