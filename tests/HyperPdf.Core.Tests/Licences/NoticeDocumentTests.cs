// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Licences;

namespace HyperPdf.Core.Tests.Licences;

/// <summary>Tests reading the third-party notices file and the licence allow list.</summary>
public sealed class NoticeDocumentTests
{
    /// <summary>The licences in the sample.</summary>
    private const int GroupCount = 2;

    /// <summary>The components under MIT in the sample.</summary>
    private const int MitEntries = 2;

    /// <summary>The components in the sample.</summary>
    private const int EntryCount = 3;

    /// <summary>The licence of the first group in the sample.</summary>
    private const string Mit = "MIT";

    /// <summary>A sample file in the format the generator writes.</summary>
    private const string Sample = """
        # Third-party notices

        Intro line.

        ## MIT

        ### HyperPDF
        - Origin: This application
        - Copyright: Copyright (c) 2026 Glenn Watson

        ````text
        MIT License

        ## not a heading
        ````

        ### Avalonia
        - Version: 12.0.0
        - Origin: NuGet package
        - Link: https://avaloniaui.net

        ````text
        Avalonia licence.
        ````

        ## Apache-2.0

        ### Kokoro
        - Origin: Downloaded when you turn this on

        ````text
        Apache text.
        ````
        """;

    /// <summary>Components are grouped by licence in file order, with their fields and text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesGroupsEntriesAndText()
    {
        var document = NoticeDocument.Parse(Sample);

        await Assert.That(document.Groups.Count).IsEqualTo(GroupCount);
        await Assert.That(document.Groups[0].Licence).IsEqualTo(Mit);
        await Assert.That(document.Groups[0].Entries.Count).IsEqualTo(MitEntries);
        var own = document.Groups[0].Entries[0];
        await Assert.That(own.Name).IsEqualTo("HyperPDF");
        await Assert.That(own.Copyright).IsEqualTo("Copyright (c) 2026 Glenn Watson");
        await Assert.That(own.Text).IsEqualTo("MIT License\n\n## not a heading");
        var avalonia = document.Groups[0].Entries[1];
        await Assert.That(avalonia.Version).IsEqualTo("12.0.0");
        await Assert.That(avalonia.Link).IsEqualTo("https://avaloniaui.net");
        await Assert.That(document.Groups[1].Entries[0].Origin).IsEqualTo("Downloaded when you turn this on");
        await Assert.That(document.AllEntries().Count).IsEqualTo(EntryCount);
    }

    /// <summary>Text with no components reads as an empty document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyTextHasNoGroups() => await Assert.That(NoticeDocument.Parse("# Title\n\nOnly an intro.").Groups.Count).IsEqualTo(0);

    /// <summary>Expressions pass only when every licence in them is on the allow list.</summary>
    /// <param name="expression">The SPDX expression.</param>
    /// <param name="allowed">Whether it passes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("MIT", true)]
    [Arguments("BSD-3-Clause", true)]
    [Arguments("Apache-2.0", true)]
    [Arguments("MIT AND Apache-2.0", true)]
    [Arguments("(MIT OR Apache-2.0)", true)]
    [Arguments("GPL-3.0-only", false)]
    [Arguments("MIT AND LGPL-2.1-only", false)]
    [Arguments("", false)]
    public async Task AllowListChecksEveryLicence(string expression, bool allowed) => await Assert.That(LicenceAllowList.IsAllowed(expression)).IsEqualTo(allowed);
}
