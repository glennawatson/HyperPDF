// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;

namespace HyperPdf.ViewModels.Rendering;

/// <summary>A tile factory whose owner releases the graphics resources it shares between tiles.</summary>
public interface IOwnedSurfaceFactory : IRenderSurfaceFactory, IDisposable;
