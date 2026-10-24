using UnityEngine;

namespace PangGolf
{
    /// <summary>Slowly drifts the cloud layer with the breeze and wraps it back.</summary>
    public class CloudDrift : MonoBehaviour
    {
        Vector3 origin;
        float offset;

        void Start() { origin = transform.position; }

        void Update()
        {
            offset = Mathf.Repeat(offset + Time.deltaTime * 1.5f, 600f);
            float k = offset < 300f ? offset : 600f - offset; // ping-pong so the sky never jumps
            transform.position = origin + new Vector3(k, 0f, k * 0.35f);
        }
    }
}
