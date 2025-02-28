using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Extensions.Utility;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using Coocoo3DGraphics;
using DefaultEcs.Command;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        public EntityCommandRecorder recorder;

        [Export("UICommand", typeof(Action))]
        [ExportMetadata("MenuItem", "保存场景")]
        public void SaveScene()
        {
            UIImGui.UITaskQueue.Enqueue(new PlatformIOTask()
            {
                type = PlatformIOTaskType.SaveFile,
                title = "保存场景",
                filter = ".coocoo3DScene\0*.coocoo3DScene\0\0",
                fileExtension = "coocoo3DScene\0\0",
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
            mainCaches.ProxyCall(() =>
            {
                Dictionary<Texture2D, string> reverse = CacheUtil.MakeReverse(mainCaches);
                foreach (var texture in mainCaches.textureCaches.Values)
                {
                    texture.Dispose();
                }
                mainCaches.textureCaches.Clear();

                foreach (var obj in scene.gameObjects.Values)
                {
                    if (obj.Has<MMDRendererComponent>())
                    {
                        var r1 = obj.Get<MMDRendererComponent>();
                        foreach (var mat in r1.Materials)
                        {
                            foreach (var p in mat.Parameters.Keys)
                            {
                                if (mat.Parameters[p] is Texture2D t1)
                                {
                                    mat.Parameters[p] = mainCaches.GetTexturePreloaded(reverse[t1]);
                                }
                            }
                        }
                    }
                    if (obj.Has<MeshRendererComponent>())
                    {
                        var r1 = obj.Get<MeshRendererComponent>();
                        foreach (var mat in r1.Materials)
                        {
                            foreach (var p in mat.Parameters.Keys)
                            {
                                if (mat.Parameters[p] is Texture2D t1)
                                {
                                    mat.Parameters[p] = mainCaches.GetTexturePreloaded(reverse[t1]);
                                }
                            }
                        }
                    }
                }
            });
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

        [Export("UISceneCommand", typeof(Action))]
        [ExportMetadata("MenuItem", "复制物体")]
        public void DuplicateObject()
        {
            if (editorContext.selectedObject.IsAlive)
            {
                scene.DuplicateObject(editorContext.selectedObject);
            }
        }

        [Export("UISceneCommand", typeof(Action))]
        [ExportMetadata("MenuItem", "移除物体")]
        public void RemoveObject()
        {
            if (editorContext.selectedObject.IsAlive)
            {
                recorder.Record(editorContext.selectedObject).Dispose();
                editorContext.RemoveObjectMessage(editorContext.selectedObject);
            }
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
