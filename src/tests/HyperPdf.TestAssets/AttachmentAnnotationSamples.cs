// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>Builds a document with a file attachment annotation on its page.</summary>
public static class AttachmentAnnotationSamples
{
    /// <summary>The name of the attached file.</summary>
    public static readonly string FileName = "notes.txt";

    /// <summary>The contents of the attached file.</summary>
    public static readonly string FileText = "hello from the page";

    /// <summary>The object number of the annotation.</summary>
    public static readonly int AnnotationObject = 4;

    /// <summary>Creates a one page document whose page has a paper clip icon that holds <see cref="FileName"/>.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [4 0 R] >>",
        "<< /Type /Annot /Subtype /FileAttachment /Rect [20 120 60 160] /Name /Paperclip /Contents (Notes) /FS 5 0 R >>",
        $"<< /Type /Filespec /F ({FileName}) /UF ({FileName}) /EF << /F 6 0 R /UF 6 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile", FileText));
}
