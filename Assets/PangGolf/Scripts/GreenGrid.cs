using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PangGolf
{
    /// <summary>
    /// Putting grid draped over the green with flowing dots that show the slope,
    /// like the green read in Pangya.
    /// </summary>
    public class GreenGrid : MonoBehaviour
    {
        const float Spacing = 1.5f;

        Course course;
        readonly List<Transform> dots = new List<Transform>();
        readonly List<Vector3> dotBase = new List<Vector3>();
        readonly List<Vector3> dotFlow = new List<Vector3>();
        readonly List<float> dotPhase = new List<float>();
        bool visible = true;

        public static GreenGrid Build(Course c)
        {
            var go = new GameObject("GreenGrid");
            go.transform.SetParent(c.transform, false);
            var g = go.AddComponent<GreenGrid>();
            g.course = c;
            g.Create();
            return g;
        }

        void Create()
        {
            var lineMat = ProcGen.Unlit(Color.white);
            Vector3 gc = course.GreenCenter;
            float r = course.hole.greenRadius + 2f;

            for (int axis = 0; axis < 2; axis++)
            {
                for (float o = -r; o <= r + 0.01f; o += Spacing)
                {
                    float half = Mathf.Sqrt(Mathf.Max(0f, r * r - o * o));
                    if (half < 0.5f) continue;
                    var pts = new List<Vector3>();
                    for (float s = -half; s <= half + 0.01f; s += 0.5f)
                    {
                        Vector3 p = axis == 0 ? new Vector3(gc.x + s, 0, gc.z + o) : new Vector3(gc.x + o, 0, gc.z + s);
                        if (course.SurfaceAt(p) != Surface.Green && pts.Count == 0) continue;
                        if (course.SurfaceAt(p) != Surface.Green) break;
                        p.y = course.GroundY(p) + 0.03f;
                        pts.Add(p);
                    }
                    if (pts.Count < 2) continue;
                    var go = new GameObject("GridLine");
                    go.transform.SetParent(transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = lineMat;
                    lr.useWorldSpace = true;
                    lr.widthMultiplier = 0.035f;
                    lr.positionCount = pts.Count;
                    lr.SetPositions(pts.ToArray());
                    lr.shadowCastingMode = ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.startColor = lr.endColor = new Color(1f, 1f, 1f, 0.35f);
                }
            }

            var dotMat = ProcGen.Unlit(Color.white);
            int i = 0;
            for (float x = -r; x <= r; x += Spacing)
            {
                for (float z = -r; z <= r; z += Spacing)
                {
                    Vector3 p = new Vector3(gc.x + x + Spacing * 0.5f, 0, gc.z + z + Spacing * 0.5f);
                    if (course.SurfaceAt(p) != Surface.Green) continue;
                    p.y = course.GroundY(p);
                    Vector3 n = course.Normal(p);
                    Vector3 down = Vector3.ProjectOnPlane(Vector3.down, n);
                    float slope = down.magnitude;
                    var d = ProcGen.Prim(PrimitiveType.Sphere, transform, dotMat, false, false);
                    d.GetComponent<MeshRenderer>().receiveShadows = false;
                    d.transform.localScale = Vector3.one * 0.09f;
                    var block = new MaterialPropertyBlock();
                    float k = Mathf.Clamp01(slope / 0.06f);
                    Color c = k < 0.5f ? Color.Lerp(new Color(0.6f, 0.9f, 1f), new Color(1f, 1f, 0.4f), k * 2f)
                                       : Color.Lerp(new Color(1f, 1f, 0.4f), new Color(1f, 0.35f, 0.25f), (k - 0.5f) * 2f);
                    block.SetColor("_Color", c);
                    d.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                    dots.Add(d.transform);
                    dotBase.Add(p);
                    dotFlow.Add(slope > 1e-4f ? down.normalized * Mathf.Lerp(0.3f, 1.2f, k) : Vector3.zero);
                    dotPhase.Add((i++ * 0.137f) % 1f);
                }
            }
        }

        public void SetVisible(bool v)
        {
            if (v == visible) return;
            visible = v;
            gameObject.SetActive(v);
        }

        void Update()
        {
            float t = Time.time * 0.6f;
            for (int i = 0; i < dots.Count; i++)
            {
                float ph = (t + dotPhase[i]) % 1f;
                Vector3 p = dotBase[i] + dotFlow[i] * ph;
                p.y = course.GroundY(p) + 0.05f;
                dots[i].position = p;
            }
        }
    }
}
