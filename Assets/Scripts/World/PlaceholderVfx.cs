// Temporary code-configured particle effect (placeholder art).
using UnityEngine;

namespace Signal.World
{
    public class PlaceholderVfx : MonoBehaviour
    {
        public enum Style
        {
            Splash, Respawn, Jump, DoubleJump, Land, Dodge, LightAttack, HeavyAttack, Bash, Damage, Death
        }

        private enum ShapeMode { Up, RingUp, Forward, Backward, Sphere }

        [SerializeField] private Style style = Style.Splash;
        [SerializeField] private Color tint = new Color(0.5f, 0.8f, 1f, 1f);

        private void Awake() => Build();

        private void Build()
        {
            var ps = GetComponent<ParticleSystem>();
            if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Preset p = GetPreset(style);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startColor = tint;
            main.duration = p.Duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.LifeMin, p.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.SpeedMin, p.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(p.SizeMin, p.SizeMax);
            main.gravityModifier = p.Gravity;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, p.Burst) });

            ApplyShape(ps.shape, p.Shape, p.Radius);

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.sharedMaterial == null)
                renderer.sharedMaterial = BuildMaterial();
        }

        private static void ApplyShape(ParticleSystem.ShapeModule shape, ShapeMode mode, float radius)
        {
            shape.enabled = true;
            shape.radius = radius;
            switch (mode)
            {
                case ShapeMode.Up:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 20f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    break;
                case ShapeMode.RingUp:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 80f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    break;
                case ShapeMode.Forward:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 18f;
                    shape.rotation = Vector3.zero;
                    break;
                case ShapeMode.Backward:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25f;
                    shape.rotation = new Vector3(0f, 180f, 0f);
                    break;
                case ShapeMode.Sphere:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    break;
            }
        }

        private readonly struct Preset
        {
            public readonly float Duration, LifeMin, LifeMax, SpeedMin, SpeedMax, SizeMin, SizeMax, Gravity, Radius;
            public readonly int Burst;
            public readonly ShapeMode Shape;

            public Preset(float duration, float lifeMin, float lifeMax, float speedMin, float speedMax,
                float sizeMin, float sizeMax, float gravity, int burst, ShapeMode shape, float radius)
            {
                Duration = duration; LifeMin = lifeMin; LifeMax = lifeMax;
                SpeedMin = speedMin; SpeedMax = speedMax; SizeMin = sizeMin; SizeMax = sizeMax;
                Gravity = gravity; Burst = burst; Shape = shape; Radius = radius;
            }
        }

        private static Preset GetPreset(Style style) => style switch
        {
            Style.Splash      => new(0.6f, 0.35f, 0.7f,  3f,   6f,   0.06f, 0.14f, 1.2f, 24, ShapeMode.Up,      0.15f),
            Style.Respawn     => new(1f,   0.6f,  1f,    1.5f, 3f,   0.08f, 0.18f, -0.2f, 30, ShapeMode.Sphere, 0.30f),
            Style.Jump        => new(0.5f, 0.3f,  0.5f,  1.5f, 2.5f, 0.05f, 0.12f, 0.3f, 12, ShapeMode.RingUp,  0.15f),
            Style.DoubleJump  => new(0.6f, 0.4f,  0.7f,  2.5f, 4.5f, 0.06f, 0.15f, 0.1f, 22, ShapeMode.Up,      0.18f),
            Style.Land        => new(0.5f, 0.3f,  0.6f,  2f,   4f,   0.05f, 0.13f, 0.2f, 22, ShapeMode.RingUp,  0.25f),
            Style.Dodge       => new(0.4f, 0.2f,  0.4f,  2f,   4f,   0.05f, 0.11f, 0f,   14, ShapeMode.Backward,0.15f),
            Style.LightAttack => new(0.3f, 0.15f, 0.3f,  4f,   7f,   0.04f, 0.10f, 0f,   10, ShapeMode.Forward, 0.10f),
            Style.HeavyAttack => new(0.5f, 0.25f, 0.5f,  5f,   9f,   0.07f, 0.17f, 0f,   20, ShapeMode.Forward, 0.15f),
            Style.Bash        => new(0.45f,0.2f,  0.4f,  3f,   6f,   0.06f, 0.14f, 0.3f, 16, ShapeMode.Forward, 0.15f),
            Style.Damage      => new(0.4f, 0.2f,  0.45f, 2f,   4f,   0.05f, 0.12f, 0.2f, 14, ShapeMode.Sphere,  0.12f),
            Style.Death       => new(1.1f, 0.5f,  1.1f,  2f,   5f,   0.08f, 0.20f, -0.1f,32, ShapeMode.Sphere,  0.30f),
            _                 => new(0.5f, 0.3f,  0.6f,  2f,   4f,   0.06f, 0.14f, 0.2f, 16, ShapeMode.Sphere,  0.2f),
        };

        private Material BuildMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            return new Material(shader) { color = tint };
        }
    }
}
