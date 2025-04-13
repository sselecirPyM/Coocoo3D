using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;

namespace RenderPipelines
{
    public partial class SkinningCompute
    {
        public void Execute(Mesh mesh, ReadOnlySpan<byte> matrices)
        {
            context.SetPSO(shader_skinning);
            var graphicsContext = context.renderPipelineView.graphicsContext;
			var data = matrices.ToArray();


            graphicsContext.SetComputeResources(ct =>
            {
                ct.SetUAV(0, mesh, "POSITION0");
                ct.SetUAV(1, mesh, "NORMAL0");
                ct.SetUAV(2, mesh, "TANGENT0");
                ct.SetSRV<byte>(0, data);
                ct.SetSRV(1, mesh, "BONES0");
				ct.SetSRV(2, mesh, "WEIGHTS0");
            });
            graphicsContext.Dispatch((mesh.GetVertexCount() + 63) / 64, 1, 1);
        }
        public void Dispose()
        {
            shader_skinning?.Dispose();
        }
        VariantComputeShader shader_skinning = new VariantComputeShader("""
StructuredBuffer<float4x4> _matrices : register(t0);
StructuredBuffer<uint2> _boneIndice : register(t1);
StructuredBuffer<float4> _boneWeight : register(t2);
RWStructuredBuffer<float3> _position : register(u0);
RWStructuredBuffer<float3> _normal : register(u1);
RWStructuredBuffer<float4> _tangent : register(u2);

#define MAX_BONE_MATRICES 1024
[numthreads(64, 1, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	uint id = dtid.x;
	uint width;
	uint stride;
	_position.GetDimensions(width, stride);
	if(id >= width)
	{
		return;
	}
	uint boneI[4];
	boneI[0] = _boneIndice[id].x & 0xFFFF;
	boneI[1] = (_boneIndice[id].x >> 16) & 0xFFFF;
	boneI[2] = _boneIndice[id].y & 0xFFFF;
	boneI[3] = (_boneIndice[id].y >> 16) & 0xFFFF;
	
	matrix mx = {
		0,0,0,0,
		0,0,0,0,
		0,0,0,0,
		0,0,0,0 };
	for (int i = 0; i < 4; i++)
	{
		uint iBone = iBone = boneI[i];
		float fWeight = _boneWeight[id][i];
		if (iBone < MAX_BONE_MATRICES)
		{
			mx += fWeight * _matrices[iBone];
		}
	}

	float3 position = _position[id];
	float3 normal = _normal[id];
	float4 tangent = _tangent[id];
	_position[id] = mul(float4(position, 1), mx).xyz;
	_normal[id] = mul(float4(normal, 0), mx).xyz;
	_tangent[id] = float4(mul(float4(tangent.xyz, 0), mx).xyz, tangent.w);
}
			
""", "csmain", null);
        public RenderHelper context;
    }
}
