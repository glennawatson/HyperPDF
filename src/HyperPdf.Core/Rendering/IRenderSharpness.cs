// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Rendering;

/// <summary>Sets how sharp the pages of a document draw.</summary>
public interface IRenderSharpness : IDocumentFeature
{
    /// <summary>Gets or sets the choices. Pages rendered after the change use them.</summary>
    RenderSharpness Sharpness { get; set; }
}
