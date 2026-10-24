using UnityEngine;
using UnityEngine.Rendering;

namespace PangGolf
{
    /// <summary>Procedural textures, meshes and materials so the game needs no imported assets.</summary>
    public static class ProcGen
    {
        // ---------- Materials ----------

        static Shader standard;
        public static Shader Standard
        {
            get
            {
                if (standard == null) standard = Shader.Find("Standard");
                if (standard == null) standard = Shader.Find("Universal Render Pipeline/Lit");
                return standard;
            }
        }

        public static Material Lit(Color c, float gloss = 0.1f, float metal = 0f)
        {
            var m = new Material(Standard);
            m.color = c;
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            return m;
        }

        public static Material Emissive(Color c, Color emission)
        {
            var m = Lit(c, 0.2f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        public static Material Transparent(Color c, float gloss)
        {
            var m = Lit(c, gloss);
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        public static Material Unlit(Color c)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh);
            m.color = c;
            return m;
        }

        public static Material Particle(Texture2D tex)
        {
            var sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.mainTexture = tex;
            if (m.HasProperty("_Mode"))
            {
                // Particles/Standard Unlit: fade mode
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        // ---------- Primitives ----------

        public static GameObject Prim(PrimitiveType type, Transform parent, Material mat, bool keepCollider = false, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static GameObject MeshObject(string name, Mesh mesh, Transform parent, Material mat, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ---------- Meshes ----------

        /// <summary>Cone with base radius 1 at y=0 and tip at y=1.</summary>
        public static Mesh Cone(int segments = 14)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 tip = Vector3.up;
                Vector3 n = Vector3.Cross(tip - p0, p1 - p0).normalized;
                if (n.y < 0) n = -n;
                int b = verts.Count;
                verts.Add(p0); verts.Add(tip); verts.Add(p1);
                Vector3 n0 = (new Vector3(p0.x, 0.5f, p0.z)).normalized;
                Vector3 n1 = (new Vector3(p1.x, 0.5f, p1.z)).normalized;
                norms.Add(n0); norms.Add(n); norms.Add(n1);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                // base
                int c = verts.Count;
                verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
                norms.Add(Vector3.down); norms.Add(Vector3.down); norms.Add(Vector3.down);
                tris.Add(c); tris.Add(c + 1); tris.Add(c + 2);
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Flat disc of radius 1 in the XZ plane facing up.</summary>
        public static Mesh Disc(int segments = 48)
        {
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero; uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % segments + 1;
                tris[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { vertices = verts, uv = uvs, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Flat ground plane from -outer..size+outer with a square hole 0..size (where the terrain sits).</summary>
        public static Mesh SquareFrame(float size, float outer)
        {
            float a = -outer, b = size + outer;
            var rects = new[]
            {
                new Rect(a, a, b - a, outer),          // south
                new Rect(a, size, b - a, outer),       // north
                new Rect(a, 0f, outer, size),          // west
                new Rect(size, 0f, outer, size),       // east
            };
            var verts = new Vector3[16];
            var tris = new int[24];
            for (int i = 0; i < 4; i++)
            {
                var r = rects[i];
                int v = i * 4;
                verts[v] = new Vector3(r.xMin, 0, r.yMin);
                verts[v + 1] = new Vector3(r.xMin, 0, r.yMax);
                verts[v + 2] = new Vector3(r.xMax, 0, r.yMax);
                verts[v + 3] = new Vector3(r.xMax, 0, r.yMin);
                int t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            var m = new Mesh { vertices = verts, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------- Textures ----------

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Tileable grass-like texture.</summary>
        public static Texture2D Grass(int size, Color a, Color b, float noiseScale, float stripes, int seed, float speckle = 0.25f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    // Tileable noise via sampling on a torus-ish combination
                    float n = TileNoise(u, v, noiseScale, seed);
                    float n2 = TileNoise(u, v, noiseScale * 4f, seed + 7);
                    float s = Hash(x, y, seed);
                    float k = Mathf.Clamp01(0.5f + (n - 0.5f) * 1.2f + (n2 - 0.5f) * 0.5f + (s - 0.5f) * speckle);
                    Color c = Color.Lerp(a, b, k);
                    if (stripes > 0f)
                    {
                        float stripe = Mathf.Sin(u * Mathf.PI * 2f * stripes) > 0 ? 1.06f : 0.9f;
                        c *= stripe;
                    }
                    c.a = 0.05f;
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true);
            return t;
        }

        public static float TileNoise(float u, float v, float scale, int seed)
        {
            // Blend four offset samples so the result tiles seamlessly.
            float o = seed * 13.37f;
            float a = Mathf.PerlinNoise(o + u * scale, o + v * scale);
            float b = Mathf.PerlinNoise(o + (u - 1f) * scale, o + v * scale);
            float c = Mathf.PerlinNoise(o + u * scale, o + (v - 1f) * scale);
            float d = Mathf.PerlinNoise(o + (u - 1f) * scale, o + (v - 1f) * scale);
            float ab = Mathf.Lerp(a, b, u);
            float cd = Mathf.Lerp(c, d, u);
            return Mathf.Lerp(ab, cd, v);
        }

        /// <summary>Alpha texture with a few grass blades for terrain detail billboards.</summary>
        public static Texture2D GrassBlades(int size = 64)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
            var rnd = new System.Random(5);
            for (int b = 0; b < 9; b++)
            {
                float x0 = (float)(rnd.NextDouble() * 0.8 + 0.1) * size;
                float lean = (float)(rnd.NextDouble() - 0.5) * size * 0.4f;
                float h = (float)(rnd.NextDouble() * 0.4 + 0.6) * size;
                float w = size * 0.05f;
                for (int y = 0; y < (int)h; y++)
                {
                    float f = y / h;
                    float cx = x0 + lean * f * f;
                    float hw = w * (1f - f) + 0.5f;
                    for (int x = Mathf.Max(0, (int)(cx - hw)); x <= Mathf.Min(size - 1, (int)(cx + hw)); x++)
                    {
                        float shade = 0.65f + 0.35f * f;
                        px[y * size + x] = new Color(shade, shade, shade, 1f);
                    }
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(true);
            return t;
        }

        public static Texture2D SoftDot(int size = 64)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1, 1, 1, a * a);
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(true);
            return t;
        }

        public static Texture2D Circle(int size, Color fill, Color border, float borderWidth)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    Color c;
                    if (d > r) c = new Color(0, 0, 0, 0);
                    else if (d > r - borderWidth) c = border;
                    else c = fill;
                    float aa = Mathf.Clamp01(r - d);
                    c.a *= aa;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.filterMode = FilterMode.Bilinear;
            t.Apply();
            return t;
        }

        /// <summary>Arrow pointing up (towards +y in GUI space it points to the top).</summary>
        public static Texture2D Arrow(int size, Color c)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f;  // -0.5..0.5
                    float v = (y + 0.5f) / size;         // 0 bottom .. 1 top
                    bool head = v > 0.55f && Mathf.Abs(u) < (1f - v) * 1.0f;
                    bool shaft = v <= 0.58f && v > 0.08f && Mathf.Abs(u) < 0.11f;
                    px[y * size + x] = head || shaft ? c : new Color(0, 0, 0, 0);
                }
            t.SetPixels(px);
            t.filterMode = FilterMode.Bilinear;
            t.Apply();
            return t;
        }

        public static Texture2D VerticalGradient(Color top, Color bottom, int h = 64)
        {
            var t = new Texture2D(1, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++) t.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / (h - 1)));
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        public static Texture2D HorizontalGradient(Color left, Color right, int w = 128)
        {
            var t = new Texture2D(w, 1, TextureFormat.RGBA32, false);
            for (int x = 0; x < w; x++) t.SetPixel(x, 0, Color.Lerp(left, right, (float)x / (w - 1)));
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }
    }
}
