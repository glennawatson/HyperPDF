// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks annotation classification without decoded name allocations.</summary>
public sealed class AccessibilityPageScanTests
{
    /// <summary>The hidden annotation flag.</summary>
    private const int HiddenFlag = 1 << 1;

    /// <summary>The no-view annotation flag.</summary>
    private const int NoViewFlag = 1 << 5;

    /// <summary>Annotation kinds and visibility flags are read from interned names without allocating.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <param name="flags">The annotation flags.</param>
    /// <param name="expected">The expected annotation kind.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(KnownName.Link, 0, (int)AnnotationKind.Link)]
    [Arguments(KnownName.Widget, 0, (int)AnnotationKind.Widget)]
    [Arguments(KnownName.Popup, 0, (int)AnnotationKind.Skipped)]
    [Arguments(KnownName.PrinterMark, 0, (int)AnnotationKind.Skipped)]
    [Arguments(KnownName.TrapNet, 0, (int)AnnotationKind.Skipped)]
    [Arguments(KnownName.Text, 0, (int)AnnotationKind.Other)]
    [Arguments(KnownName.None, 0, (int)AnnotationKind.Other)]
    [Arguments(KnownName.Link, HiddenFlag, (int)AnnotationKind.Skipped)]
    [Arguments(KnownName.Widget, NoViewFlag, (int)AnnotationKind.Skipped)]
    public async Task Classify_SubtypeAndFlags_DoesNotAllocate(KnownName subtype, int flags, int expected)
    {
        var annotation = new PdfDictionary(null);
        annotation.Set(KnownName.Subtype, PdfValue.FromName(subtype));
        annotation.Set(KnownName.F, PdfValue.FromInteger(flags));
        _ = AccessibilityPageScan.Classify(annotation);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var actual = AccessibilityPageScan.Classify(annotation);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That((int)actual).IsEqualTo(expected);
        await Assert.That(allocated).IsEqualTo(0);
    }
}
