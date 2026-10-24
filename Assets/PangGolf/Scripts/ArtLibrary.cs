using System.Collections.Generic;
using UnityEngine;

namespace PangGolf
{
    /// <summary>
    /// Optional real art. Drop free models into Assets/PangGolf/Resources/Art/Models (any sub-folder layout)
    /// and terrain textures into Assets/PangGolf/Resources/Art/Terrain - the course uses them automatically
    /// and falls back to the procedural versions for anything that is missing.
    /// Models are sorted by file name: palm, pine/fir, bush/shrub/plant, rock/stone, flower, tree/oak/birch...
    /// </summary>
    public static class ArtLibrary
    {
        public enum Cat { Tree, Pine, Palm, Bush, Rock, Flower }

        static Dictionary<Cat, List<GameObject>> models;

        static readonly string[] Skip = { "dead", "log", "stump", "mushroom", "path", "fence", "grass", "cliff", "lilypad", "lily", "petal", "leaf", "branch", "twig", "pebble_square" };

        public static int Count(Cat c) { Load(); return models[c].Count; }

        static void Load()
        {
            if (models != null) return;
            models = new Dictionary<Cat, List<GameObject>>();
            foreach (Cat c in System.Enum.GetValues(typeof(Cat))) models[c] = new List<GameObject>();

            foreach (var go in Resources.LoadAll<GameObject>("Art/Models"))
            {
                Cat c;
                if (Categorize(go.name, out c)) models[c].Add(go);
            }
            // Explicit folders win regardless of the file name
            AddFolder("Art/Trees", Cat.Tree);
            AddFolder("Art/Pines", Cat.Pine);
            AddFolder("Art/Palms", Cat.Palm);
            AddFolder("Art/Bushes", Cat.Bush);
            AddFolder("Art/Rocks", Cat.Rock);
            AddFolder("Art/Flowers", Cat.Flower);

            int total = 0;
            foreach (var kv in models) total += kv.Value.Count;
            if (total > 0)
                Debug.Log(string.Format("PangGolf art: {0} trees, {1} pines, {2} palms, {3} bushes, {4} rocks, {5} flowers",
                    models[Cat.Tree].Count, models[Cat.Pine].Count, models[Cat.Palm].Count, models[Cat.Bush].Count, models[Cat.Rock].Count, models[Cat.Flower].Count));
        }

        static void AddFolder(string path, Cat c)
        {
            foreach (var go in Resources.LoadAll<GameObject>(path))
                if (!models[c].Contains(go)) models[c].Add(go);
        }

        static bool Categorize(string name, out Cat c)
        {
            c = Cat.Tree;
            string n = name.ToLowerInvariant();
            foreach (var s in Skip) if (n.Contains(s)) return false;
            if (n.Contains("palm")) { c = Cat.Palm; return true; }
            if (n.Contains("pine") || n.Contains("fir") || n.Contains("spruce") || n.Contains("conifer") || n.Contains("cypress")) { c = Cat.Pine; return true; }
            if (n.Contains("flower") || n.Contains("tulip") || n.Contains("daisy") || n.Contains("rose")) { c = Cat.Flower; return true; }
            if (n.Contains("bush") || n.Contains("shrub") || n.Contains("plant") || n.Contains("fern") || n.Contains("hedge")) { c = Cat.Bush; return true; }
            if (n.Contains("rock") || n.Contains("stone") || n.Contains("boulder")) { c = Cat.Rock; return true; }
            if (n.Contains("tree") || n.Contains("oak") || n.Contains("birch") || n.Contains("maple") || n.Contains("willow")
                || n.Contains("cherry") || n.Contains("sakura") || n.Contains("poplar")) { c = Cat.Tree; return true; }
            return false;
        }

        /// <summary>
        /// Places a random model of the category, scaled to <paramref name="height"/> metres and sitting on the ground.
        /// Returns null when no model of that category was supplied.
        /// </summary>
        public static GameObject Spawn(Cat c, Transform parent, Vector3 pos, float height, bool colliders)
        {
            Load();
            var list = models[c];
            if (list.Count == 0) return null;
            var prefab = list[Random.Range(0, list.Count)];
            var go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * prefab.transform.rotation;

            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Object.Destroy(go); return null; }

            Bounds b = WorldBounds(rends);
            float k = height / Mathf.Max(0.01f, b.size.y);
            go.transform.localScale = go.transform.localScale * k;
            b = WorldBounds(rends);
            float sink = c == Cat.Rock ? b.size.y * 0.2f : 0.05f;
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y - sink, pos.z - b.center.z);
            foreach (var r in rends)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            if (colliders && c != Cat.Bush && c != Cat.Flower)
            {
                // Colliders live on an upright helper so rotated/scaled imports don't skew them
                b = WorldBounds(rends);
                var holder = new GameObject(go.name + " Collider");
                holder.transform.SetParent(go.transform, true);
                holder.transform.position = new Vector3(pos.x, b.min.y, pos.z);
                holder.transform.rotation = Quaternion.identity;
                Vector3 ls = holder.transform.lossyScale;
                holder.transform.localScale = new Vector3(holder.transform.localScale.x / ls.x, holder.transform.localScale.y / ls.y, holder.transform.localScale.z / ls.z);
                if (c == Cat.Rock)
                {
                    var sc = holder.AddComponent<SphereCollider>();
                    sc.radius = Mathf.Min(b.size.x, b.size.z) * 0.45f;
                    sc.center = new Vector3(b.center.x - pos.x, b.size.y * 0.5f, b.center.z - pos.z);
                }
                else
                {
                    var trunk = holder.AddComponent<CapsuleCollider>();
                    trunk.direction = 1;
                    trunk.radius = 0.3f;
                    trunk.height = b.size.y;
                    trunk.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    var crown = holder.AddComponent<SphereCollider>();
                    crown.radius = Mathf.Min(b.size.x, b.size.z) * 0.38f;
                    crown.center = new Vector3(b.center.x - pos.x, b.size.y * 0.65f, b.center.z - pos.z);
                }
            }
            return go;
        }

        static Bounds WorldBounds(Renderer[] rends)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        // ------------------------------------------------------------------ Terrain textures

        /// <summary>
        /// User texture for a terrain layer from Resources/Art/Terrain/&lt;name&gt; (and &lt;name&gt;_Normal).
        /// </summary>
        public static Texture2D TerrainTexture(string name, out Texture2D normal)
        {
            normal = Resources.Load<Texture2D>("Art/Terrain/" + name + "_Normal");
            return Resources.Load<Texture2D>("Art/Terrain/" + name);
        }

        /// <summary>Colour-scaled copy of a readable texture (used for mowing stripes); returns the source if unreadable.</summary>
        public static Texture2D Tinted(Texture2D src, float mul, Color tint)
        {
            if (src == null) return null;
            if (!src.isReadable)
            {
                Debug.LogWarning("PangGolf: " + src.name + " is not readable - enable Read/Write in its import settings for mowing stripes.");
                return src;
            }
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color32(
                    (byte)Mathf.Clamp(c.r * mul * tint.r, 0, 255),
                    (byte)Mathf.Clamp(c.g * mul * tint.g, 0, 255),
                    (byte)Mathf.Clamp(c.b * mul * tint.b, 0, 255), c.a);
            }
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 8;
            t.Apply(true);
            return t;
        }
    }
}
