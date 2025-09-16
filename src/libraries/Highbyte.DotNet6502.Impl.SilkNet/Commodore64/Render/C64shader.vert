#version 330 core

layout(location = 0) in vec3 vPosition;

out vec2 fViewPortPos;

void main(void)
{
    gl_Position = vec4(vPosition, 1.0);
    fViewPortPos = gl_Position.xy/gl_Position.w;
}