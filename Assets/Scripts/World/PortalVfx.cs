// A looping, code-built portal effect for the exit hole: a ring of motes that swirl and rise out of the portal surface, plus a soft central glow that pulses.
using UnityEngine;

namespace Signal.World
{
    [DisallowMultipleComponent]
    public sealed class PortalVfx : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Radius of the swirling ring — set it to roughly match the portal hole.")]
        private float radius = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("How high the motes drift up out of the portal.")]
        private float riseHeight = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("How fast the ring swirls.")]
        private float swirlSpeed = 1.3f;

        [Header("Colour")]
        [SerializeField] private Color innerColor = new Color(0.45f, 0.85f, 1f, 1f);
        [SerializeField] private Color outerColor = new Color(0.65f, 0.35f, 1f, 1f);

        [SerializeField]
        [Tooltip("Optional particle material. Empty = a simple built-in one (squarish placeholder). Drop in an additive/soft particle material here for a nicer glow.")]
        private Material particleMaterial;

        private void Awake()
        {
            BuildSwirl();
        }

        private ParticleSystem NewSystem(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial != null ? particleMaterial : BuildMaterial();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        private void BuildSwirl()
        {
            ParticleSystem ps = NewSystem("Swirl");

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
            main.startColor = new ParticleSystem.MinMaxGradient(innerColor, outerColor);
            main.maxParticles = 300;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 45f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.65f;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(swirlSpeed);
            vel.y = new ParticleSystem.MinMaxCurve(riseHeight * 0.35f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut();

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0.5f, 1f, 0.4f));

            ps.Play();
        }

        private static ParticleSystem.MinMaxGradient FadeInOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(g);
        }

        private static AnimationCurve Curve(float a, float b, float c)
            => new AnimationCurve(new Keyframe(0f, a), new Keyframe(0.5f, b), new Keyframe(1f, c));

        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            return new Material(shader);
        }
    }
}
