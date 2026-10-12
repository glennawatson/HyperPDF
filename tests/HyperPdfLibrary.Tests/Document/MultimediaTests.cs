// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for multimedia and 3D annotation data.</summary>
public sealed class MultimediaTests
{
    /// <summary>The number of multimedia annotations on the sample page.</summary>
    private const int AnnotationCount = 5;

    /// <summary>The rotation of the sample movie.</summary>
    private const int MovieRotation = 90;

    /// <summary>The width of the sample movie frame.</summary>
    private const int FrameWidth = 4;

    /// <summary>The height of the sample movie frame.</summary>
    private const int FrameHeight = 3;

    /// <summary>The play rate of the sample movie.</summary>
    private const double MovieRate = 2;

    /// <summary>The sampling rate of the sample sound.</summary>
    private const double SoundRate = 22_050;

    /// <summary>The channels of the sample sound.</summary>
    private const int SoundChannels = 2;

    /// <summary>The bits per sample of the sample sound.</summary>
    private const int SoundBits = 16;

    /// <summary>The camera distance of the sample 3D view.</summary>
    private const double CameraDistance = 5;

    /// <summary>The number of values in a camera-to-world matrix.</summary>
    private const int MatrixLength = 12;

    /// <summary>The number of rich media views.</summary>
    private const int ViewCount = 2;

    /// <summary>The kinds are recognised, in page order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationKindsAreRecognised()
    {
        using var document = Open();
        var media = PdfDocumentMedia.GetMultimediaAnnotations(document, PdfDocumentPages.GetPage(document, 0));

        await Assert.That(media.Length).IsEqualTo(AnnotationCount);
        await Assert.That(media[0].Kind).IsEqualTo(PdfMultimediaKind.Screen);
        await Assert.That(media[4].Kind).IsEqualTo(PdfMultimediaKind.ThreeD);
        await Assert.That(PdfDocumentMedia.GetMultimediaAnnotations(document).Length).IsEqualTo(AnnotationCount);
        await Assert.That(media[0].Bounds).IsNotNull();
    }

    /// <summary>A screen annotation reads its rendition action, clip and triggers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScreenReadsRenditionAndClip()
    {
        using var document = Open();
        var screen = PdfDocumentMedia.GetMultimediaAnnotations(document)[0].Screen!;
        var rendition = ((RenditionAction)screen.Action!.Action.Value!).Rendition!;
        var clip = rendition.Clip!;

        await Assert.That(screen.Title).IsEqualTo("scr");
        await Assert.That(screen.Triggers[0].Event).IsEqualTo("E");
        await Assert.That(rendition.Name).IsEqualTo("r");
        await Assert.That(clip.Subtype).IsEqualTo("MCD");
        await Assert.That(clip.FileName).IsEqualTo("m.mp4");
        await Assert.That(clip.ContentType).IsEqualTo("video/mp4");
        await Assert.That(clip.Permission).IsEqualTo("TEMPALWAYS");
    }

    /// <summary>A movie annotation reads its file, frame and activation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovieReadsActivation()
    {
        using var document = Open();
        var movie = PdfDocumentMedia.GetMultimediaAnnotations(document)[1].Movie!;

        await Assert.That(movie.Title).IsEqualTo("mv");
        await Assert.That(movie.File).IsEqualTo("m.mov");
        await Assert.That(movie.Aspect).IsEquivalentTo([FrameWidth, FrameHeight]);
        await Assert.That(movie.Rotation).IsEqualTo(MovieRotation);
        await Assert.That(movie.HasPoster).IsTrue();
        await Assert.That(movie.Activation.Mode).IsEqualTo("Repeat");
        await Assert.That(movie.Activation.Rate).IsEqualTo(MovieRate);
        await Assert.That(movie.Activation.ShowControls).IsTrue();
        await Assert.That(movie.Activation.Start).IsEqualTo("5");
    }

    /// <summary>A sound annotation reads its sampling parameters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoundReadsParameters()
    {
        using var document = Open();
        var sound = PdfDocumentMedia.GetMultimediaAnnotations(document)[2].Sound!;

        await Assert.That(sound.Rate).IsEqualTo(SoundRate);
        await Assert.That(sound.Channels).IsEqualTo(SoundChannels);
        await Assert.That(sound.BitsPerSample).IsEqualTo(SoundBits);
        await Assert.That(sound.Encoding).IsEqualTo("Signed");
    }

    /// <summary>A rich media annotation reads its assets, configurations and conditions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichMediaReadsAssetsAndConfigurations()
    {
        using var document = Open();
        var rich = PdfDocumentMedia.GetMultimediaAnnotations(document)[3].RichMedia!;

        await Assert.That(rich.Assets.Length).IsEqualTo(1);
        await Assert.That(rich.Assets[0].Name).IsEqualTo("a.swf");
        await Assert.That(rich.Assets[0].Data).IsNotNull();
        await Assert.That(rich.Configurations[0].Subtype).IsEqualTo("Flash");
        await Assert.That(rich.Configurations[0].Instances[0].AssetName).IsEqualTo("a.swf");
        await Assert.That(rich.ActivationCondition).IsEqualTo("PO");
        await Assert.That(rich.DeactivationCondition).IsEqualTo("XD");
        await Assert.That(rich.ViewCount).IsEqualTo(ViewCount);
    }

    /// <summary>A 3D annotation reads its stream subtype, views and activation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreeDReadsStreamAndViews()
    {
        using var document = Open();
        var model = PdfDocumentMedia.GetMultimediaAnnotations(document)[4].ThreeD!;
        var view = model.Stream!.Views[0];

        await Assert.That(model.Stream.Subtype).IsEqualTo("U3D");
        await Assert.That(model.Stream.DefaultViewIndex).IsEqualTo(0);
        await Assert.That(view.ExternalName).IsEqualTo("Front");
        await Assert.That(view.InternalName).IsEqualTo("v1");
        await Assert.That(view.MatrixMode).IsEqualTo("M");
        await Assert.That(view.CameraToWorld.Length).IsEqualTo(MatrixLength);
        await Assert.That(view.CameraDistance).IsEqualTo(CameraDistance);
        await Assert.That(view.Projection).IsEqualTo("P");
        await Assert.That(model.Activation!.ActivateOn).IsEqualTo("PO");
        await Assert.That(model.Activation.DeactivateOn).IsEqualTo("PC");
        await Assert.That(model.InitialViewSelector).IsEqualTo("0");
    }

    /// <summary>Opens the sample document with one annotation of each kind.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Open() => StructureDocuments.OpenPage(
        string.Empty,
        "/Annots [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R]",
        "<< /Type /Annot /Subtype /Screen /Rect [0 0 10 10] /T (scr) /A 9 0 R /AA << /E 9 0 R >> >>",
        "<< /Type /Annot /Subtype /Movie /Rect [0 0 10 10] /T (mv) /Movie << /F (m.mov) /Aspect [4 3] /Rotate 90 /Poster true >> "
        + "/A << /Mode /Repeat /Rate 2 /ShowControls true /Start 5 >> >>",
        "<< /Type /Annot /Subtype /Sound /Rect [0 0 10 10] /Sound 10 0 R >>",
        "<< /Type /Annot /Subtype /RichMedia /Rect [0 0 10 10] /RichMediaContent << /Assets << /Names [(a.swf) 11 0 R] >> "
        + "/Configurations [<< /Subtype /Flash /Name (cfg) /Instances [<< /Subtype /Flash /Asset 11 0 R >>] >>] /Views [1 2] >> "
        + "/RichMediaSettings << /Activation << /Condition /PO >> /Deactivation << /Condition /XD >> >> >>",
        "<< /Type /Annot /Subtype /3D /Rect [0 0 10 10] /3DD 12 0 R /3DA << /A /PO /D /PC >> /3DV 0 >>",
        "<< /S /Rendition /OP 0 /R 13 0 R /AN 4 0 R >>",
        MiniPdf.Stream("/R 22050 /C 2 /B 16 /E /Signed", "abcd"),
        "<< /Type /Filespec /F (a.swf) /EF << /F 14 0 R >> >>",
        MiniPdf.Stream(
            "/Type /3D /Subtype /U3D /DV 0 /VA [<< /XN (Front) /IN (v1) /MS /M /C2W [1 0 0 0 1 0 0 0 1 0 0 0] /CO 5 /P << /Subtype /P >> >>]",
            "u3d"),
        "<< /Type /Rendition /S /MR /N (r) /C 15 0 R >>",
        MiniPdf.Stream("/Type /EmbeddedFile", "swf"),
        "<< /Type /MediaClip /S /MCD /N (clip) /D 16 0 R /CT (video/mp4) /P << /TF (TEMPALWAYS) >> >>",
        "<< /Type /Filespec /F (m.mp4) >>");
}
