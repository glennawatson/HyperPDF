// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace HyperPdf.Skia;

/// <summary>Options for configuring the Skia rendering subsystem.</summary>
[System.Diagnostics.DebuggerDisplay("SkiaOptions: {MaxGpuResourceSizeBytes}")]
public sealed record SkiaOptions
{
    /// <summary>Gets or sets the maximum GPU resource cache limit in bytes, or null for default.</summary>
    public long? MaxGpuResourceSizeBytes { get; init; }

    /// <summary>Gets or sets a value indicating whether stencil buffers should be used, or null for default.</summary>
    public bool? UseStencilBuffers { get; init; }

    /// <summary>Gets a value indicating whether group opacity uses an intermediate layer.</summary>
    public bool UseOpacitySaveLayer { get; init; }
}
