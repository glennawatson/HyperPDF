// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Media.U3d;
using HyperPdfLibrary.Writing;
using static HyperPdfLibrary.Tests.Media.U3dFramePixels;
using static HyperPdfLibrary.Tests.Media.U3dTestScenes;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks the still picture and fallback text of a 3D annotation, that nothing starts on its own, and that saving keeps the U3D bytes and view settings.</summary>
public sealed class Pdf3DPresenterTests
{
    /// <summary>The width and height of the picture.</summary>
    private const int Side = 64;

    /// <summary>The centre pixel of the picture.</summary>
    private const int Centre = 32;

    /// <summary>The annotation's own description.</summary>
    private const string Description = "A red square";

    /// <summary>The default activation: the model starts when the page opens.</summary>
    private const string OpenActivation = "<< /A /PO /AIS /L >>";

    /// <summary>The view dictionary list of the 3D stream: one view named like the model's camera.</summary>
    private const string Views = "/VA [<< /XN (Front) /IN (Camera) /MS /M /C2W [1 0 0 0 1 0 0 0 1 0 0 0] /CO 5 >>]";

    /// <summary>The chain of views for a stream with a second view and a default view by name.</summary>
    private const string TwoViews = "/VA [<< /XN (Other) /IN (Nothing) >> << /XN (Front) /IN (Camera) >>]";

    /// <summary>The largest picture side that is still too many pixels.</summary>
    private const int TooManyPixelsSide = 5_000;

    /// <summary>The drawn model has its picture, its description and the fact that the user must start it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PresentsTheModel()
    {
        using var document = Open(Annotation("PO"), Stream("U3D", RedQuad(true), Views));

        var presentation = Pdf3DPresenter.Present(ThreeD(document), Description, Side, Side, CancellationToken.None);

        await Assert.That(presentation.Frame).IsNotNull();
        await Assert.That(Pixel(presentation.Frame!, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(presentation.Frame!.ViewName).IsEqualTo(CameraNode);
        await Assert.That(presentation.FallbackText).StartsWith(Description);
        await Assert.That(presentation.FallbackText).Contains("3 scene nodes");
        await Assert.That(presentation.RequiresUserAction).IsTrue();
        await Assert.That(presentation.ActivationNote).Contains("waits until you start it");
    }

    /// <summary>A model that asks to start when its page is shown also waits, and one that asks to start explicitly has nothing to say.</summary>
    /// <param name="condition">The activation condition.</param>
    /// <param name="expectNote">Whether a note is expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("PV", true)]
    [Arguments("XA", false)]
    public async Task ReportsWhatTheDocumentAsksFor(string condition, bool expectNote)
    {
        using var document = Open(Annotation(condition), Stream("U3D", RedQuad(false), Views));

        var presentation = Pdf3DPresenter.Present(ThreeD(document), null, Side, Side, CancellationToken.None);

        await Assert.That(presentation.ActivationNote is not null).IsEqualTo(expectNote);
        await Assert.That(presentation.RequiresUserAction).IsTrue();
    }

    /// <summary>The view comes from the initial view dictionary, the stream's default view index, or its default view name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesTheViewTheAnnotationNames()
    {
        var byDefault = Open(Annotation("PO"), Stream("U3D", RedQuad(false), TwoViews, "/DV 1"));
        var initial = Open(AnnotationWithInitialView(), Stream("U3D", RedQuad(false), TwoViews, "/DV 0"));
        var byName = Open(Annotation("PO"), Stream("U3D", RedQuad(false), TwoViews, "/DV (Camera)"));
        var none = Open(Annotation("PO"), Stream("U3D", RedQuad(false), string.Empty));

        using (byDefault)
        using (initial)
        using (byName)
        using (none)
        {
            var viaIndex = Pdf3DPresenter.Present(ThreeD(byDefault), null, Side, Side, CancellationToken.None);
            var viaInitial = Pdf3DPresenter.Present(ThreeD(initial), null, Side, Side, CancellationToken.None);
            var viaName = Pdf3DPresenter.Present(ThreeD(byName), null, Side, Side, CancellationToken.None);
            var viaNothing = Pdf3DPresenter.Present(ThreeD(none), null, Side, Side, CancellationToken.None);

            await Assert.That(viaIndex.Frame!.ViewName).IsEqualTo(CameraNode);
            await Assert.That(viaInitial.Frame!.ViewName).IsEqualTo(CameraNode);
            await Assert.That(viaName.Frame!.ViewName).IsEqualTo(CameraNode);
            await Assert.That(viaNothing.Frame!.ViewName).IsEqualTo(CameraNode);
        }
    }

    /// <summary>Data that is not U3D, too large, or without a mesh gives no picture and a plain text, and an invalid size gives no picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FallsBackWithoutAPicture()
    {
        var emptyScene = new U3dFileWriter(false).WithAmbient().WithCamera().ToArray();
        var prc = Open(Annotation("PO"), Stream("PRC", [1, 1, 1, 1], string.Empty));
        var empty = Open(Annotation("PO"), Stream("U3D", emptyScene, string.Empty));
        var big = Open(Annotation("PO"), Stream("U3D", RedQuad(false), Views));

        using (prc)
        using (empty)
        using (big)
        {
            var notU3d = Pdf3DPresenter.Present(ThreeD(prc), Description, Side, Side, CancellationToken.None);
            var noMesh = Pdf3DPresenter.Present(ThreeD(empty), null, Side, Side, CancellationToken.None);
            var oversized = Pdf3DPresenter.Present(ThreeD(big), null, new(Side, Side, null, Vector4.Zero), U3dLimits.Default with { MaxBytes = 1 }, CancellationToken.None);
            var badSize = Pdf3DPresenter.Present(ThreeD(big), null, 0, Side, CancellationToken.None);
            var hugeSize = Pdf3DPresenter.Present(ThreeD(big), null, TooManyPixelsSide, TooManyPixelsSide, CancellationToken.None);

            await Assert.That(notU3d.Model).IsNull();
            await Assert.That(notU3d.Frame).IsNull();
            await Assert.That(notU3d.FallbackText).IsEqualTo($"{Description} This 3D content is not U3D data that can be shown.");
            await Assert.That(noMesh.Frame).IsNull();
            await Assert.That(noMesh.FallbackText).Contains("0 meshes");
            await Assert.That(oversized.Model).IsNull();
            await Assert.That(badSize.Frame).IsNull();
            await Assert.That(hugeSize.Frame).IsNull();
            await Assert.That(badSize.Model).IsNotNull();
        }
    }

    /// <summary>What the decoder could not apply is part of the fallback text, so a person who cannot see the picture hears it too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsOutWhatWasNotShown()
    {
        var writer = new U3dFileWriter(false);
        var file = writer.Add(U3dFileWriter.Block(0xFFFFFF36, new U3dValueWriter(true).String("Points").ToArray())).WithMesh(Quad(QuadResource, QuadCorners)).ToArray();
        using var document = Open(Annotation("PO"), Stream("U3D", file, string.Empty));

        var presentation = Pdf3DPresenter.Present(ThreeD(document), null, Side, Side, CancellationToken.None);

        await Assert.That(presentation.FallbackText).Contains("Point sets and line sets are not drawn.");
    }

    /// <summary>Scripts and actions in the meta data are recorded and never run, and the presentation still waits for the user.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverRunsActionsInTheMetaData()
    {
        var meta = U3dFileWriter.Meta(default(U3dMetaEntry) with { Attributes = 0x100, Key = "OnLoad", Value = "app.launchURL('http://example.invalid')" });
        var writer = new U3dFileWriter(false);
        var file = writer.Add(U3dFileWriter.Block(0xFFFFFF21, new U3dValueWriter(true).String("Root").U32(0).ToArray(), meta)).ToArray();
        using var document = Open(Annotation("PO"), Stream("U3D", file, string.Empty));

        var presentation = Pdf3DPresenter.Present(ThreeD(document), null, Side, Side, CancellationToken.None);

        await Assert.That(presentation.Model!.Metadata.Single().AsksToRun).IsTrue();
        await Assert.That(presentation.RequiresUserAction).IsTrue();
    }

    /// <summary>Missing arguments are refused and a cancelled token stops the work.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksItsArguments()
    {
        using var document = Open(Annotation("PO"), Stream("U3D", RedQuad(false), Views));
        var annotation = ThreeD(document);
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        U3dRenderOptions options = new(Side, Side, null, Vector4.Zero);

        await Assert.That(static () => Pdf3DPresenter.Present(null!, null, Side, Side, CancellationToken.None)).Throws<ArgumentNullException>();
        await Assert.That(() => Pdf3DPresenter.Present(annotation, null, null!, U3dLimits.Default, CancellationToken.None)).Throws<ArgumentNullException>();
        await Assert.That(() => Pdf3DPresenter.Present(annotation, null, options, null!, CancellationToken.None)).Throws<ArgumentNullException>();
        await Assert.That(() => Pdf3DPresenter.Present(annotation, null, Side, Side, cancel.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Saving the document keeps the U3D bytes and the view settings, however it is saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingKeepsTheModelAndItsViews()
    {
        var payload = RedQuad(true);
        using var document = Open(Annotation("PO"), Stream("U3D", payload, Views));
        var before = ThreeD(document);

        using var incremental = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);

        foreach (var saved in new[] { incremental, compact })
        {
            var after = ThreeD(saved);
            await Assert.That(after.Stream!.Data.DecodeToArray()).IsEquivalentTo(payload);
            await Assert.That(after.Stream.Views.Length).IsEqualTo(before.Stream!.Views.Length);
            await Assert.That(after.Stream.Views[0].InternalName).IsEqualTo(before.Stream.Views[0].InternalName);
            await Assert.That(after.Stream.Views[0].CameraDistance).IsEqualTo(before.Stream.Views[0].CameraDistance);
            await Assert.That(after.Stream.Views[0].CameraToWorld).IsEquivalentTo(before.Stream.Views[0].CameraToWorld);
            await Assert.That(after.Activation!.ActivateOn).IsEqualTo(before.Activation!.ActivateOn);
        }
    }

    /// <summary>An unrelated edit, saved, leaves the model and its views as they were and the saved model still draws the same picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnEditElsewhereKeepsThePicture()
    {
        using var document = Open(Annotation("PO"), Stream("U3D", RedQuad(true), Views));
        var first = Pdf3DPresenter.Present(ThreeD(document), Description, Side, Side, CancellationToken.None);

        _ = PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Crop, new(0, 0, Side, Side));
        using var saved = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        var second = Pdf3DPresenter.Present(ThreeD(saved), Description, Side, Side, CancellationToken.None);

        await Assert.That(second.Frame!.Rgba).IsEquivalentTo(first.Frame!.Rgba);
        await Assert.That(second.FallbackText).IsEqualTo(first.FallbackText);
    }

    /// <summary>Reads the 3D data of the document's only 3D annotation.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The 3D data.</returns>
    private static Pdf3DAnnotation ThreeD(PdfDocument document) =>
        PdfDocumentMedia.GetMultimediaAnnotations(document, PdfDocumentPages.GetPage(document, 0)).Single().ThreeD!;

    /// <summary>Opens a one-page document with a 3D annotation.</summary>
    /// <param name="annotation">The annotation object.</param>
    /// <param name="stream">The 3D stream object.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string annotation, string stream) => PdfDocumentReader.Open(
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [4 0 R] >>",
            annotation,
            stream),
        null);

    /// <summary>Makes the annotation object with an activation condition.</summary>
    /// <param name="condition">The activation condition.</param>
    /// <returns>The object text.</returns>
    private static string Annotation(string condition) =>
        $"<< /Type /Annot /Subtype /3D /Rect [0 0 100 100] /Contents ({Description}) /3DD 5 0 R /3DA << /A /{condition} /AIS /L >> /3DV 0 >>";

    /// <summary>Makes the annotation object with an initial view dictionary.</summary>
    /// <returns>The object text.</returns>
    private static string AnnotationWithInitialView() =>
        $"<< /Type /Annot /Subtype /3D /Rect [0 0 100 100] /3DD 5 0 R /3DA {OpenActivation} /3DV << /XN (Front) /IN (Camera) >> >>";

    /// <summary>Makes the 3D stream object.</summary>
    /// <param name="subtype">The stream's subtype.</param>
    /// <param name="data">The stream data.</param>
    /// <param name="entries">Further dictionary entries.</param>
    /// <returns>The object text.</returns>
    private static string Stream(string subtype, byte[] data, string entries) => Stream(subtype, data, entries, string.Empty);

    /// <summary>Makes the 3D stream object.</summary>
    /// <param name="subtype">The stream's subtype.</param>
    /// <param name="data">The stream data.</param>
    /// <param name="entries">Further dictionary entries.</param>
    /// <param name="more">Yet more dictionary entries.</param>
    /// <returns>The object text.</returns>
    private static string Stream(string subtype, byte[] data, string entries, string more) =>
        MiniPdf.Stream($"/Type /3D /Subtype /{subtype} {entries} {more}", Encoding.Latin1.GetString(data));
}
