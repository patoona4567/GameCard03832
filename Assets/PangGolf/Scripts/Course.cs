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
        const int ARes = 1024;
        const int DRes = 512;
        const float ObExtra = 48f;

        // Terrain layer indices
        const int LRough = 0, LFairLight = 1, LFairDark = 2, LGreenLight = 3, LGreenDark = 4, LSand = 5, LDeep = 6, LFringe = 7, LayerCount = 8;

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
        Transform flagPivot;
        Mesh flagMesh;
        Vector3[] flagBase;
        Vector3[] flagVerts;
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
            LengthYards = totalLen / Clubs.Yard;

            BuildWater();
            BuildTrees();
            BuildPalmsAndRocks();
            BuildForestBelt();
            BuildStakes();
            BuildTeeMarkers();
            BuildFlag();
            BuildMinimap();
        }

        Vector3 OnGround(float x, float z)
        {
            var p = new Vector3(x, 0, z);
            p.y = x < 0f || z < 0f || x > Size || z > Size ? EdgeH : GroundY(p);
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

            var grassN = ProcGen.NormalMap(256, 12f, 5f, 1f, 11);
            var sandN = ProcGen.NormalMap(256, 6f, 3f, 0.6f, 12, 3f);
            var layers = new TerrainLayer[LayerCount];
            layers[LRough] = Layer(ProcGen.Grass(256, new Color(0.2f, 0.42f, 0.1f), new Color(0.32f, 0.55f, 0.15f), 6f, 0f, 1, 0.4f), grassN, 7f);
            layers[LFairLight] = Layer(ProcGen.Grass(256, new Color(0.4f, 0.7f, 0.2f), new Color(0.47f, 0.77f, 0.25f), 4f, 0f, 2, 0.18f), grassN, 9f);
            layers[LFairDark] = Layer(ProcGen.Grass(256, new Color(0.31f, 0.6f, 0.16f), new Color(0.38f, 0.67f, 0.2f), 4f, 0f, 3, 0.18f), grassN, 9f);
            layers[LGreenLight] = Layer(ProcGen.Grass(256, new Color(0.48f, 0.82f, 0.28f), new Color(0.53f, 0.87f, 0.32f), 9f, 0f, 4, 0.06f), grassN, 4f);
            layers[LGreenDark] = Layer(ProcGen.Grass(256, new Color(0.4f, 0.74f, 0.24f), new Color(0.45f, 0.79f, 0.28f), 9f, 0f, 5, 0.06f), grassN, 4f);
            layers[LSand] = Layer(ProcGen.Grass(256, new Color(0.88f, 0.8f, 0.6f), new Color(0.97f, 0.91f, 0.74f), 10f, 0f, 6, 0.35f), sandN, 5f);
            layers[LDeep] = Layer(ProcGen.Grass(256, new Color(0.14f, 0.3f, 0.09f), new Color(0.26f, 0.4f, 0.13f), 5f, 0f, 7, 0.5f), grassN, 10f);
            layers[LFringe] = Layer(ProcGen.Grass(256, new Color(0.36f, 0.66f, 0.2f), new Color(0.42f, 0.72f, 0.24f), 7f, 0f, 8, 0.12f), grassN, 5f);
            td.terrainLayers = layers;

            // Classify every alphamap texel, with mowing stripes on fairway and green
            var idx = new byte[ARes, ARes];
            var surfDetail = new Surface[DRes, DRes];
            Vector2 gc = hole.path[hole.path.Length - 1];
            for (int zi = 0; zi < ARes; zi++)
            {
                float z = (zi + 0.5f) * Size / ARes;
                for (int xi = 0; xi < ARes; xi++)
                {
                    float x = (xi + 0.5f) * Size / ARes;
                    float along;
                    Surface s = Classify(x, z, out along);
                    int layer;
                    switch (s)
                    {
                        case Surface.Fairway:
                            layer = Mathf.FloorToInt(along / 8f) % 2 == 0 ? LFairLight : LFairDark;
                            break;
                        case Surface.Green:
                            layer = Mathf.FloorToInt((x * 0.71f + z * 0.71f) / 3f) % 2 == 0 ? LGreenLight : LGreenDark;
                            break;
                        case Surface.Tee: layer = LFringe; break;
                        case Surface.Bunker:
                        case Surface.Water: layer = LSand; break;
                        case Surface.OutOfBounds: layer = LDeep; break;
                        default:
                            layer = Vector2.Distance(new Vector2(x, z), gc) < hole.greenRadius * 1.12f + 2.5f ? LFringe : LRough;
                            break;
                    }
                    idx[zi, xi] = (byte)layer;
                    if ((zi & 1) == 0 && (xi & 1) == 0) surfDetail[zi / 2, xi / 2] = s;
                }
            }
            var map = new float[ARes, ARes, LayerCount];
            for (int zi = 0; zi < ARes; zi++)
            {
                for (int xi = 0; xi < ARes; xi++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int zz = Mathf.Clamp(zi + dz, 0, ARes - 1), xx = Mathf.Clamp(xi + dx, 0, ARes - 1);
                            map[zi, xi, idx[zz, xx]] += 1f / 9f;
                        }
                }
            }
            td.SetAlphamaps(0, 0, map);

            // Grass and wild flowers
            td.detailPrototypes = new[]
            {
                Detail(ProcGen.GrassBlades(64, 1, false), new Color(0.5f, 0.75f, 0.28f), new Color(0.62f, 0.7f, 0.32f), 0.35f, 0.7f, 0.25f, 0.55f),
                Detail(ProcGen.GrassBlades(64, 2, false), new Color(0.38f, 0.62f, 0.2f), new Color(0.5f, 0.6f, 0.25f), 0.5f, 0.9f, 0.45f, 0.9f),
                Detail(ProcGen.GrassBlades(64, 3, true), new Color(1f, 1f, 1f), new Color(0.95f, 0.95f, 0.9f), 0.35f, 0.6f, 0.3f, 0.5f),
            };
#if UNITY_2022_2_OR_NEWER
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
#endif
            var grassA = new int[DRes, DRes];
            var grassB = new int[DRes, DRes];
            var flowers = new int[DRes, DRes];
            for (int zi = 0; zi < DRes; zi++)
            {
                for (int xi = 0; xi < DRes; xi++)
                {
                    var s = surfDetail[zi, xi];
                    float patch = Mathf.PerlinNoise(xi * 0.09f + sx, zi * 0.09f + sz);
                    if (s == Surface.Rough)
                    {
                        grassA[zi, xi] = Random.value < 0.7f ? 2 : 1;
                        if (patch > 0.6f) grassB[zi, xi] = 1;
                        if (patch > 0.72f && Random.value < 0.25f) flowers[zi, xi] = 1;
                    }
                    else if (s == Surface.OutOfBounds)
                    {
                        grassA[zi, xi] = Random.Range(2, 4);
                        grassB[zi, xi] = patch > 0.45f ? Random.Range(1, 3) : 0;
                        if (patch > 0.65f && Random.value < 0.35f) flowers[zi, xi] = Random.Range(1, 3);
                    }
                }
            }
            td.SetDetailLayer(0, 0, 0, grassA);
            td.SetDetailLayer(0, 0, 1, grassB);
            td.SetDetailLayer(0, 0, 2, flowers);
            td.wavingGrassStrength = 0.35f;
            td.wavingGrassAmount = 0.35f;
            td.wavingGrassSpeed = 0.5f;
            td.wavingGrassTint = new Color(0.75f, 0.85f, 0.6f);

            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain";
            tgo.transform.SetParent(transform, false);
            var col = tgo.GetComponent<TerrainCollider>();
            if (col != null) Destroy(col); // ball physics samples the heightmap directly
            terrain = tgo.GetComponent<Terrain>();
            var tsh = Shader.Find("Nature/Terrain/Standard");
            if (tsh != null) terrain.materialTemplate = new Material(tsh);
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 1500f;
            terrain.detailObjectDistance = 110f;
            terrain.detailObjectDensity = 1f;
            terrain.treeDistance = 0f;
            terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        }

        static TerrainLayer Layer(Texture2D tex, Texture2D normal, float tile)
        {
            return new TerrainLayer
            {
                diffuseTexture = tex,
                normalMapTexture = normal,
                normalScale = 0.8f,
                tileSize = new Vector2(tile, tile),
                smoothness = 0f,
                metallic = 0f,
            };
        }

        static DetailPrototype Detail(Texture2D tex, Color healthy, Color dry, float minW, float maxW, float minH, float maxH)
        {
            return new DetailPrototype
            {
                prototypeTexture = tex,
                renderMode = DetailRenderMode.GrassBillboard,
                healthyColor = healthy,
                dryColor = dry,
                minWidth = minW, maxWidth = maxW,
                minHeight = minH, maxHeight = maxH,
                noiseSpread = 0.4f,
            };
        }

        void BuildWater()
        {
            var mat = ProcGen.Water();
            var disc = ProcGen.DiscGrid();
            for (int i = 0; i < hole.ponds.Length; i++)
            {
                var p = hole.ponds[i];
                var w = ProcGen.MeshObject("Pond", disc, transform, mat, false);
                w.transform.position = new Vector3(p.x, waterY[i], p.y);
                w.transform.localScale = new Vector3(p.z + 7f, 1f, p.z + 7f);
                var r = w.GetComponent<MeshRenderer>();
                r.receiveShadows = false;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            }
        }

        // Shared tree materials (built once per hole)
        Material trunkMat, pineMat, palmTrunkMat, palmLeafMat, rockMat;
        Material[] leafMats;
        Mesh coneMesh;

        void EnsureTreeMaterials()
        {
            if (trunkMat != null) return;
            trunkMat = ProcGen.Lit(new Color(0.4f, 0.27f, 0.16f), 0.05f);
            leafMats = new[]
            {
                ProcGen.Foliage(new Color(0.2f, 0.5f, 0.14f)),
                ProcGen.Foliage(new Color(0.28f, 0.6f, 0.17f)),
                ProcGen.Foliage(new Color(0.38f, 0.64f, 0.15f)),
                ProcGen.Foliage(new Color(0.55f, 0.7f, 0.2f)),
                ProcGen.Foliage(new Color(0.85f, 0.55f, 0.65f), 0.1f), // blossom
            };
            pineMat = ProcGen.Foliage(new Color(0.1f, 0.34f, 0.16f), 0.06f);
            palmTrunkMat = ProcGen.Lit(new Color(0.55f, 0.45f, 0.32f), 0.05f);
            palmLeafMat = ProcGen.Foliage(new Color(0.3f, 0.6f, 0.18f), 0.2f);
            rockMat = ProcGen.VertexLit();
            coneMesh = ProcGen.Cone(16);
        }

        void BuildTrees()
        {
            EnsureTreeMaterials();
            var root = new GameObject("Trees").transform;
            root.SetParent(transform, false);

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in hole.path) { minX = Mathf.Min(minX, p.x); minZ = Mathf.Min(minZ, p.y); maxX = Mathf.Max(maxX, p.x); maxZ = Mathf.Max(maxZ, p.y); }
            minX = Mathf.Max(25f, minX - 130f); minZ = Mathf.Max(25f, minZ - 130f);
            maxX = Mathf.Min(Size - 25f, maxX + 130f); maxZ = Mathf.Min(Size - 25f, maxZ + 130f);

            float fw = hole.fairwayWidth * 0.5f;
            for (int attempt = 0; attempt < 4000 && trees.Count < 460; attempt++)
            {
                float x = Random.Range(minX, maxX), z = Random.Range(minZ, maxZ);
                var pi = Path(x, z);
                if (pi.d < fw + 18f) continue;
                if (Vector2.Distance(new Vector2(x, z), hole.path[0]) < 18f) continue;
                if (Vector2.Distance(new Vector2(x, z), hole.path[hole.path.Length - 1]) < hole.greenRadius + 14f) continue;
                if (NearHazard(x, z, 4f, 7f)) continue;
                // Clumps: denser where low-frequency noise is high
                float clump = Mathf.PerlinNoise(x * 0.02f + sz, z * 0.02f + sx);
                float chance = (pi.d < fw + ObExtra ? 0.12f : 0.55f) + clump * 0.35f;
                if (Random.value > chance) continue;
                var pos = OnGround(x, z);
                if (TooClose(pos, 6.5f)) continue;
                trees.Add(pos);
                float r = Random.value;
                if (r < 0.3f) MakePine(root, pos, Random.Range(0.8f, 1.5f), true);
                else MakeRoundTree(root, pos, Random.Range(0.8f, 1.5f), true, Random.value < 0.06f);
            }

            // Small bushes and flower beds along the rough line
            var bushMat = ProcGen.Foliage(new Color(0.28f, 0.52f, 0.18f), 0.05f);
            var flowerMats = new[]
            {
                ProcGen.Foliage(new Color(0.98f, 0.45f, 0.62f), 0.05f),
                ProcGen.Foliage(new Color(1f, 0.85f, 0.3f), 0.05f),
                ProcGen.Foliage(new Color(0.95f, 0.95f, 1f), 0.05f),
                ProcGen.Foliage(new Color(0.65f, 0.5f, 1f), 0.05f),
            };
            for (int i = 0; i < 220; i++)
            {
                float x = Random.Range(minX, maxX), z = Random.Range(minZ, maxZ);
                var pi = Path(x, z);
                if (pi.d < fw + 10f || pi.d > fw + 40f) continue;
                if (Vector2.Distance(new Vector2(x, z), hole.path[hole.path.Length - 1]) < hole.greenRadius + 8f) continue;
                if (SurfaceAt(new Vector3(x, 0, z)) != Surface.Rough) continue;
                var pos = OnGround(x, z);
                bool flower = Random.value < 0.45f;
                int blobs = flower ? 4 : 2;
                var mat = flower ? flowerMats[Random.Range(0, flowerMats.Length)] : bushMat;
                for (int b = 0; b < blobs; b++)
                {
                    var bush = ProcGen.Prim(PrimitiveType.Sphere, root, mat);
                    Vector2 off = Random.insideUnitCircle * (flower ? 0.9f : 0.5f);
                    bush.transform.position = pos + new Vector3(off.x, 0.15f, off.y);
                    float sc = flower ? Random.Range(0.45f, 0.75f) : Random.Range(0.9f, 1.5f);
                    bush.transform.localScale = new Vector3(sc, sc * 0.65f, sc);
                }
            }
        }

        bool NearHazard(float x, float z, float bunkerMargin, float pondMargin)
        {
            var q = new Vector2(x, z);
            foreach (var b in hole.bunkers) if (Vector2.Distance(q, new Vector2(b.x, b.y)) < b.z + bunkerMargin) return true;
            foreach (var w in hole.ponds) if (Vector2.Distance(q, new Vector2(w.x, w.y)) < w.z + pondMargin) return true;
            return false;
        }

        bool TooClose(Vector3 pos, float dist)
        {
            float d2 = dist * dist;
            foreach (var t in trees) if ((t - pos).sqrMagnitude < d2) return true;
            return false;
        }

        void MakeRoundTree(Transform root, Vector3 pos, float s, bool colliders, bool blossom)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(root, false);
            tree.position = pos;
            tree.rotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f));

            var trunk = ProcGen.Prim(PrimitiveType.Cylinder, tree, trunkMat, colliders);
            trunk.transform.localPosition = new Vector3(0, 1.6f * s, 0);
            trunk.transform.localScale = new Vector3(0.42f * s, 1.6f * s, 0.42f * s);
            // Branch stubs
            for (int k = 0; k < 2; k++)
            {
                var br = ProcGen.Prim(PrimitiveType.Cylinder, tree, trunkMat);
                br.transform.localPosition = new Vector3((k == 0 ? 0.5f : -0.45f) * s, 2.9f * s, (k == 0 ? 0.2f : -0.3f) * s);
                br.transform.localRotation = Quaternion.Euler(0, 0, k == 0 ? -40f : 38f);
                br.transform.localScale = new Vector3(0.2f * s, 0.7f * s, 0.2f * s);
            }

            var mat = blossom ? leafMats[leafMats.Length - 1] : leafMats[Random.Range(0, leafMats.Length - 1)];
            var main = ProcGen.Prim(PrimitiveType.Sphere, tree, mat, colliders);
            main.transform.localPosition = new Vector3(0, 4.6f * s, 0);
            main.transform.localScale = new Vector3(4.4f, 3.8f, 4.4f) * s;
            int blobs = Random.Range(4, 7);
            for (int k = 0; k < blobs; k++)
            {
                var blob = ProcGen.Prim(PrimitiveType.Sphere, tree, mat, colliders && k == 0);
                float a = k * Mathf.PI * 2f / blobs + Random.value * 0.6f;
                float rr = Random.Range(1.3f, 2.1f);
                blob.transform.localPosition = new Vector3(Mathf.Cos(a) * rr, Random.Range(3.4f, 5.8f), Mathf.Sin(a) * rr) * s;
                blob.transform.localScale = Vector3.one * Random.Range(2.2f, 3.2f) * s;
            }
            var top = ProcGen.Prim(PrimitiveType.Sphere, tree, mat);
            top.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), 6.3f, Random.Range(-0.4f, 0.4f)) * s;
            top.transform.localScale = Vector3.one * 2.6f * s;
        }

        void MakePine(Transform root, Vector3 pos, float s, bool colliders)
        {
            var tree = new GameObject("Pine").transform;
            tree.SetParent(root, false);
            tree.position = pos;
            tree.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            var trunk = ProcGen.Prim(PrimitiveType.Cylinder, tree, trunkMat, colliders);
            trunk.transform.localPosition = new Vector3(0, 1.4f * s, 0);
            trunk.transform.localScale = new Vector3(0.38f * s, 1.4f * s, 0.38f * s);
            int tiers = Random.Range(3, 5);
            for (int k = 0; k < tiers; k++)
            {
                float r = (2.8f - k * 2.2f / tiers) * s;
                var cone = ProcGen.MeshObject("Tier", coneMesh, tree, pineMat);
                cone.transform.localPosition = new Vector3(0, (1.8f + k * 1.7f) * s, 0);
                cone.transform.localRotation = Quaternion.Euler(0, k * 37f, 0);
                cone.transform.localScale = new Vector3(r, 3.0f * s, r);
                if (colliders)
                {
                    var cc = cone.AddComponent<CapsuleCollider>();
                    cc.direction = 1; cc.radius = 0.55f; cc.height = 1f; cc.center = new Vector3(0, 0.5f, 0);
                }
            }
        }

        void MakePalm(Transform root, Vector3 pos, float s)
        {
            var palm = new GameObject("Palm").transform;
            palm.SetParent(root, false);
            palm.position = pos;
            palm.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            float h = Random.Range(6f, 8.5f) * s;
            float lean = Random.Range(0.8f, 2.2f) * s;
            ProcGen.MeshObject("Trunk", ProcGen.PalmTrunk(h, lean), palm, palmTrunkMat);
            var crown = ProcGen.MeshObject("Fronds", ProcGen.PalmFronds(Random.Range(8, 11), Random.Range(0, 9999)), palm, palmLeafMat);
            crown.transform.localPosition = new Vector3(lean, h, 0f);
            crown.transform.localScale = Vector3.one * s;
            var col = palm.gameObject.AddComponent<CapsuleCollider>();
            col.direction = 1; col.radius = 0.3f * s; col.height = h; col.center = new Vector3(lean * 0.4f, h * 0.5f, 0f);
            var crownCol = crown.AddComponent<SphereCollider>();
            crownCol.radius = 2.2f; crownCol.center = new Vector3(0, -0.4f, 0);
            for (int k = 0; k < 3; k++)
            {
                var coconut = ProcGen.Prim(PrimitiveType.Sphere, crown.transform, trunkMat);
                coconut.transform.localPosition = new Vector3(Mathf.Cos(k * 2.1f) * 0.3f, -0.25f, Mathf.Sin(k * 2.1f) * 0.3f);
                coconut.transform.localScale = Vector3.one * 0.32f;
            }
        }

        void BuildPalmsAndRocks()
        {
            EnsureTreeMaterials();
            var root = new GameObject("Palms & Rocks").transform;
            root.SetParent(transform, false);

            // Palms ring the ponds and frame the green
            foreach (var p in hole.ponds)
            {
                int n = Random.Range(3, 6);
                for (int i = 0; i < n * 3 && n > 0; i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    float r = p.z + Random.Range(7f, 14f);
                    float x = p.x + Mathf.Cos(a) * r, z = p.y + Mathf.Sin(a) * r;
                    var s = SurfaceAt(new Vector3(x, 0, z));
                    if (s != Surface.Rough && s != Surface.OutOfBounds) continue;
                    if (Path(x, z).d < hole.fairwayWidth * 0.5f + 6f) continue;
                    var pos = OnGround(x, z);
                    if (TooClose(pos, 5f)) continue;
                    trees.Add(pos);
                    MakePalm(root, pos, Random.Range(0.85f, 1.15f));
                    n--;
                }
            }
            Vector2 gc = hole.path[hole.path.Length - 1];
            Vector2 back = (gc - hole.path[hole.path.Length - 2]).normalized;
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.Atan2(back.y, back.x) + (i - 1) * 0.6f + Random.Range(-0.15f, 0.15f);
                float r = hole.greenRadius + Random.Range(16f, 24f);
                Vector3 pos = OnGround(gc.x + Mathf.Cos(a) * r, gc.y + Mathf.Sin(a) * r);
                if (NearHazard(pos.x, pos.z, 3f, 6f) || TooClose(pos, 5f)) continue;
                trees.Add(pos);
                MakePalm(root, pos, Random.Range(0.9f, 1.2f));
            }

            // Rocks by the water and scattered in the deep rough
            for (int i = 0; i < 40; i++)
            {
                Vector3 pos;
                if (hole.ponds.Length > 0 && i < 18)
                {
                    var p = hole.ponds[i % hole.ponds.Length];
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    float r = p.z + Random.Range(2f, 6f);
                    pos = OnGround(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r);
                }
                else
                {
                    float x = Random.Range(40f, Size - 40f), z = Random.Range(40f, Size - 40f);
                    if (Path(x, z).d < hole.fairwayWidth * 0.5f + 30f) continue;
                    pos = OnGround(x, z);
                }
                if (Path(pos.x, pos.z).d < hole.fairwayWidth * 0.5f + 3f) continue;
                var mesh = ProcGen.Rock(Random.Range(0, 9999));
                if (mesh == null) break;
                var rock = ProcGen.MeshObject("Rock", mesh, root, rockMat);
                float sc = Random.Range(0.6f, 2.2f);
                rock.transform.position = pos + Vector3.down * 0.15f * sc;
                rock.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                rock.transform.localScale = new Vector3(sc * Random.Range(0.9f, 1.4f), sc, sc * Random.Range(0.9f, 1.4f));
                rock.AddComponent<SphereCollider>().radius = 0.75f;
            }
        }

        /// <summary>Dense band of trees around the terrain border so the world doesn't just end.</summary>
        void BuildForestBelt()
        {
            EnsureTreeMaterials();
            var root = new GameObject("Forest Belt").transform;
            root.SetParent(transform, false);
            float fw = hole.fairwayWidth * 0.5f;
            int placed = 0;
            for (int attempt = 0; attempt < 2500 && placed < 320; attempt++)
            {
                float x = Random.Range(-90f, Size + 90f), z = Random.Range(-90f, Size + 90f);
                float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
                if (edge > 70f) continue;
                if (Path(x, z).d < fw + ObExtra + 25f) continue;
                var pos = OnGround(x, z);
                if (TooClose(pos, 7f)) continue;
                trees.Add(pos);
                placed++;
                if (Random.value < 0.4f) MakePine(root, pos, Random.Range(1.1f, 1.8f), false);
                else MakeRoundTree(root, pos, Random.Range(1.1f, 1.8f), false, false);
            }
        }

        void BuildStakes()
        {
            var root = new GameObject("OB Stakes").transform;
            root.SetParent(transform, false);
            var mat = ProcGen.Lit(Color.white, 0.3f);
            var cap = ProcGen.Lit(new Color(0.9f, 0.15f, 0.15f), 0.3f);
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
                        var top = ProcGen.Prim(PrimitiveType.Cylinder, root, cap);
                        top.transform.position = OnGround(p.x, p.y) + Vector3.up * 1.02f;
                        top.transform.localScale = new Vector3(0.13f, 0.05f, 0.13f);
                    }
                }
            }
        }

        void BuildTeeMarkers()
        {
            Vector2 dir = (hole.path[1] - hole.path[0]).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            var mat = ProcGen.Lit(new Color(0.15f, 0.45f, 1f), 0.7f);
            var wood = ProcGen.Lit(new Color(0.5f, 0.33f, 0.2f), 0.2f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 p = hole.path[0] + nrm * 3.5f * side + dir * 1.5f;
                var m = ProcGen.Prim(PrimitiveType.Sphere, transform, mat);
                m.transform.position = OnGround(p.x, p.y) + Vector3.up * 0.12f;
                m.transform.localScale = Vector3.one * 0.3f;
            }
            // Bench and ball washer beside the tee
            Vector2 bp = hole.path[0] + nrm * 9f - dir * 2f;
            Vector3 bench = OnGround(bp.x, bp.y);
            Quaternion rot = Quaternion.LookRotation(new Vector3(nrm.x, 0, nrm.y));
            var seat = ProcGen.Prim(PrimitiveType.Cube, transform, wood);
            seat.transform.position = bench + Vector3.up * 0.45f;
            seat.transform.rotation = rot;
            seat.transform.localScale = new Vector3(1.8f, 0.08f, 0.45f);
            var backrest = ProcGen.Prim(PrimitiveType.Cube, transform, wood);
            backrest.transform.position = bench + Vector3.up * 0.8f + rot * new Vector3(0, 0, 0.2f);
            backrest.transform.rotation = rot;
            backrest.transform.localScale = new Vector3(1.8f, 0.4f, 0.06f);
            for (int k = -1; k <= 1; k += 2)
            {
                var leg = ProcGen.Prim(PrimitiveType.Cube, transform, wood);
                leg.transform.position = bench + Vector3.up * 0.22f + rot * new Vector3(0.75f * k, 0, 0);
                leg.transform.rotation = rot;
                leg.transform.localScale = new Vector3(0.08f, 0.44f, 0.4f);
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
            var cupMat = ProcGen.Lit(Color.white, 0f);
            cupMat.mainTexture = ProcGen.CupTexture();
            var cupMesh = ProcGen.MeshObject("Cup", ProcGen.Disc(), root, cupMat, false);
            cupMesh.transform.position = Cup + n * 0.02f;
            cupMesh.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            cupMesh.transform.localScale = new Vector3(BallSim.CupRadius, 1f, BallSim.CupRadius);

            var pole = ProcGen.Prim(PrimitiveType.Cylinder, root, ProcGen.Lit(new Color(0.97f, 0.97f, 0.97f), 0.8f));
            pole.transform.position = Cup + Vector3.up * (BallSim.PinHeight * 0.5f);
            pole.transform.localScale = new Vector3(0.05f, BallSim.PinHeight * 0.5f, 0.05f);
            var bands = ProcGen.Lit(new Color(0.9f, 0.15f, 0.12f), 0.6f);
            for (int k = 0; k < 4; k++)
            {
                var band = ProcGen.Prim(PrimitiveType.Cylinder, root, bands);
                band.transform.position = Cup + Vector3.up * (0.35f + k * 0.5f);
                band.transform.localScale = new Vector3(0.055f, 0.1f, 0.055f);
            }

            var knob = ProcGen.Prim(PrimitiveType.Sphere, root, ProcGen.Lit(new Color(1f, 0.85f, 0.2f), 0.85f, 0.7f));
            knob.transform.position = Cup + Vector3.up * BallSim.PinHeight;
            knob.transform.localScale = Vector3.one * 0.1f;

            flagPivot = new GameObject("FlagPivot").transform;
            flagPivot.SetParent(root, false);
            flagPivot.position = Cup + Vector3.up * (BallSim.PinHeight - 0.05f);
            flagMesh = ProcGen.FlagMesh(10, 5, 0.85f, 0.55f);
            flagBase = flagMesh.vertices;
            flagVerts = new Vector3[flagBase.Length];
            var flagMat = ProcGen.Lit(new Color(0.95f, 0.15f, 0.12f), 0.25f);
            ProcGen.MeshObject("Flag", flagMesh, flagPivot, flagMat);
        }

        public void SetWind(Vector3 wind)
        {
            windStrength = wind.magnitude;
            if (windStrength > 0.01f) windDir = wind / windStrength;
        }

        void Update()
        {
            if (flagPivot == null) return;
            float t = Time.time;
            Quaternion yaw = Quaternion.LookRotation(windStrength > 0.01f ? windDir : Vector3.forward, Vector3.up);
            float droop = Mathf.Lerp(60f, 4f, Mathf.Clamp01(windStrength / 5f));
            flagPivot.rotation = yaw * Quaternion.Euler(droop, Mathf.Sin(t * 0.7f) * 6f, 0f);

            float speed = 4f + windStrength * 1.2f;
            float amp = 0.05f + windStrength * 0.012f;
            for (int i = 0; i < flagBase.Length; i++)
            {
                Vector3 v = flagBase[i];
                float k = v.z / 0.85f;
                float wave = Mathf.Sin(v.z * 7f - t * speed) * amp * k + Mathf.Sin(v.z * 13f + v.y * 5f - t * speed * 1.7f) * amp * 0.35f * k;
                flagVerts[i] = new Vector3(v.x + wave, v.y - k * k * 0.04f, v.z);
            }
            flagMesh.vertices = flagVerts;
            flagMesh.RecalculateNormals();
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
                    float along;
                    Color c;
                    switch (Classify(wx, wz, out along))
                    {
                        case Surface.Fairway: c = Mathf.FloorToInt(along / 8f) % 2 == 0 ? new Color(0.5f, 0.82f, 0.32f) : new Color(0.43f, 0.75f, 0.28f); break;
                        case Surface.Tee: c = new Color(0.55f, 0.85f, 0.4f); break;
                        case Surface.Green: c = new Color(0.6f, 0.95f, 0.45f); break;
                        case Surface.Bunker: c = new Color(0.97f, 0.9f, 0.68f); break;
                        case Surface.Water: c = new Color(0.2f, 0.55f, 0.92f); break;
                        case Surface.OutOfBounds: c = new Color(0.16f, 0.3f, 0.12f); break;
                        default: c = new Color(0.28f, 0.52f, 0.2f); break;
                    }
                    float shade = (AnalyticHeight(wx - 2f, wz - 2f) - h) * 0.12f;
                    c = c * (1f - Mathf.Clamp(shade, -0.25f, 0.25f));
                    c.a = 0.95f;
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
                        if (xx >= 0 && yy >= 0 && xx < res && yy < res && Mathf.Abs(dx) + Mathf.Abs(dy) < 2) px[yy * res + xx] = treeCol;
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
            float along;
            return Classify(p.x, p.z, out along);
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

        /// <summary>Surface type at a point; <paramref name="along"/> is the distance along the hole (for mowing stripes).</summary>
        Surface Classify(float x, float z, out float along)
        {
            along = 0f;
            if (x < 20f || z < 20f || x > Size - 20f || z > Size - 20f) return Surface.OutOfBounds;
            Vector2 q = new Vector2(x, z);
            for (int i = 0; i < hole.ponds.Length; i++)
            {
                var p = hole.ponds[i];
                if (Vector2.Distance(q, new Vector2(p.x, p.y)) < p.z + 5f && AnalyticHeight(x, z) < waterY[i]) return Surface.Water;
            }
            float wob = Mathf.PerlinNoise(x * 0.05f + sx, z * 0.05f + sz) - 0.5f;
            foreach (var b in hole.bunkers)
                if (Vector2.Distance(q, new Vector2(b.x, b.y)) < b.z * (0.88f + wob * 0.2f)) return Surface.Bunker;
            if (Vector2.Distance(q, hole.path[hole.path.Length - 1]) < hole.greenRadius * (1f + wob * 0.12f)) return Surface.Green;
            if (Vector2.Distance(q, hole.path[0]) < 6f) return Surface.Tee;

            var pi = Path(x, z);
            along = pi.t * totalLen;
            float fwHalf = hole.fairwayWidth * 0.5f;
            float fw = fwHalf * Mathf.Lerp(1f, 0.6f, Smooth(0.85f, 1f, pi.t)) * (1f + wob * 0.3f);
            if (!pi.beyond && pi.t > hole.fairwayStart && pi.d < fw) return Surface.Fairway;
            if (pi.d < fwHalf + ObExtra) return Surface.Rough;
            return Surface.OutOfBounds;
        }
    }
}
