// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A layer (optional content group) that can be shown or hidden.</summary>
/// <param name="Id">The layer's object number in the file.</param>
/// <param name="Name">The layer's name.</param>
/// <param name="IsVisible">Whether it is shown.</param>
[DebuggerDisplay("DocumentLayer: {Name} visible={IsVisible}")]
public sealed record DocumentLayer(int Id, string Name, bool IsVisible);
