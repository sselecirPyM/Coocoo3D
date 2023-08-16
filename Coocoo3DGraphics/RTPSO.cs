﻿using System;
using System.Collections.Generic;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public class RTPSO : IDisposable
{
    public RayTracingShaderDescription[] rayGenShaders;
    public RayTracingShaderDescription[] hitGroups;
    public RayTracingShaderDescription[] missShaders;
    public string[] exports;
    public byte[] datas;
    public ResourceAccessType[] shaderAccessTypes;
    public ResourceAccessType[] localShaderAccessTypes;
    public ID3D12StateObject so;
    internal RootSignature globalRootSignature;
    internal RootSignature localRootSignature;
    public int localSize = 32;

    internal bool InitializeSO(ID3D12Device5 device)
    {
        if (exports == null || exports.Length == 0)
            return false;

        globalRootSignature?.Dispose();
        globalRootSignature = new RootSignature();
        globalRootSignature.RayTracing(shaderAccessTypes);
        globalRootSignature.Sign1(device);

        List<StateSubObject> stateSubObjects = new List<StateSubObject>();

        List<ExportDescription> exportDescriptions = new List<ExportDescription>();
        foreach (var export in exports)
            exportDescriptions.Add(new ExportDescription(export));

        stateSubObjects.Add(new StateSubObject(new DxilLibraryDescription(datas, exportDescriptions.ToArray())));
        stateSubObjects.Add(new StateSubObject(new HitGroupDescription("emptyhitgroup", HitGroupType.Triangles, null, null, null)));
        foreach (var hitGroup in hitGroups)
        {
            stateSubObjects.Add(new StateSubObject(new HitGroupDescription(hitGroup.name, HitGroupType.Triangles, hitGroup.anyHit, hitGroup.closestHit, hitGroup.intersection)));
        }
        if (localShaderAccessTypes != null)
        {
            localRootSignature?.Dispose();
            localRootSignature = new RootSignature();
            localRootSignature.LocalRootSignature(localShaderAccessTypes);
            localRootSignature.Sign1(device);
            localSize += localShaderAccessTypes.Length * 8;
            stateSubObjects.Add(new StateSubObject(new LocalRootSignature(localRootSignature.rootSignature)));
            string[] hitGroups = new string[this.hitGroups.Length];
            for (int i = 0; i < this.hitGroups.Length; i++)
                hitGroups[i] = this.hitGroups[i].name;
            stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], hitGroups)));
        }

        stateSubObjects.Add(new StateSubObject(new RaytracingShaderConfig(64, 20)));
        stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], exports)));
        stateSubObjects.Add(new StateSubObject(new RaytracingPipelineConfig(2)));
        stateSubObjects.Add(new StateSubObject(new GlobalRootSignature(globalRootSignature.rootSignature)));
        var result = device.CreateStateObject(new StateObjectDescription(StateObjectType.RaytracingPipeline, stateSubObjects.ToArray()), out so);
        if (result.Failure)
            return false;
        return true;
    }

    public void Dispose()
    {
        so?.Release();
        so = null;
        globalRootSignature?.Dispose();
        globalRootSignature = null;
        localRootSignature?.Dispose();
        localRootSignature = null;
    }
}
