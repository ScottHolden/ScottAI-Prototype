using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ScottAIPrototype;

public class OpenGLVideoRenderer : IVideoRenderer
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

    private static readonly string vertexCode = """
		#version 330 core
		layout (location = 0) in vec2 position;            
		layout (location = 1) in vec2 inTexCoord;

		out vec2 texCoord;
		void main(){
			texCoord = inTexCoord;
			gl_Position = vec4(position.x, position.y, 0.0f, 1.0f);
		}
		""";

    private static readonly string fragmentCode = """
		#version 330 core
		uniform vec2 iResolution;
		uniform vec3 iActivity;
		uniform float iTime;
		out vec4 fragColor;
		vec2 fragCoord = gl_FragCoord.xy;

		float noise(vec3 p)
		{
			vec3 i = floor(p);
			vec4 a = dot(i, vec3(1., 57., 21.)) + vec4(0., 57., 21., 78.);
			vec3 f = cos((p-i)*acos(-1.))*(-.5)+.5;
			a = mix(sin(cos(a)*a),sin(cos(1.+a)*(1.+a)), f.x);
			a.xy = mix(a.xz, a.yw, f.y);
			return mix(a.x, a.y, f.z);
		}

		float sphere(vec3 p, vec4 spr)
		{
			return length(spr.xyz-p) - spr.w;
		}

		float flame(vec3 p)
		{
			float d = sphere(p*vec3(iActivity.y,.5,1.), vec4(.0,-1.,.0,1.));
			return d + (noise(p+vec3(.0,iTime*2.,.0)) + noise(p*3.)*.5)*.25*(p.y);
		}

		float scene(vec3 p)
		{
			return min(100.-length(p) , abs(flame(p)) );
		}

		vec4 raymarch(vec3 org, vec3 dir)
		{
			float d = 0.0, glow = 0.0, eps = 0.02;
			vec3  p = org;
			bool glowed = false;
			for(int i=0; i<64; i++)
			{
				d = scene(p) + eps;
				p += d * dir;
				if( d>eps )
				{
					if(flame(p) < .0)
						glowed=true;
					if(glowed)
						glow = float(i)/64.;
				}
			}
			return vec4(p,glow);
		}

		void main()
		{
			vec2 v = -1.0 + 2.0 * fragCoord.xy / iResolution.xy;
			v.x *= iResolution.x/iResolution.y;
			vec3 org = vec3(0., -2., 4.);
			vec3 dir = normalize(vec3(v.x*1.6, -v.y, -1.5));
			vec4 p = raymarch(org, dir);
			vec4 col = mix(vec4(0.1,.5,.1,1.), vec4(0.1,.5,iActivity.x,1.), p.y*.02+.4);
			fragColor = mix(vec4(0.), col, pow(p.w*2.,4.)) * iActivity.z;
		}
		""";
    private readonly uint _width;
    private readonly uint _height;
    private float _targetActive = 0.1f;
    private float _talking = 0.0f;
    private float _targetOpacity = 0.0f;
    private float currentActive = 0.1f;
    private float currentTalking = 1.0f;
    private float currentOpacity = 0.0f;
    private GL? gl = null;
    private uint fbo;
    private uint vao;
    private uint program;
    private IWindow? window = null;
    private readonly DateTime _initTime = DateTime.Now;
    private readonly ILogger _logger;
    public RenderSize RenderSize { get; }
    public OpenGLVideoRenderer(RenderSize renderSize, ILogger logger)
    {
        _width = renderSize.Width;
        _height = renderSize.Height;
        RenderSize = renderSize;
        _logger = logger;
    }
    public unsafe void Init()
    {
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

        var vertexShader = BuildShader(gl, ShaderType.VertexShader, vertexCode);
        var fragmentShader = BuildShader(gl, ShaderType.FragmentShader, fragmentCode);

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
    private static uint BuildShader(GL gl, ShaderType shaderType, string code)
    {
        var shader = gl.CreateShader(shaderType);
        gl.ShaderSource(shader, code);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status != (int)GLEnum.True) throw new Exception($"Failed to compile {shaderType}: {gl.GetShaderInfoLog(shader)}");
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
    public unsafe void Render(byte* arrayBuffer)
    {
        if (gl == null || window == null) throw new Exception("NOT INITED!");
        if (currentActive < _targetActive) currentActive = Math.Min(currentActive + 0.1f, 1.0f);
        else if (currentActive > _targetActive) currentActive = Math.Max(currentActive - 0.1f, 0.1f);

        if (currentOpacity < _targetOpacity) currentOpacity = Math.Min(currentOpacity + 0.05f, 1.0f);
        else if (currentOpacity > _targetOpacity) currentOpacity = Math.Max(currentOpacity - 0.05f, 0.0f);

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
        gl.Uniform3(ul3, currentActive, currentTalking, currentOpacity);

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
    public void SetAs(bool active) => _targetActive = active ? 1.0f : 0.1f;
    public void SetAmp(float value) => _talking = Math.Clamp(value, 0.0f, 1.0f);
    public void FadeIn() => _targetOpacity = 1.0f;
    public void FadeOut() => _targetOpacity = 0.0f;
}
