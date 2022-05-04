using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.UI.Attributes;
using Coocoo3D.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Coocoo3D.UI
{
    static class UIImGui
    {
        public static void GUI(Coocoo3DMain main)
        {
            if (!initialized)
            {
                InitTex(main);
                InitKeyMap();
                initialized = true;
            }
            var io = ImGui.GetIO();
            Vector2 mouseMoveDelta = new Vector2();
            while (main.imguiInput.mouseMoveDelta.TryDequeue(out var moveDelta))
            {
                mouseMoveDelta += moveDelta;
            }

            var context = main.RPContext;
            io.DisplaySize = new Vector2(context.swapChain.width, context.swapChain.height);
            io.DeltaTime = (float)context.dynamicContextRead.RealDeltaTime;
            GameObject selectedObject = null;

            positionChange = false;
            rotationChange = false;
            if (main.SelectedGameObjects.Count == 1)
            {
                selectedObject = main.SelectedGameObjects[0];
                position = selectedObject.Transform.position;
                if (rotationCache != selectedObject.Transform.rotation)
                {
                    rotation = QuaternionToEularYXZ(selectedObject.Transform.rotation);
                    rotationCache = selectedObject.Transform.rotation;
                }
            }


            ImGui.NewFrame();

            if (demoWindowOpen)
                ImGui.ShowDemoWindow(ref demoWindowOpen);

            DockSpace(main);
            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("常用"))
            {
                Common(main);
            }
            ImGui.End();
            ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("资源"))
            {
                var _openRequest = Resources();
                if (openRequest == null)
                    openRequest = _openRequest;
            }
            ImGui.End();
            ImGui.SetNextWindowPos(new Vector2(800, 0), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("设置"))
            {
                SettingsPanel(main);
            }
            ImGui.End();
            ImGui.SetNextWindowSize(new Vector2(350, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(750, 0), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("场景层级"))
            {
                SceneHierarchy(main);
            }
            ImGui.End();
            int d = 0;
            foreach (var visualChannel in main.RPContext.visualChannels.Values)
            {
                ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(new Vector2(300 + d, 0), ImGuiCond.FirstUseEver);
                if (visualChannel.Name != "main")
                {
                    bool open = true;
                    if (ImGui.Begin(string.Format("场景视图 - {0}###SceneView/{0}", visualChannel.Name), ref open))
                    {
                        SceneView(main, visualChannel, io.MouseWheel, mouseMoveDelta);
                    }
                    if (!open)
                    {
                        context.DelayRemoveVisualChannel(visualChannel.Name);
                    }
                }
                else
                {
                    if (ImGui.Begin(string.Format("场景视图 - {0}###SceneView/{0}", visualChannel.Name)))
                    {
                        SceneView(main, visualChannel, io.MouseWheel, mouseMoveDelta);
                    }
                }
                ImGui.End();
                d += 50;
            }
            ImGui.SetNextWindowSize(new Vector2(300, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(0, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("物体"))
            {
                if (selectedObject != null)
                {
                    GameObjectPanel(main, selectedObject);
                }
            }
            ImGui.End();
            Popups(main, selectedObject);
            ImGui.Render();
            if (selectedObject != null)
            {
                bool transformChange = rotationChange || positionChange;
                if (rotationChange)
                {
                    rotationCache = Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
                }
                if (transformChange)
                {
                    main.CurrentScene.setTransform[selectedObject] = new(position, rotationCache);
                }
            }
        }

        static void Common(Coocoo3DMain main)
        {
            var camera = main.RPContext.currentChannel.camera;
            if (ImGui.TreeNode("transform"))
            {
                if (ImGui.DragFloat3("位置", ref position, 0.01f))
                {
                    positionChange = true;
                }
                Vector3 a = rotation / MathF.PI * 180;
                if (ImGui.DragFloat3("旋转", ref a))
                {
                    rotation = a * MathF.PI / 180;
                    rotationChange = true;
                }
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("相机"))
            {
                ImGui.DragFloat("距离", ref camera.Distance);
                ImGui.DragFloat3("焦点", ref camera.LookAtPoint, 0.05f);
                Vector3 a = camera.Angle / MathF.PI * 180;
                if (ImGui.DragFloat3("角度", ref a))
                    camera.Angle = a * MathF.PI / 180;
                float fov = camera.Fov / MathF.PI * 180;
                if (ImGui.DragFloat("FOV", ref fov, 0.5f, 0.1f, 179.9f))
                    camera.Fov = fov * MathF.PI / 180;
                ImGui.DragFloat("近裁剪", ref camera.nearClip, 0.5f, 0.1f, float.MaxValue);
                ImGui.DragFloat("远裁剪", ref camera.farClip, 10.0f, 0.1f, float.MaxValue);

                ImGui.Checkbox("使用镜头运动文件", ref camera.CameraMotionOn);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("录制"))
            {
                var recordSettings = main.RPContext.recordSettings;
                ImGui.DragFloat("开始时间", ref recordSettings.StartTime);
                ImGui.DragFloat("结束时间", ref recordSettings.StopTime);
                ImGui.DragInt("宽度", ref recordSettings.Width, 32, 32, 16384);
                ImGui.DragInt("高度", ref recordSettings.Height, 8, 8, 16384);
                ImGui.DragFloat("FPS", ref recordSettings.FPS, 1, 1, 1000);
                if (ImGui.Button("开始录制"))
                {
                    requestRecord = true;
                }
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("帮助"))
            {
                Help();
                ImGui.TreePop();
            }
            if (ImGui.Button("播放"))
            {
                PlayControl.Play(main);
            }
            ImGui.SameLine();
            if (ImGui.Button("暂停"))
            {
                PlayControl.Pause(main);
            }
            ImGui.SameLine();
            if (ImGui.Button("停止"))
            {
                PlayControl.Stop(main);
            }
            if (ImGui.Button("跳到最前"))
            {
                PlayControl.Front(main);
            }
            ImGui.SameLine();
            if (ImGui.Button("重置物理"))
            {
                main.GameDriverContext.RequireResetPhysics = true;
            }
            if (ImGui.Button("快进"))
            {
                PlayControl.FastForward(main);
            }
            ImGui.Text(String.Format("Fps:{0:f1}", main.framePerSecond));
        }

        static void SettingsPanel(Coocoo3DMain main)
        {
            ImGui.Checkbox("垂直同步", ref main.performanceSettings.VSync);
            ImGui.Checkbox("节省CPU", ref main.performanceSettings.SaveCpuPower);
            ImGui.Checkbox("多线程渲染", ref main.performanceSettings.MultiThreadRendering);
            float a = (float)(1.0 / Math.Clamp(main.frameInterval, 1e-4, 1));
            if (ImGui.DragFloat("帧率限制", ref a, 10, 1, 5000))
            {
                main.frameInterval = Math.Clamp(1 / a, 1e-4f, 1f);
            }
            var scene = main.CurrentScene;
            ref Settings settings = ref scene.settings;
            ImGui.Checkbox("线框", ref settings.Wireframe);
            ImGui.SliderInt("天空盒最高质量", ref settings.SkyBoxMaxQuality, 256, 2048);

            ComboBox("调试渲染", ref scene.settings.DebugRenderType);

            var rpc = main.RPContext;
            ImGui.Checkbox("新渲染管线", ref rpc.NewRenderPipeline);
            if (rpc.NewRenderPipeline)
            {
                ShowParam1(main, rpc.currentChannel.renderPipelineView);
                int renderPipelineIndex = 0;
                string[] newRPs = new string[rpc.RenderPipelineTypes.Length];
                for (int i = 0; i < newRPs.Length; i++)
                {
                    newRPs[i] = rpc.RenderPipelineTypes[i].ToString();
                    if (rpc.RenderPipelineTypes[i] == rpc.currentChannel.renderPipeline?.GetType())
                    {
                        renderPipelineIndex = i;
                    }
                }

                if (ImGui.Combo("渲染管线", ref renderPipelineIndex, newRPs, newRPs.Length))
                {
                    rpc.currentChannel.DelaySetRenderPipeline(rpc.RenderPipelineTypes[renderPipelineIndex], rpc, rpc.rpBasePth);
                }
            }
            else
            {
                var passSetting = rpc.dynamicContextRead.passSetting;
                ShowParams(passSetting.ShowSettingParameters, settings.Parameters);
                ShowTextures(main, "settings", passSetting.ShowSettingTextures, settings.Parameters);
                int renderPipelineIndex = 0;
                var passSettings = main.mainCaches.PassSettings;
                if (passSettings.Count != renderPipelines.Length)
                {
                    renderPipelines = new string[passSettings.Count];
                    renderPipelineKeys = new string[passSettings.Count];
                }
                int _i = 0;
                foreach (var pair in passSettings)
                {
                    renderPipelines[_i] = pair.Value.Name;
                    renderPipelineKeys[_i] = pair.Key;
                    _i++;
                }
                for (int i = 0; i < renderPipelineKeys.Length; i++)
                {
                    if (renderPipelineKeys[i] == rpc.currentPassSetting)
                        renderPipelineIndex = i;
                }
                if (ImGui.Combo("渲染管线", ref renderPipelineIndex, renderPipelines, renderPipelines.Length))
                {
                    rpc.currentPassSetting = renderPipelineKeys[renderPipelineIndex];
                }
            }

            if (ImGui.Button("添加视口"))
            {
                int c = 1;
                while (true)
                {
                    if (!main.RPContext.visualChannels.ContainsKey(c.ToString()))
                    {
                        main.RPContext.DelayAddVisualChannel(c.ToString());
                        break;
                    }
                    c++;
                }
            }
            if (ImGui.Button("保存场景"))
            {
                requestSave = true;
            }
            if (ImGui.Button("重新加载纹理"))
            {
                main.mainCaches.ReloadTextures = true;
            }
            if (ImGui.Button("重新加载Shader"))
            {
                main.mainCaches.ReloadShaders = true;
            }
            ImGui.TextUnformatted("绘制三角形数：" + main.drawTriangleCount); ;
        }

        static void ShowParam1(Coocoo3DMain main, RenderPipeline.RenderPipelineView view)
        {
            if (view == null) return;
            ImGui.Separator();
            string filter = ImFilter("过滤参数", "参数名称");

            var renderPipeline = view.renderPipeline;
            foreach (var param in view.UIUsages)
            {
                var val = param.Value;

                if (val.UIShowType != UIShowType.All && val.UIShowType != UIShowType.Global) continue;
                if (!Contains(val.Name, filter) && !Contains(param.Key, filter))
                    continue;

                var member = val.MemberInfo;
                object obj = member.GetValue<object>(renderPipeline);
                var type = obj.GetType();
                if (type.IsEnum)
                {
                    if (ComboBox(val.Name, ref obj))
                    {
                        member.SetValue(renderPipeline, obj);
                    }
                }
                else
                {
                    ShowParam1(main, val, view);
                }
            }
        }

        static void ShowParam1(Coocoo3DMain main, UIUsage param, RenderPipeline.RenderPipelineView view)
        {
            var renderPipeline = view.renderPipeline;
            var member = param.MemberInfo;
            object obj = member.GetValue<object>(renderPipeline);

            string displayName = param.Name;
            string name = member.Name;
            var sliderAttribute = param.sliderAttribute;
            var colorAttribute = param.colorAttribute;
            var dragFloatAttribute = param.dragFloatAttribute;
            var dragIntAttribute = param.dragIntAttribute;
            switch (obj)
            {
                case bool val:
                    if (ImGui.Checkbox(displayName, ref val))
                    {
                        member.SetValue(renderPipeline, val);
                        view.InvalidDependents(name);
                    }
                    break;
                case float val:
                    if (sliderAttribute != null)
                    {
                        if (ImGui.SliderFloat(displayName, ref val, sliderAttribute.Min, sliderAttribute.Max))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    break;
                case Vector2 val:
                    if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat2(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    break;
                case Vector3 val:
                    if (colorAttribute != null)
                    {
                        if (ImGui.ColorEdit3(displayName, ref val, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat3(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    break;
                case Vector4 val:
                    if (colorAttribute != null)
                    {
                        if (ImGui.ColorEdit4(displayName, ref val, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat4(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    break;
                case int val:
                    if (dragIntAttribute != null)
                    {
                        if (ImGui.DragInt(displayName, ref val, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                        {
                            member.SetValue(renderPipeline, val);
                            view.InvalidDependents(name);
                        }
                    }
                    break;
                case string val:
                    if (ImGui.InputText(displayName, ref val, 256))
                    {
                        member.SetValue(renderPipeline, val);
                        view.InvalidDependents(name);
                    }
                    break;
                case Coocoo3DGraphics.Texture2D tex2d:
                    view.textureReplacement.TryGetValue(name, out string rep);
                    if (ShowTexture(main, "global", name, ref rep, tex2d))
                    {
                        view.SetReplacement(name, rep);
                        view.InvalidDependents(name);
                    }
                    break;
                default:
                    ImGui.Text(displayName + " 不支持的类型");
                    break;
            }
            if (param.Description != null && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(param.Description);
                ImGui.EndTooltip();
            }
        }

        static void ShowParam1(Coocoo3DMain main, RenderPipeline.RenderPipelineView view, Dictionary<string, object> parameters)
        {
            if (view == null) return;
            ImGui.Separator();
            string filter = ImFilter("过滤参数", "参数名称");

            var renderPipeline = view.renderPipeline;
            foreach (var param in view.UIUsages)
            {
                var val = param.Value;

                if (val.UIShowType != UIShowType.All && val.UIShowType != UIShowType.Material) continue;
                if (!Contains(val.Name, filter) && !Contains(param.Key, filter))
                    continue;

                var member = val.MemberInfo;
                object obj = member.GetValue<object>(renderPipeline);
                var type = obj.GetType();
                if (type.IsEnum)
                {
                    ComboBox(val.Name, ref obj);
                }
                else
                {
                    ShowParam1(main, val, view, parameters);
                }
            }
        }

        static void ShowParam1(Coocoo3DMain main, UIUsage param, RenderPipeline.RenderPipelineView view, Dictionary<string, object> parameters)
        {
            var renderPipeline = view.renderPipeline;
            var member = param.MemberInfo;
            object obj = member.GetValue<object>(renderPipeline);
            var type = obj.GetType();

            string displayName = param.Name;
            string name = member.Name;

            bool propertyOverride = false;
            if (parameters.TryGetValue(name, out var parameter) && type == parameter.GetType())
            {
                propertyOverride = true;
                obj = parameter;
            }
            if (propertyOverride)
                ImGui.PushStyleColor(ImGuiCol.Text, 0xffaaffaa);
            var sliderAttribute = param.sliderAttribute;
            var colorAttribute = param.colorAttribute;
            var dragFloatAttribute = param.dragFloatAttribute;
            var dragIntAttribute = param.dragIntAttribute;
            switch (obj)
            {
                case bool val:
                    if (ImGui.Checkbox(displayName, ref val))
                    {
                        parameters[name] = val;
                    }
                    break;
                case float val:
                    if (param.sliderAttribute != null)
                    {
                        if (ImGui.SliderFloat(displayName, ref val, sliderAttribute.Min, sliderAttribute.Max))
                        {
                            parameters[name] = val;
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            parameters[name] = val;
                        }
                    }
                    break;
                case Vector2 val:
                    if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat2(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            parameters[name] = val;
                        }
                    }
                    break;
                case Vector3 val:
                    if (colorAttribute != null)
                    {
                        if (ImGui.ColorEdit3(displayName, ref val))
                        {
                            parameters[name] = val;
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat3(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            parameters[name] = val;
                        }
                    }
                    break;
                case Vector4 val:
                    if (colorAttribute != null)
                    {
                        if (ImGui.ColorEdit4(displayName, ref val))
                        {
                            parameters[name] = val;
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat4(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            parameters[name] = val;
                        }
                    }
                    break;
                case int val:
                    if (dragIntAttribute != null)
                    {
                        if (ImGui.DragInt(displayName, ref val, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                        {
                            parameters[name] = val;
                        }
                    }
                    break;
                case string val:
                    if (ImGui.InputText(displayName, ref val, 256))
                    {
                        parameters[name] = val;
                    }
                    break;
                case Coocoo3DGraphics.Texture2D tex2d:
                    string rep = null;
                    if (parameters.TryGetValue(name, out object o1) && o1 is string o2)
                    {
                        rep = o2;
                    }
                    if (ShowTexture(main, "global", name, ref rep, tex2d))
                    {
                        parameters[name] = rep;
                    }
                    break;
                default:
                    ImGui.Text(displayName + " 不支持的类型");
                    break;
            }
            if (propertyOverride)
                ImGui.PopStyleColor();

            if (param.Description != null && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(param.Description);
                ImGui.EndTooltip();
            }
        }

        static void ShowParams(
            Dictionary<string, RenderPipeline.PassParameter> showParams,
            Dictionary<string, object> values)
        {
            if (showParams == null) return;
            ImGui.Separator();

            string filter = ImFilter("查找参数", "搜索参数名称");

            bool tempB;
            float tempF;
            Vector2 tempF2;
            Vector3 tempF3;
            Vector4 tempF4;
            int tempI;
            foreach (var param in showParams)
            {
                var val = param.Value;
                if (!Contains(val.Name, filter) && !Contains(param.Key, filter))
                    continue;
                if (val.IsHidden)
                    continue;
                switch (val.Type)
                {
                    case "bool":
                        {
                            if (values.TryGetTypedValue(param.Key, out bool x1))
                                tempB = x1;
                            else
                                tempB = (bool)val.defaultValue;
                            if (ImGui.Checkbox(val.Name, ref tempB))
                            {
                                values[param.Key] = tempB;
                            }
                        }
                        break;
                    case "int":
                        {
                            if (values.TryGetTypedValue(param.Key, out int x1))
                                tempI = x1;
                            else
                                tempI = (int)val.defaultValue;

                            if (ImGui.DragInt(val.Name, ref tempI, 1, (int)val.minValue, (int)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempI;
                            }
                        }
                        break;
                    case "sliderInt":
                        {
                            if (values.TryGetTypedValue(param.Key, out int x1))
                                tempI = x1;
                            else
                                tempI = (int)val.defaultValue;
                            if (ImGui.SliderInt(val.Name, ref tempI, (int)val.minValue, (int)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempI;
                            }
                        }
                        break;
                    case "float":
                        {
                            if (values.TryGetTypedValue(param.Key, out float x1))
                                tempF = x1;
                            else
                                tempF = (float)val.defaultValue;
                            if (ImGui.DragFloat(val.Name, ref tempF, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempF;
                            }
                        }
                        break;
                    case "sliderFloat":
                        {
                            if (values.TryGetTypedValue(param.Key, out float x1))
                                tempF = x1;
                            else
                                tempF = (float)val.defaultValue;
                            if (ImGui.SliderFloat(val.Name, ref tempF, (float)val.minValue, (float)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempF;
                            }
                        }
                        break;
                    case "float2":
                        {
                            if (values.TryGetTypedValue(param.Key, out Vector2 x1))
                                tempF2 = x1;
                            else
                                tempF2 = (Vector2)val.defaultValue;
                            if (ImGui.DragFloat2(val.Name, ref tempF2, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempF2;
                            }
                        }
                        break;
                    case "float3":
                        {
                            if (values.TryGetTypedValue(param.Key, out Vector3 x1))
                                tempF3 = x1;
                            else
                                tempF3 = (Vector3)val.defaultValue;
                            if (ImGui.DragFloat3(val.Name, ref tempF3, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempF3;
                            }
                        }
                        break;
                    case "float4":
                        {
                            if (values.TryGetTypedValue(param.Key, out Vector4 x1))
                                tempF4 = x1;
                            else
                                tempF4 = (Vector4)val.defaultValue;
                            if (ImGui.DragFloat4(val.Name, ref tempF4, (float)val.step, (float)val.minValue, (float)val.maxValue, val.Format))
                            {
                                values[param.Key] = tempF4;
                            }
                        }
                        break;
                    case "color3":
                        {
                            if (values.TryGetTypedValue(param.Key, out Vector3 x1))
                                tempF3 = x1;
                            else
                                tempF3 = (Vector3)val.defaultValue;
                            if (ImGui.ColorEdit3(val.Name, ref tempF3, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR))
                            {
                                values[param.Key] = tempF3;
                            }
                        }
                        break;
                    case "color4":
                        {
                            if (values.TryGetTypedValue(param.Key, out Vector4 x1))
                                tempF4 = x1;
                            else
                                tempF4 = (Vector4)val.defaultValue;
                            if (ImGui.ColorEdit4(val.Name, ref tempF4, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR))
                            {
                                values[param.Key] = tempF4;
                            }
                        }
                        break;
                }
                if (val.Tooltip != null && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(val.Tooltip);
                    ImGui.EndTooltip();
                }
            }
        }

        static void ShowTextures(Coocoo3DMain main, string id, Dictionary<string, string> showTextures, Dictionary<string, object> textures)
        {
            if (showTextures == null) return;
            if (showTextures.Count == 0) return;
            string filter = ImFilter("查找纹理", "查找纹理");
            foreach (var texSlot in showTextures)
            {
                string texture0 = null;
                bool hasTexture = textures.TryGetValue(texSlot.Key, out var o1);
                if (o1 is string s0)
                    texture0 = s0;

                if (!Contains(texSlot.Key, filter) && (!hasTexture || !Contains(texture0, filter))) continue;
                if (ShowTexture(main, id, texSlot.Key, ref texture0))
                {
                    textures[texSlot.Key] = texture0;
                }
            }
        }

        static bool ShowTexture(Coocoo3DMain main, string id, string slot, ref string texPath, Coocoo3DGraphics.Texture2D texture = null)
        {
            bool textureChange = false;
            var cache = main.mainCaches;
            bool hasTexture = texPath != null && cache.TryGetTexture(texPath, out texture);

            IntPtr imageId = main.widgetRenderer.ShowTexture(texture);
            ImGui.Text(slot);
            Vector2 imageSize = new Vector2(120, 120);
            if (ImGui.ImageButton(imageId, imageSize))
            {
                StartSelectResource(id, slot);
            }
            if (CheckResourceSelect(id, slot, out string result))
            {
                cache.Texture(result);
                texPath = result;
                textureChange = true;
            }
            if (hasTexture)
            {
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(texPath);
                    ImGui.EndTooltip();
                }
            }
            return textureChange;
        }

        static void DockSpace(Coocoo3DMain main)
        {
            ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            var viewPort = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(viewPort.Size, ImGuiCond.Always);
            ImGui.SetNextWindowViewport(viewPort.ID);

            if (ImGui.Begin("Dockspace", window_flags))
            {
                var tex = main.RPContext.visualChannels.FirstOrDefault().Value.GetAOV(Caprice.Attributes.AOVType.Color);
                IntPtr imageId = main.widgetRenderer.ShowTexture(tex);
                ImGuiDockNodeFlags dockNodeFlag = ImGuiDockNodeFlags.PassthruCentralNode;
                ImGui.GetWindowDrawList().AddImage(imageId, viewPort.WorkPos, viewPort.WorkPos + viewPort.WorkSize);
                ImGui.DockSpace(ImGui.GetID("MyDockSpace"), Vector2.Zero, dockNodeFlag);
            }
            ImGui.End();
            ImGui.PopStyleVar(3);
        }

        static bool c0;
        static FileInfo Resources()
        {
            if (ImGui.Button("打开文件夹"))
            {
                requireOpenFolder = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("刷新"))
            {
                viewRequest = currentFolder;
            }
            ImGui.SameLine();
            if (ImGui.Button("后退"))
            {
                if (viewStack.Count > 0)
                    viewRequest = viewStack.Pop();
            }
            string filter = ImFilter("查找文件", "查找文件");

            ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV |
                ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable;
            if (c0)//avoid bug
                tableFlags |= ImGuiTableFlags.ScrollY;
            c0 = true;

            var windowSize = ImGui.GetWindowSize();
            var itemSize = windowSize - ImGui.GetCursorPos();
            itemSize.X = 0;
            itemSize.Y -= 8;

            ImGui.BeginTable("resources", 2, tableFlags, Vector2.Max(itemSize, new Vector2(0, 28)), 0);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("文件名");
            ImGui.TableSetupColumn("大小");
            ImGui.TableHeadersRow();
            FileInfo open1 = null;

            lock (storageItems)
            {
                bool _requireClear = false;
                foreach (var item in storageItems)
                {
                    if (!Contains(item.Name, filter))
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    DirectoryInfo folder = item as DirectoryInfo;
                    FileInfo file = item as FileInfo;
                    if (ImGui.Selectable(item.Name, false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (folder != null)
                        {
                            viewStack.Push(currentFolder);
                            viewRequest = folder;
                            _requireClear = true;
                        }
                        else if (file != null)
                        {
                            open1 = file;
                        }
                    }
                    ImGui.TableSetColumnIndex(1);
                    if (file != null)
                    {
                        ImGui.TextUnformatted(String.Format("{0} KB", (file.Length + 1023) / 1024));
                    }
                }
                if (_requireClear)
                    storageItems.Clear();
            }

            ImGui.EndTable();
            return open1;
        }

        static void Help()
        {
            if (ImGui.TreeNode("基本操作"))
            {
                ImGui.Text(@"旋转视角 - 按住鼠标右键拖动
平移镜头 - 按住鼠标中键拖动
拉近、拉远镜头 - 鼠标滚轮
修改物体位置、旋转 - 双击修改，或者在数字上按住左键然后拖动");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("支持格式"))
            {
                ImGui.Text(@"当前版本支持pmx、glTF格式模型，
vmd格式动作");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("编写着色器"))
            {
                ImGui.TextWrapped(@"首先把Samples文件夹里的内容，复制粘贴到硬盘上的其他位置，然后开始修改。
双击.coocoox文件来加载。
点击设置里的重新加载着色器来重新加载。
文件的格式每个版本都可能有些许改动。
");
                ImGui.TreePop();
            }
            if (ImGui.Button("显示ImGuiDemoWindow"))
            {
                demoWindowOpen = true;
            }
        }

        static void SceneHierarchy(Coocoo3DMain main)
        {
            if (ImGui.Button("新光源"))
            {
                NewLighting(main);
            }
            ImGui.SameLine();
            //if (ImGui.Button("新粒子"))
            //{
            //    NewParticle(main);
            //}
            //ImGui.SameLine();
            //if (ImGui.Button("新体积"))
            //{
            //    UISharedCode.NewVolume(main);
            //}
            //ImGui.SameLine();
            bool removeObject = false;
            if (ImGui.Button("移除物体"))
            {
                removeObject = true;
            }
            //while (gameObjectSelected.Count < main.CurrentScene.gameObjects.Count)
            //{
            //    gameObjectSelected.Add(false);
            //}
            string filter = ImFilter("查找物体", "查找名称");
            var gameObjects = main.CurrentScene.gameObjects;
            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (!Contains(gameObject.Name, filter)) continue;
                bool selected = gameObjectSelectIndex == i;
                bool selected1 = ImGui.Selectable(gameObject.Name + "###" + gameObject.GetHashCode(), ref selected);
                if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                {
                    int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                    if (n_next >= 0 && n_next < gameObjects.Count)
                    {
                        gameObjects[i] = gameObjects[n_next];
                        gameObjects[n_next] = gameObject;
                        ImGui.ResetMouseDragDelta();
                    }
                }
                if (selected1 || main.SelectedGameObjects.Count < 1)
                {
                    gameObjectSelectIndex = i;
                    main.SelectedGameObjects.Clear();
                    main.SelectedGameObjects.Add(gameObject);
                }
            }
            if (removeObject)
            {
                foreach (var gameObject in main.SelectedGameObjects)
                    main.CurrentScene.RemoveGameObject(gameObject);
                main.SelectedGameObjects.Clear();
                gameObjectSelectIndex = -1;
            }
        }

        static void GameObjectPanel(Coocoo3DMain main, GameObject gameObject)
        {
            var lighting = gameObject.GetComponent<LightingComponent>();
            var volumeComponent = gameObject.GetComponent<VolumeComponent>();
            var renderer = gameObject.GetComponent<MMDRendererComponent>();
            var meshRenderer = gameObject.GetComponent<MeshRendererComponent>();
            var particleEffect = gameObject.GetComponent<ParticleEffectComponent>();

            ImGui.InputText("名称", ref gameObject.Name, 256);
            if (ImGui.TreeNode("描述"))
            {
                ImGui.Text(gameObject.Description);
                if (renderer != null)
                {
                    var mesh = main.mainCaches.GetModel(renderer.meshPath).GetMesh();
                    ImGui.Text(string.Format("顶点数：{0} 索引数：{1} 材质数：{2}\n模型文件：{3}\n动作文件：{4}",
                        mesh.GetVertexCount(), mesh.GetIndexCount(), renderer.Materials.Count,
                        renderer.meshPath, renderer.motionPath));
                }

                ImGui.TreePop();
            }
            if (ImGui.TreeNode("transform"))
            {
                if (ImGui.DragFloat3("位置", ref position, 0.01f))
                {
                    positionChange = true;
                }
                Vector3 a = rotation / MathF.PI * 180;
                if (ImGui.DragFloat3("旋转", ref a))
                {
                    rotation = a * MathF.PI / 180;
                    rotationChange = true;
                }
                ImGui.TreePop();
            }
            if (renderer != null)
            {
                RendererComponent(main, renderer);
            }
            if (meshRenderer != null)
            {
                RendererComponent(main, meshRenderer);
            }
            if (lighting != null && ImGui.TreeNode("光照"))
            {
                int current = (int)lighting.LightingType;
                ImGui.ColorEdit3("颜色", ref lighting.Color, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float);
                ImGui.DragFloat("范围", ref lighting.Range);
                ImGui.Combo("类型", ref current, lightTypeString, 2);
                ImGui.TreePop();
                lighting.LightingType = (LightingType)current;
            }
            if (volumeComponent != null && ImGui.TreeNode("体积"))
            {
                ImGui.DragFloat3("尺寸", ref volumeComponent.Size);
                ImGui.TreePop();
            }
            if (particleEffect != null && ImGui.TreeNode("粒子特效"))
            {
                ImGui.InputInt("最大粒子数量", ref particleEffect.maxParticleCount);
                ImGui.TreePop();
            }
        }

        static void RendererComponent(Coocoo3DMain main, MMDRendererComponent renderer)
        {
            if (ImGui.TreeNode("材质"))
            {
                ShowMaterials(main, renderer.Materials);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("变形"))
            {
                ImGui.Checkbox("蒙皮", ref renderer.skinning);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("关闭蒙皮可以提高性能");
                ImGui.Checkbox("锁定动作", ref renderer.LockMotion);
                var morphStates = renderer.morphStateComponent;
                if (renderer.LockMotion)
                {
                    string filter = ImFilter("搜索变形", "搜索变形");
                    for (int i = 0; i < morphStates.morphs.Count; i++)
                    {
                        MorphDesc morpth = morphStates.morphs[i];
                        if (!Contains(morpth.Name, filter)) continue;
                        if (ImGui.SliderFloat(morpth.Name, ref morphStates.Weights.Origin[i], 0, 1))
                        {
                            main.GameDriverContext.RequireResetPhysics = true;
                        }
                    }
                }
                ImGui.TreePop();
            }
        }

        static void RendererComponent(Coocoo3DMain main, MeshRendererComponent renderer)
        {
            if (ImGui.TreeNode("材质"))
            {
                ShowMaterials(main, renderer.Materials);
                ImGui.TreePop();
            }
        }

        static void ShowMaterials(Coocoo3DMain main, List<RenderMaterial> materials)
        {
            if (ImGui.BeginChild("materials", new Vector2(120, 400)))
            {
                ImGui.PushItemWidth(120);
                for (int i = 0; i < materials.Count; i++)
                {
                    RenderMaterial material = materials[i];
                    bool selected = i == materialSelectIndex;
                    ImGui.Selectable(string.Format("{0}##{1}", material.Name, i), ref selected);
                    if (selected) materialSelectIndex = i;
                }
                ImGui.PopItemWidth();
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("materialProperty", new Vector2(200, 400)))
            {
                if (materialSelectIndex >= 0 && materialSelectIndex < materials.Count)
                {
                    var material = materials[materialSelectIndex];
                    ImGui.Text(material.Name);
                    if (ImGui.Button("修改此物体所有材质"))
                    {
                        StartEditParam();
                    }
                    var passSetting = main.RPContext.dynamicContextRead.passSetting;

                    if (main.RPContext.NewRenderPipeline)
                    {
                        ShowParam1(main, main.RPContext.currentChannel.renderPipelineView, material.Parameters);
                    }
                    else
                    {
                        ShowParams(passSetting.ShowParameters, material.Parameters);
                        ShowTextures(main, "material", passSetting.ShowTextures, material.Parameters);
                    }
                }
            }
            ImGui.EndChild();
        }

        static void SceneView(Coocoo3DMain main, RenderPipeline.VisualChannel channel, float mouseWheelDelta, Vector2 mouseMoveDelta)
        {
            var io = ImGui.GetIO();
            var tex = channel.GetAOV(Caprice.Attributes.AOVType.Color);
            IntPtr imageId = main.widgetRenderer.ShowTexture(tex);
            Vector2 pos = ImGui.GetCursorScreenPos();
            Vector2 spaceSize = Vector2.Max(ImGui.GetWindowSize() - new Vector2(20, 40), new Vector2(100, 100));
            channel.sceneViewSize = new Numerics.Int2((int)spaceSize.X, (int)spaceSize.Y);
            Vector2 texSize = new Vector2(tex.width, tex.height);
            float factor = MathF.Max(MathF.Min(spaceSize.X / texSize.X, spaceSize.Y / texSize.Y), 0.01f);
            Vector2 imageSize = texSize * factor;


            ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddImage(imageId, pos, pos + imageSize);
            var vpMatrix = channel.cameraData.vpMatrix;
            ImGui.PushClipRect(pos, pos + imageSize, true);
            foreach (var light in main.RPContext.dynamicContextRead.pointLights)
            {
                Vector4 p1 = Vector4.Transform(new Vector4(light.Position, 1), vpMatrix);
                p1 /= p1.W;
                p1.Y = -p1.Y;
                Vector2 basePos = pos + (new Vector2(p1.X, p1.Y) * 0.5f + new Vector2(0.5f, 0.5f)) * imageSize;
                if (p1.X > -1 && p1.X < 1 && p1.Y > -1 && p1.Y < 1)
                    drawList.AddTriangle(basePos + new Vector2(-0.8660254f, -0.5f) * 10, basePos + new Vector2(0, 1) * 10, basePos + new Vector2(0.8660254f, -0.5f) * 10, 0xffffffff, 2);
            }
            int hoveredIndex = -1;
            string toolTipMessage = "";
            Vector2 mousePos = ImGui.GetMousePos();
            var scene = main.CurrentScene;
            for (int i = 0; i < scene.gameObjects.Count; i++)
            {
                GameObject obj = scene.gameObjects[i];
                Vector4 p1 = Vector4.Transform(new Vector4(obj.Transform.position, 1), vpMatrix);
                p1 /= p1.W;
                p1.Y = -p1.Y;
                Vector2 basePos = pos + (new Vector2(p1.X, p1.Y) * 0.5f + new Vector2(0.5f, 0.5f)) * imageSize;
                Vector2 p2 = Vector2.Abs(basePos - mousePos);
                if (p2.X < 10 && p2.Y < 10)
                {
                    toolTipMessage += obj.Name + "\n";
                    hoveredIndex = i;
                    drawList.AddNgon(basePos, 10, 0xffffffff, 4);
                }
                if (gameObjectSelectIndex == i)
                    drawList.AddNgon(basePos, 10, 0xffffff77, 4);
            }
            ImGui.PopClipRect();
            if (ImGui.IsItemActive())
            {
                if (io.MouseDown[1])
                    channel.camera.RotateDelta(new Vector3(-mouseMoveDelta.Y, mouseMoveDelta.X, 0) / 200);
                if (io.MouseDown[2])
                    channel.camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 400);
                main.RPContext.currentChannel = channel;
            }
            if (ImGui.IsItemHovered())
            {
                channel.camera.Distance += mouseWheelDelta * 0.6f;
                if (io.MouseReleased[0] && ImGui.IsItemFocused())
                {
                    gameObjectSelectIndex = hoveredIndex;
                }
            }
            if (!string.IsNullOrEmpty(toolTipMessage))
            {
                ImGui.BeginTooltip();
                ImGui.Text(toolTipMessage);
                ImGui.EndTooltip();
            }
        }

        static void StartSelectResource(string id, string slot)
        {
            requestOpenResource = true;
            fileOpenId = id;
            fileOpenSlot = slot;
        }

        static bool CheckResourceSelect(string id, string slot, out string selectedResource)
        {
            if (id == fileOpenId && slot == fileOpenSlot && fileOpenResult != null)
            {
                selectedResource = fileOpenResult;
                fileOpenResult = null;
                fileOpenId = null;
                fileOpenSlot = null;
                return true;
            }
            else
                selectedResource = null;
            return false;
        }

        static void StartEditParam()
        {
            paramEdit = new Dictionary<string, object>();
            requestParamEdit = true;
        }

        static void Popups(Coocoo3DMain main, GameObject gameObject)
        {
            if (requestOpenResource.SetFalse())
            {
                ImGui.OpenPopup("选择资源");
                popupOpenResource = true;
            }
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("选择资源", ref popupOpenResource))
            {
                if (ImGui.Button("关闭"))
                {
                    popupOpenResource = false;
                }
                var _open = Resources();
                if (_open != null)
                {
                    fileOpenResult = _open.FullName;
                    popupOpenResource = false;
                }
                ImGui.EndPopup();
            }
            if (requestParamEdit.SetFalse())
            {
                ImGui.OpenPopup("编辑参数");
                popupParamEdit = true;
            }
            var passSetting = main.RPContext.dynamicContextRead.passSetting;
            if (ImGui.BeginPopupModal("编辑参数", ref popupParamEdit))
            {
                if (main.RPContext.NewRenderPipeline)
                {
                    ShowParam1(main, main.RPContext.currentChannel.renderPipelineView, paramEdit);
                }
                else
                    ShowParams(passSetting.ShowParameters, paramEdit);

                if (ImGui.Button("确定"))
                {
                    var meshRenderer = gameObject.GetComponent<MeshRendererComponent>();
                    var mmdRenderer = gameObject.GetComponent<MMDRendererComponent>();
                    IList<RenderMaterial> materials = null;
                    if (meshRenderer != null)
                    {
                        materials = meshRenderer.Materials;
                    }
                    if (mmdRenderer != null)
                    {
                        materials = mmdRenderer.Materials;
                    }
                    foreach (var material in materials)
                        foreach (var param in paramEdit)
                        {
                            material.Parameters[param.Key] = param.Value;
                        }

                    paramEdit = null;

                    popupParamEdit = false;
                }
                if (ImGui.Button("取消"))
                {
                    popupParamEdit = false;
                }
                ImGui.EndPopup();
            }
        }

        public static bool ComboBox<T>(string label, ref T val) where T : struct, Enum
        {
            string valName = val.ToString();
            string[] enums = Enum.GetNames<T>();
            string[] enumsTranslation = enums;

            int sourceI = Array.FindIndex(enums, u => u == valName);
            int sourceI2 = sourceI;

            bool result = ImGui.Combo(string.Format("{1}###{0}", label, label), ref sourceI, enumsTranslation, enumsTranslation.Length);
            if (sourceI != sourceI2)
                val = Enum.Parse<T>(enums[sourceI]);

            return result;
        }

        public static bool ComboBox(string label, ref object val)
        {
            var type = val.GetType();
            string valName = val.ToString();
            string[] enums = Enum.GetNames(type);
            string[] enumsTranslation = enums;

            int sourceI = Array.FindIndex(enums, u => u == valName);
            int sourceI2 = sourceI;

            bool result = ImGui.Combo(string.Format("{1}###{0}", label, label), ref sourceI, enumsTranslation, enumsTranslation.Length);
            if (sourceI != sourceI2)
                val = Enum.Parse(type, enums[sourceI]);

            return result;
        }


        static void NewLighting(Coocoo3DMain main)
        {
            LightingComponent lightingComponent = new LightingComponent();
            lightingComponent.Color = new Vector3(3, 3, 3);
            lightingComponent.Range = 50;
            GameObject gameObject = new GameObject();
            gameObject.AddComponent(lightingComponent);
            gameObject.Name = "Lighting";
            gameObject.Transform = new(new Vector3(0, 1, 0), Quaternion.CreateFromYawPitchRoll(0, 1.3962634015954636615389526147909f, 0));
            main.CurrentScene.AddGameObject(gameObject);
        }

        static void NewVolume(Coocoo3DMain main)
        {
            VolumeComponent volumeComponent = new VolumeComponent();
            volumeComponent.Size = new Vector3(100, 50, 100);
            GameObject gameObject = new GameObject();
            gameObject.AddComponent(volumeComponent);
            gameObject.Name = "Volume";
            gameObject.Transform = new(new Vector3(0, 25, 0), Quaternion.Identity);
            main.CurrentScene.AddGameObject(gameObject);
        }

        static void NewParticle(Coocoo3DMain main)
        {
            ParticleEffectComponent particleEffectComponent = new ParticleEffectComponent();
            GameObject gameObject = new GameObject();
            gameObject.AddComponent(particleEffectComponent);
            gameObject.Name = "Particle";
            gameObject.Transform = new(new Vector3(0, 0, 0), Quaternion.Identity);
            main.CurrentScene.AddGameObject(gameObject);
        }

        static string fileOpenId = null;

        public static bool initialized = false;

        public static bool demoWindowOpen = false;
        public static Vector3 position;
        public static Vector3 rotation;
        public static Quaternion rotationCache;
        public static bool rotationChange;
        public static bool positionChange;

        public static int materialSelectIndex = 0;
        public static int gameObjectSelectIndex = -1;
        public static bool requireOpenFolder;
        public static bool requestRecord;
        public static bool requestSave;

        public static Stack<DirectoryInfo> viewStack = new Stack<DirectoryInfo>();
        public static List<FileSystemInfo> storageItems = new List<FileSystemInfo>();
        public static DirectoryInfo currentFolder;
        public static DirectoryInfo viewRequest;
        public static FileInfo openRequest;
        //public static List<bool> gameObjectSelected = new List<bool>();

        static Vector3 QuaternionToEularYXZ(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Asin(2.0 * (ei - jk));
            result.Y = (float)Math.Atan2(2.0 * (ej + ik), 1 - 2.0 * (ii + jj));
            result.Z = (float)Math.Atan2(2.0 * (ek + ij), 1 - 2.0 * (ii + kk));
            return result;
        }

        static void InitTex(Coocoo3DMain main)
        {
            var caches = main.mainCaches;
            ImGui.SetCurrentContext(ImGui.CreateContext());
            Coocoo3DGraphics.Uploader uploader = new Coocoo3DGraphics.Uploader();
            var io = ImGui.GetIO();
            io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());
            unsafe
            {
                byte* data;
                io.Fonts.GetTexDataAsRGBA32(out data, out int width, out int height, out int bytesPerPixel);
                int size = width * height * bytesPerPixel;
                Span<byte> spanByte1 = new Span<byte>(data, size);

                uploader.Texture2DRaw(spanByte1, Vortice.DXGI.Format.R8G8B8A8_UNorm, width, height);
            }
            var texture2D = new Coocoo3DGraphics.Texture2D();
            io.Fonts.TexID = caches.GetPtr("imgui_font");
            caches.SetTexture("imgui_font", texture2D);
            caches.TextureReadyToUpload.Enqueue(new(texture2D, uploader));
        }

        static void InitKeyMap()
        {
            var io = ImGui.GetIO();

            io.KeyMap[(int)ImGuiKey.Tab] = (int)ImGuiKey.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)ImGuiKey.LeftArrow;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)ImGuiKey.RightArrow;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)ImGuiKey.UpArrow;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)ImGuiKey.DownArrow;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)ImGuiKey.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)ImGuiKey.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)ImGuiKey.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)ImGuiKey.End;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)ImGuiKey.Insert;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)ImGuiKey.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)ImGuiKey.Backspace;
            io.KeyMap[(int)ImGuiKey.Space] = (int)ImGuiKey.Space;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)ImGuiKey.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)ImGuiKey.Escape;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)ImGuiKey.KeyPadEnter;
            io.KeyMap[(int)ImGuiKey.A] = 'A';
            io.KeyMap[(int)ImGuiKey.C] = 'C';
            io.KeyMap[(int)ImGuiKey.V] = 'V';
            io.KeyMap[(int)ImGuiKey.X] = 'X';
            io.KeyMap[(int)ImGuiKey.Y] = 'Y';
            io.KeyMap[(int)ImGuiKey.Z] = 'Z';

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        static bool requestOpenResource = false;
        static bool popupOpenResource = false;
        static string fileOpenResult;
        static string fileOpenSlot;

        static bool requestParamEdit = false;
        static bool popupParamEdit = false;
        static Dictionary<string, object> paramEdit;

        static string[] lightTypeString = new[] { "方向光", "点光" };
        static string[] renderPipelines = new string[0] { };
        static string[] renderPipelineKeys = new string[0] { };

        static string ImFilter(string lable, string hint)
        {
            uint id = ImGui.GetID(lable);
            string filter = filters.GetValueOrDefault(id, "");
            if (ImGui.InputTextWithHint(lable, hint, ref filter, 128))
            {
                filters[id] = filter;
            }
            return filter;
        }
        static Dictionary<uint, string> filters = new Dictionary<uint, string>();

        static bool Contains(string input, string filter)
        {
            return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
