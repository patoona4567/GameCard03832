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
            QualitySettings.shadowDistance = 260f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.pixelLightCount = 4;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_SunSizeConvergence", 6f);
                sky.SetFloat("_AtmosphereThickness", 0.85f);
                sky.SetColor("_SkyTint", new Color(0.42f, 0.6f, 0.9f));
                sky.SetColor("_GroundColor", new Color(0.36f, 0.42f, 0.36f));
                sky.SetFloat("_Exposure", 1.3f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.56f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.3f, 0.22f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.6f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.fogStartDistance = 380f;
            RenderSettings.fogEndDistance = 2600f;

            BuildScenery(parent);
            return sun;
        }

        static void BuildScenery(Transform parent)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            Vector3 centre = new Vector3(Course.Size * 0.5f, 0f, Course.Size * 0.5f);

            // Endless meadow beyond the terrain
            var groundMat = ProcGen.Lit(new Color(0.22f, 0.42f, 0.15f), 0.05f);
            var ground = ProcGen.MeshObject("Meadow", ProcGen.SquareFrame(Course.Size, 5000f), root, groundMat, false);
            ground.transform.position = new Vector3(0f, Course.EdgeH - 0.05f, 0f);

            // Mountains ring
            var cone = ProcGen.Cone(18);
            var mountainMats = new[]
            {
                ProcGen.Lit(new Color(0.3f, 0.45f, 0.35f), 0.05f),
                ProcGen.Lit(new Color(0.36f, 0.5f, 0.42f), 0.05f),
                ProcGen.Lit(new Color(0.42f, 0.52f, 0.5f), 0.05f),
            };
            var snow = ProcGen.Lit(new Color(0.95f, 0.97f, 1f), 0.3f);
            var rnd = new System.Random(3);
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.2f;
                float r = 900f + (float)rnd.NextDouble() * 500f;
                float h = 160f + (float)rnd.NextDouble() * 260f;
                float w = 260f + (float)rnd.NextDouble() * 260f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, Course.EdgeH - 5f, Mathf.Sin(a) * r);
                var m = ProcGen.MeshObject("Mountain", cone, root, mountainMats[i % mountainMats.Length], false);
                m.transform.position = p;
                m.transform.localScale = new Vector3(w, h, w);
                if (h > 330f)
                {
                    var cap = ProcGen.MeshObject("Snow", cone, root, snow, false);
                    cap.transform.position = p + Vector3.up * h * 0.72f;
                    cap.transform.localScale = new Vector3(w * 0.285f, h * 0.29f, w * 0.285f);
                }
            }

            // Puffy clouds
            var cloudMat = ProcGen.Emissive(new Color(1f, 1f, 1f), new Color(0.35f, 0.37f, 0.4f));
            for (int i = 0; i < 18; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = 300f + (float)rnd.NextDouble() * 900f;
                Vector3 c = centre + new Vector3(Mathf.Cos(a) * r, 220f + (float)rnd.NextDouble() * 120f, Mathf.Sin(a) * r);
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(root, false);
                cloud.position = c;
                int blobs = 4 + rnd.Next(4);
                for (int b = 0; b < blobs; b++)
                {
                    var s = ProcGen.Prim(PrimitiveType.Sphere, cloud, cloudMat, false, false);
                    s.transform.localPosition = new Vector3((b - blobs * 0.5f) * 14f, (float)rnd.NextDouble() * 6f, (float)rnd.NextDouble() * 12f - 6f);
                    float sc = 22f + (float)rnd.NextDouble() * 18f;
                    s.transform.localScale = new Vector3(sc * 1.4f, sc * 0.7f, sc);
                    s.GetComponent<MeshRenderer>().receiveShadows = false;
                }
                cloud.gameObject.AddComponent<CloudDrift>();
            }
        }
    }
}
