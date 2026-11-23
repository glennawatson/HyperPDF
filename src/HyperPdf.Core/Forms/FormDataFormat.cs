// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms;

/// <summary>The kinds of file a form's answers can be written to.</summary>
public enum FormDataFormat
{
    /// <summary>Forms Data Format, the PDF-based file.</summary>
    Fdf = 0,

    /// <summary>XML Forms Data Format, the XML file.</summary>
    Xfdf = 1,
}
