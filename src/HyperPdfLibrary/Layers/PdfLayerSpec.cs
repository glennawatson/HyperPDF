// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>A layer (an optional content group, ISO 32000-2 8.11.2) to add to a document.</summary>
/// <param name="Name">The name shown to the user.</param>
[DebuggerDisplay("PdfLayerSpec: {Name} visible={IsVisible}")]
public sealed record PdfLayerSpec(string Name)
{
    /// <summary>Gets a value indicating whether the layer starts shown (the default configuration's state).</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Gets a value indicating whether users cannot change the layer's state (the default configuration's <c>/Locked</c>).</summary>
    public bool IsLocked { get; init; }

    /// <summary>Gets the intent: <c>View</c> (the default) or <c>Design</c>.</summary>
    public string Intent { get; init; } = "View";

    /// <summary>Gets how the layer is used, or <see langword="null"/> for no usage information.</summary>
    public PdfLayerUsage? Usage { get; init; }
}
