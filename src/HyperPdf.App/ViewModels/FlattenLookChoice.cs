// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Flattening;

namespace HyperPdf.App.ViewModels;

/// <summary>Which look to keep, as offered in the flattened copy window.</summary>
/// <param name="Look">The look.</param>
/// <param name="Name">The short name.</param>
/// <param name="Description">What the choice means, in plain words.</param>
[DebuggerDisplay("FlattenLookChoice: {Name}")]
public sealed record FlattenLookChoice(FlattenCopyLook Look, string Name, string Description);
