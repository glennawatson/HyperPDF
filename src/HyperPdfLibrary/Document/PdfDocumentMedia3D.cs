// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads three-dimensional media entries.</summary>
public static class PdfDocumentMedia3D
{
    /// <summary>The key that names the type of a media dictionary.</summary>
    private const string SubtypeKey = "Subtype";

    /// <summary>Reads a 3D view dictionary.</summary>
    /// <param name="view">The view dictionary.</param>
    /// <returns>The view.</returns>
    internal static Pdf3DView Read3DView(PdfDictionary view) => new(
view.Text("XN"),
view.Text("IN"),
view.NameText("MS"),
[.. view.Array("C2W").Numbers()],
view.Num("CO", 0),
view.Dict("P")?.NameText(PdfDocumentMedia3D.SubtypeKey),
view.Dict("RM")?.NameText(PdfDocumentMedia3D.SubtypeKey),
view.Dict("LS")?.NameText(PdfDocumentMedia3D.SubtypeKey),
[.. view.Dict("BG")?.Array("C").Numbers() ?? []],
view.Array("SA")?.Count ?? 0,
view.Array("NA")?.Count ?? 0);

    /// <summary>Reads a rich media annotation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The rich media data.</returns>
    internal static PdfRichMediaAnnotation ReadRichMedia(PdfDocument document, PdfDictionary annot)
    {
        var content = annot.Dict("RichMediaContent");
        var settings = annot.Dict("RichMediaSettings");
        return new(
PdfDocumentMedia3D.ReadRichMediaAssets(content?.Dict("Assets")),
PdfDocumentMedia3D.ReadRichMediaConfigurations(document, content?.Array("Configurations")),
settings?.Dict("Activation")?.NameText("Condition"),
settings?.Dict("Deactivation")?.NameText("Condition"),
content?.Array("Views")?.Count ?? 0);
    }

    /// <summary>Reads a 3D annotation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The 3D data.</returns>
    internal static Pdf3DAnnotation Read3D(PdfDocument document, PdfDictionary annot)
    {
        var activation = annot.Dict("3DA");
        var initial = annot.Value("3DV");
        var initialView = initial.AsDictionary();
        return new(
PdfDocumentMedia3D.Read3DStream(document, annot.Value("3DD")),
activation is null ? null : PdfDocumentMedia3D.Read3DActivation(activation),
initialView is null ? null : PdfDocumentMedia3D.Read3DView(initialView),
initialView is null ? initial.ScalarText(document.Objects) : null);
    }

    /// <summary>Reads the assets name tree of a rich media annotation.</summary>
    /// <param name="assets">The <c>/Assets</c> name tree root, or null.</param>
    /// <returns>The assets.</returns>
    private static ImmutableArray<PdfRichMediaAsset> ReadRichMediaAssets(PdfDictionary? assets)
    {
        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(assets, entries);
        var result = new PdfRichMediaAsset[entries.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var embedded = entries[i].Value.AsDictionary()?.GetDictionary(KnownName.EF);
            result[i] = new(PdfText.Decode(entries[i].Key.AsStringBytes()), embedded?.GetStream(KnownName.UF) ?? embedded?.GetStream(KnownName.F));
        }

        return [.. result];
    }

    /// <summary>Reads 3D activation parameters.</summary>
    /// <param name="activation">The <c>/3DA</c> dictionary.</param>
    /// <returns>The activation.</returns>
    private static Pdf3DActivation Read3DActivation(PdfDictionary activation) => new(
activation.NameText("A") ?? "XA",
activation.NameText("D") ?? "PI",
activation.NameText("AIS") ?? "I",
activation.NameText("DIS") ?? "U",
activation.Flag("TB", true),
activation.Flag("NP", false));

    /// <summary>Reads the configurations of a rich media annotation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="configurations">The <c>/Configurations</c> array, or null.</param>
    /// <returns>The configurations.</returns>
    private static ImmutableArray<PdfRichMediaConfiguration> ReadRichMediaConfigurations(PdfDocument document, PdfArray? configurations)
    {
        var result = new List<PdfRichMediaConfiguration>();
        for (var i = 0; configurations is not null && i < configurations.Count; i++)
        {
            if (configurations.GetDictionary(i) is { } configuration)
            {
                result.Add(new(
configuration.NameText(PdfDocumentMedia3D.SubtypeKey),
configuration.Text("Name"),
PdfDocumentMedia3D.ReadRichMediaInstances(document, configuration.Array("Instances"))));
            }
        }

        return [.. result];
    }

    /// <summary>Reads the instances of a configuration.</summary>
    /// <param name="document">The document.</param>
    /// <param name="instances">The <c>/Instances</c> array, or null.</param>
    /// <returns>The instances.</returns>
    private static ImmutableArray<PdfRichMediaInstance> ReadRichMediaInstances(PdfDocument document, PdfArray? instances)
    {
        var result = new List<PdfRichMediaInstance>();
        for (var i = 0; instances is not null && i < instances.Count; i++)
        {
            if (instances.GetDictionary(i) is { } instance)
            {
                result.Add(new(instance.NameText(PdfDocumentMedia3D.SubtypeKey) ?? string.Empty, PdfDocumentFileSpecs.ReadFileSpec(document, instance.Value("Asset")), instance.Dict("Params")));
            }
        }

        return [.. result];
    }

    /// <summary>Reads the 3D data of an annotation: a 3D stream, or a 3D reference dictionary that points to one.</summary>
    /// <param name="document">The document.</param>
    /// <param name="value">The <c>/3DD</c> value.</param>
    /// <returns>The 3D stream, or null.</returns>
    private static Pdf3DStream? Read3DStream(PdfDocument document, PdfValue value)
    {
        var stream = value.AsStream() ?? value.AsDictionary()?.Value("3DD").AsStream();
        if (stream is null)
        {
            return null;
        }

        var dictionary = stream.Dictionary;
        var views = new List<Pdf3DView>();
        var list = dictionary.Array("VA");
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is { } view)
            {
                views.Add(PdfDocumentMedia3D.Read3DView(view));
            }
        }

        var defaultView = dictionary.Value("DV");
        return new(
dictionary.NameText(PdfDocumentMedia3D.SubtypeKey),
stream,
[.. views],
defaultView.IsNumber ? defaultView.AsInt32() : null,
defaultView.IsNumber ? null : defaultView.ScalarText(document.Objects));
    }
}
