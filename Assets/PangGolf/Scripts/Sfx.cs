using UnityEngine;

namespace PangGolf
{
    /// <summary>Synthesised sound effects (no audio files needed).</summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource src;
        AudioClip hit, perfect, putt, cup, bounce, splash, cheer, tick, power, miss;

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;

            var rnd = new System.Random(1);
            hit = Make("hit", 0.35f, t =>
                Noise(rnd) * Mathf.Exp(-t * 45f) * 0.6f +
                Mathf.Sin(2f * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 70f) * 0.35f +
                Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 20f) * 0.6f);
            perfect = Make("perfect", 1.1f, t =>
                Bell(t, 0.00f, 1046.5f) + Bell(t, 0.09f, 1318.5f) + Bell(t, 0.18f, 1568f) + Bell(t, 0.27f, 2093f) * 0.8f +
                Noise(rnd) * Mathf.Exp(-t * 60f) * 0.5f +
                Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 20f) * 0.6f);
            putt = Make("putt", 0.2f, t =>
                Mathf.Sin(2f * Mathf.PI * 950f * t) * Mathf.Exp(-t * 90f) * 0.6f + Noise(rnd) * Mathf.Exp(-t * 120f) * 0.2f);
            cup = Make("cup", 0.6f, t =>
                Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 18f) * 0.7f +
                Rattle(rnd, t, 0.07f) + Rattle(rnd, t, 0.15f) * 0.7f + Rattle(rnd, t, 0.21f) * 0.4f);
            bounce = Make("bounce", 0.15f, t => Noise(rnd) * Mathf.Exp(-t * 55f) * 0.35f + Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 40f) * 0.4f, 0.15f);
            splash = Make("splash", 0.9f, t => Noise(rnd) * Mathf.Exp(-t * 4.5f) * (1f - Mathf.Exp(-t * 60f)) * 0.7f, 0.25f);
            cheer = Make("cheer", 2.4f, t =>
            {
                float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((2.4f - t) * 1.2f);
                float voices = 0f;
                for (int i = 0; i < 6; i++) voices += Mathf.Sin(2f * Mathf.PI * (300f + i * 73f) * t + Mathf.Sin(t * (5f + i)) * 3f);
                return (Noise(rnd) * 0.6f + voices * 0.06f) * env * (0.7f + 0.3f * Mathf.Sin(t * 23f));
            }, 0.35f);
            tick = Make("tick", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 1400f * t) * Mathf.Exp(-t * 120f) * 0.4f);
            power = Make("power", 0.5f, t => Mathf.Sin(2f * Mathf.PI * (300f + t * 1600f) * t) * Mathf.Exp(-t * 4f) * 0.4f);
            miss = Make("miss", 0.35f, t =>
                Noise(rnd) * Mathf.Exp(-t * 35f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 14f) * 0.6f);
        }

        delegate float Wave(float t);

        static AudioClip Make(string name, float seconds, Wave w, float lowpass = 1f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = w(i / (float)Rate);
                y += (x - y) * lowpass;
                data[i] = Mathf.Clamp(y, -1f, 1f);
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Noise(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

        static float Bell(float t, float start, float freq)
        {
            if (t < start) return 0f;
            float u = t - start;
            return (Mathf.Sin(2f * Mathf.PI * freq * u) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2.01f * u)) * Mathf.Exp(-u * 6f) * 0.28f;
        }

        static float Rattle(System.Random r, float t, float at)
        {
            if (t < at) return 0f;
            float u = t - at;
            return (Noise(r) * 0.4f + Mathf.Sin(2f * Mathf.PI * 2400f * u) * 0.4f) * Mathf.Exp(-u * 70f);
        }

        void Play(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (c == null) return;
            src.pitch = pitch;
            src.PlayOneShot(c, vol);
        }

        public void Hit(float strength) { Play(hit, Mathf.Lerp(0.5f, 1f, strength)); }
        public void Perfect() { Play(perfect); }
        public void Putt() { Play(putt, 0.8f); }
        public void Cup() { Play(cup); }
        public void Bounce(float v) { Play(bounce, Mathf.Clamp01(v / 10f) * 0.8f, Random.Range(0.9f, 1.1f)); }
        public void Splash() { Play(splash); }
        public void Cheer() { Play(cheer, 0.8f); }
        public void Tick() { Play(tick, 0.5f); }
        public void PowerUp() { Play(power, 0.7f); }
        public void Miss() { Play(miss, 0.8f); }
    }
}
