using UnityEngine;

namespace PangGolf
{
    public class GolfCamera : MonoBehaviour
    {
        public enum Mode { Aim, Landing, GreenTop, Follow, Flyover, Orbit, Title, Showcase }

        public Mode mode = Mode.Title;
        public float flyT;
        GolfGame game;
        Vector3 posVel;
        Vector3 look;
        bool snap = true;

        float kick, shake;
        Camera cam;
        float baseFov = 55f;

        public void Init(GolfGame g)
        {
            game = g;
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
        }

        public void Snap() { snap = true; }

        /// <summary>FOV punch + shake, e.g. on a powerful hit.</summary>
        public void Kick(float strength)
        {
            kick = Mathf.Max(kick, strength);
            shake = Mathf.Max(shake, strength * 0.5f);
        }

        void LateUpdate()
        {
            var course = game.course;
            if (course == null) return;
            float dt = Time.deltaTime;
            Vector3 b = game.BallPos;
            Vector3 d = game.AimDir;
            Vector3 targetPos, targetLook;
            float smooth = 0.25f;

            switch (mode)
            {
                case Mode.Aim:
                {
                    bool putt = game.CurrentClub.putter;
                    float back = putt ? 3.4f : 6.5f, up = putt ? 1.5f : 2.7f, ahead = putt ? 6f : 35f;
                    targetPos = b - d * back + Vector3.up * up;
                    targetLook = b + d * ahead + Vector3.up * (putt ? -0.6f : 1.2f);
                    break;
                }
                case Mode.Landing:
                {
                    Vector3 p = game.predicted;
                    targetPos = p - d * 26f + Vector3.up * 15f;
                    targetLook = p;
                    smooth = 0.35f;
                    break;
                }
                case Mode.GreenTop:
                {
                    Vector3 gc = course.GreenCenter;
                    targetPos = gc + Vector3.up * 30f - d * 12f;
                    targetLook = gc;
                    smooth = 0.35f;
                    break;
                }
                case Mode.Follow:
                {
                    Vector3 v = game.BallVel;
                    Vector3 flat = new Vector3(v.x, 0f, v.z);
                    Vector3 dir = flat.sqrMagnitude > 0.04f ? flat.normalized : d;
                    bool rolling = game.sim.mode != BallMode.Flight;
                    float dist = rolling ? 5f : 11f, h = rolling ? 2.2f : 4.5f;
                    targetPos = b - dir * dist + Vector3.up * h;
                    targetLook = b;
                    smooth = 0.18f;
                    break;
                }
                case Mode.Flyover:
                {
                    Vector3 p = course.PointOnPath(flyT);
                    Vector3 n = course.PointOnPath(Mathf.Min(1f, flyT + 0.12f));
                    Vector3 fwd = n - p; fwd.y = 0f;
                    if (fwd.sqrMagnitude < 1f) fwd = course.GreenCenter - course.TeePos;
                    fwd.y = 0f; fwd.Normalize();
                    targetPos = p - fwd * 25f + Vector3.up * 45f;
                    targetLook = Vector3.Lerp(n, course.Cup, flyT);
                    smooth = 0.6f;
                    break;
                }
                case Mode.Orbit:
                {
                    float a = Time.time * 0.45f;
                    Vector3 c = course.Cup;
                    targetPos = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.5f + Vector3.up * 2f;
                    targetLook = c + Vector3.up * 0.4f;
                    smooth = 0.5f;
                    break;
                }
                case Mode.Showcase:
                {
                    Vector3 focus = game.showcasePos + Vector3.up * 0.75f;
                    float sway = Mathf.Sin(Time.time * 0.35f) * 28f;
                    Vector3 dir = Quaternion.Euler(0f, sway, 0f) * game.showcaseDir;
                    targetPos = focus + dir * 3.4f + Vector3.up * 0.35f;
                    Vector3 camRight = Vector3.Cross(Vector3.up, -dir).normalized;
                    targetLook = focus + camRight * 0.75f; // golfer sits left of centre, stats on the right
                    smooth = 0.3f;
                    break;
                }
                default: // Title: slow orbit over the hole
                {
                    float a = Time.time * 0.05f;
                    Vector3 c = Vector3.Lerp(course.TeePos, course.GreenCenter, 0.5f);
                    targetPos = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 230f + Vector3.up * 110f;
                    targetLook = c;
                    smooth = 1f;
                    break;
                }
            }

            float gy = course.GroundY(targetPos);
            if (targetPos.y < gy + 1.2f) targetPos.y = gy + 1.2f;

            if (snap)
            {
                snap = false;
                posVel = Vector3.zero;
                transform.position = targetPos;
                look = targetLook;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref posVel, smooth);
                look = Vector3.Lerp(look, targetLook, 1f - Mathf.Exp(-dt * 7f));
            }

            Vector3 fwdLook = look - transform.position;
            if (fwdLook.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(fwdLook, Vector3.up);

            kick = Mathf.MoveTowards(kick, 0f, dt * 2.2f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.6f);
            if (cam != null) cam.fieldOfView = baseFov - kick * 7f;
            if (shake > 0f)
            {
                float t = Time.time * 40f;
                transform.rotation *= Quaternion.Euler(Mathf.Sin(t) * shake * 0.8f, Mathf.Cos(t * 1.3f) * shake * 0.8f, 0f);
            }
        }
    }
}
