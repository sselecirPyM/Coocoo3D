using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Coocoo3D.Utility;
using Coocoo3D.Present;
using Coocoo3D.Components;

namespace Coocoo3D.Core
{
    public class ParticleSystem
    {
        public Scene scene;
        public GameDriverContext gameDriverContext;
        Random Random = new Random();

        public Dictionary<int, ParticleHolder> particles = new();
        List<(GameObject, ParticleHolder)> particles2 = new();
        public void Update()
        {
            foreach (var gameObject in scene.gameObjectLoadList)
            {
                if (gameObject.TryGetComponent<VisualComponent>(out var visual) && visual.UIShowType == Caprice.Display.UIShowType.Particle)
                {
                    particles.Add(gameObject.id, new ParticleHolder());
                }
            }
            foreach (var gameObject in scene.gameObjectRemoveList)
            {
                particles.Remove(gameObject.id);
            }

            float deltaTime = (float)gameDriverContext.DeltaTime;

            particles2.Clear();
            foreach (var gameObject in scene.gameObjects)
            {
                if (particles.TryGetValue(gameObject.id, out var particle))
                {
                    particles2.Add((gameObject, particle));
                }
            }
            foreach (var pair in particles2)
            {
                var particle = pair.Item2;
                var material = pair.Item1.GetComponent<VisualComponent>().material;
                var parameters = material.Parameters;

                int parameterCount = 0;
                Vector2 particleLife = Vector2.Zero;
                Vector2 particleRandomSpeed = Vector2.Zero;
                Vector2 particleScale = Vector2.One;
                Vector3 particleInitialSpeed = Vector3.Zero;
                Vector3 particleAcceleration = Vector3.Zero;
                if (parameters.TryGetValue("ParticleCount", out var p0))
                {
                    parameterCount = (p0 is int t0) ? t0 : 0;
                }
                if (parameters.TryGetValue("ParticleLife", out var p1))
                {
                    particleLife = (p1 is Vector2 t0) ? t0 : Vector2.Zero;
                }
                if (parameters.TryGetValue("ParticleRandomSpeed", out var p2))
                {
                    particleRandomSpeed = (p2 is Vector2 t0) ? t0 : Vector2.Zero;
                }
                if (parameters.TryGetValue("ParticleInitialSpeed", out var p3))
                {
                    particleInitialSpeed = (p3 is Vector3 t0) ? t0 : Vector3.One;
                }
                if (parameters.TryGetValue("ParticleScale", out var p4))
                {
                    particleScale = (p4 is Vector2 t0) ? t0 : Vector2.One;
                }
                if (parameters.TryGetValue("ParticleAcceleration", out var p5))
                {
                    particleAcceleration = (p5 is Vector3 t0) ? t0 : Vector3.Zero;
                }
                if (particle.positions.Count < parameterCount)
                {
                    particle.Add(Random.GetVector3(-1, 1), -0.001f);
                }
                for (int i = 0; i < particle.positions.Count; i++)
                {
                    particle.positions[i] += particle.velocities[i] * deltaTime;
                    particle.velocities[i] += particleAcceleration * deltaTime;
                    particle.lifes[i] -= deltaTime;
                    if (particle.lifes[i] <= 0)
                    {
                        particle.lifes[i] = (float)(Random.NextDouble() * particleLife.Y) + particleLife.X;
                        particle.positions[i] = Random.GetVector3(-1, 1);
                        particle.velocities[i] = Vector3.Normalize(Random.GetVector3(-1, 1)) * ((float)(Random.NextDouble() * particleRandomSpeed.Y) + particleRandomSpeed.X)
                            + particleInitialSpeed;
                        particle.scales[i] = (float)(Random.NextDouble() * particleScale.Y) + particleScale.X; ;
                    }
                }
                while (particle.positions.Count > parameterCount && particle.positions.Count > 0)
                {
                    particle.Remove(0);
                }
            }
        }
    }
}
