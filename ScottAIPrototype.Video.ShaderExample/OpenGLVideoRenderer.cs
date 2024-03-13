using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ScottAIPrototype;

public class OpenGLVideoRenderer(ILogger _logger) : IVideoRenderer
{
    private static readonly float[] vertices =
    [
        1.0f,  1.0f, 0.0f,
        1.0f, -1.0f, 0.0f,
        -1.0f, -1.0f, 0.0f,
        -1.0f,  1.0f, 0.0f
    ];

    private static readonly uint[] indices =
    [
        0u, 1u, 3u,
        1u, 2u, 3u
    ];

    private uint _width = 0;
    private uint _height = 0;
    private GL? gl = null;
    private uint fbo;
    private uint vao;
    private uint program;
    private IWindow? window = null;
    private readonly DateTime _initTime = DateTime.Now;
    public unsafe void Init(RenderSize renderSize)
    {
        _width = renderSize.Width;
        _height = renderSize.Height;
        var windowOptions = WindowOptions.Default with
        {
            Size = new Vector2D<int>((int)_width, (int)_height),
            Title = "ScottAI",
            IsVisible = false
        };
        window = Window.Create(windowOptions);
        window.Initialize();
        window.DoEvents();
        gl = window.CreateOpenGL();
        gl.Enable(EnableCap.Texture2D);
        gl.Enable(EnableCap.DepthTest);

        gl.ClearColor(System.Drawing.Color.CornflowerBlue);

        fbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

        uint textureColorBuffer = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, textureColorBuffer);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, new int[] { (int)GLEnum.Linear });
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, new int[] { (int)GLEnum.Linear });
        gl.BindTexture(TextureTarget.Texture2D, 0);

        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureColorBuffer, 0);

        uint rbo = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, _width, _height);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, rbo);

        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            throw new Exception("Framebuffer incomplete");
        }
        _logger.LogInformation("Framebuffer complete");

        vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);

        uint vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);

        uint ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);

        gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);

        var vertexShader = BuildShader(gl, new OneToOneVertexShader());
        var fragmentShader = BuildShader(gl, new GhostFlameFragmentShader());

        program = BuildProgram(gl, vertexShader, fragmentShader);
        ShaderCleanup(gl, program, vertexShader, fragmentShader);

        uint positionLocation = 0;
        gl.EnableVertexAttribArray(positionLocation);
        gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        // Cleanup
        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

        _logger.LogInformation("vao built");
    }
    private static uint BuildShader(GL gl, IShader shaderSource)
    {
        var shader = gl.CreateShader(shaderSource.ShaderType);
        gl.ShaderSource(shader, shaderSource.Source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status != (int)GLEnum.True) throw new Exception($"Failed to compile {shaderSource.ShaderType}: {gl.GetShaderInfoLog(shader)}");
        return shader;
    }
    private static void ShaderCleanup(GL gl, uint program, params uint[] shaders)
    {
        foreach (uint shader in shaders) gl.DetachShader(program, shader);
        foreach (uint shader in shaders) gl.DeleteShader(shader);
    }
    private static uint BuildProgram(GL gl, params uint[] shaders)
    {
        var program = gl.CreateProgram();
        foreach (uint shader in shaders) gl.AttachShader(program, shader);
        gl.LinkProgram(program);
        gl.GetProgram(program, GLEnum.LinkStatus, out var status);
        if (status != (int)GLEnum.True) throw new Exception("Failed to link program: " + gl.GetProgramInfoLog(program));
        return program;
    }
    private float _talking = 0.0f;
    private float currentTalking = 1.0f;


    private readonly LerpStep _active = new(0.1f, 0.1f, 1.0f, 0.1f, 0.1f);
    private readonly LerpStep _opacity = new(0.05f, 0.0f, 1.0f, 0.0f, 0.0f);
    public unsafe void Render(byte* arrayBuffer)
    {
        if (gl == null || window == null || _width == 0 || _height == 0) throw new Exception("NOT INITED!");
        _active.Step();
        _opacity.Step();

        var tv = 1.0f - _talking / 5f;
        currentTalking = Math.Clamp((currentTalking + tv + tv) / 3f, 0.8f, 1f);

        gl.ClearColor(System.Drawing.Color.Black);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        gl.BindVertexArray(vao);
        gl.UseProgram(program);

        var ul1 = gl.GetUniformLocation(program, "iResolution");
        var ul2 = gl.GetUniformLocation(program, "iTime");
        var ul3 = gl.GetUniformLocation(program, "iActivity");
        gl.Uniform2(ul1, (float)(_width), (float)(_height));
        gl.Uniform1(ul2, (float)(DateTime.Now - _initTime).TotalMilliseconds / 1000f);
        gl.Uniform3(ul3, _active.Value, currentTalking, _opacity.Value);

        gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

        gl.ReadBuffer(GLEnum.ColorAttachment0);
        gl.ReadPixels(0, 0, _width, _height, GLEnum.Rgba, GLEnum.UnsignedByte, arrayBuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    public void Dispose()
    {
        gl?.Dispose();
        window?.Dispose();
    }
    public void SetAs(bool active) => _active.SetTarget(active ? 1.0f : 0.1f);
    public void SetAmp(float value) => _talking = Math.Clamp(value, 0.0f, 1.0f);
    public void FadeIn() => _opacity.SetTarget(1.0f);
    public void FadeOut() => _opacity.SetTarget(0.0f);
}
