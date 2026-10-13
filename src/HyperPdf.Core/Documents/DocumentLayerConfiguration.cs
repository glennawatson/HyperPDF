// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A set of layer states the file offers: its default or an alternate.</summary>
/// <param name="Index">The configuration's id for <see cref="ILayerSource.SelectConfiguration"/>.</param>
/// <param name="Name">The configuration's name.</param>
/// <param name="IsActive">Whether it is the one in use.</param>
[DebuggerDisplay("DocumentLayerConfiguration: {Name} active={IsActive}")]
public sealed record DocumentLayerConfiguration(int Index, string Name, bool IsActive);
