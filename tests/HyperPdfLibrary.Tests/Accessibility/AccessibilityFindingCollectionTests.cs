// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks capped findings, exclusive leases and detached results.</summary>
public sealed class AccessibilityFindingCollectionTests
{
    /// <summary>The page index used by the test finding.</summary>
    private const int PageIndex = 3;

    /// <summary>The test structure element's object number.</summary>
    private const int ElementNumber = 7;

    /// <summary>Claim copies retain the original values and computed claim status.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Claim_CopyAndDeconstruction_PreservesValues()
    {
        var original = new PdfUaClaim(PdfUaClaim.Part1, null, "1.7", false);
        var copy = original with { Part = null };
        var (part, revision, version, namespaces) = original;
        await Assert.That(original.IsClaimed).IsTrue();
        await Assert.That(copy.IsClaimed).IsFalse();
        await Assert.That(original.Part).IsEqualTo(PdfUaClaim.Part1);
        await Assert.That(part).IsEqualTo(PdfUaClaim.Part1);
        await Assert.That(revision).IsNull();
        await Assert.That(version).IsEqualTo("1.7");
        await Assert.That(namespaces).IsFalse();
    }

    /// <summary>Page values copy and deconstruct without changing the original snapshot.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Page_CopyAndDeconstruction_PreservesValues()
    {
        var original = new PdfAccessibilityPage(PageIndex, 1, true, 0, 1, 0);
        var copy = original with { AnnotationCount = 0 };
        var (pageIndex, annotations, tabs, untagged, artifacts, graphics) = original;
        await Assert.That(copy).IsNotEqualTo(original);
        await Assert.That(original.AnnotationCount).IsEqualTo(1);
        await Assert.That(pageIndex).IsEqualTo(PageIndex);
        await Assert.That(annotations).IsEqualTo(1);
        await Assert.That(tabs).IsTrue();
        await Assert.That(untagged).IsEqualTo(0);
        await Assert.That(artifacts).IsEqualTo(1);
        await Assert.That(graphics).IsEqualTo(0);
    }

    /// <summary>Findings compare by their values, and copies preserve the original snapshot.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Finding_CopyAndDeconstruction_PreservesValues()
    {
        var original = new PdfAccessibilityFinding(PdfAccessibilityCode.NoLanguage, "Missing language", PageIndex, new(ElementNumber, 0));
        var equal = new PdfAccessibilityFinding(original.Code, original.Message, original.PageIndex, original.ElementId);
        var copy = original with { PageIndex = AccessibilityFindings.DocumentLevel };
        var (code, message, pageIndex, elementId) = original;
        await Assert.That(equal).IsEqualTo(original);
        await Assert.That(copy).IsNotEqualTo(original);
        await Assert.That(original.PageIndex).IsEqualTo(PageIndex);
        await Assert.That(copy.PageIndex).IsEqualTo(AccessibilityFindings.DocumentLevel);
        await Assert.That(code).IsEqualTo(original.Code);
        await Assert.That(message).IsEqualTo(original.Message);
        await Assert.That(pageIndex).IsEqualTo(PageIndex);
        await Assert.That(elementId).IsEqualTo(new PdfObjectId(ElementNumber, 0));
    }

    /// <summary>Every finding is counted, but retained findings stop at the per-code cap.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Add_ExceedsCap_KeepsTotalCount()
    {
        var pools = new ScratchPools();
        var state = AccessibilityFindingCollection.Rent(pools);
        try
        {
            for (var index = 0; index <= AccessibilityFindings.MaxFindingsPerCode; index++)
            {
                AccessibilityFindingCollection.Add(state, PdfAccessibilityCode.NoLanguage);
            }

            var counts = AccessibilityFindingCollection.GetCounts(state);
            await Assert.That(state.Findings.Count).IsEqualTo(AccessibilityFindings.MaxFindingsPerCode);
            await Assert.That(counts.Length).IsEqualTo(1);
            await Assert.That(counts[0].Count).IsEqualTo(AccessibilityFindings.MaxFindingsPerCode + 1);
            await Assert.That(counts[0].Code).IsEqualTo(PdfAccessibilityCode.NoLanguage);
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, state);
        }
    }

    /// <summary>Returning working storage cannot change independently retained findings or counts.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Release_DetachedResults_PreservesValues()
    {
        var pools = new ScratchPools();
        var first = AccessibilityFindingCollection.Rent(pools);
        PdfAccessibilityFinding[] findings;
        PdfAccessibilityCount[] counts;
        try
        {
            AccessibilityFindingCollection.Add(first, PdfAccessibilityCode.NoLanguage, PageIndex, new(ElementNumber, 0));
            findings = first.Findings.ToArray();
            counts = AccessibilityFindingCollection.GetCounts(first);
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, first);
        }

        var second = AccessibilityFindingCollection.Rent(pools);
        try
        {
            await Assert.That(ReferenceEquals(first.Findings, second.Findings)).IsTrue();
            await Assert.That(second.Findings.Count).IsEqualTo(0);
            await Assert.That(AccessibilityFindingCollection.GetCounts(second)).IsSameReferenceAs(Array.Empty<PdfAccessibilityCount>());
            AccessibilityFindingCollection.Add(second, PdfAccessibilityCode.NoMetadata);
            await Assert.That(findings[0].Code).IsEqualTo(PdfAccessibilityCode.NoLanguage);
            await Assert.That(findings[0].PageIndex).IsEqualTo(PageIndex);
            await Assert.That(findings[0].ElementId).IsEqualTo(new PdfObjectId(ElementNumber, 0));
            await Assert.That(counts[0].Count).IsEqualTo(1);
            await Assert.That(counts[0].Code).IsEqualTo(PdfAccessibilityCode.NoLanguage);
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, second);
        }
    }

    /// <summary>Overlapping report operations receive separate working storage.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Rent_OverlappingOperations_HasExclusiveStorage()
    {
        var pools = new ScratchPools();
        var first = AccessibilityFindingCollection.Rent(pools);
        var second = AccessibilityFindingCollection.Rent(pools);
        try
        {
            await Assert.That(ReferenceEquals(first.Findings, second.Findings)).IsFalse();
            await Assert.That(ReferenceEquals(first.Counts, second.Counts)).IsFalse();
            AccessibilityFindingCollection.Add(first, PdfAccessibilityCode.NoLanguage);
            AccessibilityFindingCollection.Add(second, PdfAccessibilityCode.NoMetadata);
            await Assert.That(AccessibilityFindingCollection.GetCounts(first)[0].Code).IsEqualTo(PdfAccessibilityCode.NoLanguage);
            await Assert.That(AccessibilityFindingCollection.GetCounts(second)[0].Code).IsEqualTo(PdfAccessibilityCode.NoMetadata);
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, first);
            AccessibilityFindingCollection.Release(pools, second);
        }
    }
}
