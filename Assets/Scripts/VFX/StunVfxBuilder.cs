using UnityEngine;

namespace Signal.VFX
{
    /// <summary>
    /// Builds a lightweight "swirling stars" stun aura on a bare prefab at load time — a single
    /// looping <see cref="ParticleSystem"/> whose particles orbit the enemy's head. Kept in code so
    /// the StunVFX prefab is just this component (+ a pooled marker) and is trivial to tweak or
    /// replace with an artist-made effect later. Because the pool gathers particle systems lazily,
    /// building here on Awake is enough for pooling to pick it up.
    /// </summary>
    public class StunVfxBuilder : MonoBehaviour
    {
        [SerializeField] private Color starColor = new Color(1f, 0.86f, 0.2f);
        [SerializeField, Min(0.05f)] private float ringRadius = 0.45f;
        [SerializeField, Min(1f)] private float orbitSpeed = 3.5f;

        // Add the particle system in code so the prefab is just this component + PooledVfx (no huge
        // ParticleSystem block to hand-author or keep in sync).
        private void Awake()
        {
            ParticleSystem ps = GetComponent<ParticleSystem>();
            if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
            Build(ps);
        }

        private void Build(ParticleSystem ps)
        {
            // A fresh ParticleSystem defaults to playOnAwake and is already playing by the time we
            // get here, and main.duration cannot be assigned on a playing system. Stop and clear
            // first; setting playOnAwake = false below does not stop an already-running system.
            ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // A looping ring of small "stars" that orbit the head; sits still until Play()'d and
            // stops the instant it's Stop()'d, so the stun system controls its exact duration.
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 1.2f;
            main.startSpeed = 0f;                         // motion comes from the orbital module
            main.startSize = 0.14f;
            main.startColor = starColor;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // follow the head when parented
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 10f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ringRadius;
            shape.radiusThickness = 0f;                   // emit right on the ring
            shape.rotation = new Vector3(90f, 0f, 0f);    // ring lies flat (around the head)

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = orbitSpeed;                    // the swirl

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, PopCurve());

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(starColor);

            // A simple billboarded point sprite reads as a twinkling star without needing an asset.
            // Pick whatever unlit/particle shader the active pipeline ships so it never renders as
            // the magenta error material (URP first, then cross-pipeline fallbacks).
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
            // Small → full → small, so each star twinkles.
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
