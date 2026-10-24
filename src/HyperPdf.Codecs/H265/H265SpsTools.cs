// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The coding tools a sequence parameter set switches on.</summary>
/// <param name="AmpEnabled">Whether inter blocks may split into uneven halves and quarters.</param>
/// <param name="SaoEnabled">Whether sample adaptive offset may be applied.</param>
/// <param name="StrongIntraSmoothing">Whether large intra blocks use the strong smoothing filter.</param>
/// <param name="TemporalMvpEnabled">Whether motion vector prediction may use the co-located picture.</param>
internal readonly record struct H265SpsTools(bool AmpEnabled, bool SaoEnabled, bool StrongIntraSmoothing, bool TemporalMvpEnabled);
