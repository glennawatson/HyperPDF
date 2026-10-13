// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Builds documents that refuse document-level edits, for the edit tests.</summary>
internal static class EditGuardSamples
{
    /// <summary>Builds a one page document certified at a DocMDP level.</summary>
    /// <param name="permission">The level: 1 allows no change, 2 form filling and signing, 3 also annotations.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] Certified(int permission) =>
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] /SigFlags 3 >> /Perms << /DocMDP 5 0 R >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>",
            "<< /FT /Sig /T (Cert) /V 5 0 R /Type /Annot /Subtype /Widget /Rect [0 0 0 0] /P 3 0 R >>",
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Sig /ByteRange [0 0 0 0] /Contents <00> /Reference [<< /Type /SigRef /TransformMethod /DocMDP /TransformParams << /P {permission} >> >>] >>"));
}
