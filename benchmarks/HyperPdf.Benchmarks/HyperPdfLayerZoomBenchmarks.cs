// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the layer features of ISO 32000-2 8.11.4: the zoom-limited visibility check and zoom class a renderer makes
/// for each page request and piece of marked content, choosing an alternate configuration, and reading the layer tree.
/// The warm checks must not allocate.
/// </summary>
public class HyperPdfLayerZoomBenchmarks
{
    /// <summary>The zoom-managed group's object number.</summary>
    private const int ZoomGroupNumber = 4;

    /// <summary>The membership dictionary's object number.</summary>
    private const int MembershipNumber = 6;

    /// <summary>The magnification the checks use, inside the group's zoom range.</summary>
    private const double Magnification = 1.25;

    /// <summary>The layer properties: a zoom-managed group, a plain group, a labelled order and an alternate configuration.</summary>
    private const string Properties =
        "<< /OCGs [4 0 R 5 0 R] /D << /BaseState /OFF /ON [5 0 R] /Order [4 0 R [(Extras) 5 0 R]] "
        + "/AS [<< /Event /View /Category [/Zoom] /OCGs [4 0 R] >>] >> /Configs [<< /Name (Alternate) /OFF [5 0 R] >>] >>";

    /// <summary>The document's bytes.</summary>
    private byte[] _file = [];

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The document's layers.</summary>
    private PdfOptionalContent _content = null!;

    /// <summary>A reference to the zoom-managed group.</summary>
    private PdfValue _group;

    /// <summary>A reference to a membership dictionary over the zoom-managed group.</summary>
    private PdfValue _membership;

    /// <summary>The configuration the next selection chooses.</summary>
    private int _configuration;

    /// <summary>Opens the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _file = MiniPdf.Build(
            $"<< /Type /Catalog /Pages 2 0 R /OCProperties {Properties} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
            "<< /Type /OCG /Name (Zoomed) /Usage << /Zoom << /min 0.5 /max 2 >> >> >>",
            "<< /Type /OCG /Name (Plain) >>",
            "<< /Type /OCMD /OCGs [4 0 R] >>");
        _document = PdfDocumentReader.Open(_file, null);
        _content = PdfDocumentLayers.GetOptionalContent(_document);
        _group = PdfValue.FromReference(new(ZoomGroupNumber, 0));
        _membership = PdfValue.FromReference(new(MembershipNumber, 0));
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Checks a zoom-managed group at a magnification.</summary>
    /// <returns>Whether it is shown.</returns>
    [Benchmark]
    public bool IsGroupVisibleAtZoom() => _content.IsVisibleAt(_group, Magnification);

    /// <summary>Checks a membership dictionary over a zoom-managed group at a magnification.</summary>
    /// <returns>Whether it is shown.</returns>
    [Benchmark]
    public bool IsMembershipVisibleAtZoom() => _content.IsVisibleAt(_membership, Magnification);

    /// <summary>Finds the zoom class a renderer keys its pictures by.</summary>
    /// <returns>The class.</returns>
    [Benchmark]
    public int GetZoomClass() => _content.GetZoomClass(Magnification);

    /// <summary>Alternates between the default and the alternate configuration.</summary>
    /// <returns>Whether it applied.</returns>
    [Benchmark]
    public bool SelectConfiguration()
    {
        _configuration = _configuration == 0 ? PdfLayerConfiguration.DefaultIndex : 0;
        return _content.SelectConfiguration(_configuration);
    }

    /// <summary>Opens the bytes and reads the layer tree.</summary>
    /// <returns>The number of top-level entries.</returns>
    [Benchmark]
    public int ReadLayerTree()
    {
        using var document = PdfDocumentReader.Open(_file, null);
        return PdfDocumentLayers.GetOptionalContent(document).Tree.Count;
    }
}
