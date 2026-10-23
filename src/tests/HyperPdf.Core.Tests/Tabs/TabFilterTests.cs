// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Tabs;

namespace HyperPdf.Core.Tests.Tabs;

/// <summary>Tests for <see cref="TabFilter"/>.</summary>
public sealed class TabFilterTests
{
    /// <summary>The tabs searched.</summary>
    private static readonly TabSummary[] Tabs =
    [
        new("tax-return-2025.pdf", "Tax Return", "documents/Finance"),
        new("Résumé.pdf", "Curriculum vitae", "documents/Jobs"),
        new("manual.pdf", "Washing machine manual", "documents/House"),
    ];

    /// <summary>Verifies every word must match, in any field and any order, ignoring case and accents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesAllWords()
    {
        await Assert.That(TabFilter.Matches("2025 TAX", Tabs[0].FileName, Tabs[0].Title, Tabs[0].Folder)).IsTrue();
        await Assert.That(TabFilter.Matches("finance return", Tabs[0].FileName, Tabs[0].Title, Tabs[0].Folder)).IsTrue();
        await Assert.That(TabFilter.Matches("resume", Tabs[1].FileName, Tabs[1].Title, Tabs[1].Folder)).IsTrue();
        await Assert.That(TabFilter.Matches("tax house", Tabs[0].FileName, Tabs[0].Title, Tabs[0].Folder)).IsFalse();
        await Assert.That(TabFilter.Matches("  ", Tabs[2].FileName, Tabs[2].Title, Tabs[2].Folder)).IsTrue();
    }

    /// <summary>Verifies filtering writes the matching indexes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiltersIndexes()
    {
        const int manual = 2;
        var output = new int[Tabs.Length];

        var count = TabFilter.Filter("documents pdf machine", Tabs, output);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(output[0]).IsEqualTo(manual);
    }
}
