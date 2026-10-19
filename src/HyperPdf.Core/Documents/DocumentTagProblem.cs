// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>Something about the tags that a person should look at, in plain words.</summary>
/// <param name="Message">What is wrong and why it matters.</param>
/// <param name="PageIndex">The zero based page it is on; -1 when it concerns the whole document.</param>
/// <param name="TagId">The tag it concerns; 0 when there is none to select.</param>
[DebuggerDisplay("DocumentTagProblem: {Message} page {PageIndex}")]
public sealed record DocumentTagProblem(string Message, int PageIndex, int TagId);
