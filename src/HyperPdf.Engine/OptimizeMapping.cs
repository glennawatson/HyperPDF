// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Optimizing;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Engine;

/// <summary>Converts between the app's optimisation records and the library's.</summary>
internal static class OptimizeMapping
{
    /// <summary>Maps the app's settings to the library's options.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The options.</returns>
    internal static PdfOptimizeOptions ToOptions(OptimizeSettings settings)
    {
        var language = string.IsNullOrWhiteSpace(settings.Language) ? null : settings.Language.Trim();
        return Preset(settings.Preset) with
        {
            FixAccessibility = settings.FixAccessibility,
            Language = language,
            AddInferredTags = settings.AddInferredTags,
            Cleanup = settings.CleanUp ? PdfCleanupItems.All : PdfCleanupItems.None,
        };
    }

    /// <summary>Wraps a progress receiver so the library's reports reach it as the app's.</summary>
    /// <param name="progress">The receiver, or <see langword="null"/>.</param>
    /// <returns>The library receiver, or <see langword="null"/>.</returns>
    internal static IProgress<PdfOptimizeProgress>? ToProgress(IProgress<OptimizeProgress>? progress) =>
        progress is null ? null : new ProgressRelay(progress);

    /// <summary>Maps the library's report to the app's.</summary>
    /// <param name="report">The library report.</param>
    /// <returns>The app report.</returns>
    internal static OptimizeReport ToReport(in PdfOptimizeReport report)
    {
        var skipped = new OptimizeSkip[report.Skipped.Count];
        for (var i = 0; i < skipped.Length; i++)
        {
            skipped[i] = new((OptimizeArea)report.Skipped[i].Category, report.Skipped[i].Reason);
        }

        return new(report.BytesBefore, report.BytesAfter, (OptimizeWriteMode)report.Mode, [.. report.Warnings], skipped)
        {
            TagsInferred = report.TagsInferred,
            FiguresNeedingAltText = report.FiguresNeedingAltText,
            WasSigned = report.WasSigned,
            IsEncrypted = report.IsEncrypted,
            PdfAPart = report.PdfAPart,
            ClaimRemoved = ArchiveMapping.ToRemoval(report.PdfAClaim),
            ClaimRepaired = ArchiveMapping.ToRepair(report.PdfAClaim),
        };
    }

    /// <summary>Gets the library preset for a choice.</summary>
    /// <param name="preset">The choice.</param>
    /// <returns>The options.</returns>
    private static PdfOptimizeOptions Preset(OptimizePreset preset) => preset switch
    {
        OptimizePreset.Smaller => PdfOptimizeOptions.Smaller,
        OptimizePreset.KeepQuality => PdfOptimizeOptions.KeepQuality,
        _ => PdfOptimizeOptions.Balanced,
    };

    /// <summary>Forwards the library's progress reports as the app's.</summary>
    /// <param name="target">The receiver.</param>
    private sealed class ProgressRelay(IProgress<OptimizeProgress> target) : IProgress<PdfOptimizeProgress>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Report(PdfOptimizeProgress value) => target.Report(new((OptimizeStep)value.Phase, value.Completed, value.Total));
    }
}
