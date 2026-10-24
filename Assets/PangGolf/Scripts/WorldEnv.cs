using UnityEngine;
using UnityEngine.Rendering;

namespace PangGolf
{
    /// <summary>Sun, sky, fog, distant scenery and quality settings.</summary>
    public static class WorldEnv
    {
        public static Light Build(Transform parent)
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 280f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.04f, 0.12f, 0.35f);
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.realtimeReflectionProbes = true;
            QualitySettings.softParticles = false;
            QualitySettings.lodBias = 2f;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(40f, -38f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunDisk", 2f); // high quality sun disk
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_SunSizeConvergence", 5f);
                sky.SetFloat("_AtmosphereThickness", 0.8f);
                sky.SetColor("_SkyTint", new Color(0.4f, 0.58f, 0.92f));
                sky.SetColor("_GroundColor", new Color(0.38f, 0.45f, 0.4f));
                sky.SetFloat("_Exposure", 1.35f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.sun = sun;

            // Ambient light sampled from the sky (soft blue fill from above, green bounce from below)
            if (RenderSettings.skybox != null)
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.15f;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
                RenderSettings.ambientEquatorColor = new Color(0.5f, 0.56f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.28f, 0.3f, 0.22f);
            }
            RenderSettings.reflectionIntensity = 0.8f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.92f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 2400f;

            BuildScenery(parent);
            return sun;
        }

        static void BuildScenery(Transform parent)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            Vector3 centre = new Vector3(Course.Size * 0.5f, 0f, Course.Size * 0.5f);

            // Endless meadow beyond the terrain, textured to match the deep rough
            var groundMat = ProcGen.Lit(Color.white, 0.02f);
            groundMat.mainTexture = ProcGen.Grass(256, new Color(0.15f, 0.31f, 0.1f), new Color(0.26f, 0.4f, 0.14f), 5f, 0f, 7, 0.5f);
            var ground = ProcGen.MeshObject("Meadow", ProcGen.SquareFrame(Course.Size, 5000f), root, groundMat, false);
            ground.transform.position = new Vector3(0f, Course.EdgeH - 0.05f, 0f);

            // Mountain ring: noisy peaks with grass, rock and snow
            var mountainMat = ProcGen.VertexLit();
            var rnd = new System.Random(3);
            for (int i = 0; i < 30; i++)
            {
                float a = i / 30f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.15f;
                float r = 950f + (float)rnd.NextDouble() * 550f;
                float h = 140f + (float)rnd.NextDouble() * 300f;
                float w = 280f + (float)rnd.NextDouble() * 280f;
                bool snowy = h > 320f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, Course.EdgeH - 8f, Mathf.Sin(a) * r);
                var m = ProcGen.MeshObject("Mountain", ProcGen.Mountain(i * 7 + 1, snowy), root, mountainMat, false);
                m.transform.position = p;
                m.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                m.transform.localScale = new Vector3(w, h, w);
            }
            // Gentle green foothills closer in
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.2f;
                float r = 560f + (float)rnd.NextDouble() * 250f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, Course.EdgeH - 2f, Mathf.Sin(a) * r);
                var m = ProcGen.MeshObject("Hill", ProcGen.Mountain(i * 13 + 5, false), root, mountainMat, false);
                m.transform.position = p;
                m.transform.localScale = new Vector3(160f + (float)rnd.NextDouble() * 120f, 40f + (float)rnd.NextDouble() * 50f, 160f + (float)rnd.NextDouble() * 120f);
            }

            BuildClouds(root, centre, rnd);

            var birds = new GameObject("Birds");
            birds.transform.SetParent(root, false);
            birds.AddComponent<Birds>();
        }

        static void BuildClouds(Transform root, Vector3 centre, System.Random rnd)
        {
            var go = new GameObject("Clouds");
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.position = centre;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var mat = ProcGen.Particle(ProcGen.CloudPuff());
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(1.25f, 1.25f, 1.28f, 1f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 10f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(true);
            ps.Play();

            var list = new System.Collections.Generic.List<ParticleSystem.Particle>();
            for (int c = 0; c < 26; c++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float dist = 250f + (float)rnd.NextDouble() * 1200f;
                Vector3 cc = new Vector3(Mathf.Cos(a) * dist, 190f + (float)rnd.NextDouble() * 140f, Mathf.Sin(a) * dist);
                float yaw = (float)rnd.NextDouble() * Mathf.PI;
                Vector3 ax = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 az = new Vector3(-ax.z, 0f, ax.x);
                float len = 60f + (float)rnd.NextDouble() * 90f;
                int puffs = 10 + rnd.Next(10);
                for (int i = 0; i < puffs; i++)
                {
                    float u = (float)rnd.NextDouble() * 2f - 1f;
                    float hump = 1f - u * u;
                    float oy = (float)rnd.NextDouble() * 22f * hump;
                    Vector3 off = ax * (u * len) + az * (((float)rnd.NextDouble() * 2f - 1f) * 25f) + Vector3.up * oy;
                    float lightK = Mathf.Clamp01(oy / 22f * 0.8f + 0.3f);
                    Color col = Color.Lerp(new Color(0.72f, 0.77f, 0.88f), Color.white, lightK);
                    var p = new ParticleSystem.Particle
                    {
                        position = cc + off,
                        startSize = (40f + (float)rnd.NextDouble() * 45f) * (0.6f + 0.5f * hump),
                        startColor = col,
                        startLifetime = 100000f,
                        remainingLifetime = 100000f,
                        rotation = (float)rnd.NextDouble() * 360f,
                        velocity = Vector3.zero,
                    };
                    list.Add(p);
                }
            }
            ps.SetParticles(list.ToArray(), list.Count);
            go.AddComponent<CloudDrift>();
        }
    }
}
