// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Skia;

/// <summary>Provides temporary access to the drawing context's Skia canvas.</summary>
public interface ISkiaSharpApiLeaseFeature
{
    /// <summary>Leases the canvas until the returned owner is disposed.</summary>
    /// <returns>The canvas lease.</returns>
    ISkiaSharpApiLease Lease();
}
