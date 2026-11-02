// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Media;

/// <summary>
/// What a viewer shows for a 3D annotation before the user does anything: a still picture of the model when it could be drawn,
/// and always a text description to fall back on. Nothing in a 3D annotation starts on its own: the model's activation setting,
/// scripts and actions in its meta data are recorded here as text and wait for the user.
/// </summary>
/// <param name="Model">The decoded model, or <see langword="null"/> when the annotation holds no U3D data that could be read.</param>
/// <param name="Frame">The still picture, or <see langword="null"/> when the model has nothing that can be drawn.</param>
/// <param name="FallbackText">The text to show or read aloud when there is no picture: the annotation's own description, then what the model holds and what could not be read.</param>
/// <param name="ActivationNote">What the document asks for (for example that the model starts when the page opens), or <see langword="null"/>; the viewer waits for the user either way.</param>
[DebuggerDisplay("Pdf3DPresentation: {FallbackText}")]
public sealed record Pdf3DPresentation(U3dModel? Model, U3dFrame? Frame, string FallbackText, string? ActivationNote)
{
    /// <summary>Gets a value indicating whether the user must ask for interaction to start. It is always <see langword="true"/>: this library starts none.</summary>
    public bool RequiresUserAction => true;
}
