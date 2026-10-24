using System.Collections.Generic;
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

        /// <summary>Finds one of our custom shaders; returns null if missing or failed to compile.</summary>
        public static Shader Custom(string name)
        {
            var sh = Shader.Find(name);
            return sh != null && sh.isSupported ? sh : null;
        }

        /// <summary>Wind-swayed foliage with colour variation, falls back to Standard.</summary>
        public static Material Foliage(Color c, float sway = 0.12f, bool doubleSided = false)
        {
            var sh = Custom("PangGolf/Foliage");
            if (sh == null) return Lit(c, 0.1f);
            var m = new Material(sh);
            m.color = c;
            m.SetFloat("_Sway", sway);
            m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Lambert lit material driven by mesh vertex colours.</summary>
        public static Material VertexLit()
        {
            var sh = Custom("PangGolf/VertexLit");
            if (sh == null) return Lit(new Color(0.4f, 0.5f, 0.42f), 0.05f);
            var m = new Material(sh);
            m.enableInstancing = true;
            return m;
        }

        public static Material Water()
        {
            var sh = Custom("PangGolf/Water");
            if (sh == null) return Transparent(new Color(0.08f, 0.42f, 0.62f, 0.78f), 0.96f);
            var m = new Material(sh);
            m.SetTexture("_NormalTex", NormalMap(256, 9, 6f, 0.9f, 0));
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
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++) uvs[i] = new Vector2(verts[i].x / 10f, verts[i].z / 10f);
            var m = new Mesh { vertices = verts, triangles = tris, uv = uvs };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Polar grid disc (radius 1) with enough vertices for vertex waves.</summary>
        public static Mesh DiscGrid(int rings = 24, int segments = 64)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var tris = new List<int>();
            for (int r = 1; r <= rings; r++)
            {
                float rad = (float)r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                tris.Add(0); tris.Add(1 + (s + 1) % segments); tris.Add(1 + s);
            }
            for (int r = 1; r < rings; r++)
            {
                int a0 = 1 + (r - 1) * segments, b0 = 1 + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    tris.Add(a0 + s); tris.Add(a0 + s1); tris.Add(b0 + s1);
                    tris.Add(a0 + s); tris.Add(b0 + s1); tris.Add(b0 + s);
                }
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Noisy mountain with vertex colours (grass, rock, snow). Base radius 1, height 1.</summary>
        public static Mesh Mountain(int seed, bool snowy)
        {
            const int rings = 18, segs = 40;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            float o = seed * 9.73f;
            for (int r = 0; r <= rings; r++)
            {
                float rad = (float)r / rings;
                for (int s = 0; s < segs; s++)
                {
                    float a = s * Mathf.PI * 2f / segs;
                    float nx = Mathf.Cos(a), nz = Mathf.Sin(a);
                    float n = Mathf.PerlinNoise(o + nx * 1.7f + rad * 2.3f, o + nz * 1.7f + rad * 1.9f);
                    float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(o + a * 1.3f, rad * 3f) * 2f - 1f);
                    float h = Mathf.Pow(1f - rad, 1.5f) * (0.75f + 0.45f * n) + ridge * 0.08f * (1f - rad);
                    if (r == rings) h = 0f;
                    float rr = rad * (0.85f + 0.3f * n);
                    verts.Add(new Vector3(nx * rr, h, nz * rr));
                    Color grass = Color.Lerp(new Color(0.24f, 0.42f, 0.22f), new Color(0.32f, 0.5f, 0.28f), n);
                    Color rock = Color.Lerp(new Color(0.42f, 0.42f, 0.44f), new Color(0.55f, 0.53f, 0.5f), ridge);
                    Color c = Color.Lerp(grass, rock, Mathf.InverseLerp(0.3f, 0.6f, h + (n - 0.5f) * 0.15f));
                    if (snowy) c = Color.Lerp(c, new Color(0.96f, 0.97f, 1f), Mathf.InverseLerp(0.68f, 0.78f, h + (n - 0.5f) * 0.1f));
                    cols.Add(c);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int s1 = (s + 1) % segs;
                    int a0 = r * segs + s, a1 = r * segs + s1, b0 = (r + 1) * segs + s, b1 = (r + 1) * segs + s1;
                    tris.Add(a0); tris.Add(a1); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(b0);
                }
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Lumpy rock: a noisy sphere with grey vertex colours.</summary>
        public static Mesh Rock(int seed)
        {
            var src = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            Vector3[] baseVerts = src != null ? src.vertices : null;
            int[] baseTris = src != null ? src.triangles : null;
            if (baseVerts == null || baseVerts.Length == 0) return null;
            float o = seed * 3.31f;
            var verts = new Vector3[baseVerts.Length];
            var cols = new Color[baseVerts.Length];
            for (int i = 0; i < baseVerts.Length; i++)
            {
                Vector3 v = baseVerts[i].normalized;
                float n = Mathf.PerlinNoise(o + v.x * 1.8f + v.y, o + v.z * 1.8f - v.y);
                float k = 0.75f + 0.5f * n;
                verts[i] = new Vector3(v.x * k, Mathf.Max(v.y * k * 0.7f, -0.2f), v.z * k);
                float g = 0.42f + 0.25f * n + (v.y > 0.6f ? 0.05f : 0f);
                cols[i] = v.y > 0.7f && n > 0.55f ? new Color(0.35f, 0.5f, 0.25f) : new Color(g, g * 0.98f, g * 0.94f);
            }
            var m = new Mesh { vertices = verts, colors = cols, triangles = baseTris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Duplicates triangles with flipped winding/normals so a thin mesh renders from both sides.</summary>
        public static void MakeDoubleSided(List<Vector3> verts, List<Vector3> norms, List<int> tris)
        {
            int n = verts.Count, t = tris.Count;
            for (int i = 0; i < n; i++) { verts.Add(verts[i]); norms.Add(-norms[i]); }
            for (int i = 0; i < t; i += 3) { tris.Add(tris[i] + n); tris.Add(tris[i + 2] + n); tris.Add(tris[i + 1] + n); }
        }

        /// <summary>Palm crown: drooping fronds radiating from the origin.</summary>
        public static Mesh PalmFronds(int fronds, int seed)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            const int segs = 7;
            for (int f = 0; f < fronds; f++)
            {
                float yaw = f * Mathf.PI * 2f / fronds + (float)rnd.NextDouble() * 0.4f;
                float len = 3.2f + (float)rnd.NextDouble() * 1.2f;
                float droop = 0.9f + (float)rnd.NextDouble() * 0.6f;
                Vector3 dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                int b = verts.Count;
                for (int i = 0; i <= segs; i++)
                {
                    float t = (float)i / segs;
                    Vector3 spine = dir * (len * t) + Vector3.up * (0.6f * t - droop * t * t * 1.4f);
                    float w = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.15f + 0.05f)) * 0.55f;
                    Vector3 nrm = Vector3.Cross(side, dir * len + Vector3.up * (0.6f - 2.8f * droop * t)).normalized;
                    if (nrm.y < 0f) nrm = -nrm;
                    verts.Add(spine - side * w + Vector3.down * w * 0.25f); norms.Add(nrm);
                    verts.Add(spine); norms.Add(nrm);
                    verts.Add(spine + side * w + Vector3.down * w * 0.25f); norms.Add(nrm);
                }
                for (int i = 0; i < segs; i++)
                {
                    int r0 = b + i * 3, r1 = b + (i + 1) * 3;
                    tris.Add(r0); tris.Add(r0 + 1); tris.Add(r1 + 1);
                    tris.Add(r0); tris.Add(r1 + 1); tris.Add(r1);
                    tris.Add(r0 + 1); tris.Add(r0 + 2); tris.Add(r1 + 2);
                    tris.Add(r0 + 1); tris.Add(r1 + 2); tris.Add(r1 + 1);
                }
            }
            MakeDoubleSided(verts, norms, tris);
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Curved palm trunk made of rings, height ~h, leaning along +x.</summary>
        public static Mesh PalmTrunk(float h, float lean)
        {
            const int rings = 10, segs = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                Vector3 c = new Vector3(lean * t * t, h * t, 0f);
                float rad = Mathf.Lerp(0.28f, 0.16f, t) * (r % 2 == 0 ? 1.08f : 1f);
                for (int s = 0; s < segs; s++)
                {
                    float a = s * Mathf.PI * 2f / segs;
                    verts.Add(c + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int s1 = (s + 1) % segs;
                    int a0 = r * segs + s, a1 = r * segs + s1, b0 = (r + 1) * segs + s, b1 = (r + 1) * segs + s1;
                    tris.Add(a0); tris.Add(b1); tris.Add(a1);
                    tris.Add(a0); tris.Add(b0); tris.Add(b1);
                }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Subdivided rectangle (0..w along +z, 0..-h along -y) used for the waving flag.</summary>
        public static Mesh FlagMesh(int cols, int rows, float w, float h)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                {
                    verts.Add(new Vector3(0f, -h * y / rows, w * x / cols));
                    norms.Add(Vector3.right);
                }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * (cols + 1) + x;
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + cols + 2);
                    tris.Add(i); tris.Add(i + cols + 2); tris.Add(i + cols + 1);
                }
            MakeDoubleSided(verts, norms, tris);
            var m = new Mesh();
            m.MarkDynamic();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
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

        /// <summary>Alpha texture with grass blades (optionally with flower heads) for terrain detail billboards.</summary>
        public static Texture2D GrassBlades(int size = 64, int seed = 5, bool flowers = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.4f, 0.6f, 0.3f, 0);
            var rnd = new System.Random(seed);
            Color[] petals = { new Color(1f, 0.45f, 0.65f), new Color(1f, 0.9f, 0.3f), new Color(1f, 1f, 1f), new Color(0.7f, 0.55f, 1f) };
            int blades = flowers ? 6 : 11;
            for (int b = 0; b < blades; b++)
            {
                float x0 = (float)(rnd.NextDouble() * 0.8 + 0.1) * size;
                float lean = (float)(rnd.NextDouble() - 0.5) * size * 0.45f;
                float h = (float)(rnd.NextDouble() * 0.45 + 0.5) * size * (flowers ? 0.8f : 1f);
                float w = size * (flowers ? 0.025f : 0.045f);
                float tint = (float)rnd.NextDouble() * 0.2f;
                float cx = x0;
                for (int y = 0; y < (int)h; y++)
                {
                    float f = y / h;
                    cx = x0 + lean * f * f;
                    float hw = w * (1f - f * 0.9f) + 0.5f;
                    for (int x = Mathf.Max(0, (int)(cx - hw)); x <= Mathf.Min(size - 1, (int)(cx + hw)); x++)
                    {
                        float shade = 0.55f + 0.45f * f + tint;
                        px[y * size + x] = flowers ? new Color(0.35f * shade, 0.6f * shade, 0.25f * shade, 1f) : new Color(shade, shade, shade, 1f);
                    }
                }
                if (flowers)
                {
                    Color pc = petals[rnd.Next(petals.Length)];
                    int r = Mathf.Max(2, size / 14);
                    int fy = (int)h;
                    for (int y = -r; y <= r; y++)
                        for (int x = -r; x <= r; x++)
                        {
                            if (x * x + y * y > r * r) continue;
                            int xx = (int)cx + x, yy = fy + y;
                            if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                            bool centre = x * x + y * y <= (r / 2) * (r / 2);
                            px[yy * size + xx] = centre ? new Color(1f, 0.8f, 0.2f, 1f) : pc;
                        }
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Tileable normal map from fractal noise. Stored as RGB = normal (A = 1), which
        /// UnpackNormal decodes correctly on both DXT5nm and plain RGB platforms.
        /// </summary>
        public static Texture2D NormalMap(int size, float scale, float strength, float detail, int seed, float ripples = 0f)
        {
            var hgt = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float h = TileNoise(u, v, scale, seed) + TileNoise(u, v, scale * 3f, seed + 3) * 0.5f * detail
                              + Hash(x, y, seed) * 0.15f * detail;
                    if (ripples > 0f) h += Mathf.Sin((u * 6f + TileNoise(u, v, 3f, seed + 9) * 0.6f) * Mathf.PI * 2f * ripples) * 0.35f;
                    hgt[y * size + x] = h;
                }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = hgt[y * size + (x + size - 1) % size], r = hgt[y * size + (x + 1) % size];
                    float d = hgt[((y + size - 1) % size) * size + x], u = hgt[((y + 1) % size) * size + x];
                    Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true);
            return t;
        }

        /// <summary>Soft puffy cloud sprite.</summary>
        public static Texture2D CloudPuff(int size = 128)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.6f + Mathf.PerlinNoise(x * 0.15f + 7f, y * 0.15f) * 0.4f;
                    float a = Mathf.Clamp01((1f - d) * 1.6f - (1f - n) * 0.55f);
                    a = a * a * (3f - 2f * a);
                    float shade = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(dy * 0.5f + 0.6f + (n - 0.5f) * 0.4f));
                    px[y * size + x] = new Color(shade, shade, shade * 1.02f, a);
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(true);
            return t;
        }

        /// <summary>Rounded rectangle for 9-sliced HUD panels.</summary>
        public static Texture2D RoundedRect(int size, int radius, Color top, Color bottom, Color border, int borderWidth)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float inside = Mathf.Clamp01(radius - d + 0.5f);
                    float edge = Mathf.Clamp01(d - (radius - borderWidth) + 0.5f);
                    Color fill = Color.Lerp(bottom, top, (float)y / (size - 1));
                    Color c = Color.Lerp(fill, border, edge);
                    c.a *= inside;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply();
            return t;
        }

        /// <summary>Radial gradient used for the cup so it reads as a deep hole.</summary>
        public static Texture2D CupTexture(int size = 64)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float g = Mathf.Lerp(0.0f, 0.22f, Mathf.Pow(d, 3f)) + (dy > 0.2f && d > 0.75f ? 0.12f : 0f);
                    px[y * size + x] = new Color(g, g, g * 1.05f, 1f);
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
