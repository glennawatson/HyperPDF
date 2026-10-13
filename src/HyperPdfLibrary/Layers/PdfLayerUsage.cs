// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>What a layer is meant for (the optional content usage dictionary, ISO 32000-2 8.11.4.4, Table 100).</summary>
[DebuggerDisplay("PdfLayerUsage: view={ViewState} print={PrintState} export={ExportState}")]
public sealed record PdfLayerUsage
{
    /// <summary>Gets whether the layer is meant to be shown on screen, or <see langword="null"/> to say nothing.</summary>
    public bool? ViewState { get; init; }

    /// <summary>Gets whether the layer is meant to be printed, or <see langword="null"/> to say nothing.</summary>
    public bool? PrintState { get; init; }

    /// <summary>Gets whether the layer is meant to be exported, or <see langword="null"/> to say nothing.</summary>
    public bool? ExportState { get; init; }

    /// <summary>Gets the name of the program or feature that made the layer, or <see langword="null"/>.</summary>
    public string? Creator { get; init; }

    /// <summary>Gets the kind of content the layer holds, such as <c>Artwork</c> or <c>Technical</c>; used with <see cref="Creator"/>.</summary>
    public string? CreatorSubtype { get; init; }
}
