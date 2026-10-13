// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>An optional content configuration: the default /D or one of the alternate /Configs (ISO 32000-2, 8.11.4.3).</summary>
/// <param name="Index">The position in /Configs, or <see cref="DefaultIndex"/> for the default configuration /D.</param>
/// <param name="Name">The configuration's name.</param>
/// <param name="AppliesToView">Whether its /Intent includes View, so it can be applied for viewing.</param>
/// <param name="IsActive">Whether it is the configuration in use.</param>
[DebuggerDisplay("PdfLayerConfiguration: {Name} index={Index} active={IsActive}")]
public readonly record struct PdfLayerConfiguration(int Index, string Name, bool AppliesToView, bool IsActive)
{
    /// <summary>Gets the <see cref="Index"/> of the default configuration /D.</summary>
    public static int DefaultIndex => -1;
}
