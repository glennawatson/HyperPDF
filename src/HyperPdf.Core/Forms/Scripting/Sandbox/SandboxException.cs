// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>A form script that uses something the sandbox does not allow, is not well formed, or ran past its limits.</summary>
public sealed class SandboxException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SandboxException"/> class.</summary>
    public SandboxException()
        : base("The form script cannot be run.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SandboxException"/> class.</summary>
    /// <param name="message">What was not allowed or went wrong, in plain words.</param>
    public SandboxException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SandboxException"/> class.</summary>
    /// <param name="message">What was not allowed or went wrong, in plain words.</param>
    /// <param name="innerException">The error that led to this one.</param>
    public SandboxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
