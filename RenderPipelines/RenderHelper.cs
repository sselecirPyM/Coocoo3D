using Arch.Core;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Vortice.Direct3D12.Shader;
using Vortice.Dxc;

namespace RenderPipelines;

public class RenderHelper
{
    LinearPool<Mesh> meshPool = new();

    public Mesh quadMesh = new Mesh();
    public Mesh cubeMesh = new Mesh();

    public RenderPipeline renderPipeline;

    public Dictionary<MMDRendererComponent, Mesh> meshOverrides = new();

    public RenderPipelineView renderPipelineView;

    GraphicsContext graphicsContext => renderPipelineView.graphicsContext;

    public List<MMDRendererComponent> renderers = new List<MMDRendererComponent>();

    public List<MeshRenderable<ModelMaterial>> Renderables = new List<MeshRenderable<ModelMaterial>>();

    public void UpdateRenderables()
    {
        Renderables.Clear();
        Renderables.AddRange(MeshRenderables<ModelMaterial>());
    }

    IEnumerable<MeshRenderable<T>> MeshRenderables<T>() where T : class, new()
    {
        foreach (var renderer in renderers)
        {
            var model = renderer.model;
            var meshOverride = meshOverrides[renderer];
            for (int i = 0; i < renderer.Materials.Count; i++)
            {
                var material = renderer.Materials[i];
                var submesh = model.Submeshes[i];
                yield return GetRenderable<T>(submesh, meshOverride, renderer.LocalToWorld, material);
            }
        }
        //foreach (var renderer in rpc.meshRenderers)
        //{
        //    var model = renderer.model;
        //    var mesh = model.GetMesh();
        //    for (int i = 0; i < renderer.Materials.Count; i++)
        //    {
        //        var material = renderer.Materials[i];
        //        var submesh = model.Submeshes[i];
        //        yield return GetRenderable<T>(submesh, mesh, renderer.transform.GetMatrix(), material);
        //    }
        //}
    }

    MeshRenderable<T> GetRenderable<T>(Submesh submesh, Mesh mesh, Matrix4x4 transform, RenderMaterial material) where T : class, new()
    {
        material.Type = Caprice.Display.UIShowType.Material;
        MeshRenderable<T> renderable = new MeshRenderable<T>
        {
            indexStart = submesh.indexOffset,
            indexCount = submesh.indexCount,
            vertexStart = submesh.vertexStart,
            vertexCount = submesh.vertexCount,
            drawDoubleFace = submesh.DrawDoubleFace,
            mesh = mesh,
            transform = transform,
            material = renderPipeline.UIMaterial(material) as T
        };
        return renderable;
    }

    public void InitializeResources()
    {
        quadMesh.LoadIndex<int>(4, new int[] { 0, 1, 2, 2, 1, 3 });
        cubeMesh.LoadIndex<int>(4, new int[]
        {
            0,1,2,
            2,1,3,
            0,2,4,
            2,6,4,
            1,5,7,
            3,1,7,
            2,3,7,
            2,7,6,
            1,0,4,
            1,4,5,
            4,7,5,
            4,6,7,
        });
        var graphicsContext = renderPipelineView.graphicsContext;
        graphicsContext.UploadMesh(quadMesh);
        graphicsContext.UploadMesh(cubeMesh);
        _BasePath = renderPipelineView.BasePath;
    }
    QueryDescription rendererQuery = new QueryDescription().WithAll<MMDRendererComponent, Transform>();
    public void UpdateGPUResource()
    {
        var world = renderPipelineView.rpc.scene.world;
        renderers.Clear();
        world.Query(rendererQuery, (ref MMDRendererComponent renderer, ref Transform transform) =>
        {
            //renderer.SetTransform(transform);
            renderers.Add(renderer);
        });

        Morph();
    }

    SkinningCompute skinningCompute = new SkinningCompute();
    void Morph()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].WriteMatriticesData();
        }

        var graphicsContext = renderPipelineView.graphicsContext;
        meshPool.Reset();
        meshOverrides.Clear();

        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var model = renderer.model;
            var mesh = meshPool.Get(() => new Mesh());
            mesh.LoadIndex<int>(model.vertexCount, null);
            mesh.baseMesh = model.GetMesh();
            meshOverrides[renderer] = mesh;
            if (!renderer.skinning)
                continue;

            graphicsContext.UpdateMeshOneFrame<Vector3>(mesh, renderer.MeshPosition, MeshRenderable.POSITION);
            graphicsContext.CopyBaseMesh(mesh, MeshRenderable.NORMAL);
            graphicsContext.CopyBaseMesh(mesh, MeshRenderable.TANGENT);
            graphicsContext.EndUpdateMesh(mesh);
        }
        skinningCompute.context = this;
        Span<Matrix4x4> matrices = stackalloc Matrix4x4[1024];
        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            if (!renderer.skinning)
                continue;
            var mesh = meshOverrides[renderer];

            int matrixCount = Math.Min(renderer.BoneMatricesData.Length, 1024);
            for (int j = 0; j < matrixCount; j++)
            {
                matrices[j] = Matrix4x4.Transpose(renderer.BoneMatricesData[j]);
            }
            if (matrixCount > 0)
                skinningCompute.Execute(mesh, MemoryMarshal.AsBytes(matrices.Slice(0, matrixCount)));
        }
    }

    public void DrawQuad2(int instanceCount = 1)
    {
        var graphicsContext = renderPipelineView.graphicsContext;
        graphicsContext.SetMesh(quadMesh);
        graphicsContext.DrawIndexedInstanced2(6, instanceCount, 0, 0, 0);
    }

    public void DrawCube2(int instanceCount = 1)
    {
        var graphicsContext = renderPipelineView.graphicsContext;
        graphicsContext.SetMesh(cubeMesh);
        graphicsContext.DrawIndexedInstanced2(36, instanceCount, 0, 0, 0);
    }

    #region write object

    public static string _BasePath;

    static byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines, out ID3D12ShaderReflection reflection)
    {
        var shaderModel = DxcShaderModel.Model6_0;
        var options = new DxcCompilerOptions() { ShaderModel = shaderModel };
        var result = DxcCompiler.Compile(shaderStage, shaderCode, entryPoint, options, fileName, dxcDefines, null);
        if (result.GetStatus() != SharpGen.Runtime.Result.Ok)
        {
            string err = result.GetErrors();
            result.Dispose();
            throw new Exception(err);
        }
        byte[] resultData = result.GetResult().AsBytes();
        reflection = DxcCompiler.Utils.CreateReflection<ID3D12ShaderReflection>(result.GetOutput(DxcOutKind.Reflection));

        result.Dispose();
        return resultData;
    }

    static byte[] LoadShaderLibrary(string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines, out ID3D12LibraryReflection reflection)
    {
        var shaderModel = DxcShaderModel.Model6_3;
        var options = new DxcCompilerOptions() { ShaderModel = shaderModel };
        var result = DxcCompiler.Compile(DxcShaderStage.Library, shaderCode, entryPoint, options, fileName, dxcDefines, null);
        if (result.GetStatus() != SharpGen.Runtime.Result.Ok)
        {
            string err = result.GetErrors();
            result.Dispose();
            throw new Exception(err);
        }
        byte[] resultData = result.GetResult().AsBytes();

        reflection = DxcCompiler.Utils.CreateReflection<ID3D12LibraryReflection>(result.GetOutput(DxcOutKind.Reflection));

        result.Dispose();
        return resultData;
    }

    public static ComputeShader CreateComputeShader(string source, string entry, string fileName = null)
    {
        if (fileName != null)
        {
            fileName = Path.Combine(_BasePath, fileName);
        }
        var cs = LoadShader(DxcShaderStage.Compute, source, entry, fileName, null, out var reflection);
        return new ComputeShader(cs, reflection);
    }

    public static ComputeShader CreateComputeShader<T>(string source, string entry, T e, string fileName = null) where T : struct, Enum
    {
        var defs = GetDxcDefines(e);
        var cs = LoadShader(DxcShaderStage.Compute, source, entry, fileName, defs, out var reflection);
        return new ComputeShader(cs, reflection);
    }

    public static PSO CreatePipeline(string source, string vsEntry, string gsEntry, string psEntry, string fileName = null)
    {
        if (fileName != null)
        {
            fileName = Path.Combine(_BasePath, fileName);
        }

        ID3D12ShaderReflection vsr = null;
        ID3D12ShaderReflection gsr = null;
        ID3D12ShaderReflection psr = null;
        var vs = vsEntry != null ? LoadShader(DxcShaderStage.Vertex, source, vsEntry, fileName, null, out vsr) : null;
        var gs = gsEntry != null ? LoadShader(DxcShaderStage.Geometry, source, gsEntry, fileName, null, out gsr) : null;
        var ps = psEntry != null ? LoadShader(DxcShaderStage.Pixel, source, psEntry, fileName, null, out psr) : null;

        return new PSO(vs, gs, ps, vsr, gsr, psr);
    }

    public static PSO CreatePipeline<T>(string source, string vsEntry, string gsEntry, string psEntry, T e, string fileName = null) where T : struct, Enum
    {
        if (fileName != null)
        {
            fileName = Path.Combine(_BasePath, fileName);
        }

        ID3D12ShaderReflection vsr = null;
        ID3D12ShaderReflection gsr = null;
        ID3D12ShaderReflection psr = null;
        var defs = GetDxcDefines(e);
        var vs = vsEntry != null ? LoadShader(DxcShaderStage.Vertex, source, vsEntry, fileName, defs, out vsr) : null;
        var gs = gsEntry != null ? LoadShader(DxcShaderStage.Geometry, source, gsEntry, fileName, defs, out gsr) : null;
        var ps = psEntry != null ? LoadShader(DxcShaderStage.Pixel, source, psEntry, fileName, defs, out psr) : null;
        return new PSO(vs, gs, ps, vsr, gsr, psr);
    }

    static DxcDefine[] GetDxcDefines<T>(T e) where T : struct, Enum
    {
        var arr = Enum.GetValues<T>();
        var defs = new List<DxcDefine>();
        foreach (var a in arr)
        {
            if (Convert.ToInt32(a) == 0)
                continue;
            if (e.HasFlag(a))
                defs.Add(new DxcDefine
                {
                    Name = a.ToString(),
                    Value = "1"
                });
        }
        return defs.ToArray();
    }

    #endregion

    #region RenderResource
    public VersionedDictionary<string, RTPSO> RTPSOs = new();

    string[] GetExports(ID3D12LibraryReflection reflection)
    {
        int count = reflection.Description.FunctionCount;
        List<string> exports = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var func = reflection.GetFunctionByIndex(i);
            var description = func.Description;
            string a = description.Name[2..description.Name.IndexOf("@@")];
            exports.Add(a);
        }

        return exports.ToArray();
    }

    void GetRayTracingExports(RTPSO rtpso, string source)
    {
        List<string> exports = new List<string>();
        List<string> missingShaders = new List<string>();
        List<string> rayGenShaders = new List<string>();

        var regex = new Regex("\\w+");
        string GetName(int start)
        {
            var m1 = regex.Match(source, start);
            if (m1.Success)
            {
                string result = m1.NextMatch().Value;
                exports.Add(result);
                return result;
            }
            return null;
        }
        ACAutomaton acAutomaton = new ACAutomaton();
        acAutomaton.AddMatch("[shader(\"raygeneration\")]", (s, e) =>
        {
            rayGenShaders.Add(GetName(e));
        });
        acAutomaton.AddMatch("[shader(\"miss\")]", (s, e) =>
        {
            missingShaders.Add(GetName(e));
        });
        acAutomaton.AddMatch("[shader(\"closesthit\")]", (s, e) =>
        {
            GetName(e);
        });
        acAutomaton.BuildFail();
        acAutomaton.Search(source);
        rtpso.exports = exports.ToArray();
        rtpso.missShaders = missingShaders.ToArray();
        rtpso.rayGenShaders = rayGenShaders.ToArray();
    }

    public RTPSO GetRTPSO(IReadOnlyList<(string, string)> keywords, HitGroupDescription[] hitGroups, string path)
    {
        string xPath;
        if (keywords != null)
        {
            //keywords.Sort((x, y) => x.CompareTo(y));
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(path);
            foreach (var keyword in keywords)
            {
                stringBuilder.Append(keyword.Item1);
                stringBuilder.Append(keyword.Item2);
            }
            xPath = stringBuilder.ToString();
        }
        else
        {
            xPath = path;
        }
        return GetT(RTPSOs, xPath, path, file =>
        {
            try
            {
                string source = File.ReadAllText(file.FullName);
                DxcDefine[] dxcDefines = null;
                if (keywords != null)
                {
                    dxcDefines = new DxcDefine[keywords.Count];
                    for (int i = 0; i < keywords.Count; i++)
                    {
                        dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                    }
                }
                byte[] result = LoadShaderLibrary(source, "", path, dxcDefines, out var reflection);

                RTPSO rtpso = new RTPSO();
                rtpso.datas = result;
                rtpso.libraryReflection = reflection;
                rtpso.hitGroups = hitGroups;

                rtpso.exports = GetExports(reflection);
                return rtpso;
            }
            catch (Exception e)
            {
                Console.WriteLine(path);
                Console.WriteLine(e);
                return null;
            }
        });
    }
    public T GetT<T>(VersionedDictionary<string, T> caches, string path, string realPath, Func<FileInfo, T> createFun) where T : class
    {
        if (!caches.TryGetValue(path, out var file))
        {
            try
            {
                FileInfo fileInfo = new FileInfo(realPath);
                file = createFun(fileInfo);
                caches[path] = file;
            }
            catch (Exception e)
            {
                if (file is IDisposable disposable)
                    disposable?.Dispose();
                file = null;
                caches[path] = file;
                Console.WriteLine(e.Message);
            }
        }
        return file;
    }

    #endregion

    #region

    public void SetPSO(PSO pso, PSODesc desc)
    {
        var renderTargets = renderPipelineView.RenderTargets;
        if (pso.pixelShader != null && renderTargets.Count > 0)
            desc.rtvFormat = renderTargets[0].GetFormat();
        else
            desc.rtvFormat = Vortice.DXGI.Format.Unknown;
        desc.renderTargetCount = renderTargets.Count;
        graphicsContext.SetPSO(pso, desc);
    }

    #endregion
    public void Dispose()
    {
        meshOverrides.Clear();
        quadMesh?.Dispose();
        cubeMesh?.Dispose();
        skinningCompute.Dispose();
        foreach (var obj in meshPool.list1)
        {
            obj.Dispose();
        }
        foreach (var rtc in RTPSOs)
        {
            rtc.Value?.Dispose();
        }
        RTPSOs.Clear();
    }
}
