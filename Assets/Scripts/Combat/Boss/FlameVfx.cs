// Code-built fire particle effects (placeholder art, in the same spirit as the project's other procedural VFX).
using UnityEngine;

namespace Signal.Combat.Boss
{
    public static class FlameVfx
    {
        private static readonly Color Hot = new Color(1f, 0.55f, 0.12f, 1f);
        private static readonly Color Cool = new Color(0.9f, 0.15f, 0.05f, 1f);

        public static ParticleSystem BuildJet(GameObject host, float range, float halfAngleDeg)
        {
            var ps = host.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float life = Mathf.Max(0.2f, range / 14f);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(range * 0.7f, range * 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hot, Cool);
            main.gravityModifier = -0.05f;
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 120f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = halfAngleDeg;
            shape.radius = 0.25f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut();

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Rise());

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        public static ParticleSystem BuildPatch(GameObject host, float radius)
        {
            var ps = host.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hot, Cool);
            main.gravityModifier = -0.08f;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = Mathf.Clamp(radius * radius * 10f, 20f, 160f);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = new ParticleSystem.MinMaxCurve(2f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut();

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        private static ParticleSystem.MinMaxGradient FadeOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        private static AnimationCurve Rise()
            => new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.2f));

        private static Material Material()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            return new Material(shader);
        }
    }
}
