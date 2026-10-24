using UnityEngine;

namespace PangGolf
{
    /// <summary>
    /// Camera post-processing for the built-in pipeline: bloom, ACES tone mapping,
    /// saturation/contrast grading and vignette. Disables itself if the shader is unavailable.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public float threshold = 0.95f;
        public float knee = 0.5f;
        public float bloom = 0.45f;
        public float exposure = 0.9f;
        public float saturation = 1.18f;
        public float contrast = 1.06f;
        public float vignette = 0.6f;
        public Color tint = new Color(1.02f, 1f, 0.97f, 1f);
        public float flash; // brief white-out, e.g. on a PANGYA

        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[8];

        void Awake()
        {
            var sh = Shader.Find("Hidden/PangGolf/Post");
            if (sh == null || !sh.isSupported) { enabled = false; return; }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }

        void Update()
        {
            if (flash > 0f) flash = Mathf.Max(0f, flash - Time.deltaTime * 2.5f);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }

            mat.SetFloat("_Threshold", threshold);
            mat.SetFloat("_Knee", knee);
            mat.SetFloat("_BloomIntensity", bloom + flash * 2f);
            mat.SetFloat("_Exposure", exposure * (1f + flash * 0.6f));
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Contrast", contrast);
            mat.SetFloat("_Vignette", vignette);
            mat.SetColor("_Tint", tint);

            var fmt = RenderTextureFormat.DefaultHDR;
            int w = src.width / 2, h = src.height / 2;
            var cur = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, mat, 0);
            chain[0] = cur;
            int n = 1;
            for (; n < chain.Length; n++)
            {
                w /= 2; h /= 2;
                if (w < 8 || h < 8) break;
                var rt = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, rt, mat, 1);
                chain[n] = rt;
                cur = rt;
            }
            for (int i = n - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, chain[i], mat, 2);
                cur = chain[i];
            }
            mat.SetTexture("_BloomTex", cur);
            Graphics.Blit(src, dst, mat, 3);
            for (int i = 0; i < n; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
        }
    }
}
