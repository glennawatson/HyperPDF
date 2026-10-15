// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for optional content visibility, which follows PDFium.</summary>
public sealed class OptionalContentTests
{
    /// <summary>The first group's object number.</summary>
    private const int GroupA = 4;

    /// <summary>The second group's object number.</summary>
    private const int GroupB = 5;

    /// <summary>The first membership dictionary's object number when a test adds one after two groups.</summary>
    private const int MembershipAfterTwo = 6;

    /// <summary>The second membership dictionary's object number when a test adds one after two groups.</summary>
    private const int SecondMembershipAfterTwo = 7;

    /// <summary>The number of layers in the radio group test.</summary>
    private const int TwoLayers = 2;

    /// <summary>The radio-group changes checked while another thread evaluates its membership.</summary>
    private const int RadioChanges = 2000;

    /// <summary>Properties that list group A and turn it off.</summary>
    private const string GroupAOff = "<< /OCGs [4 0 R] /D << /OFF [4 0 R] >> >>";

    /// <summary>A plain group.</summary>
    private const string Group = "<< /Type /OCG /Name (A) >>";

    /// <summary>A group without a /Type, which PDFium treats as a group.</summary>
    private const string UntypedGroup = "<< /Name (A) >>";

    /// <summary>A second plain group.</summary>
    private const string OtherGroup = "<< /Type /OCG /Name (B) >>";

    /// <summary>A dictionary without a /Type is a group, so /OFF hides it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DictionaryWithoutTypeIsAGroup()
    {
        using var document = Open(GroupAOff, UntypedGroup);

        await Assert.That(Shown(document, GroupA)).IsFalse();
        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Layers[0].IsVisible).IsFalse();
    }

    /// <summary>A group whose intent is not View is always shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DesignIntentGroupIsAlwaysShown()
    {
        using var document = Open(GroupAOff, "<< /Type /OCG /Name (A) /Intent /Design >>");

        await Assert.That(Shown(document, GroupA)).IsTrue();
        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Layers[0].IsVisible).IsTrue();
    }

    /// <summary>Without an /AS entry, a group's /Usage /View /ViewState does not change the state the configuration gives it (ISO 32000-2, 8.11.4.3).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewStateWithoutAnAutoStateLeavesTheConfigurationAlone()
    {
        using var document = Open("<< /OCGs [4 0 R] /D << /ON [4 0 R] >> >>", "<< /Type /OCG /Name (A) /Usage << /View << /ViewState /OFF >> >> >>");

        await Assert.That(Shown(document, GroupA)).IsTrue();
    }

    /// <summary>The configuration's /AS array applies for the View event.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoStateAppliesForTheViewEvent()
    {
        const string group = "<< /Type /OCG /Name (A) /Usage << /View << /ViewState /ON >> >> >>";
        using var withAutoState = Open("<< /OCGs [4 0 R] /D << /BaseState /OFF /AS [<< /Event /View /Category [/View] /OCGs [4 0 R] >>] >> >>", group);
        using var without = Open("<< /OCGs [4 0 R] /D << /BaseState /OFF >> >>", group);

        await Assert.That(Shown(withAutoState, GroupA)).IsTrue();
        await Assert.That(Shown(without, GroupA)).IsFalse();
    }

    /// <summary>/ON is applied after /BaseState and /OFF after /ON, so a group in both is hidden.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffWinsOverOn()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /BaseState /OFF /ON [4 0 R 5 0 R] /OFF [5 0 R] >> >>", Group, OtherGroup);

        await Assert.That(Shown(document, GroupA)).IsTrue();
        await Assert.That(Shown(document, GroupB)).IsFalse();
    }

    /// <summary>The /D arrays apply even when /OCGs is missing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConfigurationApplyWithoutAnOcgsArray()
    {
        using var document = Open("<< /D << /OFF [4 0 R] >> >>", Group);

        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).HasLayers).IsTrue();
        await Assert.That(Shown(document, GroupA)).IsFalse();
    }

    /// <summary>A membership dictionary counts only groups that resolve and is shown when none do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MembershipCountsOnlyResolvableGroups()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /OFF [4 0 R] >> >>",
            Group,
            OtherGroup,
            "<< /Type /OCMD /OCGs [90 0 R] /P /AllOn >>",
            "<< /Type /OCMD /OCGs [4 0 R 90 0 R] /P /AnyOn >>");

        await Assert.That(Shown(document, MembershipAfterTwo)).IsTrue();
        await Assert.That(Shown(document, SecondMembershipAfterTwo)).IsFalse();
    }

    /// <summary>A dictionary with /OCGs but no /Type is a membership dictionary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntypedDictionaryWithOcgsIsAMembership()
    {
        using var document = Open(GroupAOff, Group, OtherGroup, "<< /OCGs [4 0 R] >>");

        await Assert.That(Shown(document, MembershipAfterTwo)).IsFalse();
    }

    /// <summary>Showing a layer hides the other layers of its radio button group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowingALayerHidesItsRadioGroupSiblings()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /OFF [5 0 R] /RBGroups [[4 0 R 5 0 R]] >> >>", Group, OtherGroup);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var version = content.Version;

        await Assert.That(content.SetVisible(GroupB, true)).IsTrue();
        var layers = content.Layers;

        await Assert.That(layers.Count).IsEqualTo(TwoLayers);
        await Assert.That(layers[0].IsVisible).IsFalse();
        await Assert.That(layers[1].IsVisible).IsTrue();
        await Assert.That(content.Version).IsNotEqualTo(version);
    }

    /// <summary>Repeated reads reuse a visibility snapshot, and changing a layer preserves the earlier snapshot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerSnapshotsStayStableAfterVisibilityChanges()
    {
        using var document = Open(GroupAOff, Group);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var before = content.Layers;

        await Assert.That(ReferenceEquals(before, content.Layers)).IsTrue();
        await Assert.That(content.SetVisible(GroupA, true)).IsTrue();
        var after = content.Layers;

        await Assert.That(before[0].IsVisible).IsFalse();
        await Assert.That(after[0].IsVisible).IsTrue();
        await Assert.That(ReferenceEquals(after, content.Layers)).IsTrue();
    }

    /// <summary>A membership expression sees complete radio-group changes while another thread switches the active layer.</summary>
    /// <param name="cancellationToken">Stops the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadioGroupMembershipNeverSeesBothLayersOn(CancellationToken cancellationToken)
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /OFF [5 0 R] /RBGroups [[4 0 R 5 0 R]] >> >>",
            Group,
            OtherGroup,
            "<< /Type /OCMD /OCGs [4 0 R 5 0 R] /P /AllOn >>");
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = Task.Run(
            async () =>
            {
                await start.Task.WaitAsync(cancellationToken);
                for (var i = 0; i < RadioChanges; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = content.SetVisible(GroupA, true);
                    _ = content.SetVisible(GroupB, true);
                }
            },
            cancellationToken);
        var reader = Task.Run(
            async () =>
            {
                await start.Task.WaitAsync(cancellationToken);
                for (var i = 0; i < RadioChanges; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Shown(document, MembershipAfterTwo))
                    {
                        return false;
                    }
                }

                return true;
            },
            cancellationToken);
        _ = start.TrySetResult();
        await Task.WhenAll(writer, reader);
        await Assert.That(await reader).IsTrue();
    }

    /// <summary>Hiding a layer leaves the other layers of its radio button group alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidingALayerLeavesItsRadioGroupSiblings()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /RBGroups [[4 0 R 5 0 R]] >> >>", Group, OtherGroup);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.SetVisible(GroupA, false)).IsTrue();

        await Assert.That(content.Layers[0].IsVisible).IsFalse();
        await Assert.That(content.Layers[1].IsVisible).IsTrue();
    }

    /// <summary>Checks whether content marked with a group or membership dictionary is shown.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The object number of the group or membership dictionary.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private static bool Shown(PdfDocument document, int id) =>
        PdfDocumentLayers.GetOptionalContent(document).IsVisible(PdfValue.FromReference(new(id, 0)));

    /// <summary>Opens a one page document with optional content.</summary>
    /// <param name="properties">The /OCProperties dictionary.</param>
    /// <param name="objects">The bodies of objects 4 onwards.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string properties, params string[] objects)
    {
        string[] header =
        [
            $"<< /Type /Catalog /Pages 2 0 R /OCProperties {properties} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
        ];
        return PdfDocumentReader.Open(MiniPdf.Build([.. header, .. objects]), null);
    }
}
