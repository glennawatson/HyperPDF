// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The block types of the base profile of ECMA-363 (fourth edition, clause 9).</summary>
public enum U3dBlockType : uint
{
    /// <summary>No block type.</summary>
    None = 0,

    /// <summary>The file header, which starts every file.</summary>
    FileHeader = 0x00443355,

    /// <summary>A reference to another file.</summary>
    FileReference = 0xFFFFFF12,

    /// <summary>A modifier chain, which holds the declaration blocks of an object and its modifiers.</summary>
    ModifierChain = 0xFFFFFF14,

    /// <summary>A change of priority for the continuation blocks that follow.</summary>
    PriorityUpdate = 0xFFFFFF15,

    /// <summary>A declaration of a new kind of object, which is how the format is extended.</summary>
    NewObjectType = 0xFFFFFF16,

    /// <summary>A group node.</summary>
    GroupNode = 0xFFFFFF21,

    /// <summary>A model node.</summary>
    ModelNode = 0xFFFFFF22,

    /// <summary>A light node.</summary>
    LightNode = 0xFFFFFF23,

    /// <summary>A view node.</summary>
    ViewNode = 0xFFFFFF24,

    /// <summary>The declaration of a continuous level of detail mesh.</summary>
    ClodMeshDeclaration = 0xFFFFFF31,

    /// <summary>The declaration of a point set.</summary>
    PointSetDeclaration = 0xFFFFFF36,

    /// <summary>The declaration of a line set.</summary>
    LineSetDeclaration = 0xFFFFFF37,

    /// <summary>The base mesh data of a continuous level of detail mesh.</summary>
    ClodBaseMeshContinuation = 0xFFFFFF3B,

    /// <summary>The progressive refinement data of a continuous level of detail mesh.</summary>
    ClodProgressiveMeshContinuation = 0xFFFFFF3C,

    /// <summary>The data of a point set.</summary>
    PointSetContinuation = 0xFFFFFF3E,

    /// <summary>The data of a line set.</summary>
    LineSetContinuation = 0xFFFFFF3F,

    /// <summary>A 2D glyph modifier.</summary>
    GlyphModifier = 0xFFFFFF41,

    /// <summary>A subdivision modifier.</summary>
    SubdivisionModifier = 0xFFFFFF42,

    /// <summary>An animation modifier.</summary>
    AnimationModifier = 0xFFFFFF43,

    /// <summary>A bone weight modifier.</summary>
    BoneWeightModifier = 0xFFFFFF44,

    /// <summary>A shading modifier.</summary>
    ShadingModifier = 0xFFFFFF45,

    /// <summary>A continuous level of detail modifier.</summary>
    ClodModifier = 0xFFFFFF46,

    /// <summary>A light resource.</summary>
    LightResource = 0xFFFFFF51,

    /// <summary>A view resource.</summary>
    ViewResource = 0xFFFFFF52,

    /// <summary>A lit texture shader.</summary>
    LitTextureShader = 0xFFFFFF53,

    /// <summary>A material resource.</summary>
    MaterialResource = 0xFFFFFF54,

    /// <summary>The declaration of a texture.</summary>
    TextureDeclaration = 0xFFFFFF55,

    /// <summary>A motion resource.</summary>
    MotionResource = 0xFFFFFF56,

    /// <summary>The image data of a texture.</summary>
    TextureContinuation = 0xFFFFFF5C,
}
