// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Tests.Settings;

/// <summary>Tests for <see cref="LastPages"/>, which remembers where documents were closed.</summary>
public sealed class LastPagesTests
{
    /// <summary>A remembered document.</summary>
    private const string Report = "/docs/report.pdf";

    /// <summary>Another remembered document.</summary>
    private const string Notes = "/docs/notes.pdf";

    /// <summary>A page in the report.</summary>
    private const int ReportPage = 7;

    /// <summary>A later page in the report.</summary>
    private const int LaterPage = 12;

    /// <summary>The report and the notes.</summary>
    private const int TwoDocuments = 2;

    /// <summary>More documents than are remembered.</summary>
    private const int ManyDocuments = 250;

    /// <summary>The most documents remembered.</summary>
    private const int Remembered = 200;

    /// <summary>A newer record replaces the older one, and unknown documents start at the first page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemembersTheNewestPage()
    {
        List<LastViewedPage> pages = [];
        LastPages.Remember(pages, Report, ReportPage);
        LastPages.Remember(pages, Notes, 1);
        LastPages.Remember(pages, Report, LaterPage);

        await Assert.That(pages.Count).IsEqualTo(TwoDocuments);
        await Assert.That(pages[^1].FilePath).IsEqualTo(Report);
        await Assert.That(LastPages.Find(pages, Report)).IsEqualTo(LaterPage);
        await Assert.That(LastPages.Find(pages, "/docs/unknown.pdf")).IsEqualTo(0);
    }

    /// <summary>Only the most recent documents are kept, so settings stay small.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForgetsTheOldestDocuments()
    {
        List<LastViewedPage> pages = [];
        for (var i = 0; i < ManyDocuments; i++)
        {
            LastPages.Remember(pages, string.Create(CultureInfo.InvariantCulture, $"/docs/{i}.pdf"), i);
        }

        await Assert.That(pages.Count).IsEqualTo(Remembered);
        await Assert.That(LastPages.Find(pages, "/docs/0.pdf")).IsEqualTo(0);
        await Assert.That(LastPages.Find(pages, "/docs/249.pdf")).IsEqualTo(ManyDocuments - 1);
    }
}
