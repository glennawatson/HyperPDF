// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia.Platform;

namespace HyperPdf.Skia;

/// <summary>Represents a lease of the underlying platform graphics context.</summary>
public interface ISkiaSharpPlatformGraphicsApiLease : IDisposable
{
    /// <summary>Gets the platform graphics context.</summary>
    IPlatformGraphicsContext Context { get; }
}
