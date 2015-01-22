// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System.Collections.Generic;

using SiliconStudio.Paradox.Shaders;

namespace SiliconStudio.Paradox.Assets.Materials
{
    /// <summary>
    /// Base interface for all <see cref="IComputeNode"/>
    /// </summary>
    public interface IComputeNode
    {
        /// <summary>
        /// Gets the children.
        /// </summary>
        /// <param name="context">The context to get the children.</param>
        /// <returns>The list of children.</returns>
        IEnumerable<IComputeNode> GetChildren(object context = null);

        /// <summary>
        /// Generates the shader source equivalent for this node
        /// </summary>
        /// <returns>ShaderSource.</returns>
        ShaderSource GenerateShaderSource(MaterialGeneratorContext context, MaterialComputeColorKeys baseKeys);

        /// <summary>
        /// Determines whether the specified node is valid.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="baseKeys">The base keys.</param>
        /// <returns></returns>
        bool IsValid(MaterialGeneratorContext context);
    }
}