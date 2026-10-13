// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>What flattening does with one annotation.</summary>
internal enum PdfFlattenAction
{
    /// <summary>Leaves the annotation on the page.</summary>
    Keep = 0,

    /// <summary>Draws the annotation into the page content and removes it.</summary>
    Draw = 1,

    /// <summary>Removes the annotation without drawing it, as a form field that is no longer a field.</summary>
    Remove = 2,
}
