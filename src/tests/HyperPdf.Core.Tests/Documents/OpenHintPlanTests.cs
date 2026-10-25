// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Checks that a document's open hints are followed only as far as the reader's settings allow.</summary>
public sealed class OpenHintPlanTests
{
    /// <summary>The page the test document opens at.</summary>
    private const int OpenPage = 2;

    /// <summary>The copies the test document asks for.</summary>
    private const int Copies = 3;

    /// <summary>The hints a document makes that ask for everything.</summary>
    private static readonly DocumentOpenHints Everything = new(
        OpenPage,
        OpenPanel.Outline,
        PageLayoutMode.DualCover,
        true,
        true,
        new(true, Copies, true, DuplexBinding.ShortEdge),
        true);

    /// <summary>Verifies that with every setting on, each hint is followed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EveryHint_IsFollowedWhenTheSettingsAllowIt()
    {
        var plan = OpenHintPlan.Create(Everything, new(), false);

        await Assert.That(plan.StartPage).IsEqualTo(OpenPage);
        await Assert.That(plan.Panel).IsEqualTo(OpenPanel.Outline);
        await Assert.That(plan.Layout).IsEqualTo(PageLayoutMode.DualCover);
        await Assert.That(plan.RightToLeft).IsTrue();
        await Assert.That(plan.UseDocumentTitle).IsTrue();
        await Assert.That(plan.Print).IsEqualTo(Everything.Print);
    }

    /// <summary>Verifies that the page the reader chose beats the document's opening page.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChosenPage_BeatsTheOpenAction()
    {
        var plan = OpenHintPlan.Create(Everything, new(), true);

        await Assert.That(plan.StartPage).IsEqualTo(-1);
    }

    /// <summary>Verifies that turning each setting off leaves out its hint, and no other.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EachSetting_TurnsItsOwnHintOff()
    {
        var allOff = new AppSettings { FollowOpenAction = false, FollowPageMode = false, FollowPageLayout = false, FollowDisplayDocTitle = false, FollowPrintDefaults = false };

        var plan = OpenHintPlan.Create(Everything, allOff, false);

        await Assert.That(plan.StartPage).IsEqualTo(-1);
        await Assert.That(plan.Panel).IsEqualTo(OpenPanel.None);
        await Assert.That(plan.Layout).IsNull();
        await Assert.That(plan.RightToLeft).IsFalse();
        await Assert.That(plan.UseDocumentTitle).IsFalse();
        await Assert.That(plan.Print).IsNull();
        await Assert.That(OpenHintPlan.Create(Everything, new() { FollowPageMode = false }, false).StartPage).IsEqualTo(OpenPage);
    }

    /// <summary>Verifies that a document that asks for nothing changes nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocumentWithoutHints_ChangesNothing()
    {
        var plan = OpenHintPlan.Create(DocumentOpenHints.None, new(), false);

        await Assert.That(plan.StartPage).IsEqualTo(-1);
        await Assert.That(plan.Panel).IsEqualTo(OpenPanel.None);
        await Assert.That(plan.Layout).IsNull();
        await Assert.That(plan.RightToLeft).IsFalse();
        await Assert.That(plan.UseDocumentTitle).IsFalse();
    }
}
