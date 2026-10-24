using UnityEngine;

namespace PangGolf
{
    public class CloudDrift : MonoBehaviour
    {
        float speed;
        void Start() { speed = Random.Range(0.6f, 1.6f); }
        void Update() { transform.position += new Vector3(speed, 0f, speed * 0.3f) * Time.deltaTime; }
    }
}
