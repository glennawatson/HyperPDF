// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>
/// Tests for optional content usage (ISO 32000-2, 8.11.4): the Export event, alternate configurations, the layer order,
/// locked layers, /ListMode and zoom limits.
/// </summary>
public sealed class OptionalContentUsageTests
{
    /// <summary>The first group's object number.</summary>
    private const int GroupA = 4;

    /// <summary>The second group's object number.</summary>
    private const int GroupB = 5;

    /// <summary>The third group's object number.</summary>
    private const int GroupC = 6;

    /// <summary>The fourth group's object number.</summary>
    private const int GroupD = 7;

    /// <summary>The first membership dictionary's object number after one group.</summary>
    private const int MembershipAfterOne = 5;

    /// <summary>The number of groups in the order tests.</summary>
    private const int FourLayers = 4;

    /// <summary>The number of entries in the configuration list of the alternate configuration tests.</summary>
    private const int FourConfigurations = 4;

    /// <summary>The position in /Configs of the configuration without a view intent.</summary>
    private const int DesignConfiguration = 1;

    /// <summary>The position in /Configs of the configuration that hides every group.</summary>
    private const int HideAllConfiguration = 2;

    /// <summary>A position in /Configs that does not exist.</summary>
    private const int MissingConfiguration = 7;

    /// <summary>A magnification below the zoom range.</summary>
    private const double QuarterZoom = 0.25;

    /// <summary>The lower end of the zoom range.</summary>
    private const double HalfZoom = 0.5;

    /// <summary>A magnification inside the zoom range.</summary>
    private const double JustBelowTwo = 1.99;

    /// <summary>The upper end of the zoom range.</summary>
    private const double DoubleZoom = 2;

    /// <summary>A magnification above the zoom range.</summary>
    private const double EightTimesZoom = 8;

    /// <summary>A magnification between the limits of the one-sided ranges.</summary>
    private const double OneAndAHalf = 1.5;

    /// <summary>The number of entries in the small trees.</summary>
    private const int TwoEntries = 2;

    /// <summary>The zoom class below every limit.</summary>
    private const int ClassBelow = 0;

    /// <summary>The zoom class between the limits.</summary>
    private const int ClassInside = 1;

    /// <summary>The zoom class above every limit.</summary>
    private const int ClassAbove = 2;

    /// <summary>A zoom range from half to double magnification.</summary>
    private const string HalfToDouble = "<< /min 0.5 /max 2 >>";

    /// <summary>A zoom range that starts at double magnification.</summary>
    private const string FromDouble = "<< /min 2 >>";

    /// <summary>The properties for the zoom tests: group A is managed by zoom, the rest follow the base state.</summary>
    private const string ZoomProperties =
        "<< /OCGs [4 0 R 5 0 R] /D << /BaseState /OFF /ON [5 0 R] /AS [<< /Event /View /Category [/Zoom] /OCGs [4 0 R] >>] >> >>";

    /// <summary>The properties for the alternate configuration tests.</summary>
    private const string AlternateConfigurations =
        "<< /OCGs [4 0 R 5 0 R] /D << /Name (Main) /OFF [5 0 R] >> /Configs ["
        + "<< /Name (Second) /OFF [4 0 R] >> "
        + "<< /Name (Design) /Intent /Design >> "
        + "<< /OFF [4 0 R 5 0 R] >>] >>";

    /// <summary>A plain group.</summary>
    private const string PlainA ="<< /Type /OCG /Name (A) >>";

    /// <summary>A second plain group.</summary>
    private const string PlainB = "<< /Type /OCG /Name (B) >>";

    /// <summary>A third plain group.</summary>
    private const string PlainC = "<< /Type /OCG /Name (C) >>";

    /// <summary>A fourth plain group.</summary>
    private const string PlainD = "<< /Type /OCG /Name (D) >>";

    /// <summary>The page dictionary without resources.</summary>
    private const string BarePage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>";

    /// <summary>The Export usage of a group that is excluded when exporting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExportStateOffExcludesAGroupFromExport()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /AS [<< /Event /Export /Category [/Export] /OCGs [4 0 R 5 0 R] >>] >> >>",
            "<< /Type /OCG /Name (A) /Usage << /Export << /ExportState /OFF >> >> >>",
            PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsExported(Ref(GroupA))).IsFalse();
        await Assert.That(content.IsExported(Ref(GroupB))).IsTrue();
        await Assert.That(content.IsVisible(Ref(GroupA))).IsTrue();
        await Assert.That(content.IsPrinted(Ref(GroupA))).IsTrue();
        await Assert.That(content.Layers[0].IsExported).IsFalse();
        await Assert.That(content.Layers[1].IsExported).IsTrue();
        await Assert.That(content.IsShownFor(Ref(GroupA), PdfOptionalContentEvent.Export)).IsFalse();
    }

    /// <summary>A usage application dictionary for the Export event can consult another category.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExportAutoStateConsultsTheNamedCategory()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /AS [<< /Event /Export /Category [/Print] /OCGs [4 0 R] >>] >> >>",
            "<< /Type /OCG /Name (A) /Usage << /Print << /PrintState /OFF >> >> >>",
            "<< /Type /OCG /Name (B) /Usage << /Print << /PrintState /OFF >> >> >>");
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsExported(Ref(GroupA))).IsFalse();
        await Assert.That(content.IsExported(Ref(GroupB))).IsTrue();
        await Assert.That(content.IsPrinted(Ref(GroupA))).IsTrue();
        await Assert.That(content.IsVisible(Ref(GroupA))).IsTrue();
    }

    /// <summary>Print and Export do not follow the viewer's layer switches.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewerSwitchesDoNotChangePrintOrExport()
    {
        using var document = Open("<< /OCGs [4 0 R] /D << >> >>", PlainA);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.SetVisible(GroupA, false)).IsTrue();

        await Assert.That(content.IsVisible(Ref(GroupA))).IsFalse();
        await Assert.That(content.IsPrinted(Ref(GroupA))).IsTrue();
        await Assert.That(content.IsExported(Ref(GroupA))).IsTrue();
    }

    /// <summary>The default configuration and the alternates are listed with their names and whether they suit viewing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConfigurationsAreListed()
    {
        using var document = Open(AlternateConfigurations, PlainA, PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var configurations = content.Configurations;

        await Assert.That(configurations.Count).IsEqualTo(FourConfigurations);
        await Assert.That(configurations[0].Index).IsEqualTo(PdfLayerConfiguration.DefaultIndex);
        await Assert.That(configurations[0].Name).IsEqualTo("Main");
        await Assert.That(configurations[0].IsActive).IsTrue();
        await Assert.That(configurations[1].Name).IsEqualTo("Second");
        await Assert.That(configurations[DesignConfiguration + 1].AppliesToView).IsFalse();
        await Assert.That(configurations[HideAllConfiguration + 1].Name).IsEqualTo("Configuration 3");
        await Assert.That(content.ActiveConfiguration).IsEqualTo(PdfLayerConfiguration.DefaultIndex);
    }

    /// <summary>Choosing an alternate configuration resets every layer to its states and raises the version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectingAConfigurationAppliesItsStates()
    {
        using var document = Open(AlternateConfigurations, PlainA, PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var version = content.Version;

        await Assert.That(content.Layers[0].IsVisible).IsTrue();
        await Assert.That(content.Layers[1].IsVisible).IsFalse();
        await Assert.That(content.SelectConfiguration(0)).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsFalse();
        await Assert.That(content.Layers[1].IsVisible).IsTrue();
        await Assert.That(content.ActiveConfiguration).IsEqualTo(0);
        await Assert.That(content.Version).IsNotEqualTo(version);
        await Assert.That(content.Configurations[1].IsActive).IsTrue();

        await Assert.That(content.SelectConfiguration(HideAllConfiguration)).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsFalse();
        await Assert.That(content.Layers[1].IsVisible).IsFalse();

        await Assert.That(content.SelectConfiguration(PdfLayerConfiguration.DefaultIndex)).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsTrue();
        await Assert.That(content.Layers[1].IsVisible).IsFalse();
    }

    /// <summary>A configuration whose intent leaves out View, or that does not exist, is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConfigurationWithoutViewIntentIsRefused()
    {
        using var document = Open(AlternateConfigurations, PlainA, PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var version = content.Version;

        await Assert.That(content.SelectConfiguration(DesignConfiguration)).IsFalse();
        await Assert.That(content.SelectConfiguration(MissingConfiguration)).IsFalse();
        await Assert.That(content.ActiveConfiguration).IsEqualTo(PdfLayerConfiguration.DefaultIndex);
        await Assert.That(content.Version).IsEqualTo(version);
    }

    /// <summary>The default configuration /D is in use at the start, even when an alternate has a View intent (ISO 32000-2, 8.11.4.3).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultConfigurationIsUsedFirst()
    {
        using var document = Open(
            "<< /OCGs [4 0 R] /D << >> /Configs [<< /Name (Viewer) /Intent /View /OFF [4 0 R] >>] >>",
            PlainA);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.ActiveConfiguration).IsEqualTo(PdfLayerConfiguration.DefaultIndex);
        await Assert.That(content.Layers[0].IsVisible).IsTrue();
        await Assert.That(content.SelectConfiguration(0)).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsFalse();
    }

    /// <summary>Layers followed by an array own it as sublayers; an array that starts with text is a label.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrderBuildsANestedTree()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R 6 0 R 7 0 R] /D << /Order [4 0 R [5 0 R 6 0 R] [(Group) 7 0 R]] >> >>",
            PlainA,
            PlainB,
            PlainC,
            PlainD);
        var tree = PdfDocumentLayers.GetOptionalContent(document).Tree;

        await Assert.That(tree.Count).IsEqualTo(TwoEntries);
        await Assert.That(tree[0].Name).IsEqualTo("A");
        await Assert.That(tree[0].LayerId).IsEqualTo(GroupA);
        await Assert.That(tree[0].Children.Select(static child => child.Name)).IsEquivalentTo(["B", "C"]);
        await Assert.That(tree[1].IsLabel).IsTrue();
        await Assert.That(tree[1].Name).IsEqualTo("Group");
        await Assert.That(tree[1].Children.Length).IsEqualTo(1);
        await Assert.That(tree[1].Children[0].LayerId).IsEqualTo(GroupD);
    }

    /// <summary>Groups the order leaves out are not presented, but they remain layers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrderLeavesOutUnlistedGroups()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /Order [4 0 R] >> >>", PlainA, PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.Tree.Count).IsEqualTo(1);
        await Assert.That(content.Layers.Count).IsEqualTo(TwoEntries);
    }

    /// <summary>An array of groups with no label and no group before it joins the level it appears in.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnlabelledLeadingArrayJoinsTheLevel()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /Order [[4 0 R 5 0 R]] >> >>", PlainA, PlainB);

        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).Tree.Count).IsEqualTo(TwoEntries);
    }

    /// <summary>Without /Order every layer is listed in /OCGs order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingOrderListsEveryLayer()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R 6 0 R 7 0 R] >>", PlainA, PlainB, PlainC, PlainD);
        var tree = PdfDocumentLayers.GetOptionalContent(document).Tree;

        await Assert.That(tree.Count).IsEqualTo(FourLayers);
        await Assert.That(tree.Select(static node => node.LayerId)).IsEquivalentTo([GroupA, GroupB, GroupC, GroupD]);
    }

    /// <summary>An alternate configuration without /Order uses the default configuration's order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlternateConfigurationInheritsTheOrder()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /Order [5 0 R 4 0 R] >> /Configs [<< /Name (X) >>] >>",
            PlainA,
            PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.SelectConfiguration(0)).IsTrue();
        await Assert.That(content.Tree.Select(static node => node.LayerId)).IsEquivalentTo([GroupB, GroupA]);
    }

    /// <summary>The configuration's /Order replaces the default one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConfigurationOrderOverridesTheDefault()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /Order [5 0 R 4 0 R] >> /Configs [<< /Name (X) /Order [4 0 R] >>] >>",
            PlainA,
            PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.SelectConfiguration(0)).IsTrue();
        await Assert.That(content.Tree.Count).IsEqualTo(1);
    }

    /// <summary>A locked layer reports it and refuses a change from the user interface.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LockedLayerRefusesChanges()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /OFF [5 0 R] /Locked [5 0 R] >> >>", PlainA, PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var version = content.Version;

        await Assert.That(content.Layers[0].IsLocked).IsFalse();
        await Assert.That(content.Layers[1].IsLocked).IsTrue();
        await Assert.That(content.IsLocked(GroupB)).IsTrue();
        await Assert.That(content.SetVisible(GroupB, true)).IsFalse();
        await Assert.That(content.Layers[1].IsVisible).IsFalse();
        await Assert.That(content.Version).IsEqualTo(version);
        await Assert.That(content.SetVisible(GroupA, false)).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsFalse();
    }

    /// <summary>Showing a layer does not hide a locked layer of its radio button group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LockedSiblingStaysWhenARadioButtonIsShown()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /OFF [4 0 R] /Locked [5 0 R] /RBGroups [[4 0 R 5 0 R]] >> >>",
            PlainA,
            PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.SetVisible(GroupA, true)).IsTrue();

        await Assert.That(content.Layers[0].IsVisible).IsTrue();
        await Assert.That(content.Layers[1].IsVisible).IsTrue();
    }

    /// <summary>Reset returns the layers to the configuration's states, and only raises the version when something changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetReturnsToTheConfigurationStates()
    {
        using var document = Open("<< /OCGs [4 0 R 5 0 R] /D << /OFF [5 0 R] >> >>", PlainA, PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.ResetToDefault()).IsFalse();
        _ = content.SetVisible(GroupA, false);
        _ = content.SetVisible(GroupB, true);
        var version = content.Version;

        await Assert.That(content.ResetToDefault()).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsTrue();
        await Assert.That(content.Layers[1].IsVisible).IsFalse();
        await Assert.That(content.Version).IsNotEqualTo(version);
        await Assert.That(content.ResetToDefault()).IsFalse();
    }

    /// <summary>A zoom-managed group is shown from /min up to, not including, /max.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomRangeShowsAGroupFromMinUpToMax()
    {
        using var document = Open(ZoomProperties, ZoomGroup(HalfToDouble), PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsVisibleAt(Ref(GroupA), QuarterZoom)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), HalfZoom)).IsTrue();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), 1)).IsTrue();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), JustBelowTwo)).IsTrue();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), DoubleZoom)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), EightTimesZoom)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupB), EightTimesZoom)).IsTrue();
        await Assert.That(content.Layers[0].ZoomMin).IsEqualTo(HalfZoom);
        await Assert.That(content.Layers[0].ZoomMax).IsEqualTo(DoubleZoom);
        await Assert.That(content.Layers[0].HasZoomLimit).IsTrue();
        await Assert.That(content.Layers[1].HasZoomLimit).IsFalse();
    }

    /// <summary>A magnification-independent check ignores zoom limits, and the auto state turns a group on even when the base state is off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomManagedGroupIsOnWhenTheBaseStateIsOff()
    {
        using var document = Open(ZoomProperties, ZoomGroup(HalfToDouble), PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsVisible(Ref(GroupA))).IsTrue();
        await Assert.That(content.Layers[0].IsVisible).IsTrue();
    }

    /// <summary>A range with only /min or only /max limits one side.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OneSidedZoomRangesLimitOneSide()
    {
        using var document = Open(
            "<< /OCGs [4 0 R 5 0 R] /D << /AS [<< /Event /View /Category [/Zoom] /OCGs [4 0 R 5 0 R] >>] >> >>",
            ZoomGroup(FromDouble),
            "<< /Type /OCG /Name (B) /Usage << /Zoom << /max 1 >> >> >>");
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsVisibleAt(Ref(GroupA), HalfZoom)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupB), HalfZoom)).IsTrue();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), OneAndAHalf)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupB), OneAndAHalf)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), DoubleZoom)).IsTrue();
        await Assert.That(content.IsVisibleAt(Ref(GroupB), DoubleZoom)).IsFalse();
    }

    /// <summary>Without a usage application dictionary the standard does not set states from zoom.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomIsIgnoredWithoutAnAutoState()
    {
        using var document = Open("<< /OCGs [4 0 R] >>", ZoomGroup(FromDouble));
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsVisibleAt(Ref(GroupA), QuarterZoom)).IsTrue();
        await Assert.That(content.Layers[0].HasZoomLimit).IsFalse();
        await Assert.That(content.GetZoomClass(QuarterZoom)).IsEqualTo(content.GetZoomClass(EightTimesZoom));
    }

    /// <summary>The zoom class changes exactly when a limit is crossed, so renderers cache by it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomClassChangesOnlyAtLimits()
    {
        using var document = Open(ZoomProperties, ZoomGroup(HalfToDouble), PlainB);
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.GetZoomClass(QuarterZoom)).IsEqualTo(ClassBelow);
        await Assert.That(content.GetZoomClass(HalfZoom)).IsEqualTo(ClassInside);
        await Assert.That(content.GetZoomClass(1)).IsEqualTo(ClassInside);
        await Assert.That(content.GetZoomClass(JustBelowTwo)).IsEqualTo(ClassInside);
        await Assert.That(content.GetZoomClass(DoubleZoom)).IsEqualTo(ClassAbove);
        await Assert.That(content.GetZoomClass(EightTimesZoom)).IsEqualTo(ClassAbove);
    }

    /// <summary>A membership dictionary follows the zoom state of its groups.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MembershipFollowsTheZoomOfItsGroups()
    {
        using var document = Open(
            ZoomProperties,
            ZoomGroup(HalfToDouble),
            "<< /Type /OCMD /OCGs [4 0 R] >>");
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsVisibleAt(Ref(MembershipAfterOne), QuarterZoom)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(MembershipAfterOne), 1)).IsTrue();
    }

    /// <summary>Selecting a configuration recomputes the zoom limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomLimitsFollowTheConfiguration()
    {
        using var document = Open(
            "<< /OCGs [4 0 R] /D << >> /Configs [<< /Name (Zoomed) /AS [<< /Event /View /Category [/Zoom] /OCGs [4 0 R] >>] >>] >>",
            ZoomGroup(FromDouble));
        var content = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(content.IsVisibleAt(Ref(GroupA), 1)).IsTrue();
        await Assert.That(content.SelectConfiguration(0)).IsTrue();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), 1)).IsFalse();
        await Assert.That(content.IsVisibleAt(Ref(GroupA), DoubleZoom)).IsTrue();
    }

    /// <summary>/ListMode is read, and the groups a page refers to come from its resources and XObjects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisiblePagesListModeFindsTheGroupsOfAPage()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [4 0 R 5 0 R 7 0 R] /D << /ListMode /VisiblePages >> >> >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Properties << /P1 4 0 R >> /XObject << /X 6 0 R >> >> >>",
                PlainA,
                PlainB,
                MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 1 1] /OC 5 0 R", string.Empty),
                PlainC),
            null);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var page = PdfDocumentPages.GetPage(document, 0);

        await Assert.That(content.ListMode).IsEqualTo(PdfLayerListMode.VisiblePages);
        await Assert.That(content.GetLayerIdsOnPage(page)).IsEquivalentTo([GroupA, GroupB]);
    }

    /// <summary>/ListMode is all pages unless the file says otherwise.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListModeDefaultsToAllPages()
    {
        using var document = Open("<< /OCGs [4 0 R] >>", PlainA);

        await Assert.That(PdfDocumentLayers.GetOptionalContent(document).ListMode).IsEqualTo(PdfLayerListMode.AllPages);
    }

    /// <summary>Writes a group with a zoom range.</summary>
    /// <param name="zoom">The /Zoom dictionary.</param>
    /// <returns>The group.</returns>
    private static string ZoomGroup(string zoom) => $"<< /Type /OCG /Name (A) /Usage << /Zoom {zoom} >> >>";

    /// <summary>Makes a reference to an object.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The reference.</returns>
    private static PdfValue Ref(int number) => PdfValue.FromReference(new(number, 0));

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
            BarePage,
        ];
        return PdfDocumentReader.Open(MiniPdf.Build([.. header, .. objects]), null);
    }
}
