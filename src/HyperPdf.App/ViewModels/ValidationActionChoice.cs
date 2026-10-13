// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>A choice offered in the validation data and timestamp window.</summary>
/// <param name="Action">What the choice adds.</param>
/// <param name="Name">The short name.</param>
/// <param name="Description">What the choice does, in plain words.</param>
[DebuggerDisplay("ValidationActionChoice: {Name}")]
public sealed record ValidationActionChoice(ValidationAction Action, string Name, string Description);
