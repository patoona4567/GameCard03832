using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PangGolf
{
    [System.Serializable]
    public class HoleDef
    {
        public string name;
        public int par;
        public Vector2[] path;          // tee ... green centre, world XZ in metres
        public float fairwayWidth = 40f;
        public float fairwayStart = 0.12f; // fraction of the path before the fairway begins
        public float greenRadius = 15f;
        public Vector2 pinOffset;
        public Vector3[] bunkers = new Vector3[0]; // x, z, radius
        public Vector3[] ponds = new Vector3[0];   // x, z, radius
        public float hills = 5f;
        public float rise;               // green elevation relative to tee
        public Vector2 greenTilt;        // green slope (m per m)
        public int seed;
        public float windMax = 6f;
    }

    public static class Holes
    {
        public static readonly HoleDef[] All =
        {
            new HoleDef
            {
                name = "Emerald Bay", par = 4,
                path = new[] { new Vector2(320, 70), new Vector2(318, 250), new Vector2(360, 400) },
                fairwayWidth = 42f, greenRadius = 15f, pinOffset = new Vector2(3, -4),
                bunkers = new[] { new Vector3(292, 238, 9), new Vector3(350, 262, 8), new Vector3(343, 386, 6), new Vector3(377, 412, 6) },
                ponds = new[] { new Vector3(290, 330, 18) },
                hills = 5f, rise = 3f, greenTilt = new Vector2(0.025f, -0.015f), seed = 11, windMax = 5f
            },
            new HoleDef
            {
                name = "Island Breeze", par = 3,
                path = new[] { new Vector2(300, 170), new Vector2(330, 320) },
                fairwayWidth = 34f, fairwayStart = 0.78f, greenRadius = 15f, pinOffset = new Vector2(-4, 3),
                bunkers = new[] { new Vector3(348, 332, 6), new Vector3(312, 336, 6), new Vector3(344, 302, 5) },
                ponds = new[] { new Vector3(318, 258, 26) },
                hills = 4f, rise = -4f, greenTilt = new Vector2(-0.02f, 0.02f), seed = 23, windMax = 7f
            },
            new HoleDef
            {
                name = "Sunset Ridge", par = 5,
                path = new[] { new Vector2(200, 70), new Vector2(215, 230), new Vector2(340, 350), new Vector2(400, 470) },
                fairwayWidth = 40f, greenRadius = 17f, pinOffset = new Vector2(5, 4),
                bunkers = new[] { new Vector3(180, 250, 10), new Vector3(250, 212, 8), new Vector3(385, 452, 7), new Vector3(420, 482, 6) },
                ponds = new[] { new Vector3(300, 382, 20) },
                hills = 7f, rise = 8f, greenTilt = new Vector2(0.015f, 0.02f), seed = 37, windMax = 8f
            },
            new HoleDef
            {
                name = "Dragon's Tail", par = 4,
                path = new[] { new Vector2(470, 100), new Vector2(460, 270), new Vector2(330, 380) },
                fairwayWidth = 38f, greenRadius = 15f, pinOffset = new Vector2(-3, -5),
                bunkers = new[] { new Vector3(490, 282, 9), new Vector3(350, 398, 6), new Vector3(316, 360, 6) },
                ponds = new[] { new Vector3(415, 255, 18) },
                hills = 6f, rise = -2f, greenTilt = new Vector2(-0.02f, -0.02f), seed = 51, windMax = 9f
            },
        };
    }

    /// <summary>
    /// One procedurally generated hole: terrain, surfaces, hazards, trees, flag.
    /// Implements <see cref="IGround"/> for the ball physics.
    /// </summary>
    public class Course : MonoBehaviour, IGround
    {
        public const float Size = 640f;
        public const float HeightRange = 120f;
        public const float BaseH = 30f;
        public const float EdgeH = 44f;
        const int HRes = 513;
        const int ARes = 512;
        const int DRes = 512;
        const float ObExtra = 48f;

        public HoleDef hole;
        public Terrain terrain;
        public Vector3 TeePos { get; private set; }
        public Vector3 GreenCenter { get; private set; }
        public Vector3 Cup { get; private set; }
        public bool UseObstacles { get { return true; } }
        public float LengthYards { get; private set; }
        public Texture2D minimap;
        public Rect mapRect;
        public readonly List<Vector3> trees = new List<Vector3>();

        TerrainData td;
        float[] segLen;
        float totalLen;
        float teeH, greenH0;
        float[] waterY;
        float sx, sz;
        Transform flagCloth;
        Vector3 windDir = Vector3.forward;
        float windStrength;

        struct PathInfo { public float d, t; public bool beyond; }

        // ------------------------------------------------------------------ Build

        public static Course Build(HoleDef h, Transform parent)
        {
            var go = new GameObject("Hole - " + h.name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Course>();
            c.Generate(h);
            return c;
        }

        void Generate(HoleDef h)
        {
            hole = h;
            Random.InitState(h.seed);
            sx = h.seed * 3.17f;
            sz = h.seed * 7.31f;

            segLen = new float[h.path.Length - 1];
            totalLen = 0f;
            for (int i = 0; i < segLen.Length; i++) { segLen[i] = Vector2.Distance(h.path[i], h.path[i + 1]); totalLen += segLen[i]; }

            Vector2 tee = h.path[0];
            Vector2 gc = h.path[h.path.Length - 1];
            teeH = BaseHeight(tee.x, tee.y) + 0.35f;
            greenH0 = BaseHeight(gc.x, gc.y);
            waterY = new float[h.ponds.Length];
            for (int i = 0; i < h.ponds.Length; i++) waterY[i] = BaseHeight(h.ponds[i].x, h.ponds[i].y) - 0.8f;

            BuildTerrain();

            TeePos = OnGround(tee.x, tee.y);
            GreenCenter = OnGround(gc.x, gc.y);
            Vector2 pin = gc + h.pinOffset;
            Cup = OnGround(pin.x, pin.y);
            LengthYards = (totalLen) / Clubs.Yard;

            BuildWater();
            BuildTrees();
            BuildStakes();
            BuildTeeMarkers();
            BuildFlag();
            BuildMinimap();
        }

        Vector3 OnGround(float x, float z)
        {
            var p = new Vector3(x, 0, z);
            p.y = GroundY(p);
            return p;
        }

        void BuildTerrain()
        {
            td = new TerrainData();
            td.heightmapResolution = HRes;
            td.size = new Vector3(Size, HeightRange, Size);
            td.alphamapResolution = ARes;
            td.baseMapResolution = 1024;
            td.SetDetailResolution(DRes, 16);

            var heights = new float[HRes, HRes];
            for (int zi = 0; zi < HRes; zi++)
            {
                float z = zi * Size / (HRes - 1);
                for (int xi = 0; xi < HRes; xi++)
                {
                    float x = xi * Size / (HRes - 1);
                    heights[zi, xi] = Mathf.Clamp01(AnalyticHeight(x, z) / HeightRange);
                }
            }
            td.SetHeights(0, 0, heights);

            // Layers: 0 rough, 1 fairway, 2 green, 3 sand, 4 deep rough
            var layers = new TerrainLayer[5];
            layers[0] = Layer(ProcGen.Grass(256, new Color(0.22f, 0.45f, 0.12f), new Color(0.33f, 0.56f, 0.17f), 6f, 0f, 1, 0.35f), 7f);
            layers[1] = Layer(ProcGen.Grass(256, new Color(0.36f, 0.64f, 0.2f), new Color(0.43f, 0.72f, 0.24f), 4f, 2f, 2, 0.15f), 24f);
            layers[2] = Layer(ProcGen.Grass(256, new Color(0.44f, 0.78f, 0.28f), new Color(0.5f, 0.84f, 0.33f), 8f, 0f, 3, 0.08f), 5f);
            layers[3] = Layer(ProcGen.Grass(256, new Color(0.83f, 0.75f, 0.56f), new Color(0.93f, 0.86f, 0.68f), 10f, 0f, 4, 0.4f), 4f);
            layers[4] = Layer(ProcGen.Grass(256, new Color(0.16f, 0.32f, 0.1f), new Color(0.27f, 0.4f, 0.15f), 5f, 0f, 5, 0.45f), 10f);
            td.terrainLayers = layers;

            var map = new float[ARes, ARes, layers.Length];
            var surf = new Surface[ARes, ARes];
            for (int zi = 0; zi < ARes; zi++)
            {
                float z = (zi + 0.5f) * Size / ARes;
                for (int xi = 0; xi < ARes; xi++)
                {
                    float x = (xi + 0.5f) * Size / ARes;
                    surf[zi, xi] = Classify(x, z, AnalyticHeight(x, z));
                }
            }
            // Soft 3x3 blur of the layer weights for smooth edges
            for (int zi = 0; zi < ARes; zi++)
            {
                for (int xi = 0; xi < ARes; xi++)
                {
                    int n = 0;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int zz = Mathf.Clamp(zi + dz, 0, ARes - 1), xx = Mathf.Clamp(xi + dx, 0, ARes - 1);
                            map[zi, xi, LayerFor(surf[zz, xx])] += 1f;
                            n++;
                        }
                    for (int l = 0; l < layers.Length; l++) map[zi, xi, l] /= n;
                }
            }
            td.SetAlphamaps(0, 0, map);

            // Grass billboards in the rough
            var grassTex = ProcGen.GrassBlades();
            td.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = grassTex,
                    renderMode = DetailRenderMode.GrassBillboard,
                    healthyColor = new Color(0.45f, 0.7f, 0.25f),
                    dryColor = new Color(0.55f, 0.65f, 0.3f),
                    minWidth = 0.35f, maxWidth = 0.7f,
                    minHeight = 0.25f, maxHeight = 0.6f,
                    noiseSpread = 0.4f,
                }
            };
#if UNITY_2022_2_OR_NEWER
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
#endif
            var detail = new int[DRes, DRes];
            for (int zi = 0; zi < DRes; zi++)
            {
                for (int xi = 0; xi < DRes; xi++)
                {
                    var s = surf[zi * ARes / DRes, xi * ARes / DRes];
                    if (s == Surface.Rough) detail[zi, xi] = Random.value < 0.6f ? 1 : 2;
                    else if (s == Surface.OutOfBounds) detail[zi, xi] = Random.Range(2, 5);
                }
            }
            td.SetDetailLayer(0, 0, 0, detail);
            td.wavingGrassStrength = 0.3f;
            td.wavingGrassAmount = 0.3f;
            td.wavingGrassTint = new Color(0.7f, 0.8f, 0.6f);

            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain";
            tgo.transform.SetParent(transform, false);
            var col = tgo.GetComponent<TerrainCollider>();
            if (col != null) Destroy(col); // ball physics samples the heightmap directly
            terrain = tgo.GetComponent<Terrain>();
            var tsh = Shader.Find("Nature/Terrain/Standard");
            if (tsh != null) terrain.materialTemplate = new Material(tsh);
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 1500f;
            terrain.detailObjectDistance = 90f;
            terrain.detailObjectDensity = 1f;
            terrain.treeDistance = 0f;
        }

        static TerrainLayer Layer(Texture2D tex, float tile)
        {
            var l = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f };
            return l;
        }

        static int LayerFor(Surface s)
        {
            switch (s)
            {
                case Surface.Fairway:
                case Surface.Tee: return 1;
                case Surface.Green: return 2;
                case Surface.Bunker:
                case Surface.Water: return 3;
                case Surface.OutOfBounds: return 4;
                default: return 0;
            }
        }

        void BuildWater()
        {
            var mat = ProcGen.Transparent(new Color(0.08f, 0.42f, 0.62f, 0.78f), 0.96f);
            var disc = ProcGen.Disc();
            for (int i = 0; i < hole.ponds.Length; i++)
            {
                var p = hole.ponds[i];
                var w = ProcGen.MeshObject("Pond", disc, transform, mat, false);
                w.transform.position = new Vector3(p.x, waterY[i], p.y);
                w.transform.localScale = new Vector3(p.z + 7f, 1f, p.z + 7f);
                w.GetComponent<MeshRenderer>().receiveShadows = true;
            }
        }

        void BuildTrees()
        {
            var root = new GameObject("Trees").transform;
            root.SetParent(transform, false);
            var trunkMat = ProcGen.Lit(new Color(0.42f, 0.29f, 0.18f), 0.05f);
            var leafMats = new[]
            {
                ProcGen.Lit(new Color(0.2f, 0.5f, 0.15f), 0.12f),
                ProcGen.Lit(new Color(0.27f, 0.58f, 0.18f), 0.12f),
                ProcGen.Lit(new Color(0.35f, 0.6f, 0.16f), 0.12f),
                ProcGen.Lit(new Color(0.52f, 0.68f, 0.2f), 0.12f),
            };
            var pineMat = ProcGen.Lit(new Color(0.12f, 0.36f, 0.16f), 0.1f);
            var coneMesh = ProcGen.Cone();

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in hole.path) { minX = Mathf.Min(minX, p.x); minZ = Mathf.Min(minZ, p.y); maxX = Mathf.Max(maxX, p.x); maxZ = Mathf.Max(maxZ, p.y); }
            minX = Mathf.Max(25f, minX - 130f); minZ = Mathf.Max(25f, minZ - 130f);
            maxX = Mathf.Min(Size - 25f, maxX + 130f); maxZ = Mathf.Min(Size - 25f, maxZ + 130f);

            float fw = hole.fairwayWidth * 0.5f;
            for (int attempt = 0; attempt < 3500 && trees.Count < 420; attempt++)
            {
                float x = Random.Range(minX, maxX), z = Random.Range(minZ, maxZ);
                var pi = Path(x, z);
                if (pi.d < fw + 18f) continue;
                if (Vector2.Distance(new Vector2(x, z), hole.path[0]) < 18f) continue;
                if (Vector2.Distance(new Vector2(x, z), hole.path[hole.path.Length - 1]) < hole.greenRadius + 14f) continue;
                bool bad = false;
                foreach (var b in hole.bunkers) if (Vector2.Distance(new Vector2(x, z), new Vector2(b.x, b.y)) < b.z + 4f) bad = true;
                foreach (var w in hole.ponds) if (Vector2.Distance(new Vector2(x, z), new Vector2(w.x, w.y)) < w.z + 7f) bad = true;
                if (bad) continue;
                float chance = pi.d < fw + ObExtra ? 0.18f : 0.75f;
                if (Random.value > chance) continue;
                var pos = OnGround(x, z);
                bool close = false;
                foreach (var t in trees) if ((t - pos).sqrMagnitude < 7.5f * 7.5f) { close = true; break; }
                if (close) continue;
                trees.Add(pos);

                float s = Random.Range(0.8f, 1.5f);
                var tree = new GameObject("Tree").transform;
                tree.SetParent(root, false);
                tree.position = pos;
                tree.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                var trunk = ProcGen.Prim(PrimitiveType.Cylinder, tree, trunkMat, true);
                trunk.transform.localPosition = new Vector3(0, 1.5f * s, 0);
                trunk.transform.localScale = new Vector3(0.45f * s, 1.5f * s, 0.45f * s);

                if (Random.value < 0.35f)
                {
                    // Pine
                    for (int k = 0; k < 3; k++)
                    {
                        float r = (2.6f - k * 0.6f) * s;
                        var cone = ProcGen.MeshObject("Pine", coneMesh, tree, pineMat);
                        cone.transform.localPosition = new Vector3(0, (2.2f + k * 1.9f) * s, 0);
                        cone.transform.localScale = new Vector3(r, 3.2f * s, r);
                        var cc = cone.AddComponent<CapsuleCollider>();
                        cc.direction = 1; cc.radius = 0.55f; cc.height = 1f; cc.center = new Vector3(0, 0.5f, 0);
                    }
                }
                else
                {
                    var mat = leafMats[Random.Range(0, leafMats.Length)];
                    var main = ProcGen.Prim(PrimitiveType.Sphere, tree, mat, true);
                    main.transform.localPosition = new Vector3(0, 4.4f * s, 0);
                    main.transform.localScale = new Vector3(4.6f, 4.0f, 4.6f) * s;
                    for (int k = 0; k < 3; k++)
                    {
                        var blob = ProcGen.Prim(PrimitiveType.Sphere, tree, mat, k == 0);
                        float a = k * 2.1f + Random.value;
                        blob.transform.localPosition = new Vector3(Mathf.Cos(a) * 1.6f, 3.6f + Random.value * 1.8f, Mathf.Sin(a) * 1.6f) * s;
                        blob.transform.localScale = Vector3.one * Random.Range(2.4f, 3.2f) * s;
                    }
                }
            }

            // Small bushes along the rough line for colour
            var bushMat = ProcGen.Lit(new Color(0.3f, 0.55f, 0.2f), 0.15f);
            var flowerMats = new[] { ProcGen.Lit(new Color(0.95f, 0.45f, 0.6f), 0.2f), ProcGen.Lit(new Color(1f, 0.85f, 0.3f), 0.2f), ProcGen.Lit(new Color(0.95f, 0.95f, 1f), 0.2f) };
            for (int i = 0; i < 160; i++)
            {
                float x = Random.Range(minX, maxX), z = Random.Range(minZ, maxZ);
                var pi = Path(x, z);
                if (pi.d < fw + 10f || pi.d > fw + 40f) continue;
                if (Vector2.Distance(new Vector2(x, z), hole.path[hole.path.Length - 1]) < hole.greenRadius + 8f) continue;
                if (Classify(x, z, AnalyticHeight(x, z)) != Surface.Rough) continue;
                var pos = OnGround(x, z);
                var bush = ProcGen.Prim(PrimitiveType.Sphere, root, Random.value < 0.4f ? flowerMats[Random.Range(0, flowerMats.Length)] : bushMat);
                bush.transform.position = pos + Vector3.up * 0.2f;
                bush.transform.localScale = new Vector3(Random.Range(0.9f, 1.6f), Random.Range(0.6f, 0.9f), Random.Range(0.9f, 1.6f));
            }
        }

        void BuildStakes()
        {
            var root = new GameObject("OB Stakes").transform;
            root.SetParent(transform, false);
            var mat = ProcGen.Lit(Color.white, 0.3f);
            float off = hole.fairwayWidth * 0.5f + ObExtra;
            for (int i = 0; i < hole.path.Length - 1; i++)
            {
                Vector2 a = hole.path[i], b = hole.path[i + 1];
                Vector2 dir = (b - a).normalized;
                Vector2 nrm = new Vector2(-dir.y, dir.x);
                for (float s = 0; s < segLen[i]; s += 14f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 p = a + dir * s + nrm * off * side;
                        if (p.x < 22 || p.y < 22 || p.x > Size - 22 || p.y > Size - 22) continue;
                        if (Path(p.x, p.y).d < off - 1f) continue;
                        var stake = ProcGen.Prim(PrimitiveType.Cylinder, root, mat);
                        stake.transform.position = OnGround(p.x, p.y) + Vector3.up * 0.5f;
                        stake.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
                    }
                }
            }
        }

        void BuildTeeMarkers()
        {
            Vector2 dir = (hole.path[1] - hole.path[0]).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            var mat = ProcGen.Lit(new Color(0.15f, 0.45f, 1f), 0.6f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 p = hole.path[0] + nrm * 3.5f * side + dir * 1.5f;
                var m = ProcGen.Prim(PrimitiveType.Sphere, transform, mat);
                m.transform.position = OnGround(p.x, p.y) + Vector3.up * 0.12f;
                m.transform.localScale = Vector3.one * 0.3f;
            }
        }

        void BuildFlag()
        {
            var root = new GameObject("Pin").transform;
            root.SetParent(transform, false);
            root.position = Cup;

            Vector3 n = Normal(Cup);
            var ring = ProcGen.MeshObject("CupRim", ProcGen.Disc(), root, ProcGen.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f), false);
            ring.transform.position = Cup + n * 0.015f;
            ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            ring.transform.localScale = new Vector3(BallSim.CupRadius + 0.03f, 1f, BallSim.CupRadius + 0.03f);
            var cupMesh = ProcGen.MeshObject("Cup", ProcGen.Disc(), root, ProcGen.Lit(new Color(0.02f, 0.02f, 0.02f), 0f), false);
            cupMesh.transform.position = Cup + n * 0.02f;
            cupMesh.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            cupMesh.transform.localScale = new Vector3(BallSim.CupRadius, 1f, BallSim.CupRadius);

            var pole = ProcGen.Prim(PrimitiveType.Cylinder, root, ProcGen.Lit(new Color(0.97f, 0.97f, 0.97f), 0.7f));
            pole.transform.position = Cup + Vector3.up * (BallSim.PinHeight * 0.5f);
            pole.transform.localScale = new Vector3(0.05f, BallSim.PinHeight * 0.5f, 0.05f);

            var ball = ProcGen.Prim(PrimitiveType.Sphere, root, ProcGen.Lit(new Color(1f, 0.85f, 0.2f), 0.8f, 0.6f));
            ball.transform.position = Cup + Vector3.up * BallSim.PinHeight;
            ball.transform.localScale = Vector3.one * 0.1f;

            var pivot = new GameObject("FlagPivot").transform;
            pivot.SetParent(root, false);
            pivot.position = Cup + Vector3.up * (BallSim.PinHeight - 0.3f);
            flagCloth = pivot;
            var cloth = ProcGen.Prim(PrimitiveType.Cube, pivot, ProcGen.Lit(new Color(0.95f, 0.15f, 0.12f), 0.2f));
            cloth.transform.localPosition = new Vector3(0, 0, 0.42f);
            cloth.transform.localScale = new Vector3(0.02f, 0.5f, 0.8f);
        }

        public void SetWind(Vector3 wind)
        {
            windStrength = wind.magnitude;
            if (windStrength > 0.01f) windDir = wind / windStrength;
        }

        void Update()
        {
            if (flagCloth == null) return;
            float flutter = Mathf.Sin(Time.time * (3f + windStrength)) * (4f + windStrength * 1.5f);
            float droop = Mathf.Lerp(55f, 0f, windStrength / 6f);
            Quaternion yaw = Quaternion.LookRotation(windStrength > 0.01f ? windDir : Vector3.forward, Vector3.up);
            flagCloth.rotation = yaw * Quaternion.Euler(droop, flutter, 0f);
        }

        void BuildMinimap()
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in hole.path) { minX = Mathf.Min(minX, p.x); minZ = Mathf.Min(minZ, p.y); maxX = Mathf.Max(maxX, p.x); maxZ = Mathf.Max(maxZ, p.y); }
            float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
            float half = Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f + 70f;
            mapRect = new Rect(cx - half, cz - half, half * 2f, half * 2f);

            const int res = 192;
            minimap = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var px = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = mapRect.x + (x + 0.5f) / res * mapRect.width;
                    float wz = mapRect.y + (y + 0.5f) / res * mapRect.height;
                    float h = AnalyticHeight(wx, wz);
                    Color c;
                    switch (Classify(wx, wz, h))
                    {
                        case Surface.Fairway: c = new Color(0.45f, 0.78f, 0.3f); break;
                        case Surface.Tee: c = new Color(0.55f, 0.85f, 0.4f); break;
                        case Surface.Green: c = new Color(0.6f, 0.95f, 0.45f); break;
                        case Surface.Bunker: c = new Color(0.95f, 0.88f, 0.65f); break;
                        case Surface.Water: c = new Color(0.2f, 0.5f, 0.9f); break;
                        case Surface.OutOfBounds: c = new Color(0.16f, 0.3f, 0.12f); break;
                        default: c = new Color(0.28f, 0.52f, 0.2f); break;
                    }
                    float shade = (AnalyticHeight(wx - 2f, wz - 2f) - h) * 0.12f;
                    c = c * (1f - Mathf.Clamp(shade, -0.25f, 0.25f));
                    c.a = 0.92f;
                    px[y * res + x] = c;
                }
            }
            var treeCol = new Color(0.1f, 0.24f, 0.08f, 0.95f);
            foreach (var t in trees)
            {
                Vector2 uv = WorldToMap(t);
                int tx = (int)(uv.x * res), ty = (int)(uv.y * res);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = tx + dx, yy = ty + dy;
                        if (xx >= 0 && yy >= 0 && xx < res && yy < res) px[yy * res + xx] = treeCol;
                    }
            }
            minimap.SetPixels(px);
            minimap.filterMode = FilterMode.Bilinear;
            minimap.wrapMode = TextureWrapMode.Clamp;
            minimap.Apply();
        }

        public Vector2 WorldToMap(Vector3 w)
        {
            return new Vector2((w.x - mapRect.x) / mapRect.width, (w.z - mapRect.y) / mapRect.height);
        }

        void OnDestroy()
        {
            if (td != null) Destroy(td);
            if (minimap != null) Destroy(minimap);
        }

        // ------------------------------------------------------------------ Queries

        public float GroundY(Vector3 p)
        {
            return terrain.SampleHeight(p) + terrain.transform.position.y;
        }

        public Vector3 Normal(Vector3 p)
        {
            return td.GetInterpolatedNormal(Mathf.Clamp01(p.x / Size), Mathf.Clamp01(p.z / Size));
        }

        public Surface SurfaceAt(Vector3 p)
        {
            return Classify(p.x, p.z, AnalyticHeight(p.x, p.z));
        }

        public bool IsOnGreen(Vector3 p) { return SurfaceAt(p) == Surface.Green; }

        /// <summary>Closest point on the hole's centre line.</summary>
        public Vector3 ClosestOnPath(Vector3 p, out float t)
        {
            Vector2 q = new Vector2(p.x, p.z);
            float best = float.MaxValue, acc = 0f;
            Vector2 bestPt = hole.path[0];
            t = 0f;
            for (int i = 0; i < segLen.Length; i++)
            {
                Vector2 a = hole.path[i], b = hole.path[i + 1];
                float u = Mathf.Clamp01(Vector2.Dot(q - a, b - a) / (segLen[i] * segLen[i]));
                Vector2 c = a + (b - a) * u;
                float d = (q - c).sqrMagnitude;
                if (d < best) { best = d; bestPt = c; t = (acc + u * segLen[i]) / totalLen; }
                acc += segLen[i];
            }
            return OnGround(bestPt.x, bestPt.y);
        }

        /// <summary>Point on the centre line at fraction t.</summary>
        public Vector3 PointOnPath(float t)
        {
            float target = Mathf.Clamp01(t) * totalLen, acc = 0f;
            for (int i = 0; i < segLen.Length; i++)
            {
                if (acc + segLen[i] >= target || i == segLen.Length - 1)
                {
                    float u = Mathf.Clamp01((target - acc) / segLen[i]);
                    Vector2 p = Vector2.Lerp(hole.path[i], hole.path[i + 1], u);
                    return OnGround(p.x, p.y);
                }
                acc += segLen[i];
            }
            return GreenCenter;
        }

        /// <summary>Finds a playable drop point near <paramref name="p"/>, moving towards the centre line.</summary>
        public Vector3 DropPoint(Vector3 p)
        {
            float t;
            Vector3 target = ClosestOnPath(p, out t);
            Vector3 q = p;
            for (int i = 0; i < 400; i++)
            {
                var s = SurfaceAt(q);
                bool clear = s != Surface.Water && s != Surface.OutOfBounds && s != Surface.Bunker;
                if (clear && !Physics.CheckSphere(q + Vector3.up * 0.5f, 0.6f)) { q.y = GroundY(q); return q; }
                q = Vector3.MoveTowards(q, target, 1f);
                if ((q - target).sqrMagnitude < 0.01f) q = Vector3.MoveTowards(q, TeePos, 1f);
            }
            return target;
        }

        // ------------------------------------------------------------------ Height / surface model

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x * freq, z * freq) - 0.5f) * 2f * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        PathInfo Path(float x, float z)
        {
            Vector2 q = new Vector2(x, z);
            var info = new PathInfo { d = float.MaxValue };
            float acc = 0f;
            int last = segLen.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                Vector2 a = hole.path[i], b = hole.path[i + 1];
                float L = segLen[i];
                float u = Vector2.Dot(q - a, b - a) / (L * L);
                bool outside = false;
                if (u < 0f) { outside = i == 0; u = 0f; }
                if (u > 1f) { outside = i == last; u = 1f; }
                float d = Vector2.Distance(q, a + (b - a) * u);
                if (d < info.d) { info.d = d; info.t = (acc + u * L) / totalLen; info.beyond = outside; }
                acc += L;
            }
            return info;
        }

        float BaseHeight(float x, float z)
        {
            var pi = Path(x, z);
            float fw = hole.fairwayWidth * 0.5f;
            float n = Fbm(x * 0.0055f + sx, z * 0.0055f + sz, 4);
            float n2 = Fbm(x * 0.025f + sz, z * 0.025f + sx, 2);
            float amp = Mathf.Lerp(0.35f, 1f, Smooth(fw, fw + 45f, pi.d));
            float h = BaseH + hole.rise * pi.t + (n * hole.hills + n2 * 0.5f) * amp;
            float outer = Smooth(fw + 35f, fw + 120f, pi.d);
            h += outer * (5f + 12f * Mathf.PerlinNoise(x * 0.012f + sx, z * 0.012f + sz));
            return h;
        }

        public float AnalyticHeight(float x, float z)
        {
            float h = BaseHeight(x, z);
            Vector2 q = new Vector2(x, z);

            float dTee = Vector2.Distance(q, hole.path[0]);
            h = Mathf.Lerp(h, teeH, 1f - Smooth(5f, 11f, dTee));

            Vector2 gc = hole.path[hole.path.Length - 1];
            float dG = Vector2.Distance(q, gc);
            float hg = greenH0 + hole.greenTilt.x * (x - gc.x) + hole.greenTilt.y * (z - gc.y)
                       + 0.18f * Mathf.Sin(x * 0.31f + hole.seed) * Mathf.Cos(z * 0.27f - hole.seed);
            h = Mathf.Lerp(h, hg, 1f - Smooth(hole.greenRadius, hole.greenRadius + 12f, dG));

            foreach (var b in hole.bunkers)
            {
                float d = Vector2.Distance(q, new Vector2(b.x, b.y));
                h -= 1.1f * (1f - Smooth(b.z * 0.5f, b.z * 1.05f, d));
            }
            foreach (var p in hole.ponds)
            {
                float d = Vector2.Distance(q, new Vector2(p.x, p.y));
                h -= 3.2f * (1f - Smooth(p.z * 0.55f, p.z + 5f, d));
            }

            float e = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            h = Mathf.Lerp(EdgeH, h, Smooth(5f, 55f, e));
            return h;
        }

        Surface Classify(float x, float z, float h)
        {
            if (x < 20f || z < 20f || x > Size - 20f || z > Size - 20f) return Surface.OutOfBounds;
            Vector2 q = new Vector2(x, z);
            for (int i = 0; i < hole.ponds.Length; i++)
            {
                var p = hole.ponds[i];
                if (Vector2.Distance(q, new Vector2(p.x, p.y)) < p.z + 5f && h < waterY[i]) return Surface.Water;
            }
            float wob = Mathf.PerlinNoise(x * 0.05f + sx, z * 0.05f + sz) - 0.5f;
            foreach (var b in hole.bunkers)
                if (Vector2.Distance(q, new Vector2(b.x, b.y)) < b.z * (0.88f + wob * 0.2f)) return Surface.Bunker;
            if (Vector2.Distance(q, hole.path[hole.path.Length - 1]) < hole.greenRadius * (1f + wob * 0.12f)) return Surface.Green;
            if (Vector2.Distance(q, hole.path[0]) < 6f) return Surface.Tee;

            var pi = Path(x, z);
            float fwHalf = hole.fairwayWidth * 0.5f;
            float fw = fwHalf * Mathf.Lerp(1f, 0.6f, Smooth(0.85f, 1f, pi.t)) * (1f + wob * 0.3f);
            if (!pi.beyond && pi.t > hole.fairwayStart && pi.d < fw) return Surface.Fairway;
            if (pi.d < fwHalf + ObExtra) return Surface.Rough;
            return Surface.OutOfBounds;
        }
    }
}
