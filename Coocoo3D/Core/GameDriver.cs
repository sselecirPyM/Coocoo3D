﻿using Coocoo3D.Common;
using Coocoo3D.RenderPipeline;
using System;

namespace Coocoo3D.Core;

public enum RendererWorkMode
{
    None,
    Playing,
    Recording,
}

public class GameDriverContext
{
    public int NeedRender;
    public bool Playing;
    public double PlayTime;
    public double DeltaTime;
    public double RealDeltaTime;
    public float FrameInterval;
    public float PlaySpeed;
    public bool RefreshScene;
    public TimeManager timeManager;


    public bool SaveCpuPower = true;
    public bool VSync = false;

    public RendererWorkMode workMode;

    public void RequireRender(bool updateEntities)
    {
        if (updateEntities)
            RefreshScene = true;
        NeedRender = 10;
    }
}

public class GameDriver
{
    public GameDriverContext gameDriverContext;

    public RenderPipelineContext renderPipelineContext;

    public bool Next()
    {
        if (toRecordMode)
        {
            toRecordMode = false;
            renderPipelineContext.recording = true;
            gameDriverContext.Playing = true;
            gameDriverContext.PlaySpeed = 1.0f;
            gameDriverContext.PlayTime = 0.0f;
            gameDriverContext.RefreshScene = true;

            RenderCount = 0;
        }
        if (toPlayMode)
        {
            toPlayMode = false;
            renderPipelineContext.recording = false;
            OnEnterPlayMode?.Invoke();
        }
        bool returnValue;
        if (renderPipelineContext.recording)
            returnValue = Recording();
        else
            returnValue = Playing();

        renderPipelineContext.RealDeltaTime = gameDriverContext.RealDeltaTime;
        renderPipelineContext.Time = gameDriverContext.PlayTime;
        renderPipelineContext.DeltaTime = gameDriverContext.Playing ? gameDriverContext.DeltaTime : 0;
        Update();
        return returnValue;
    }

    bool Playing()
    {
        GameDriverContext context = gameDriverContext;
        var timeManager = context.timeManager;
        if (!timeManager.RealTimerCorrect("frame", context.FrameInterval, out double deltaTime))
        {
            return false;
        }
        if (!(context.NeedRender > 0 || context.Playing))
        {
            return false;
        }
        context.NeedRender -= 1;

        context.RealDeltaTime = deltaTime;
        context.DeltaTime = Math.Clamp(deltaTime * context.PlaySpeed, -0.17f, 0.17f);
        if (context.Playing)
            context.PlayTime += context.DeltaTime;
        return true;
    }

    bool Recording()
    {
        gameDriverContext.NeedRender = 1;

        gameDriverContext.RealDeltaTime = FrameIntervalF;
        gameDriverContext.DeltaTime = FrameIntervalF;
        gameDriverContext.PlayTime = FrameIntervalF * RenderCount;

        return true;
    }

    void Update()
    {
        if (!renderPipelineContext.recording)
            return;

        RenderCount++;
    }

    public void RequireRender(bool updateEntities)
    {
        if (updateEntities)
            gameDriverContext.RefreshScene = true;
        gameDriverContext.NeedRender = 10;
    }

    public float FrameIntervalF = 1 / 60.0f;
    int RenderCount = 0;
    bool toRecordMode;
    bool toPlayMode;

    public event Action OnEnterPlayMode;

    public void ToRecordMode()
    {
        toRecordMode = true;
    }
    public void ToPlayMode()
    {
        toPlayMode = true;
    }
}
