using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

/// <summary>
/// OpenGL has no real deferred command buffer, so every call executes immediately against the single
/// global GL context — Begin/End are no-ops and <see cref="IGraphicsDevice.Submit"/> requires nothing extra.
/// 
/// This implementation includes state caching to minimize redundant OpenGL API calls.
/// </summary>
internal sealed unsafe class GLCommandList(GL gl) : ICommandList
{
    private readonly GL _gl = gl;
    private const int MaxVertexBuffers = 8;
    private const int MaxResourceSets = 8;
    private const int MaxTextureUnits = 32;

    // Pipeline state
    private GLPipeline? _currentPipeline;

    // Buffer state
    private GLBuffer? _indexBuffer;
    private IndexFormat _indexFormat;
    private uint _indexBufferByteOffset;
    private readonly GLBuffer?[] _currentVertexBuffers = new GLBuffer[MaxVertexBuffers];
    private readonly uint[] _currentVertexBufferOffsets = new uint[MaxVertexBuffers];

    // Resource set state
    private readonly GLResourceSet?[] _currentResourceSets = new GLResourceSet[MaxResourceSets];
    private readonly uint[] _currentResourceSetSlots = new uint[MaxResourceSets];

    // Texture unit tracking
    private readonly uint[] _currentTextureUnits = new uint[MaxTextureUnits];
    private readonly uint[] _currentSamplerBindings = new uint[MaxTextureUnits];

    // Render target state
    private uint _currentFramebufferHandle;

    // Viewport and scissor state
    private Viewport _currentViewport;
    private RectI _currentScissor;
    private bool _scissorEnabled;

    // Depth state
    private bool _depthTestEnabled;
    private bool _depthWriteEnabled;
    private CompareFunction _currentDepthCompare;

    // Cull face state
    private bool _cullFaceEnabled;
    private CullMode _currentCullMode;
    private FrontFace _currentFrontFace;

    // Blend state
    private bool _blendEnabled;
    private BlendMode _currentBlendMode;

    // Polygon mode state
    private FillMode _currentFillMode;

    // Program and VAO state
    private uint _currentProgramHandle;
    private uint _currentVertexArrayHandle;

    // Active texture unit
    private int _currentActiveTextureUnit = -1;

    public void Begin() { }
    public void End() { }

    public void SetRenderTarget(IRenderTarget? target)
    {
        var handle = target is GLRenderTarget glTarget ? glTarget.FramebufferHandle : 0;
        
        // Skip if framebuffer hasn't changed
        if (handle == _currentFramebufferHandle)
            return;

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle);
        GLErrorChecker.CheckError(_gl, "BindFramebuffer");
        _currentFramebufferHandle = handle;
    }

    public void ClearColor(Vector4 color)
    {
        // Always set clear color as it's state that affects future clears
        _gl.ClearColor(color.X, color.Y, color.Z, color.W);
        GLErrorChecker.CheckError(_gl, "ClearColor");
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        GLErrorChecker.CheckError(_gl, "Clear ColorBuffer");
    }

    public void ClearDepthStencil(float depth = 1f, byte stencil = 0)
    {
        // Always set depth clear value
        _gl.DepthMask(true);
        GLErrorChecker.CheckError(_gl, "DepthMask");
        _gl.ClearDepth(depth);
        GLErrorChecker.CheckError(_gl, "ClearDepth");
        _gl.ClearStencil(stencil);
        GLErrorChecker.CheckError(_gl, "ClearStencil");
        _gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GLErrorChecker.CheckError(_gl, "Clear DepthStencil");
    }

    public void SetViewport(in Viewport viewport)
    {
        // Skip if viewport hasn't changed
        if (_currentViewport.Equals(viewport))
            return;

        _gl.Viewport((int)viewport.X, (int)viewport.Y, (uint)viewport.Width, (uint)viewport.Height);
        GLErrorChecker.CheckError(_gl, "Viewport");
        _gl.DepthRange(viewport.MinDepth, viewport.MaxDepth);
        GLErrorChecker.CheckError(_gl, "DepthRange");
        _currentViewport = viewport;
    }

    public void SetScissor(in RectI rect)
    {
        // Skip if scissor hasn't changed
        if (_scissorEnabled && _currentScissor.Equals(rect))
            return;

        if (!_scissorEnabled)
        {
            _gl.Enable(EnableCap.ScissorTest);
            GLErrorChecker.CheckError(_gl, "Enable ScissorTest");
            _scissorEnabled = true;
        }

        _gl.Scissor(rect.X, rect.Y, (uint)rect.Width, (uint)rect.Height);
        GLErrorChecker.CheckError(_gl, "Scissor");
        _currentScissor = rect;
    }

    public void SetPipeline(IPipeline pipeline)
    {
        var glPipeline = (GLPipeline)pipeline;
        var desc = glPipeline.Description;

        // Check if pipeline has changed
        if (_currentPipeline == glPipeline)
            return;

        _currentPipeline = glPipeline;

        // Update program state
        var programHandle = glPipeline.ShaderSet.ProgramHandle;
        if (programHandle != _currentProgramHandle)
        {
            _gl.UseProgram(programHandle);
            GLErrorChecker.CheckError(_gl, "UseProgram");
            GLErrorChecker.ValidateHandle(programHandle, "Program");
            _currentProgramHandle = programHandle;
        }

        // Update VAO state
        var vaoHandle = glPipeline.VertexArrayHandle;
        if (vaoHandle != _currentVertexArrayHandle)
        {
            _gl.BindVertexArray(vaoHandle);
            GLErrorChecker.CheckError(_gl, "BindVertexArray");
            GLErrorChecker.ValidateHandle(vaoHandle, "VertexArray");
            _currentVertexArrayHandle = vaoHandle;
        }

        // Update depth test state
        if (desc.DepthTestEnabled != _depthTestEnabled)
        {
            if (desc.DepthTestEnabled)
            {
                _gl.Enable(EnableCap.DepthTest);
                GLErrorChecker.CheckError(_gl, "Enable DepthTest");
            }
            else
            {
                _gl.Disable(EnableCap.DepthTest);
                GLErrorChecker.CheckError(_gl, "Disable DepthTest");
            }
            _depthTestEnabled = desc.DepthTestEnabled;
        }

        // Update depth compare function
        if (_depthTestEnabled && desc.DepthCompare != _currentDepthCompare)
        {
            _gl.DepthFunc(GLFormats.MapCompare(desc.DepthCompare));
            GLErrorChecker.CheckError(_gl, "DepthFunc");
            _currentDepthCompare = desc.DepthCompare;
        }

        // Update depth write state
        if (desc.DepthWriteEnabled != _depthWriteEnabled)
        {
            _gl.DepthMask(desc.DepthWriteEnabled);
            GLErrorChecker.CheckError(_gl, "DepthMask");
            _depthWriteEnabled = desc.DepthWriteEnabled;
        }

        // Update cull face state
        if (desc.CullMode == CullMode.None)
        {
            if (_cullFaceEnabled)
            {
                _gl.Disable(EnableCap.CullFace);
                GLErrorChecker.CheckError(_gl, "Disable CullFace");
                _cullFaceEnabled = false;
            }
        }
        else
        {
            if (!_cullFaceEnabled)
            {
                _gl.Enable(EnableCap.CullFace);
                GLErrorChecker.CheckError(_gl, "Enable CullFace");
                _cullFaceEnabled = true;
            }

            var cullMode = desc.CullMode;
            if (cullMode != _currentCullMode)
            {
                _gl.CullFace(desc.CullMode == CullMode.Front ? TriangleFace.Front : TriangleFace.Back);
                GLErrorChecker.CheckError(_gl, "CullFace");
                _currentCullMode = cullMode;
            }
        }

        // Update front face
        if (desc.FrontFace != _currentFrontFace)
        {
            _gl.FrontFace(desc.FrontFace == FrontFace.CounterClockwise 
                ? Silk.NET.OpenGL.FrontFaceDirection.Ccw 
                : Silk.NET.OpenGL.FrontFaceDirection.CW);
            GLErrorChecker.CheckError(_gl, "FrontFace");
            _currentFrontFace = desc.FrontFace;
        }

        // Update polygon mode
        if (desc.FillMode != _currentFillMode)
        {
            _gl.PolygonMode(GLEnum.FrontAndBack, 
                desc.FillMode == FillMode.Solid ? PolygonMode.Fill : PolygonMode.Line);
            GLErrorChecker.CheckError(_gl, "PolygonMode");
            _currentFillMode = desc.FillMode;
        }

        // Update blend state
        switch (desc.Blend)
        {
            case BlendMode.Opaque:
                if (_blendEnabled)
                {
                    _gl.Disable(EnableCap.Blend);
                    GLErrorChecker.CheckError(_gl, "Disable Blend");
                    _blendEnabled = false;
                }
                break;
            case BlendMode.AlphaBlend:
                if (!_blendEnabled)
                {
                    _gl.Enable(EnableCap.Blend);
                    GLErrorChecker.CheckError(_gl, "Enable Blend");
                    _blendEnabled = true;
                }
                if (_currentBlendMode != desc.Blend)
                {
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    GLErrorChecker.CheckError(_gl, "BlendFunc Alpha");
                    _currentBlendMode = desc.Blend;
                }
                break;
            case BlendMode.Additive:
                if (!_blendEnabled)
                {
                    _gl.Enable(EnableCap.Blend);
                    GLErrorChecker.CheckError(_gl, "Enable Blend");
                    _blendEnabled = true;
                }
                if (_currentBlendMode != desc.Blend)
                {
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                    GLErrorChecker.CheckError(_gl, "BlendFunc Additive");
                    _currentBlendMode = desc.Blend;
                }
                break;
        }
    }

    public void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0)
    {
        if (_currentPipeline is null) 
            throw new InvalidOperationException("SetPipeline must be called before SetVertexBuffer.");
        
        var glBuffer = (GLBuffer)buffer;
        GLErrorChecker.ValidateHandle(glBuffer.Handle, "VertexBuffer");
        
        var vertexLayouts = _currentPipeline.ShaderSet.VertexLayouts;
        
        // Skip if buffer and offset haven't changed for this slot
        if (_currentVertexBuffers[slot] == glBuffer && _currentVertexBufferOffsets[slot] == offset)
            return;
        
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, glBuffer.Handle);
        GLErrorChecker.CheckError(_gl, "BindBuffer ArrayBuffer");
        
        // If there are vertex layouts defined, set up the attributes
        if (vertexLayouts.Length > 0)
        {
            if (slot >= vertexLayouts.Length)
                throw new ArgumentOutOfRangeException(nameof(slot), "No vertex layout registered for this slot.");
            
            var layout = vertexLayouts[slot];
            foreach (var element in layout.Elements)
            {
                var (count, type) = GLFormats.MapVertexElement(element.Format);
                _gl.EnableVertexAttribArray(element.Location);
                GLErrorChecker.CheckError(_gl, "EnableVertexAttribArray");
                _gl.VertexAttribPointer(element.Location, count, type, false, layout.Stride, (void*)(nint)(offset + element.Offset));
                GLErrorChecker.CheckError(_gl, "VertexAttribPointer");
                _gl.VertexAttribDivisor(element.Location, layout.InputRate == VertexInputRate.PerInstance ? 1u : 0u);
                GLErrorChecker.CheckError(_gl, "VertexAttribDivisor");
            }
        }

        // Cache the buffer for this slot
        _currentVertexBuffers[slot] = glBuffer;
        _currentVertexBufferOffsets[slot] = offset;
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0)
    {
        var glBuffer = (GLBuffer)buffer;
        GLErrorChecker.ValidateHandle(glBuffer.Handle, "IndexBuffer");

        // Skip if index buffer and format haven't changed
        if (_indexBuffer == glBuffer && _indexFormat == format && _indexBufferByteOffset == offset)
            return;

        _indexBuffer = glBuffer;
        _indexFormat = format;
        _indexBufferByteOffset = offset;

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, glBuffer.Handle);
        GLErrorChecker.CheckError(_gl, "BindBuffer ElementArrayBuffer");
    }

    public void SetResourceSet(uint slot, IResourceSet resourceSet)
    {
        var glSet = (GLResourceSet)resourceSet;
        var elements = glSet.Layout.Description.Elements;

        // Cache the resource set for this slot
        if (_currentResourceSets[slot] == glSet)
            return;

        _currentResourceSets[slot] = glSet;
        _currentResourceSetSlots[slot] = slot;

        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            var resource = glSet.Resources[i];

            switch (element.Kind)
            {
                case ResourceKind.UniformBuffer:
                    var buffer = (GLBuffer)resource;
                    GLErrorChecker.ValidateHandle(buffer.Handle, "UniformBuffer");
                    
                    // Skip if buffer hasn't changed for this binding
                    if (_currentTextureUnits[element.Binding] == buffer.Handle)
                        break;
                    
                    _gl.BindBufferBase(BufferTargetARB.UniformBuffer, element.Binding, buffer.Handle);
                    GLErrorChecker.CheckError(_gl, "BindBufferBase Uniform");
                    _currentTextureUnits[element.Binding] = buffer.Handle;
                    break;

                case ResourceKind.TextureReadOnly:
                    var texture = (GLTexture)resource;
                    GLErrorChecker.ValidateHandle(texture.Handle, "Texture");
                    
                    var textureUnit = (int)element.Binding;
                    
                    // Skip if texture hasn't changed for this unit
                    if (_currentActiveTextureUnit == textureUnit && _currentTextureUnits[textureUnit] == texture.Handle)
                        break;
                    
                    _gl.ActiveTexture(TextureUnit.Texture0 + textureUnit);
                    GLErrorChecker.CheckError(_gl, "ActiveTexture");
                    _currentActiveTextureUnit = textureUnit;
                    
                    _gl.BindTexture(TextureTarget.Texture2D, texture.Handle);
                    GLErrorChecker.CheckError(_gl, "BindTexture");
                    _currentTextureUnits[textureUnit] = texture.Handle;
                    break;

                case ResourceKind.Sampler:
                    var sampler = (GLSampler)resource;
                    GLErrorChecker.ValidateHandle(sampler.Handle, "Sampler");
                    
                    // Skip if sampler hasn't changed for this binding
                    if (_currentSamplerBindings[element.Binding] == sampler.Handle)
                        break;
                    
                    _gl.BindSampler(element.Binding, sampler.Handle);
                    GLErrorChecker.CheckError(_gl, "BindSampler");
                    _currentSamplerBindings[element.Binding] = sampler.Handle;
                    break;
            }
        }
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, uint destinationOffsetBytes = 0) =>
        ((GLBuffer)buffer).SetData(data, destinationOffsetBytes);

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
    {
        if (_currentPipeline is null) 
            throw new InvalidOperationException("SetPipeline must be called before DrawIndexed.");
        
        if (_indexBuffer is null) 
            throw new InvalidOperationException("SetIndexBuffer must be called before DrawIndexed.");

        var indexSize = _indexFormat == IndexFormat.UInt32 ? 4u : 2u;
        var byteOffset = _indexBufferByteOffset + firstIndex * indexSize;
        var topology = GLFormats.MapTopology(_currentPipeline.Description.Topology);
        var indexType = GLFormats.MapIndexFormat(_indexFormat);

        _gl.DrawElementsInstancedBaseVertexBaseInstance(
            topology, indexCount, indexType, (void*)(nint)byteOffset, instanceCount, vertexOffset, firstInstance);
        GLErrorChecker.CheckError(_gl, "DrawElementsInstancedBaseVertexBaseInstance");
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        if (_currentPipeline is null) 
            throw new InvalidOperationException("SetPipeline must be called before Draw.");

        var topology = GLFormats.MapTopology(_currentPipeline.Description.Topology);
        _gl.DrawArraysInstancedBaseInstance((GLEnum)topology, (int)firstVertex, vertexCount, instanceCount, firstInstance);
        GLErrorChecker.CheckError(_gl, "DrawArraysInstancedBaseInstance");
    }

    public void Dispose() { }
}