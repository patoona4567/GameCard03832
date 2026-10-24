using System.Collections.Generic;
using UnityEngine;

namespace PangGolf
{
    /// <summary>
    /// Procedural vegetation made from alpha-tested leaf cards (instead of plain spheres):
    /// leafy broadleaf trees, blossom trees, pines, palms, bushes, flower bushes and textured rocks.
    /// Meshes, textures and materials are generated once and shared by every hole.
    /// </summary>
    public static class TreeFactory
    {
        public enum Kind { Round, Blossom, Pine, Bush, FlowerBush }

        static bool ready;
        static Material barkMat, palmBarkMat, rockMat, pineMat, blossomMat, palmLeafMat, flowerBushMat;
        static Material[] leafMats, bushMats;
        static Mesh[] roundCrowns, pineCrowns, bushCrowns, trunks, pineTrunks;

        static void Init()
        {
            if (ready) return;
            ready = true;

            var leaf = LeafTexture(256, 1, LeafStyle.Leaves);
            var blossom = LeafTexture(256, 2, LeafStyle.Blossom);
            var flowers = LeafTexture(256, 3, LeafStyle.FlowerBush);
            var needles = NeedleTexture(256);
            var palm = PalmLeafTexture(256);

            leafMats = new[]
            {
                ProcGen.FoliageCards(leaf, new Color(0.42f, 0.7f, 0.26f)),
                ProcGen.FoliageCards(leaf, new Color(0.5f, 0.76f, 0.28f)),
                ProcGen.FoliageCards(leaf, new Color(0.62f, 0.78f, 0.27f)),
                ProcGen.FoliageCards(leaf, new Color(0.36f, 0.62f, 0.24f)),
            };
            bushMats = new[]
            {
                ProcGen.FoliageCards(leaf, new Color(0.4f, 0.66f, 0.25f), 0.05f),
                ProcGen.FoliageCards(leaf, new Color(0.33f, 0.58f, 0.22f), 0.05f),
            };
            blossomMat = ProcGen.FoliageCards(blossom, Color.white, 0.1f);
            flowerBushMat = ProcGen.FoliageCards(flowers, Color.white, 0.05f);
            pineMat = ProcGen.FoliageCards(needles, new Color(0.3f, 0.55f, 0.32f), 0.06f);
            palmLeafMat = ProcGen.FoliageCards(palm, new Color(0.55f, 0.8f, 0.3f), 0.22f);

            Texture2D barkN, rockN, palmN;
            var bark = BarkTexture(256, out barkN, false);
            var palmBark = BarkTexture(256, out palmN, true);
            var rock = RockTexture(256, out rockN);
            barkMat = ProcGen.Textured(bark, barkN, Color.white, 0.05f);
            palmBarkMat = ProcGen.Textured(palmBark, palmN, Color.white, 0.05f);
            rockMat = ProcGen.Textured(rock, rockN, Color.white, 0.18f, 2f);

            roundCrowns = new Mesh[6];
            for (int i = 0; i < roundCrowns.Length; i++) roundCrowns[i] = Crown(100 + i, 110, 4, new Vector3(2.5f, 2.1f, 2.5f), 1.6f);
            bushCrowns = new Mesh[3];
            for (int i = 0; i < bushCrowns.Length; i++) bushCrowns[i] = Crown(200 + i, 34, 3, new Vector3(0.95f, 0.65f, 0.95f), 0.75f);
            pineCrowns = new Mesh[3];
            for (int i = 0; i < pineCrowns.Length; i++) pineCrowns[i] = PineCrown(300 + i, 140, 6.8f, 2.7f, 1.7f);
            trunks = new Mesh[4];
            for (int i = 0; i < trunks.Length; i++) trunks[i] = Trunk(400 + i, 4.4f, 0.26f, 3);
            pineTrunks = new Mesh[2];
            for (int i = 0; i < pineTrunks.Length; i++) pineTrunks[i] = Trunk(500 + i, 7.5f, 0.24f, 0);
        }

        public static Material RockMaterial { get { Init(); return rockMat; } }

        // ------------------------------------------------------------------ Builders

        /// <summary>Builds a tree/bush at <paramref name="pos"/>. Returns its root.</summary>
        public static Transform Make(Kind kind, Transform parent, Vector3 pos, float s, bool colliders)
        {
            Init();
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            root.localScale = Vector3.one * s;

            switch (kind)
            {
                case Kind.Round:
                case Kind.Blossom:
                {
                    ProcGen.MeshObject("Trunk", trunks[Random.Range(0, trunks.Length)], root, barkMat);
                    var mat = kind == Kind.Blossom ? blossomMat : leafMats[Random.Range(0, leafMats.Length)];
                    var crown = ProcGen.MeshObject("Crown", roundCrowns[Random.Range(0, roundCrowns.Length)], root, mat);
                    crown.transform.localPosition = new Vector3(0f, 5.2f, 0f);
                    crown.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    if (colliders)
                    {
                        AddCapsule(root.gameObject, 0.3f, 4.4f, new Vector3(0f, 2.2f, 0f));
                        var sc = crown.AddComponent<SphereCollider>();
                        sc.radius = 2.2f;
                    }
                    break;
                }
                case Kind.Pine:
                {
                    ProcGen.MeshObject("Trunk", pineTrunks[Random.Range(0, pineTrunks.Length)], root, barkMat);
                    var crown = ProcGen.MeshObject("Crown", pineCrowns[Random.Range(0, pineCrowns.Length)], root, pineMat);
                    crown.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                    crown.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    if (colliders)
                    {
                        var cc = crown.AddComponent<CapsuleCollider>();
                        cc.direction = 1; cc.radius = 1.6f; cc.height = 6.8f; cc.center = new Vector3(0f, 3f, 0f);
                    }
                    break;
                }
                case Kind.Bush:
                case Kind.FlowerBush:
                {
                    var mat = kind == Kind.FlowerBush ? flowerBushMat : bushMats[Random.Range(0, bushMats.Length)];
                    var crown = ProcGen.MeshObject("Bush", bushCrowns[Random.Range(0, bushCrowns.Length)], root, mat);
                    crown.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                    break;
                }
            }
            return root;
        }

        public static Transform MakePalm(Transform parent, Vector3 pos, float s)
        {
            Init();
            var palm = new GameObject("Palm").transform;
            palm.SetParent(parent, false);
            palm.position = pos;
            palm.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            float h = Random.Range(6f, 8.5f) * s;
            float lean = Random.Range(0.8f, 2.2f) * s;
            ProcGen.MeshObject("Trunk", ProcGen.PalmTrunk(h, lean), palm, palmBarkMat);
            var crown = ProcGen.MeshObject("Fronds", ProcGen.PalmFronds(Random.Range(9, 13), Random.Range(0, 9999)), palm, palmLeafMat);
            crown.transform.localPosition = new Vector3(lean, h, 0f);
            crown.transform.localScale = Vector3.one * s * 1.1f;
            AddCapsule(palm.gameObject, 0.3f * s, h, new Vector3(lean * 0.4f, h * 0.5f, 0f));
            var crownCol = crown.AddComponent<SphereCollider>();
            crownCol.radius = 2.2f; crownCol.center = new Vector3(0, -0.4f, 0);
            var nutMat = ProcGen.Lit(new Color(0.35f, 0.24f, 0.12f), 0.3f);
            for (int k = 0; k < 3; k++)
            {
                var coconut = ProcGen.Prim(PrimitiveType.Sphere, crown.transform, nutMat);
                coconut.transform.localPosition = new Vector3(Mathf.Cos(k * 2.1f) * 0.3f, -0.25f, Mathf.Sin(k * 2.1f) * 0.3f);
                coconut.transform.localScale = Vector3.one * 0.32f;
            }
            return palm;
        }

        public static Transform MakeRock(Transform parent, Vector3 pos, float sc)
        {
            Init();
            var mesh = ProcGen.Rock(Random.Range(0, 9999));
            if (mesh == null) return null;
            var rock = ProcGen.MeshObject("Rock", mesh, parent, rockMat);
            rock.transform.position = pos + Vector3.down * 0.15f * sc;
            rock.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            rock.transform.localScale = new Vector3(sc * Random.Range(0.9f, 1.4f), sc, sc * Random.Range(0.9f, 1.4f));
            rock.AddComponent<SphereCollider>().radius = 0.75f;
            return rock.transform;
        }

        static void AddCapsule(GameObject go, float radius, float height, Vector3 centre)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1; c.radius = radius; c.height = height; c.center = centre;
        }

        // ------------------------------------------------------------------ Meshes

        /// <summary>Canopy of leaf cards spread over a few lobes; normals point out from the centre for soft shading.</summary>
        static Mesh Crown(int seed, int cards, int lobes, Vector3 radii, float cardSize)
        {
            var rnd = new System.Random(seed);
            var lobeCentres = new Vector3[lobes];
            for (int l = 0; l < lobes; l++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = l == 0 ? 0f : 0.45f + (float)rnd.NextDouble() * 0.25f;
                lobeCentres[l] = new Vector3(Mathf.Cos(a) * r * radii.x, ((float)rnd.NextDouble() - 0.4f) * radii.y * 0.6f, Mathf.Sin(a) * r * radii.z);
            }
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < cards; i++)
            {
                Vector3 lc = lobeCentres[i % lobes];
                float lobeK = i % lobes == 0 ? 1f : 0.65f;
                Vector3 dir = RandomDir(rnd);
                float r = Mathf.Lerp(0.5f, 1f, (float)rnd.NextDouble());
                Vector3 p = lc + Vector3.Scale(dir * r, radii) * lobeK;
                AddCard(verts, norms, uvs, tris, rnd, p, cardSize * (0.8f + (float)rnd.NextDouble() * 0.45f), radii);
            }
            return BuildMesh(verts, norms, uvs, tris);
        }

        /// <summary>Conical pine canopy: drooping needle cards in tiers.</summary>
        static Mesh PineCrown(int seed, int cards, float height, float radius, float cardSize)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < cards; i++)
            {
                float t = Mathf.Pow((float)rnd.NextDouble(), 0.8f);
                float y = t * height;
                float rr = radius * (1f - t) * (0.55f + 0.45f * (float)rnd.NextDouble()) + 0.3f;
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                // Cards lie mostly flat, tilted down and outwards like branches
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 n = (Vector3.up * 0.85f + outward * 0.5f).normalized;
                Vector3 tan = Vector3.Cross(n, outward).normalized;
                Vector3 bit = Vector3.Cross(tan, n);
                float s = cardSize * (1f - t * 0.55f) * (0.8f + (float)rnd.NextDouble() * 0.4f);
                Vector3 smoothN = (outward * 0.65f + Vector3.up * 0.75f).normalized;
                AddQuad(verts, norms, uvs, tris, p + outward * s * 0.3f, tan * s, bit * s, smoothN);
            }
            return BuildMesh(verts, norms, uvs, tris);
        }

        static void AddCard(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                            System.Random rnd, Vector3 p, float size, Vector3 radii)
        {
            Vector3 n = (RandomDir(rnd) + Vector3.Scale(p, new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z))).normalized;
            Vector3 tan = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            float spin = (float)rnd.NextDouble() * Mathf.PI * 2f;
            Vector3 bit = Vector3.Cross(tan, n);
            Vector3 t2 = tan * Mathf.Cos(spin) + bit * Mathf.Sin(spin);
            Vector3 b2 = Vector3.Cross(t2, n);
            Vector3 smoothN = (Vector3.Scale(p, new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z)) + Vector3.up * 0.35f).normalized;
            AddQuad(verts, norms, uvs, tris, p, t2 * size, b2 * size, smoothN);
        }

        static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                            Vector3 centre, Vector3 tan, Vector3 bit, Vector3 normal)
        {
            int b = verts.Count;
            verts.Add(centre - tan * 0.5f - bit * 0.5f); uvs.Add(new Vector2(0f, 0f));
            verts.Add(centre - tan * 0.5f + bit * 0.5f); uvs.Add(new Vector2(0f, 1f));
            verts.Add(centre + tan * 0.5f + bit * 0.5f); uvs.Add(new Vector2(1f, 1f));
            verts.Add(centre + tan * 0.5f - bit * 0.5f); uvs.Add(new Vector2(1f, 0f));
            for (int i = 0; i < 4; i++) norms.Add(normal);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        static Vector3 RandomDir(System.Random rnd)
        {
            float z = (float)rnd.NextDouble() * 2f - 1f;
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(Mathf.Cos(a) * r, z, Mathf.Sin(a) * r);
        }

        static Mesh BuildMesh(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            var m = new Mesh();
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Slightly bent, tapered trunk with branches reaching into the crown.</summary>
        static Mesh Trunk(int seed, float height, float radius, int branches)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float bendX = ((float)rnd.NextDouble() - 0.5f) * 0.5f, bendZ = ((float)rnd.NextDouble() - 0.5f) * 0.5f;
            var spine = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                spine.Add(new Vector3(bendX * t * t, height * t, bendZ * t * t));
                radii.Add(radius * Mathf.Lerp(1.35f, 0.45f, t) * (i == 0 ? 1.25f : 1f));
            }
            Tube(verts, uvs, tris, spine, radii, 8);
            for (int b = 0; b < branches; b++)
            {
                float a = b * Mathf.PI * 2f / branches + (float)rnd.NextDouble() * 0.8f;
                Vector3 start = new Vector3(bendX * 0.4f, height * (0.55f + 0.1f * b), bendZ * 0.4f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 1.1f, Mathf.Sin(a)).normalized;
                float len = 1.4f + (float)rnd.NextDouble() * 0.8f;
                var bs = new List<Vector3> { start, start + dir * len * 0.5f, start + dir * len + Vector3.up * 0.2f };
                var br = new List<float> { radius * 0.45f, radius * 0.32f, radius * 0.15f };
                Tube(verts, uvs, tris, bs, br, 6);
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void Tube(List<Vector3> verts, List<Vector2> uvs, List<int> tris, List<Vector3> spine, List<float> radii, int segs)
        {
            int baseIdx = verts.Count;
            float v = 0f;
            for (int i = 0; i < spine.Count; i++)
            {
                Vector3 fwd = (i < spine.Count - 1 ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1]).normalized;
                Vector3 side = Vector3.Cross(fwd, Mathf.Abs(fwd.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(side, fwd);
                if (i > 0) v += Vector3.Distance(spine[i], spine[i - 1]) / 1.2f;
                for (int s = 0; s <= segs; s++)
                {
                    float a = s * Mathf.PI * 2f / segs;
                    verts.Add(spine[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radii[i]);
                    uvs.Add(new Vector2((float)s / segs, v));
                }
            }
            int ring = segs + 1;
            for (int i = 0; i < spine.Count - 1; i++)
                for (int s = 0; s < segs; s++)
                {
                    int a0 = baseIdx + i * ring + s, a1 = a0 + 1, b0 = a0 + ring, b1 = b0 + 1;
                    tris.Add(a0); tris.Add(b0); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(a1);
                }
        }

        // ------------------------------------------------------------------ Textures

        enum LeafStyle { Leaves, Blossom, FlowerBush }

        /// <summary>Cluster of leaves on a transparent background (alpha-tested card).</summary>
        static Texture2D LeafTexture(int size, int seed, LeafStyle style)
        {
            var rnd = new System.Random(seed);
            var px = new Color[size * size];
            Color bg = style == LeafStyle.Blossom ? new Color(1f, 0.82f, 0.88f, 0f) : new Color(0.75f, 0.8f, 0.7f, 0f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            int count = style == LeafStyle.Blossom ? 64 : 80;
            for (int k = 0; k < count; k++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Sqrt((float)rnd.NextDouble()) * 0.38f;
                float cx = (0.5f + Mathf.Cos(ang) * dist) * size, cy = (0.5f + Mathf.Sin(ang) * dist) * size;
                float dirA = ang + ((float)rnd.NextDouble() - 0.5f) * 1.4f;
                float len = size * (0.12f + (float)rnd.NextDouble() * 0.07f);
                float wid = len * 0.42f;
                float val = 0.72f + (float)rnd.NextDouble() * 0.28f;
                float hue = ((float)rnd.NextDouble() - 0.5f) * 0.12f;
                Color leafCol = new Color(val * (1f + hue), val, val * (0.92f - hue));
                if (style == LeafStyle.Blossom)
                    leafCol = (float)rnd.NextDouble() < 0.75f ? new Color(1f, 0.72f + 0.2f * val, 0.82f + 0.12f * val) : new Color(0.5f, 0.72f, 0.38f) * val;
                DrawLeaf(px, size, cx, cy, dirA, len, wid, leafCol);
            }
            if (style == LeafStyle.FlowerBush)
            {
                Color[] petals = { new Color(1f, 0.4f, 0.6f), new Color(1f, 0.88f, 0.25f), new Color(1f, 1f, 1f), new Color(0.7f, 0.5f, 1f), new Color(1f, 0.5f, 0.3f) };
                for (int k = 0; k < 18; k++)
                {
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    float dist = Mathf.Sqrt((float)rnd.NextDouble()) * 0.36f;
                    float cx = (0.5f + Mathf.Cos(ang) * dist) * size, cy = (0.5f + Mathf.Sin(ang) * dist) * size;
                    Color pc = petals[rnd.Next(petals.Length)];
                    float pr = size * 0.035f;
                    for (int p = 0; p < 5; p++)
                    {
                        float pa = p * Mathf.PI * 2f / 5f;
                        DrawLeaf(px, size, cx + Mathf.Cos(pa) * pr, cy + Mathf.Sin(pa) * pr, pa, pr * 2.2f, pr * 1.6f, pc);
                    }
                    Disc(px, size, cx, cy, pr * 0.6f, new Color(1f, 0.85f, 0.2f));
                }
                // Leaves in the flower bush texture are green
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a > 0f && Mathf.Abs(c.r - c.g) < 0.15f && Mathf.Abs(c.g - c.b) < 0.2f)
                        px[i] = new Color(c.r * 0.42f, c.g * 0.68f, c.b * 0.28f, c.a);
                }
            }
            return Finish(px, size);
        }

        static void DrawLeaf(Color[] px, int size, float cx, float cy, float ang, float len, float wid, Color col)
        {
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            int r = Mathf.CeilToInt(len);
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
            {
                if (y < 0 || y >= size) continue;
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                {
                    if (x < 0 || x >= size) continue;
                    float dx = x - cx, dy = y - cy;
                    float a = (dx * ca + dy * sa) / len;       // along -0.5..0.5
                    float b = (-dx * sa + dy * ca);            // across (pixels)
                    if (a < -0.5f || a > 0.5f) continue;
                    float half = wid * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * (a + 0.5f)), 0.75f);
                    if (Mathf.Abs(b) > half) continue;
                    float edge = Mathf.Abs(b) / Mathf.Max(half, 0.01f);
                    float shade = 1f - edge * 0.22f - (Mathf.Abs(b) < 0.7f ? 0.18f : 0f) + (a + 0.5f) * 0.06f;
                    var c = col * shade;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
        }

        static void Disc(Color[] px, int size, float cx, float cy, float r, Color col)
        {
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) { var c = col; c.a = 1f; px[y * size + x] = c; }
                }
        }

        /// <summary>Pine twigs with needles.</summary>
        static Texture2D NeedleTexture(int size)
        {
            var rnd = new System.Random(7);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.6f, 0.7f, 0.6f, 0f);
            for (int t = 0; t < 7; t++)
            {
                float y0 = size * (0.15f + t * 0.11f), x0 = size * 0.05f, x1 = size * 0.95f;
                float droop = size * 0.08f;
                for (float x = x0; x < x1; x += 1.5f)
                {
                    float f = (x - x0) / (x1 - x0);
                    float y = y0 - droop * f * f + Mathf.Sin(f * 7f + t) * 2f;
                    Line(px, size, x, y, x + 1.5f, y, 1.6f, new Color(0.45f, 0.35f, 0.25f));
                    if (rnd.NextDouble() < 0.55)
                    {
                        float l = size * (0.05f + 0.04f * (float)rnd.NextDouble()) * (1f - f * 0.4f);
                        float v = 0.75f + 0.25f * (float)rnd.NextDouble();
                        var c = new Color(v * 0.95f, v, v * 0.95f);
                        Line(px, size, x, y, x + l * 0.5f, y + l, 1.2f, c);
                        Line(px, size, x, y, x + l * 0.5f, y - l, 1.2f, c);
                    }
                }
            }
            return Finish(px, size);
        }

        /// <summary>Feathery palm frond: a central rib with leaflets (u across, v along).</summary>
        static Texture2D PalmLeafTexture(int size)
        {
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.7f, 0.85f, 0.6f, 0f);
            float cx = size * 0.5f;
            for (int y = 0; y < size; y += 3)
            {
                float t = (float)y / size;
                float len = size * 0.48f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f + 0.04f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float v = 0.8f + 0.2f * Mathf.PerlinNoise(y * 0.2f, side * 3f);
                    Line(px, size, cx, y, cx + side * len, y + len * 0.45f, 2.2f, new Color(v * 0.9f, v, v * 0.8f));
                }
            }
            Line(px, size, cx, 0, cx, size - 1, 3f, new Color(0.85f, 0.85f, 0.6f));
            return Finish(px, size);
        }

        static void Line(Color[] px, int size, float x0, float y0, float x1, float y1, float w, Color c)
        {
            float len = Mathf.Max(1f, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
            int steps = Mathf.CeilToInt(len);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = Mathf.Lerp(x0, x1, t), y = Mathf.Lerp(y0, y1, t);
                float r = w * 0.5f * (1f - t * 0.5f);
                for (int yy = (int)(y - r); yy <= (int)(y + r); yy++)
                    for (int xx = (int)(x - r); xx <= (int)(x + r); xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                        var cc = c; cc.a = 1f;
                        px[yy * size + xx] = cc;
                    }
            }
        }

        /// <summary>Uploads the texture and keeps alpha coverage in the mips so cards don't thin out at a distance.</summary>
        static Texture2D Finish(Color[] px, int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.Apply(true);
            for (int m = 1; m < t.mipmapCount; m++)
            {
                var mp = t.GetPixels(m);
                float boost = 1f + m * 0.35f;
                for (int i = 0; i < mp.Length; i++) mp[i].a = Mathf.Clamp01(mp[i].a * boost);
                t.SetPixels(mp, m);
            }
            t.Apply(false);
            return t;
        }

        static Texture2D BarkTexture(int size, out Texture2D normal, bool palm)
        {
            var h = new float[size * size];
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n;
                    if (palm)
                    {
                        // Ring scars typical of palm trunks
                        float ring = Mathf.Abs(Mathf.Sin((v * 8f + ProcGen.TileNoise(u, v, 4f, 3) * 0.3f) * Mathf.PI));
                        n = Mathf.Pow(ring, 0.4f) * 0.8f + ProcGen.TileNoise(u, v, 24f, 5) * 0.2f;
                    }
                    else
                    {
                        float streak = ProcGen.TileNoise2(u, v, 24f, 3f, 11);
                        float fine = ProcGen.TileNoise(u, v, 32f, 12);
                        n = Mathf.Pow(streak, 1.5f) * 0.8f + fine * 0.2f;
                    }
                    h[y * size + x] = n;
                    Color dark = palm ? new Color(0.36f, 0.3f, 0.22f) : new Color(0.22f, 0.15f, 0.1f);
                    Color light = palm ? new Color(0.7f, 0.6f, 0.45f) : new Color(0.52f, 0.38f, 0.26f);
                    px[y * size + x] = Color.Lerp(dark, light, n);
                }
            normal = ProcGen.NormalFromHeight(h, size, 6f);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 4;
            t.Apply(true);
            return t;
        }

        static Texture2D RockTexture(int size, out Texture2D normal)
        {
            var h = new float[size * size];
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float big = ProcGen.TileNoise(u, v, 5f, 21);
                    float mid = ProcGen.TileNoise(u, v, 14f, 22);
                    float fine = ProcGen.TileNoise(u, v, 40f, 23);
                    float crack = 1f - Mathf.Abs(ProcGen.TileNoise(u, v, 9f, 24) * 2f - 1f);
                    float n = big * 0.5f + mid * 0.3f + fine * 0.2f - Mathf.Pow(crack, 8f) * 0.4f;
                    h[y * size + x] = n;
                    float g = 0.38f + n * 0.35f;
                    Color c = new Color(g, g * 0.97f, g * 0.92f);
                    float moss = Mathf.Clamp01((ProcGen.TileNoise(u, v, 6f, 25) - 0.62f) * 4f);
                    c = Color.Lerp(c, new Color(0.3f, 0.45f, 0.2f), moss * 0.7f);
                    px[y * size + x] = c;
                }
            normal = ProcGen.NormalFromHeight(h, size, 8f);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 4;
            t.Apply(true);
            return t;
        }
    }
}
