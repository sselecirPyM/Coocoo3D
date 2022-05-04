using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using FireRender.AMD.RenderEngine.Core;
using ProRendererWrap;

public class DisptcherProRender : IPassDispatcher, IDisposable
{
    RPRContext context;
    RPRScene scene;
    RPRCamera camera;
    RPRMaterialSystem materialSystem;

    string skyBoxFile;
    RPRImage skyBoxImage;
    RPRFrameBuffer frameBuffer;
    RPRFrameBuffer frameBufferResolved;

    Vector2 size;

    Dictionary<string, RPRImage> images = new Dictionary<string, RPRImage>();

    byte[] frameBuffer1 = new byte[2048 * 2048 * 16];

    public DisptcherProRender()
    {
        context = new RPRContext();
        Console.WriteLine("Radeon Prorender.");
        Console.WriteLine(context.GetInfo(Rpr.ContextInfo.GPU0_NAME));

        camera = new RPRCamera(context);

        materialSystem = new RPRMaterialSystem(context);
    }

    public void FrameBegin(RenderPipelineContext context)
    {
    }

    public void FrameEnd(RenderPipelineContext context)
    {

    }
    public void Dispatch(UnionShaderParam param)
    {
        param.CPUSkinning = true;
        if (skyBoxFile != param.skyBoxFile)
        {
            skyBoxFile = param.skyBoxFile;
            skyBoxImage?.Dispose();
            skyBoxImage = new RPRImage(context, skyBoxFile);
        }
        var outputTex = param.GetTex2D("_Result");
        if (size != new Vector2(outputTex.width, outputTex.height))
        {
            size = new Vector2(outputTex.width, outputTex.height);
            var desc = new Rpr.FrameBufferDesc()
            {
                FbWidth = (uint)size.X,
                FbHeight = (uint)size.Y
            };
            var format = new Rpr.FramebufferFormat()
            {
                NumComponents = 4,
                Type = (uint)Rpr.ComponentType.FLOAT32
            };
            frameBuffer?.Dispose();
            frameBufferResolved?.Dispose();
            frameBuffer = new RPRFrameBuffer(context, format, desc);
            frameBufferResolved = new RPRFrameBuffer(context, format, desc);
        }
        scene = new RPRScene(context);
        context.SetScene(scene);
        Vector3 angle = param.camera.Angle;
        camera.SetNearPlane(param.camera.near);
        camera.SetFarPlane(param.camera.far);
        camera.LookAt(param.camera.Position, param.camera.LookAtPoint, Vector3.Transform(Vector3.UnitY, Matrix4x4.CreateFromYawPitchRoll(-angle.Y, -angle.X, -angle.Z)));
        scene.SetCamera(camera);

        List<RPRShape> shapes = new List<RPRShape>();
        List<RPRLight> lights = new List<RPRLight>();
        List<RPRMaterialNode> materialNodes = new List<RPRMaterialNode>();
        var envLight = RPRLight.EnvLight(context);
        envLight.EnvironmentLightSetImage(skyBoxImage);
        envLight.EnvironmentLightSetIntensityScale((float)param.GetSettingsValue("IndirectMultiplier") / (float)Math.PI);
        envLight.SetTransform(Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateRotationY((float)Math.PI));
        scene.AttachLight(envLight);
        lights.Add(envLight);

        foreach (var light in param.directionalLights)
        {
            var light1 = RPRLight.DirectionalLight(context);
            light1.SetTransform(Matrix4x4.CreateLookAt(light.Direction, new Vector3(0, 0, 0), new Vector3(0, 1, 0)));
            light1.DirectionalLightSetRadiantPower3f(light.Color.X, light.Color.Y, light.Color.Z);
            light1.DirectionalLightSetShadowSoftnessAngle((float)param.GetSettingsValue("DirectionalLightShadowSoftnessAngle") / 180.0f * (float)Math.PI);
            scene.AttachLight(light1);
            lights.Add(light1);
        }

        foreach (var light in param.pointLights)
        {
            var light1 = RPRLight.PointLight(context);
            light1.SetTransform(Matrix4x4.CreateTranslation(light.Position));
            light1.PointLightSetRadiantPower3f(light.Color.X, light.Color.Y, light.Color.Z);
            scene.AttachLight(light1);
            lights.Add(light1);
        }

        foreach (var renderable in param.MeshRenderables(false))
        {
            renderable.mesh.TryGetBuffer(0, out byte[] vertexBuf);
            renderable.mesh.TryGetBuffer(1, out byte[] normalBuf);
            renderable.mesh.TryGetBuffer(2, out byte[] uvBuf);
            if (!renderable.gpuSkinning && renderable.meshOverride != null)
            {
                renderable.meshOverride.TryGetBuffer(0, out vertexBuf);
                renderable.meshOverride.TryGetBuffer(1, out normalBuf);
            }
            var indiceBuf = renderable.mesh.m_indexData;
            var vertStart = renderable.vertexStart;
            var vertCount = renderable.vertexCount;
            var indexStart = renderable.indexStart;
            var indexCount = renderable.indexCount;

            RPRShape shape = new RPRShape(context,
                new Span<byte>(vertexBuf, vertStart * 12, vertCount * 12),
                new Span<byte>(normalBuf, vertStart * 12, vertCount * 12),
                new Span<byte>(uvBuf, vertStart * 8, vertCount * 8),
                new Span<byte>(indiceBuf, indexStart * 4, indexCount * 4), indexCount, true);
            shape.SetTransform(renderable.transform);
            scene.AttachShape(shape);
            shapes.Add(shape);
            RPRMaterialNode materialNode = new RPRMaterialNode(materialSystem, Rpr.MaterialNodeType.UBERV2);
            RPRMaterialNode colorTexNode = GetImageNode(param.GetTex2DPath("_Albedo", renderable.material));

            RPRMaterialNode roughnessTexNode = GetImageNode(param.GetTex2DPath("_Roughness", renderable.material));
            RPRMaterialNode metallicTexNode = GetImageNode(param.GetTex2DPath("_Metallic", renderable.material));
            RPRMaterialNode emissiveTexNode = GetImageNode(param.GetTex2DPath("_Emissive", renderable.material));

            materialNodes.Add(colorTexNode);
            materialNodes.Add(materialNode);

            float Metallic = (float)param.GetSettingsValue(renderable.material, "Metallic");
            float Roughness = (float)param.GetSettingsValue(renderable.material, "Roughness");
            float Emissive = (float)param.GetSettingsValue(renderable.material, "Emissive");
            bool refraction = (bool)param.GetSettingsValue(renderable.material, "Refraction");
            float IOR = (float)param.GetSettingsValue(renderable.material, "IOR");

            materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_DIFFUSE_COLOR, colorTexNode);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_DIFFUSE_ROUGHNESS, Roughness, Roughness, Roughness, Roughness);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_DIFFUSE_WEIGHT, 1.0f, 1.0f, 1.0f, 1.0f);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_IOR, IOR, IOR, IOR, IOR);
            materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFLECTION_COLOR, colorTexNode);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_METALNESS, Metallic, Metallic, Metallic, Metallic);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_ROUGHNESS, Roughness, Roughness, Roughness, Roughness);
            materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFLECTION_WEIGHT, 1.0f, 1.0f, 1.0f, 1.0f);
            if (!refraction)
                materialNode.SetInputUByKey(Rpr.MaterialInput.UBER_REFLECTION_MODE, (uint)Rpr.UberMaterialMode.METALNESS);
            if (roughnessTexNode != null)
            {
                materialNodes.Add(roughnessTexNode);
                RPRMaterialNode selectY = new RPRMaterialNode(materialSystem, Rpr.MaterialNodeType.ARITHMETIC);
                selectY.SetInputUByKey(Rpr.MaterialInput.OP, (uint)Rpr.MaterialNodeOp.SELECT_Y);
                selectY.SetInputNByKey(Rpr.MaterialInput.COLOR0, roughnessTexNode);
                materialNodes.Add(selectY);

                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_DIFFUSE_ROUGHNESS, selectY);
                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFLECTION_ROUGHNESS, selectY);
            }
            if (metallicTexNode != null)
            {
                materialNodes.Add(metallicTexNode);
                RPRMaterialNode selectZ = new RPRMaterialNode(materialSystem, Rpr.MaterialNodeType.ARITHMETIC);
                selectZ.SetInputUByKey(Rpr.MaterialInput.OP, (uint)Rpr.MaterialNodeOp.SELECT_Z);
                selectZ.SetInputNByKey(Rpr.MaterialInput.COLOR0, metallicTexNode);
                materialNodes.Add(selectZ);
                if (!refraction)
                    materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFLECTION_METALNESS, selectZ);
            }
            if (emissiveTexNode != null)
            {
                materialNodes.Add(emissiveTexNode);
                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_EMISSION_COLOR, emissiveTexNode);
                //materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_EMISSION_WEIGHT, Emissive, Emissive, Emissive, Emissive);
                //materialNode.SetInputUByKey(Rpr.MaterialInput.UBER_EMISSION_MODE,Rpr.UberMaterialEmissionMode.);
            }
            if (refraction)
            {
                materialNode.SetInputNByKey(Rpr.MaterialInput.UBER_REFRACTION_COLOR, colorTexNode);
                //materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFRACTION_COLOR, refractionColor.X, refractionColor.Y, refractionColor.Z, 1);
                materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFRACTION_IOR, IOR, IOR, IOR, IOR);
                materialNode.SetInputFByKey(Rpr.MaterialInput.UBER_REFRACTION_WEIGHT, 1, 1, 1, 1);
            }
            shape.SetMaterial(materialNode);
        }

        int ViewportSampleCount = Math.Clamp((int)param.GetSettingsValue("ViewportSampleCount"), 1, 8);
        int RecordSampleCount = (int)param.GetSettingsValue("RecordSampleCount");

        context.SetParameterByKey1u(Rpr.ContextInfo.TONE_MAPPING_TYPE, (uint)Rpr.ToneMappingOperator.NONE);
        context.SetParameterByKey1u(Rpr.ContextInfo.ITERATIONS, param.recording ? (uint)RecordSampleCount : (uint)ViewportSampleCount);
        frameBuffer.Clear();
        frameBufferResolved.Clear();
        context.SetAOV(Rpr.Aov.COLOR, frameBuffer);
        context.Render();
        context.ResolveFrameBuffer(frameBuffer, frameBufferResolved, false);
        frameBufferResolved.GetInfo(Rpr.FrameBuffer.DATA, frameBuffer1, out int dataSize);

        scene.Clear();
        foreach (var shape in shapes)
        {
            scene.DetachShape(shape);
            shape.Dispose();
        }
        foreach (var light in lights)
        {
            scene.DetachLight(light);
            light.Dispose();
        }
        foreach (var materialNode in materialNodes)
        {
            materialNode.Dispose();
        }
        scene.Dispose();
        param.graphicsContext.UploadTexture(param.GetTex2D("_Result"), frameBuffer1);
        foreach (var renderSequence in param.RenderSequences())
        {
            param.DispatchPass(renderSequence);
        }
    }

    public void Dispose()
    {
        foreach (var image in images)
        {
            image.Value.Dispose();
        }
        images.Clear();
        materialSystem?.Dispose();
        camera?.Dispose();
        scene?.Dispose();
        context?.Dispose();
    }

    RPRMaterialNode GetImageNode(string fileName, Rpr.MaterialNodeType nodetype = Rpr.MaterialNodeType.IMAGE_TEXTURE)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        var image = GetImage(fileName);
        if (image == null)
            return null;
        RPRMaterialNode materialNode = new RPRMaterialNode(materialSystem, nodetype);
        materialNode.SetInputImageDataByKey(Rpr.MaterialInput.DATA, image);
        return materialNode;
    }

    RPRImage GetImage(string fileName)
    {
        if (images.TryGetValue(fileName, out var image))
        {
            return image;
        }
        else
        {
            image = new RPRImage(context, fileName);
            images.Add(fileName, image);
            return image;
        }
    }
}
