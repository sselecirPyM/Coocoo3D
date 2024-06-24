using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Numerics;

namespace Coocoo3D.Extensions.UI
{
    [Export(typeof(IEditorAccess))]
    public class UICommands : IEditorAccess
    {
        public Scene scene;
        public MainCaches mainCaches;
        public RenderSystem renderSystem;
        public UIImGui uiImGui;
        public EngineContext engineContext;
        public EditorContext editorContext;

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "保存场景")]
        public void SaveScene()
        {
            UIImGui.UITaskQueue.Enqueue(new PlatformIOTask()
            {
                type = PlatformIOTaskType.SaveFile,
                filter = ".coocoo3DScene\0*.coocoo3DScene\0\0",
                fileExtension = "coocoo3DScene",
                callback = (s) =>
                {
                    var scene1 = FileFormat.Coocoo3DScene.SaveScene(scene, mainCaches);
                    SaveJsonStream(new FileInfo(s).Create(), scene1);
                }
            });
        }

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "新渲染窗口")]
        public void NewRenderWindow()
        {
            int c = 1;
            while (true)
            {
                if (!renderSystem.visualChannels.ContainsKey(c.ToString()))
                {
                    uiImGui.OpenWindow(new SceneWindow(renderSystem.AddVisualChannel(c.ToString())));
                    break;
                }
                c++;
            }
        }

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "重设相机")]
        public void ResetCamera()
        {
            if (editorContext.currentChannel == null)
                return;
            var camera = editorContext.currentChannel.camera;
            camera.LookAtPoint = new Vector3(0, 1, 0);
            camera.Distance = -4.5f;
            camera.Fov = 38.0f / 180.0f * MathF.PI;
            camera.Angle = new Vector3();
        }

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "重新加载纹理")]
        public void ReloadTexture()
        {
            engineContext.BeforeFrameBegin(mainCaches._ReloadTextures);
        }

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "重新加载着色器")]
        public void ReloadShader()
        {
            engineContext.BeforeFrameBegin(mainCaches._ReloadShaders);
        }

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "加载渲染管线")]
        public void LoadRenderPipeLine()
        {
            UIImGui.requestSelectRenderPipelines = true;
        }

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "退出程序")]
        public void Exit()
        {
            UIImGui.UITaskQueue.Enqueue(new PlatformIOTask()
            {
                type = PlatformIOTaskType.Exit
            });
        }

        [Export("UISceneCommand", typeof(Action))]
        [ExportMetadata("MenuItem", "创建光照")]
        public void CreateLighting()
        {
            var world = scene.recorder.Record(scene.world);
            var gameObject = world.CreateEntity();

            VisualComponent lightComponent = new VisualComponent();
            lightComponent.material.Type = UIShowType.Light;
            gameObject.Set(lightComponent);
            gameObject.Set(new ObjectDescription
            {
                Name = "光照",
                Description = ""
            });
            gameObject.Set(new Transform(new Vector3(0, 0, 0), Quaternion.CreateFromYawPitchRoll(0, 1.3962634015954636615389526147909f, 0)));
        }

        [Export("UISceneCommand", typeof(Action))]
        [ExportMetadata("MenuItem", "创建贴花")]
        public void CreateDecal()
        {
            var world = scene.recorder.Record(scene.world);
            var gameObject = world.CreateEntity();

            VisualComponent decalComponent = new VisualComponent();
            decalComponent.material.Type = UIShowType.Decal;
            gameObject.Set(decalComponent);
            gameObject.Set(new ObjectDescription
            {
                Name = "贴花",
                Description = ""
            });
            gameObject.Set(new Transform(new Vector3(0, 0, 0), Quaternion.CreateFromYawPitchRoll(0, -1.5707963267948966192313216916398f, 0), new Vector3(1, 1, 0.1f)));
        }

        static void SaveJsonStream<T>(Stream stream, T val)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamWriter writer = new StreamWriter(stream);
            jsonSerializer.Serialize(writer, val);
        }
    }
}
