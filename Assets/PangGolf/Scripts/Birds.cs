using UnityEngine;

namespace PangGolf
{
    /// <summary>A small flock of birds gliding and flapping in lazy circles over the course.</summary>
    public class Birds : MonoBehaviour
    {
        class Bird
        {
            public Transform body, left, right;
            public float radius, height, speed, phase, flap;
        }

        Bird[] flock;
        Vector3 centre;

        void Start()
        {
            centre = new Vector3(Course.Size * 0.5f, 0f, Course.Size * 0.5f);
            var mat = ProcGen.Lit(new Color(0.15f, 0.15f, 0.18f), 0.1f);
            var white = ProcGen.Lit(new Color(0.92f, 0.92f, 0.95f), 0.1f);
            flock = new Bird[9];
            for (int i = 0; i < flock.Length; i++)
            {
                var b = new Bird
                {
                    radius = Random.Range(90f, 220f),
                    height = Random.Range(70f, 120f),
                    speed = Random.Range(0.05f, 0.09f) * (i % 3 == 0 ? -1f : 1f),
                    phase = Random.Range(0f, Mathf.PI * 2f),
                    flap = Random.Range(0f, 10f),
                };
                var m = i % 2 == 0 ? white : mat;
                b.body = new GameObject("Bird").transform;
                b.body.SetParent(transform, false);
                var torso = ProcGen.Prim(PrimitiveType.Sphere, b.body, m, false, false);
                torso.transform.localScale = new Vector3(0.25f, 0.22f, 0.8f);
                b.left = MakeWing(b.body, m, -1f);
                b.right = MakeWing(b.body, m, 1f);
                flock[i] = b;
            }
        }

        static Transform MakeWing(Transform body, Material m, float side)
        {
            var pivot = new GameObject("Wing").transform;
            pivot.SetParent(body, false);
            var wing = ProcGen.Prim(PrimitiveType.Cube, pivot, m, false, false);
            wing.transform.localPosition = new Vector3(0.6f * side, 0f, 0f);
            wing.transform.localScale = new Vector3(1.2f, 0.03f, 0.35f);
            return pivot;
        }

        void Update()
        {
            float t = Time.time;
            foreach (var b in flock)
            {
                float a = b.phase + t * b.speed;
                Vector3 pos = centre + new Vector3(Mathf.Cos(a) * b.radius, b.height + Mathf.Sin(t * 0.3f + b.phase) * 6f, Mathf.Sin(a) * b.radius);
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(b.speed);
                b.body.position = pos;
                b.body.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, -12f * Mathf.Sign(b.speed));
                // Flap in bursts, then glide
                float cycle = Mathf.Repeat(t + b.flap, 6f);
                float ang = cycle < 2.5f ? Mathf.Sin(t * 9f + b.phase) * 35f : 6f;
                b.left.localRotation = Quaternion.Euler(0f, 0f, ang);
                b.right.localRotation = Quaternion.Euler(0f, 0f, -ang);
            }
        }
    }
}
