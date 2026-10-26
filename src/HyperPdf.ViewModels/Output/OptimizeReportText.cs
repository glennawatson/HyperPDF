// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Optimizing;

namespace HyperPdf.ViewModels.Output;

/// <summary>Describes the outcome of an optimised copy in plain words.</summary>
internal static class OptimizeReportText
{
    /// <summary>The share of a whole expressed as a percentage.</summary>
    private const double Percent = 100;

    /// <summary>The last step that has a share of the bar; the finished step has none.</summary>
    private const int LastStep = 4;

    /// <summary>The weights of the steps: where each step starts on the overall bar, with the end of the bar last.</summary>
    private static readonly double[] StepStarts = [0x00, 0x05, 0x23, 0x2D, 0x37, 0x64];

    /// <summary>Gets what a step is doing, in words.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The words.</returns>
    internal static string DescribeStep(OptimizeStep step) => step switch
    {
        OptimizeStep.Checking => UiText.Get("Checking the file"),
        OptimizeStep.Analysing => UiText.Get("Reading the pages"),
        OptimizeStep.Editing => UiText.Get("Updating the pages"),
        OptimizeStep.Deduplicating => UiText.Get("Looking for repeated content"),
        OptimizeStep.Writing => UiText.Get("Writing the new file"),
        _ => UiText.Get("Finished"),
    };

    /// <summary>Retains the step as a resource key for a running operation's status.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The localized step message.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static LocalizedMessage StepMessage(OptimizeStep step) => LocalizedMessage.Get(UiText, step switch
    {
        OptimizeStep.Checking => "Checking the file",
        OptimizeStep.Analysing => "Reading the pages",
        OptimizeStep.Editing => "Updating the pages",
        OptimizeStep.Deduplicating => "Looking for repeated content",
        OptimizeStep.Writing => "Writing the new file",
        _ => "Finished",
    });

    /// <summary>Gets how far along the whole run is.</summary>
    /// <param name="progress">The step and its progress.</param>
    /// <returns>A percentage from 0 to 100.</returns>
    internal static double OverallPercent(OptimizeProgress progress)
    {
        var step = Math.Clamp((int)progress.Step, 0, LastStep);
        var start = StepStarts[step];
        var width = StepStarts[step + 1] - start;
        return progress.Step == OptimizeStep.Done ? Percent : start + (width * progress.Fraction);
    }

    /// <summary>Describes a name for a kind of change.</summary>
    /// <param name="area">The kind of change.</param>
    /// <returns>The words.</returns>
    internal static string DescribeArea(OptimizeArea area) => area switch
    {
        OptimizeArea.Layout => UiText.Get("File layout"),
        OptimizeArea.Images => UiText.Get("Pictures"),
        OptimizeArea.Streams => UiText.Get("Compression"),
        OptimizeArea.Duplicates => UiText.Get("Repeated content"),
        OptimizeArea.UnusedObjects => UiText.Get("Unused content"),
        OptimizeArea.Fonts => UiText.Get("Fonts"),
        OptimizeArea.Cleanup => UiText.Get("Clean-up"),
        OptimizeArea.Accessibility => UiText.Get("Accessibility"),
        OptimizeArea.TextLayer => UiText.Get("Text layer"),
        _ => UiText.Get("Safety"),
    };

    /// <summary>Describes the size change in one sentence.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The sentence.</returns>
    internal static string Headline(OptimizeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var before = AttachmentsViewModel.FormatSize(report.BytesBefore);
        var after = AttachmentsViewModel.FormatSize(report.BytesAfter);
        if (report.Mode == OptimizeWriteMode.Copied)
        {
            return UiText.Format("Nothing could be changed safely, so the new file is a copy of the original ({0}).", before);
        }

        if (report.BytesSaved > 0)
        {
            var share = (report.FractionSaved * Percent).ToString("0", CultureInfo.CurrentCulture);
            return UiText.Format("Saved {0}%. The file went from {1} to {2}.", share, before, after);
        }

        return report.BytesSaved == 0
            ? UiText.Format(
            "The file stayed the same size ({0}).",
            before)
            : UiText.Format("The new file is {0}, a little larger than the original {1}. Added accessibility entries or text layers can do this.", after, before);
    }

    /// <summary>Describes everything the reader should know about a finished run, one point per line.</summary>
    /// <param name="report">The report.</param>
    /// <param name="path">Where the copy was saved.</param>
    /// <returns>The lines.</returns>
    internal static string[] Lines(OptimizeReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(report);
        var lines = new List<string> { UiText.Format("Saved as {0}.", Path.GetFileName(path)), Headline(report) };
        AddNotes(lines, report);
        foreach (var skip in report.Skipped)
        {
            lines.Add(UiText.Format("Left alone, {0}: {1}", DescribeArea(skip.Area).ToLowerInvariant(), skip.Reason));
        }

        foreach (var warning in report.Warnings)
        {
            lines.Add(UiText.Format("Note: {0}", warning));
        }

        return [.. lines];
    }

    /// <summary>Joins lines into one block of text.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The text.</returns>
    internal static string Join(string[] lines)
    {
        var text = new StringBuilder();
        foreach (var line in lines)
        {
            _ = text.Append(line).Append('\n');
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>Adds the notes about signatures, encryption, conformance and tags.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="report">The report.</param>
    private static void AddNotes(List<string> lines, OptimizeReport report)
    {
        if (report.WasSigned)
        {
            lines.Add(UiText.Get("This document is signed. Its signatures stay valid because the original bytes were kept, so pictures and fonts were not shrunk."));
        }

        if (report.IsEncrypted)
        {
            lines.Add(UiText.Get("The copy is encrypted in the same way as the original."));
        }

        if (report.ClaimRemoved is { } removal)
        {
            lines.Add(ArchiveText.ClaimRemovedNotice(removal));
        }
        else if (report.ClaimRepaired is { } repair)
        {
            lines.Add(ArchiveText.ClaimRepairedNotice(repair));
        }
        else if (report.PdfAPart > 0)
        {
            lines.Add(UiText.Format("The document claims PDF/A part {0}, so only changes that keep it valid were made.", report.PdfAPart));
        }

        if (report.TagsInferred)
        {
            lines.Add(UiText.Get("A reading structure was guessed from the page layout. Check it with a screen reader before you rely on it."));
        }

        if (report.FiguresNeedingAltText <= 0)
        {
            return;
        }

        var figures = report.FiguresNeedingAltText == 1 ? UiText.Get("1 picture needs") : UiText.Format("{0} pictures need", report.FiguresNeedingAltText);
        lines.Add(UiText.Format("{0} alternative text (alt text) so a screen reader can describe it. Add it in an accessibility editor.", figures));
    }
}
