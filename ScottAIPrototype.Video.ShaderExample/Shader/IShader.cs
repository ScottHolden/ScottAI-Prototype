using Silk.NET.OpenGL;

namespace ScottAIPrototype;

internal interface IShader
{
    ShaderType ShaderType { get; }
    string Source { get; }
}
