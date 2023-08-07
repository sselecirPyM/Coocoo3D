using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines.MetaRender;

public class ParticlesEx
{
    public ParticlesEx()
    {
        properties.Add("Position", new List<Vector3>());
        properties.Add("Velocity", new List<Vector3>());
        properties.Add("Scale", new List<Vector2>());
        properties.Add("Lifetime", new List<float>());
        properties.Add("MaxLifetime", new List<float>());
    }

    public Dictionary<string, object> properties = new Dictionary<string, object>();

    public ParticleRenderable particleRenderable;

    ParticleMaterial material;

    Vector3 position;

    double lastGenerate;

    //HashSet<int> removeParticles = new HashSet<int>();
    List<int> removeParticles = new List<int>();

    public Random random = new Random();

    public void Simulate(double deltaTime)
    {
        float deltaTimef = (float)deltaTime;
        material = (ParticleMaterial)particleRenderable.material;
        Vector3 oldPosition = position;
        Vector3 newPosition = Vector3.Transform(Vector3.Zero, particleRenderable.transform);
        Vector3 deltaPosition = position - newPosition;
        float speed = Vector3.Distance(oldPosition, newPosition) / deltaTimef;
        position = newPosition;

        lastGenerate = Math.Min(lastGenerate + deltaTime + deltaPosition.Length() * material.MoveToGenerate, 100);

        double generateTime = 1 / Math.Max(material.GenerateSpeed, 1e-3);

        float generateCount = 0;
        int generateMaxCount = Math.Max((int)(lastGenerate / generateTime), 1);
        var Lifetimes = (List<float>)properties["Lifetime"];
        while (lastGenerate >= generateTime)
        {
            generateCount++;
            lastGenerate -= generateTime;

            Vector2 particleLife = material.Life;
            float lifetime = (float)random.NextDouble() * (particleLife.Y - particleLife.X) + particleLife.X + Math.Min(speed, material.LifeBySpeedMax) * material.LifeBySpeed;
            for (int i = 0; i < material.GenerateCount; i++)
            {
                if (material.MaxCount <= Lifetimes.Count)
                    break;
                Vector3 generatePosition = Vector3.Lerp(oldPosition, newPosition, generateCount / generateMaxCount);
                Vector3 generateVelocty = Vector3.TransformNormal(random.GetVector3(material.RandomSpeed.X, material.RandomSpeed.Y)
                    + material.InitialSpeed, particleRenderable.transform);
                AddParticle(generatePosition, generateVelocty, lifetime);
            }
        }
        removeParticles.Clear();
        for (int i = 0; i < Lifetimes.Count; i++)
        {
            Lifetimes[i] -= (float)deltaTime;
            if (Lifetimes[i] < 0)
                removeParticles.Add(i);
        }
        var Positions = (List<Vector3>)properties["Position"];
        var Velocities = (List<Vector3>)properties["Velocity"];
        for (int i = 0; i < Velocities.Count; i++)
        {
            Positions[i] += Velocities[i] * deltaTimef;
        }

        foreach (var pair in properties)
        {
            switch (pair.Value)
            {
                case List<float> val:
                    RemoveAll(val, removeParticles);
                    break;
                case List<Vector3> val:
                    RemoveAll(val, removeParticles);
                    break;
                case List<Vector2> val:
                    RemoveAll(val, removeParticles);
                    break;
                case List<Vector4> val:
                    RemoveAll(val, removeParticles);
                    break;
                case List<int> val:
                    RemoveAll(val, removeParticles);
                    break;
            }
        }
    }

    void AddParticle(Vector3 position, Vector3 velocity, float lifetime)
    {
        foreach (var pair in properties)
        {
            if (pair.Key == "Position" && pair.Value is List<Vector3> a)
            {
                a.Add(position);
            }
            else if (pair.Key == "Velocity" && pair.Value is List<Vector3> b)
            {
                b.Add(velocity);
            }
            else if ((pair.Key == "Lifetime" || pair.Key == "MaxLifetime") && pair.Value is List<float> c)
            {
                c.Add(lifetime);
            }
            else if (pair.Key == "Scale" && pair.Value is List<Vector2> d)
            {
                d.Add(material.Scale);
            }
            else
                switch (pair.Value)
                {
                    case List<float> val:
                        val.Add(default);
                        break;
                    case List<Vector3> val:
                        val.Add(default);
                        break;
                    case List<Vector2> val:
                        val.Add(default);
                        break;
                    case List<Vector4> val:
                        val.Add(default);
                        break;
                    case List<int> val:
                        val.Add(default);
                        break;
                }
        }
    }

    void RemoveAll<T>(List<T> list, HashSet<int> indices)
    {
        if (indices.Count == 0)
            return;
        int removeCount = 0;
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices.Contains(i))
                removeCount++;
            else
                break;
        }
        Span<T> span = CollectionsMarshal.AsSpan<T>(list);
        for (int i = removeCount; i < span.Length; i++)
        {
            if (indices.Contains(i))
                removeCount++;
            else
                span[i - removeCount] = span[i];
        }
        if (removeCount > 0)
            list.RemoveRange(list.Count - removeCount, removeCount);
    }

    void RemoveAll<T>(List<T> list, List<int> orderedIndices)
    {
        if (orderedIndices.Count == 0)
            return;
        int removeCount = 0;
        int next = -1;
        for (int i = 0; i < orderedIndices.Count; i++)
        {
            if (orderedIndices[i] == i)
                removeCount++;
            else
            {
                next = orderedIndices[i];
                break;
            }
        }
        Span<T> span = CollectionsMarshal.AsSpan<T>(list);
        for (int i = removeCount; i < span.Length; i++)
        {
            if (i == next)
            {
                removeCount++;
                if (orderedIndices.Count > removeCount)
                    next = orderedIndices[removeCount];
                else
                    next = -1;
            }
            else
                span[i - removeCount] = span[i];
        }
        Debug.Assert(orderedIndices.Count == removeCount);
        if (removeCount > 0)
            list.RemoveRange(list.Count - removeCount, removeCount);
    }
}
