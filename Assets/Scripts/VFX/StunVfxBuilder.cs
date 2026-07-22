// Builds a lightweight "swirling stars" stun aura on a bare prefab at load time — a single looping ParticleSystem whose particles orbit the enemy's head.
using UnityEngine;

namespace Signal.VFX
{
    public class StunVfxBuilder : MonoBehaviour
    {
        [SerializeField] private Color starColor = new Color(1f, 0.86f, 0.2f);
        [SerializeField, Min(0.05f)] private float ringRadius = 0.45f;
        [SerializeField, Min(1f)] private float orbitSpeed = 3.5f;

        private void Awake()
        {
            ParticleSystem ps = GetComponent<ParticleSystem>();
            if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
            Build(ps);
        }

        private void Build(ParticleSystem ps)
        {
            ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 1.2f;
            main.startSpeed = 0f;
            main.startSize = 0.14f;
            main.startColor = starColor;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 10f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ringRadius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = orbitSpeed;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, PopCurve());

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(starColor);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader != null) renderer.material = new Material(shader);
            renderer.sortingOrder = 10;
        }

        private static AnimationCurve PopCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0.2f);
            curve.AddKey(0.35f, 1f);
            curve.AddKey(1f, 0f);
            return curve;
        }

        private static Gradient FadeGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
