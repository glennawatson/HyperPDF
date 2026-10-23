// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Tests.Robustness;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>
/// Tests that media, 3D, rich media, JavaScript and XFA data is read as data and survives incremental and compact saves with the same
/// bytes (ISO 32000-2 §12.6.4, §12.7.8, §13.2, §13.6, §13.7). Reading runs nothing.
/// </summary>
public sealed class MediaDataPreservationTests
{
    /// <summary>The number of multimedia annotations in the sample.</summary>
    private const int AnnotationCount = 5;

    /// <summary>The sound rate in the sample.</summary>
    private const int SoundRate = 22_050;

    /// <summary>The channels of the sample sound.</summary>
    private const int SoundChannels = 2;

    /// <summary>The bits per sample of the sample sound.</summary>
    private const int SoundBits = 16;

    /// <summary>The number of packets in the sample XFA.</summary>
    private const int PacketCount = 3;

    /// <summary>The kinds of the sample annotations, in page order.</summary>
    private static readonly PdfMultimediaKind[] ExpectedKinds =
        [PdfMultimediaKind.Screen, PdfMultimediaKind.Movie, PdfMultimediaKind.Sound, PdfMultimediaKind.RichMedia, PdfMultimediaKind.ThreeD];

    /// <summary>The hand-built document reads as data: every annotation kind, the scripts and the XFA packets.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandBuiltDocumentIsReadAsData()
    {
        using var document = PdfDocumentReader.Open(MediaDataDocuments.Create(), null);

        var annotations = PdfDocumentMedia.GetMultimediaAnnotations(document);
        var xfa = PdfDocumentXfa.GetXfa(document)!;
        var scripts = PdfDocumentActions.GetDocumentScripts(document);

        await Assert.That(annotations.Select(static annotation => annotation.Kind)).IsEquivalentTo(ExpectedKinds);
        await Assert.That(annotations[0].Screen!.Title).IsEqualTo("Screen title");
        await Assert.That(annotations[0].Screen!.Action!.Subtype).IsEqualTo("Rendition");
        await Assert.That(annotations[1].Movie!.File).IsEqualTo("movie.mov");
        await Assert.That(annotations[1].Movie!.Activation.Mode).IsEqualTo("Repeat");
        await Assert.That(annotations[2].Sound!.Rate).IsEqualTo(SoundRate);
        await Assert.That(annotations[2].Sound!.Channels).IsEqualTo(SoundChannels);
        await Assert.That(annotations[2].Sound!.BitsPerSample).IsEqualTo(SoundBits);
        await Assert.That(annotations[2].Sound!.Data.DecodeToArray()).IsEquivalentTo(Encoding.Latin1.GetBytes(MediaDataDocuments.Payload()));
        await Assert.That(annotations[3].RichMedia!.Assets.Single().Name).IsEqualTo("movie.swf");
        await Assert.That(annotations[3].RichMedia!.Configurations.Single().Instances.Single().AssetName).IsEqualTo("movie.swf");
        await Assert.That(annotations[4].ThreeD!.Stream!.Subtype).IsEqualTo("U3D");
        await Assert.That(annotations[4].ThreeD!.Stream!.Views.Single().ExternalName).IsEqualTo("First");
        await Assert.That(xfa.Packets.Select(static packet => packet.Name)).IsEquivalentTo(MediaDataDocuments.PacketNames);
        await Assert.That(xfa.Packets.Count).IsEqualTo(PacketCount);
        await Assert.That(scripts.Select(static script => script.Name)).IsEquivalentTo(MediaDataDocuments.ScriptNames);
        await Assert.That(scripts[0].Script).IsEqualTo("var first = 1;");
        await Assert.That(annotations.Length).IsEqualTo(AnnotationCount);
    }

    /// <summary>An incremental save keeps every stream's stored bytes, so the media data reads identically, bytes included.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveKeepsMediaDataByteForByte()
    {
        using var document = PdfDocumentReader.Open(MediaDataDocuments.Create(), null);
        var before = DocumentExerciser.ReadMediaData(document, true);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Edited" });

        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);

        await Assert.That(PdfDocumentMetadata.GetInfo(saved).Title).IsEqualTo("Edited");
        await Assert.That(DocumentExerciser.ReadMediaData(saved, true)).IsEqualTo(before);
        await Assert.That(before).Contains("xfaPacket=template").And.Contains("js=b-stream").And.Contains("3dStream=U3D");
    }

    /// <summary>A compact save, with a classic table or with object streams, keeps the decoded bytes of every stream and the data read from the dictionaries.</summary>
    /// <param name="useObjectStreams">Whether to pack objects into object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompactSaveKeepsMediaData(bool useObjectStreams)
    {
        using var document = PdfDocumentReader.Open(MediaDataDocuments.Create(), null);
        var before = DocumentExerciser.ReadMediaData(document, false);

        using var saved = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, new(useObjectStreams, false)), null);

        await Assert.That(DocumentExerciser.ReadMediaData(saved, false)).IsEqualTo(before);
    }

    /// <summary>A second compact save of a saved file still keeps the data, so repeated saves do not wear it down.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedCompactSavesKeepMediaData()
    {
        using var document = PdfDocumentReader.Open(MediaDataDocuments.Create(), null);
        var before = DocumentExerciser.ReadMediaData(document, false);
        using var first = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        using var second = PdfDocumentReader.Open(PdfCompactWriter.Save(first.Objects, PdfCompactOptions.Classic), null);

        await Assert.That(DocumentExerciser.ReadMediaData(second, false)).IsEqualTo(before);
    }
}
