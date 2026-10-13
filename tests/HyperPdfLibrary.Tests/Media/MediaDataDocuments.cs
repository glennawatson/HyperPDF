// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Builds hand-made documents that hold media, 3D, rich media, JavaScript and XFA data, for reading and save tests.</summary>
internal static class MediaDataDocuments
{
    /// <summary>The length of each binary payload.</summary>
    private const int PayloadLength = 300;

    /// <summary>The step between the byte values of a payload, which is coprime with 256 so every value occurs.</summary>
    private const int PayloadStep = 7;

    /// <summary>The number of distinct byte values.</summary>
    private const int ByteValues = 256;

    /// <summary>Gets the names of the document-level scripts, in name tree order.</summary>
    internal static string[] ScriptNames => ["a-init", "b-stream"];

    /// <summary>Gets the names of the XFA packets, in array order.</summary>
    internal static string[] PacketNames => ["template", "datasets", "config"];

    /// <summary>Builds the document: a page with Screen, Movie, Sound, RichMedia and 3D annotations, document scripts and XFA packets.</summary>
    /// <returns>The file.</returns>
    internal static byte[] Create() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Names << /JavaScript 12 0 R >> /AcroForm << /Fields [] /XFA [(template) 13 0 R (datasets) 14 0 R (config) 15 0 R] >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R] >>",
        "<< /Type /Annot /Subtype /Screen /Rect [0 0 50 50] /T (Screen title) "
        + "/A << /S /Rendition /OP 0 /R << /S /MR /N (Clip) /C << /S /MCD /D 16 0 R /CT (video/mp4) >> >> >> >>",
        "<< /Type /Annot /Subtype /Movie /Rect [50 0 100 50] /T (Movie title) /Movie << /F (movie.mov) /Aspect [640 480] /Rotate 90 /Poster true >> "
        + "/A << /Start 5 /Rate 2 /Volume 0.5 /ShowControls true /Mode /Repeat >> >>",
        "<< /Type /Annot /Subtype /Sound /Rect [100 0 150 50] /Sound 17 0 R >>",
        "<< /Type /Annot /Subtype /RichMedia /Rect [150 0 200 50] /RichMediaContent << /Assets 18 0 R "
        + "/Configurations [<< /Subtype /Flash /Name (cfg) /Instances [<< /Subtype /Flash /Asset 19 0 R /Params << /FlashVars (a=b) >> >>] >>] "
        + "/Views [<< /Type /RichMediaView >>] >> /RichMediaSettings << /Activation << /Condition /PO >> /Deactivation << /Condition /XD >> >> >>",
        "<< /Type /Annot /Subtype /3D /Rect [200 0 250 50] /3DD 21 0 R /3DA << /A /PO /AIS /L /TB false >> /3DV 0 >>",
        "<< /S /JavaScript /JS (var first = 1;) >>",
        "<< /S /JavaScript /JS 11 0 R >>",
        MiniPdf.Stream(string.Empty, "var second = 2;\nvar third = \"é\";"),
        "<< /Names [(a-init) 9 0 R (b-stream) 10 0 R] >>",
        MiniPdf.Stream(string.Empty, "<template xmlns=\"http://www.xfa.org/schema/xfa-template/3.3/\"/>"),
        MiniPdf.Stream(string.Empty, "<xfa:datasets xmlns:xfa=\"http://www.xfa.org/schema/xfa-data/1.0/\"><xfa:data/></xfa:datasets>"),
        MiniPdf.Stream(string.Empty, "<config xmlns=\"http://www.xfa.org/schema/xci/3.0/\"/>"),
        MiniPdf.Stream("/Type /EmbeddedFile", Payload()),
        MiniPdf.Stream("/Type /Sound /R 22050 /C 2 /B 16 /E /Signed", Payload()),
        "<< /Names [(movie.swf) 19 0 R] >>",
        "<< /Type /Filespec /F (movie.swf) /UF (movie.swf) /EF << /F 20 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile /Subtype /application#2Fx-shockwave-flash", Payload()),
        MiniPdf.Stream("/Type /3D /Subtype /U3D /DV 0 /VA [<< /XN (First) /IN (v1) /MS /M /C2W [1 0 0 0 1 0 0 0 1 0 0 0] /CO 10 >>]", Payload()));

    /// <summary>Builds a payload that holds every byte value, so a save that changes encoding or text handling shows up.</summary>
    /// <returns>The payload, one character per byte.</returns>
    internal static string Payload()
    {
        var builder = new StringBuilder(PayloadLength);
        for (var i = 0; i < PayloadLength; i++)
        {
            _ = builder.Append((char)((i * PayloadStep) % ByteValues));
        }

        return builder.ToString();
    }
}
