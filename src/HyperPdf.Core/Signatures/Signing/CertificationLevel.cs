// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>The later changes a certification signature allows (ISO 32000-2, 12.8.2.2, the DocMDP permission).</summary>
public enum CertificationLevel
{
    /// <summary>An ordinary approval signature that does not limit later changes.</summary>
    None = 0,

    /// <summary>No later change is allowed.</summary>
    NoChanges = 1,

    /// <summary>Later changes may fill in form fields and add signatures.</summary>
    FormFillAndSign = 2,

    /// <summary>Later changes may fill in form fields, add signatures and add or change comments.</summary>
    AnnotateFormFillAndSign = 3,
}
