// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The order the packets of a JPEG 2000 codestream follow (ISO 15444-1 B.12).</summary>
public enum PdfJpxProgression
{
    /// <summary>Layer, resolution, component, position (LRCP): quality scalable.</summary>
    LayerResolutionComponentPosition = 0,

    /// <summary>Resolution, layer, component, position (RLCP): resolution scalable.</summary>
    ResolutionLayerComponentPosition = 1,

    /// <summary>Resolution, position, component, layer (RPCL).</summary>
    ResolutionPositionComponentLayer = 2,

    /// <summary>Position, component, resolution, layer (PCRL).</summary>
    PositionComponentResolutionLayer = 3,

    /// <summary>Component, position, resolution, layer (CPRL).</summary>
    ComponentPositionResolutionLayer = 4,
}
