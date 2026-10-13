// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A form to draw on a page.</summary>
/// <param name="Form">The form XObject in the document.</param>
/// <param name="Transform">Maps the form's space to the page's user space.</param>
/// <param name="Behind">Whether the form is drawn before the page's own content.</param>
/// <param name="GraphicsState">An ExtGState object drawn with the form, or an invalid id for none.</param>
/// <param name="Marking">How the drawing is marked for tagged documents.</param>
internal sealed record PdfStampRequest(PdfObjectId Form, Matrix3x2 Transform, bool Behind, PdfObjectId GraphicsState, PdfStampMarking Marking);
