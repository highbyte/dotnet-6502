using Silk.NET.OpenGL;
using System;

namespace Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers
{
    //The vertex array object abstraction.
    public class VertexArrayObject<TVertexType, TIndexType> : IDisposable
        where TVertexType : unmanaged
        where TIndexType : unmanaged
    {
        //Our handle and the GL instance this class will use, these are private because they have no reason to be public.
        //Most of the time you would want to abstract items to make things like this invisible.
        private uint _handle;
        private GL _gl;

        public VertexArrayObject(GL gl, BufferObject<TVertexType> vbo, BufferObject<TIndexType> ebo)
        {
            //Saving the GL instance.
            _gl = gl;

            //Setting out handle and binding the VBO and EBO to this VAO.
            _handle = _gl.GenVertexArray();
            Bind();
            vbo.Bind();
            ebo.Bind();
        }

        public VertexArrayObject(GL gl, BufferObject<TVertexType> vbo)
        {
            //Saving the GL instance.
            _gl = gl;

            //Setting out handle and binding the VBO and EBO to this VAO.
            _handle = _gl.GenVertexArray();
            Bind();
            vbo.Bind();
        }

        public unsafe void VertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offSet)
        {
            //Setting up a vertex attribute pointer
            _gl.VertexAttribPointer(index, count, type, false, vertexSize * (uint)sizeof(TVertexType), (void*)(offSet * sizeof(TVertexType)));
            _gl.EnableVertexAttribArray(index);
        }

        public void Bind()
        {
            _gl.BindVertexArray(_handle);
        }

        public void Unbind()
        {
            _gl.BindVertexArray(0);
        }

        public void Dispose()
        {
            //We don't delete the VBO and EBO here, as you can have one VBO stored under multiple VAO's.
            _gl.DeleteVertexArray(_handle);
        }
    }
}
