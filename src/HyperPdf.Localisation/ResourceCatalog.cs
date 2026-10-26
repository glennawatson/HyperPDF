// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.Localisation;

namespace HyperPdf.Localisation;

/// <summary>Identifies the application's generated interface resources.</summary>
public static class ResourceCatalog
{
    /// <summary>Gets the application-owned interface resource context.</summary>
    public static LocalisationContext UiText { get; } = new("HyperPdf.Localisation", "HyperPdf.Localisation.Strings");
}
