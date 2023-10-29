using System.Numerics;
using Silk.NET.OpenGL;

namespace Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers
{
    public class Shader : IDisposable
    {
        private uint _programHandle;
        public uint Handle => _programHandle;
        private GL _gl;

        public Shader(GL gl, string? vertexShaderPath = null, string? geometryShaderPath = null, string? fragmentShaderPath = null)
        {
            _gl = gl;
            _programHandle = _gl.CreateProgram();

            uint? vertexShaderHandle = null;
            if (!string.IsNullOrEmpty(vertexShaderPath))
            {
                vertexShaderHandle = LoadShader(ShaderType.VertexShader, vertexShaderPath);
                _gl.AttachShader(_programHandle, vertexShaderHandle.Value);
            }

            uint? geometryShaderHandle = null;
            if (!string.IsNullOrEmpty(geometryShaderPath))
            {
                geometryShaderHandle = LoadShader(ShaderType.GeometryShader, geometryShaderPath);
                _gl.AttachShader(_programHandle, geometryShaderHandle.Value);
            }

            uint? fragmentShaderHandle = null;
            if (!string.IsNullOrEmpty(fragmentShaderPath))
            {
                fragmentShaderHandle = LoadShader(ShaderType.FragmentShader, fragmentShaderPath);
                _gl.AttachShader(_programHandle, fragmentShaderHandle.Value);
            }


            _gl.LinkProgram(_programHandle);
            _gl.GetProgram(_programHandle, GLEnum.LinkStatus, out var status);
            if (status == 0)
                throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(_programHandle)}");

            if (vertexShaderHandle != null)
            {
                _gl.DetachShader(_programHandle, vertexShaderHandle.Value);
                _gl.DeleteShader(vertexShaderHandle.Value);
            }
            if (geometryShaderHandle != null)
            {
                _gl.DetachShader(_programHandle, geometryShaderHandle.Value);
                _gl.DeleteShader(geometryShaderHandle.Value);
            }
            if (fragmentShaderHandle != null)
            {
                _gl.DetachShader(_programHandle, fragmentShaderHandle.Value);
                _gl.DeleteShader(fragmentShaderHandle.Value);
            }
        }

        public void Use()
        {
            _gl.UseProgram(_programHandle);
        }

        public void SetUniform(string name, int value, bool skipExistCheck = false)
        {
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform1(location, value);
        }

        public void SetUniform(string name, uint value, bool skipExistCheck = false)
        {
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform1(location, value);
        }

        public void SetUniform(string name, bool value, bool skipExistCheck = false)
        {
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform1(location, value ? 1 : 0);
        }

        public unsafe void SetUniform(string name, Matrix4x4 value, bool skipExistCheck = false)
        {
            //A new overload has been created for setting a uniform so we can use the transform in our shader.
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.UniformMatrix4(location, 1, false, (float*)&value);
        }

        public unsafe void SetUniform(string name, Vector4 value, bool skipExistCheck = false)
        {
            //A new overload has been created for setting a uniform so we can use the transform in our shader.
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
        }

        public unsafe void SetUniform(string name, Vector3 value, bool skipExistCheck = false)
        {
            //A new overload has been created for setting a uniform so we can use the transform in our shader.
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }

        public unsafe void SetUniform(string name, Vector2 value, bool skipExistCheck = false)
        {
            //A new overload has been created for setting a uniform so we can use the transform in our shader.
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (!skipExistCheck && location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform2(location, value.X, value.Y);
        }

        public void SetUniform(string name, float value)
        {
            var location = _gl.GetUniformLocation(_programHandle, name);
            if (location == -1)
                throw new Exception($"{name} uniform not found on shader.");
            _gl.Uniform1(location, value);
        }

        public void BindUBO(string uniformBlockName, uint uboHandle, uint binding_point_index = 0)
        {
            var block_index = _gl.GetUniformBlockIndex(_programHandle, uniformBlockName);
            _gl.BindBufferBase(BufferTargetARB.UniformBuffer, binding_point_index, uboHandle);
            _gl.UniformBlockBinding(_programHandle, block_index, binding_point_index);
        }

        public void BindUBO(string uniformBlockName, BufferInfo bufferInfo, uint binding_point_index = 0)
        {
            var block_index = _gl.GetUniformBlockIndex(_programHandle, uniformBlockName);
            _gl.BindBufferBase(BufferTargetARB.UniformBuffer, binding_point_index, bufferInfo.Handle);
            _gl.UniformBlockBinding(_programHandle, block_index, binding_point_index);
        }

        public void BindUBO<TData>(string uniformBlockName, BufferObject<TData> bufferObject, uint binding_point_index = 0) where TData : unmanaged
        {
            BindUBO(uniformBlockName, bufferObject.BufferInfo, binding_point_index);
        }

        public int GetAttribLocation(string attribName)
        {
            return _gl.GetAttribLocation(_programHandle, attribName);
        }

        public void Dispose()
        {
            _gl.DeleteProgram(_programHandle);
        }

        private uint LoadShader(ShaderType type, string path)
        {
            var src = File.ReadAllText(path);
            var handle = _gl.CreateShader(type);
            _gl.ShaderSource(handle, src);
            _gl.CompileShader(handle);
            var infoLog = _gl.GetShaderInfoLog(handle);
            if (!string.IsNullOrWhiteSpace(infoLog))
                throw new Exception($"Error compiling shader of type {type}, failed with error {infoLog}");

            return handle;
        }
    }
}
