此文件夹包含程序所使用的Shader文件和渲染管线脚本。
在github上提issue。https://github.com/sselecirPyM/Coocoo3D

一些帮助(2022/04/07)：


    public class UnionShaderParam
    {
        public RenderPipelineContext rp;
        public public RenderPipelineDynamicContext drp { get; };
        public RuntimeMaterial material;
        public PassSetting passSetting;

        public List<MMDRendererComponent> renderers;
        public List<MeshRendererComponent> meshRenderers;

        public List<DirectionalLightData> directionalLights;
        public List<PointLightData> pointLights;

        public RenderSequence renderSequence;
        public Pass pass;
        
        public GraphicsContext graphicsContext;
        public VisualChannel visualChannel;
        public string passName;
        public string relativePath;
        public GPUWriter GPUWriter;
        public Core.Settings settings;
        public Texture2D[] renderTargets;
        public Texture2D depthStencil;
        public MainCaches mainCaches;

        public Texture2D texLoading;
        public Texture2D texError;

        public RayTracingShader rayTracingShader;

        public GraphicsDevice graphicsDevice { get; }
        
        public string skyBoxFile { get; }

        public CameraData camera { get; }

        public bool recording { get; }

        public Dictionary<string, object> customValue;
        public Dictionary<string, object> gpuValueOverride;
        
        public T GetCustomValue<T>(string name, T defaultValue);
        public void SetCustomValue<T>(string name, T value);

        public T GetPersistentValue<T>(string name, T defaultValue);
        public void SetPersistentValue<T>(string name, T value);

        public T GetGPUValueOverride<T>(string name, T defaultValue);
        public void SetGPUValueOverride<T>(string name, T value)

        public double deltaTime { get; }
        public double realDeltaTime { get; }
        public double time { get; }

        public object GetSettingsValue(string name);
        public object GetSettingsValue(RuntimeMaterial material, string name);
        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent);
        public Texture2D GetTex2D(string name, RuntimeMaterial material = null);
        public string GetTex2DPath(string name, RenderMaterial material = null);
        public TextureCube GetTexCube(string name, RuntimeMaterial material = null);
        public GPUBuffer GetBuffer(string name, RuntimeMaterial material = null);
        public void WriteCBV(SlotRes cbv);
        public byte[] GetCBVData(SlotRes cbv);
        public void WriteGPU(List<string> datas, GPUWriter writer);

        public IEnumerable<MeshRenderable> MeshRenderables(bool setmesh = true);
        
        public void SetSRVs(List<SlotRes> SRVs, RuntimeMaterial material = null);
        public void SetUAVs(List<SlotRes> UAVs, RuntimeMaterial material = null);
        public void SRVUAVs(List<SlotRes> SRVUAV, Dictionary<int, object> dict, Dictionary<int, int> flags = null, RuntimeMaterial material = null);
        public bool SwapBuffer(string buf1, string buf2);
        public bool SwapTexture(string tex1, string tex2);
        public Texture2D TextureFallBack(Texture2D _tex) => TextureStatusSelect(_tex, texLoading, texError, texError);
        public static Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error);
        public PSODesc GetPSODesc();
        public void DispatchPass(RenderSequence sequence);
    }
    
    public interface IPassDispatcher
    {
        public void FrameBegin(RenderPipelineContext context);
        public void FrameEnd(RenderPipelineContext context);
        public void Dispatch(UnionShaderParam unionShaderParam);
    }
