using UnityEngine;

namespace PangGolf
{
    /// <summary>Particle bursts: PANGYA sparkle, sand, water splash and confetti.</summary>
    public class Fx : MonoBehaviour
    {
        ParticleSystem sparkle, sand, water, confetti, dust;
        Material ringMat;

        void Awake()
        {
            ringMat = ProcGen.Unlit(Color.white);
            var mat = ProcGen.Particle(ProcGen.SoftDot());
            sparkle = Make("Sparkle", mat, 60, 0.9f, 3f, 7f, 0.1f, 0.35f, new Color(1f, 0.95f, 0.4f), new Color(0.6f, 1f, 1f), -0.1f, 0.2f);
            sand = Make("Sand", mat, 45, 1.0f, 2f, 5f, 0.08f, 0.25f, new Color(0.95f, 0.87f, 0.65f), new Color(0.85f, 0.75f, 0.5f), 1f, 0.3f);
            water = Make("Water", mat, 70, 1.2f, 3f, 7f, 0.12f, 0.35f, new Color(0.85f, 0.95f, 1f), new Color(0.5f, 0.75f, 1f), 1.2f, 0.25f);
            confetti = Make("Confetti", mat, 160, 3f, 5f, 11f, 0.12f, 0.3f, new Color(1f, 0.3f, 0.4f), new Color(0.3f, 0.6f, 1f), 0.35f, 0.5f);
            dust = Make("Dust", mat, 14, 0.6f, 0.5f, 1.5f, 0.1f, 0.3f, new Color(0.7f, 0.85f, 0.5f), new Color(0.9f, 0.95f, 0.8f), 0.4f, 0.1f);

            var col = confetti.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        ParticleSystem Make(string name, Material mat, int count, float life, float minSpeed, float maxSpeed,
                            float minSize, float maxSize, Color c1, Color c2, float gravity, float radius)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 3;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            go.SetActive(true);
            return ps;
        }

        static void Burst(ParticleSystem ps, Vector3 p)
        {
            ps.transform.position = p;
            ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
            ps.Play(true);
        }

        public void Sparkle(Vector3 p) { Burst(sparkle, p); }

        /// <summary>Expanding glowing ring on the ground (PANGYA impact).</summary>
        public void Shockwave(Vector3 p, Color c)
        {
            StartCoroutine(Ring(p, c));
        }

        System.Collections.IEnumerator Ring(Vector3 p, Color c)
        {
            var go = new GameObject("Shockwave");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = ringMat;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 40;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = t / 0.6f;
                float r = Mathf.Lerp(0.2f, 5f, 1f - (1f - k) * (1f - k));
                for (int i = 0; i < 40; i++)
                {
                    float a = i * Mathf.PI * 2f / 40f;
                    lr.SetPosition(i, p + new Vector3(Mathf.Cos(a) * r, 0.08f, Mathf.Sin(a) * r));
                }
                lr.widthMultiplier = Mathf.Lerp(0.35f, 0.02f, k);
                Color cc = c; cc.a = 1f - k;
                lr.startColor = lr.endColor = cc;
                yield return null;
            }
            Destroy(go);
        }

        public void Sand(Vector3 p) { Burst(sand, p); }
        public void Water(Vector3 p) { Burst(water, p); }
        public void Confetti(Vector3 p) { Burst(confetti, p + Vector3.up * 0.5f); }
        public void Dust(Vector3 p) { Burst(dust, p); }
    }
}
