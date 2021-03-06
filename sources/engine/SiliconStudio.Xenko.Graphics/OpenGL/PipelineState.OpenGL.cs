﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
#if SILICONSTUDIO_XENKO_GRAPHICS_API_OPENGL
using System;
using System.Collections.Generic;
using SiliconStudio.Core;
using SiliconStudio.Core.Storage;
using SiliconStudio.Xenko.Shaders;
using SiliconStudio.Core.Extensions;
#if SILICONSTUDIO_XENKO_GRAPHICS_API_OPENGLES
using OpenTK.Graphics.ES30;
#if SILICONSTUDIO_PLATFORM_MONO_MOBILE
using PrimitiveTypeGl = OpenTK.Graphics.ES30.BeginMode;
#else
using PrimitiveTypeGl = OpenTK.Graphics.ES30.PrimitiveType;
#endif
#else
using OpenTK.Graphics.OpenGL;
using PrimitiveTypeGl = OpenTK.Graphics.OpenGL.PrimitiveType;
#endif

namespace SiliconStudio.Xenko.Graphics
{
    public partial class PipelineState
    {
        internal readonly BlendState BlendState;
        internal readonly DepthStencilState DepthStencilState;

        internal readonly RasterizerState RasterizerState;

        internal readonly EffectProgram EffectProgram;

        internal readonly PrimitiveTypeGl PrimitiveType;
        internal readonly VertexAttrib[] VertexAttribs;
        internal ResourceBinder ResourceBinder;

        private PipelineState(GraphicsDevice graphicsDevice, PipelineStateDescription pipelineStateDescription) : base(graphicsDevice)
        {
            // First time, build caches
            var pipelineStateCache = GetPipelineStateCache();

            var depthClampEmulation = !pipelineStateDescription.RasterizerState.DepthClipEnable && !graphicsDevice.HasDepthClamp;
#if SILICONSTUDIO_XENKO_GRAPHICS_API_OPENGLES
            // Depth Clamp can't be emulated on OpenGL ES 2 (TODO: warning?)
            if (graphicsDevice.IsOpenGLES2)
                depthClampEmulation = false;
#endif

            // Store states
            BlendState = new BlendState(pipelineStateDescription.BlendState, pipelineStateDescription.Output.RenderTargetCount > 0);
            RasterizerState = new RasterizerState(pipelineStateDescription.RasterizerState);
            DepthStencilState = new DepthStencilState(pipelineStateDescription.DepthStencilState, pipelineStateDescription.Output.DepthStencilFormat != PixelFormat.None);

            PrimitiveType = pipelineStateDescription.PrimitiveType.ToOpenGL();

            // Compile effect
            var effectBytecode = pipelineStateDescription.EffectBytecode;
            EffectProgram = effectBytecode != null ? pipelineStateCache.EffectProgramCache.Instantiate(Tuple.Create(effectBytecode, depthClampEmulation)) : null;

            var rootSignature = pipelineStateDescription.RootSignature;
            if (rootSignature != null && effectBytecode != null)
                ResourceBinder.Compile(graphicsDevice, rootSignature.EffectDescriptorSetReflection, effectBytecode);

            // Vertex attributes
            if (pipelineStateDescription.InputElements != null)
            {
                var vertexAttribs = new List<VertexAttrib>();
                foreach (var inputElement in pipelineStateDescription.InputElements)
                {
                    // Query attribute name from effect
                    var attributeName = "a_" + inputElement.SemanticName + inputElement.SemanticIndex;
                    int attributeIndex;
                    if (!EffectProgram.Attributes.TryGetValue(attributeName, out attributeIndex))
                        continue;

                    var vertexElementFormat = VertexAttrib.ConvertVertexElementFormat(inputElement.Format);
                    vertexAttribs.Add(new VertexAttrib(
                        inputElement.InputSlot,
                        attributeIndex,
                        vertexElementFormat.Size,
                        vertexElementFormat.Type,
                        vertexElementFormat.Normalized,
                        inputElement.AlignedByteOffset));
                }

                VertexAttribs = pipelineStateCache.VertexAttribsCache.Instantiate(vertexAttribs.ToArray());
            }
        }

        internal void Apply(CommandList commandList, PipelineState previousPipeline)
        {
            // Apply states
            if (BlendState != previousPipeline.BlendState || commandList.NewBlendFactor != commandList.BoundBlendFactor)
                BlendState.Apply(commandList, previousPipeline.BlendState);
            if (RasterizerState != previousPipeline.RasterizerState)
                RasterizerState.Apply(commandList);
            if (DepthStencilState != previousPipeline.DepthStencilState || commandList.NewStencilReference != commandList.BoundStencilReference)
                DepthStencilState.Apply(commandList);
        }

        protected override void DestroyImpl()
        {
            base.DestroyImpl();

            var pipelineStateCache = GetPipelineStateCache();

            if (EffectProgram != null)
                pipelineStateCache.EffectProgramCache.Release(EffectProgram);
            if (VertexAttribs != null)
                pipelineStateCache.VertexAttribsCache.Release(VertexAttribs);
        }

        struct VertexAttribsKey
        {
            public VertexAttrib[] Attribs;
            public int Hash;

            public VertexAttribsKey(VertexAttrib[] attribs)
            {
                Attribs = attribs;
                Hash = ArrayExtensions.ComputeHash(attribs);
            }

            public bool Equals(VertexAttribsKey other)
            {
                return Hash == other.Hash && ArrayExtensions.ArraysEqual(Attribs, other.Attribs);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is VertexAttribsKey && Equals((VertexAttribsKey)obj);
            }

            public override int GetHashCode()
            {
                return Hash;
            }

            public static bool operator ==(VertexAttribsKey left, VertexAttribsKey right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(VertexAttribsKey left, VertexAttribsKey right)
            {
                return !left.Equals(right);
            }
        }

        // Small helper to cache SharpDX graphics objects
        class GraphicsCache<TSource, TKey, TValue>
        {
            private object lockObject = new object();

            // Store instantiated objects
            private readonly Dictionary<TKey, TValue> storage = new Dictionary<TKey, TValue>();
            // Used for quick removal
            private readonly Dictionary<TValue, TKey> reverse = new Dictionary<TValue, TKey>();

            private readonly Dictionary<TValue, int> counter = new Dictionary<TValue, int>();

            private readonly Func<TSource, TKey> computeKey;
            private readonly Func<TSource, TValue> computeValue;

            public GraphicsCache(Func<TSource, TKey> computeKey, Func<TSource, TValue> computeValue)
            {
                this.computeKey = computeKey;
                this.computeValue = computeValue;
            }

            public TValue Instantiate(TSource source)
            {
                lock (lockObject)
                {
                    TValue value;
                    var key = computeKey(source);
                    if (!storage.TryGetValue(key, out value))
                    {
                        value = computeValue(source);
                        storage.Add(key, value);
                        reverse.Add(value, key);
                        counter.Add(value, 1);
                    }
                    else
                    {
                        counter[value] = counter[value] + 1;
                    }

                    return value;
                }
            }

            public void Release(TValue value)
            {
                // Should we remove it from the cache?
                lock (lockObject)
                {
                    var newRefCount = counter[value] - 1;
                    counter[value] = newRefCount;
                    if (newRefCount == 0)
                    {
                        counter.Remove(value);
                        reverse.Remove(value);
                        TKey key;
                        if (reverse.TryGetValue(value, out key))
                        {
                            storage.Remove(key);
                        }
                    }
                }
            }
        }

        private DevicePipelineStateCache GetPipelineStateCache()
        {
            return GraphicsDevice.GetOrCreateSharedData(GraphicsDeviceSharedDataType.PerDevice, typeof(DevicePipelineStateCache), device => new DevicePipelineStateCache(device));
        }

        // Caches
        private class DevicePipelineStateCache : IDisposable
        {
            public readonly GraphicsCache<Tuple<EffectBytecode, bool>, EffectBytecode, EffectProgram> EffectProgramCache;
            public readonly GraphicsCache<VertexAttrib[], VertexAttribsKey, VertexAttrib[]> VertexAttribsCache;

            public DevicePipelineStateCache(GraphicsDevice graphicsDevice)
            {
                EffectProgramCache = new GraphicsCache<Tuple<EffectBytecode, bool>, EffectBytecode, EffectProgram>(source => source.Item1, source => new EffectProgram(graphicsDevice, source.Item1, source.Item2));
                VertexAttribsCache = new GraphicsCache<VertexAttrib[], VertexAttribsKey, VertexAttrib[]>(source => new VertexAttribsKey(source), source => source);
            }

            public void Dispose()
            {
            }
        }
    }
}
#endif
