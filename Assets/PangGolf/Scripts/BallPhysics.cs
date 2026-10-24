using UnityEngine;

namespace PangGolf
{
    public enum Surface { Tee, Fairway, Rough, Green, Bunker, Water, OutOfBounds }

    public enum ShotType { Normal, Power, Tomahawk, Spike, Cobra }

    public enum BallMode { Rest, Flight, Roll }

    public enum SimEvent { None, Bounce, TreeHit, PinHit, LipOut, Holed, Water, OutOfBounds, Stopped }

    public interface IGround
    {
        float GroundY(Vector3 p);
        Vector3 Normal(Vector3 p);
        Surface SurfaceAt(Vector3 p);
        Vector3 Cup { get; }
        bool UseObstacles { get; }
    }

    public struct SurfaceProps
    {
        public float restitution;   // vertical bounce
        public float impactFriction; // horizontal loss on bounce
        public float roll;          // rolling friction coefficient (decel = roll * g)
        public float spinGrip;      // how much backspin bites on landing

        public static SurfaceProps For(Surface s)
        {
            switch (s)
            {
                case Surface.Green: return new SurfaceProps { restitution = 0.2f, impactFriction = 0.55f, roll = 0.065f, spinGrip = 1.0f };
                case Surface.Fairway:
                case Surface.Tee: return new SurfaceProps { restitution = 0.3f, impactFriction = 0.3f, roll = 0.34f, spinGrip = 0.7f };
                case Surface.Rough: return new SurfaceProps { restitution = 0.16f, impactFriction = 0.55f, roll = 0.8f, spinGrip = 0.25f };
                case Surface.Bunker: return new SurfaceProps { restitution = 0.03f, impactFriction = 0.9f, roll = 3.0f, spinGrip = 0f };
                default: return new SurfaceProps { restitution = 0.2f, impactFriction = 0.5f, roll = 0.6f, spinGrip = 0.2f };
            }
        }
    }

    public struct ShotParams
    {
        public Vector3 dir;       // horizontal unit vector
        public float speed;       // m/s
        public float launchDeg;   // launch angle
        public float back;        // backspin (negative = topspin)
        public float liftSpin;    // spin that generates lift in flight
        public float side;        // curve (+ = right)
        public ShotType type;
        public float expectedCarry; // metres, used by Cobra timing
        public bool putt;
    }

    /// <summary>
    /// Deterministic golf ball simulation (flight, bounce, roll, cup).
    /// The same code is used for the shot preview, club calibration and the real shot.
    /// </summary>
    public class BallSim
    {
        public const float G = 9.81f;
        public const float KD = 0.0042f;   // drag
        public const float KL = 0.0028f;   // lift
        public const float KS = 0.0016f;   // side curve
        public const float WindMul = 1.35f;
        public const float Radius = 0.05f;
        public const float CupRadius = 0.12f;
        public const float PinHeight = 2.4f;
        public const float StepDt = 1f / 240f;

        public Vector3 pos, vel;
        public float back, side, liftSpin;
        public BallMode mode = BallMode.Rest;
        public ShotType type;
        public float time;
        public int bounces;
        public bool landed;
        public Vector3 firstLand;
        public float apexY;
        public Surface lastSurface;
        public IGround ground;

        bool apexPassed;
        float liftMul = 1f, gravMul = 1f;
        float expectedCarry;
        Vector3 launchPos;
        float cobraBoost;
        bool cobraRisen;
        bool pinTouched;

        public BallSim(IGround g) { ground = g; }

        public void Launch(Vector3 start, ShotParams sp)
        {
            start.y = Mathf.Max(start.y, ground.GroundY(start) + Radius);
            pos = start;
            type = sp.type;
            time = 0f;
            bounces = 0;
            landed = false;
            apexPassed = false;
            cobraRisen = false;
            cobraBoost = 0f;
            pinTouched = false;
            launchPos = start;
            apexY = start.y;
            expectedCarry = sp.expectedCarry;
            back = sp.back;
            side = sp.side;
            liftSpin = sp.liftSpin;
            liftMul = 1f;
            gravMul = 1f;

            if (sp.putt)
            {
                mode = BallMode.Roll;
                vel = sp.dir * sp.speed;
                back = 0f; side = 0f;
                return;
            }

            switch (type)
            {
                case ShotType.Spike: liftMul = 2.0f; break;
                case ShotType.Cobra: liftMul = 0.55f; break;
            }

            float a = sp.launchDeg * Mathf.Deg2Rad;
            vel = sp.dir * (Mathf.Cos(a) * sp.speed) + Vector3.up * (Mathf.Sin(a) * sp.speed);
            mode = BallMode.Flight;
        }

        public SimEvent Step(float dt, Vector3 wind)
        {
            if (mode == BallMode.Rest) return SimEvent.None;
            time += dt;
            return mode == BallMode.Flight ? StepFlight(dt, wind) : StepRoll(dt);
        }

        SimEvent StepFlight(float dt, Vector3 wind)
        {
            Vector3 vr = vel - wind * WindMul;
            float s = vr.magnitude;
            Vector3 acc = Vector3.down * (G * gravMul) - vr * (KD * s);

            Vector3 right = Vector3.Cross(Vector3.up, vr);
            if (right.sqrMagnitude > 1e-6f)
            {
                right.Normalize();
                Vector3 liftDir = Vector3.Cross(vr, right).normalized;
                float lc = Mathf.Clamp(0.25f + liftSpin, 0f, 1.6f) * liftMul;
                acc += liftDir * (KL * lc * s * s);
                acc += right * (KS * side * s * s);
            }

            // Special shot behaviour
            if (!apexPassed && vel.y < 0f)
            {
                apexPassed = true;
                if (type == ShotType.Tomahawk) gravMul = 1.7f;
                if (type == ShotType.Spike) { liftMul = 0f; gravMul = 2.4f; }
            }
            if (type == ShotType.Cobra && !cobraRisen)
            {
                Vector3 flat = pos - launchPos; flat.y = 0f;
                if (flat.magnitude > expectedCarry * 0.5f) { cobraRisen = true; cobraBoost = 0.55f; liftMul = 1f; }
            }
            if (cobraBoost > 0f)
            {
                acc += Vector3.up * 15f;
                cobraBoost -= dt;
            }

            Vector3 prev = pos;
            vel += acc * dt;
            pos += vel * dt;
            back *= Mathf.Exp(-0.08f * dt);
            liftSpin *= Mathf.Exp(-0.08f * dt);
            if (pos.y > apexY) apexY = pos.y;

            if (ground.UseObstacles)
            {
                // Trees and other obstacles
                RaycastHit hit;
                if (Physics.Linecast(prev, pos, out hit))
                {
                    Vector3 r = Vector3.Reflect(vel, hit.normal) * 0.28f;
                    vel = r + Random.insideUnitSphere * 0.6f;
                    pos = hit.point + hit.normal * (Radius + 0.02f);
                    back = 0f; side = 0f; liftSpin = 0f;
                    return SimEvent.TreeHit;
                }

                // Flag pole
                Vector3 cup = ground.Cup;
                Vector3 toBall = pos - cup; toBall.y = 0f;
                if (!pinTouched && toBall.magnitude < 0.03f + Radius && pos.y < cup.y + PinHeight && pos.y > cup.y)
                {
                    pinTouched = true;
                    Vector3 n = toBall.sqrMagnitude > 1e-6f ? toBall.normalized : -new Vector3(vel.x, 0, vel.z).normalized;
                    Vector3 hv = new Vector3(vel.x, 0f, vel.z);
                    hv = Vector3.Reflect(hv, n) * 0.25f;
                    vel = new Vector3(hv.x, Mathf.Min(vel.y, 0f) * 0.5f, hv.z);
                    return SimEvent.PinHit;
                }
            }

            float gy = ground.GroundY(pos);
            if (pos.y - Radius > gy) return SimEvent.None;

            // Ground contact
            Surface surf = ground.SurfaceAt(pos);
            lastSurface = surf;
            if (!landed) { landed = true; firstLand = new Vector3(pos.x, gy, pos.z); }
            if (surf == Surface.Water) { mode = BallMode.Rest; vel = Vector3.zero; pos.y = gy; return SimEvent.Water; }
            if (surf == Surface.OutOfBounds) { mode = BallMode.Rest; vel = Vector3.zero; pos.y = gy + Radius; return SimEvent.OutOfBounds; }

            // Dunk straight into the cup
            Vector3 dc = pos - ground.Cup; dc.y = 0f;
            if (dc.magnitude < CupRadius + Radius * 0.5f)
            {
                mode = BallMode.Rest; vel = Vector3.zero; pos = ground.Cup + Vector3.down * 0.06f;
                return SimEvent.Holed;
            }

            pos.y = gy + Radius;
            Vector3 nrm = ground.Normal(pos);
            SurfaceProps sp = SurfaceProps.For(surf);
            float vnMag = Vector3.Dot(vel, nrm);
            Vector3 vn = nrm * vnMag;
            Vector3 vt = vel - vn;

            float e = sp.restitution;
            float fr = sp.impactFriction;
            if (bounces == 0)
            {
                if (type == ShotType.Tomahawk) { e = 0.05f; fr = 0.85f; }
                if (type == ShotType.Spike) { e = 0.08f; fr = 0.6f; back += 1.5f; }
                if (type == ShotType.Cobra) { e = 0.3f; fr = 0.25f; back = -0.3f; }
            }

            vt *= (1f - fr);
            Vector3 fwd = new Vector3(vt.x, 0f, vt.z);
            if (fwd.sqrMagnitude > 1e-6f)
            {
                fwd.Normalize();
                // Backspin bites (can pull the ball back), topspin pushes it on.
                float brake = back * 6f * sp.spinGrip * Mathf.Clamp01(-vnMag / 12f + 0.35f);
                vt -= fwd * brake;
            }
            vel = vt - vn * e;
            back *= 0.35f;
            side *= 0.25f;
            bounces++;

            if (Vector3.Dot(vel, nrm) < 1.1f || bounces >= 7)
            {
                mode = BallMode.Roll;
                vel = Vector3.ProjectOnPlane(vel, nrm);
            }
            return SimEvent.Bounce;
        }

        SimEvent StepRoll(float dt)
        {
            Surface surf = ground.SurfaceAt(pos);
            lastSurface = surf;
            if (surf == Surface.Water) { mode = BallMode.Rest; vel = Vector3.zero; return SimEvent.Water; }
            if (surf == Surface.OutOfBounds) { mode = BallMode.Rest; vel = Vector3.zero; return SimEvent.OutOfBounds; }

            Vector3 n = ground.Normal(pos);
            SurfaceProps sp = SurfaceProps.For(surf);
            Vector3 gT = Vector3.ProjectOnPlane(Vector3.down * G, n);
            Vector3 v = Vector3.ProjectOnPlane(vel, n);
            float speed = v.magnitude;
            float fr = sp.roll * G;
            // A little residual backspin keeps braking after landing
            if (back > 0.05f) { fr += back * 2.5f; back *= Mathf.Exp(-3f * dt); }

            if (speed < 0.06f && gT.magnitude < fr * 1.05f)
            {
                vel = Vector3.zero; mode = BallMode.Rest;
                pos.y = ground.GroundY(pos) + Radius;
                return SimEvent.Stopped;
            }

            Vector3 acc = gT;
            if (speed > 1e-4f) acc -= v / speed * fr;
            Vector3 nv = v + acc * dt;
            if (Vector3.Dot(nv, v) < 0f && gT.magnitude < fr)
            {
                vel = Vector3.zero; mode = BallMode.Rest;
                pos.y = ground.GroundY(pos) + Radius;
                return SimEvent.Stopped;
            }

            Vector3 prev = pos;
            pos += nv * dt;
            pos.y = ground.GroundY(pos) + Radius;
            vel = nv;

            if (ground.UseObstacles)
            {
                RaycastHit hit;
                Vector3 lift = Vector3.up * 0.05f;
                if (Physics.Linecast(prev + lift, pos + lift, out hit))
                {
                    vel = Vector3.Reflect(vel, hit.normal) * 0.4f;
                    vel.y = 0f;
                    pos = prev;
                    return SimEvent.TreeHit;
                }
            }

            // Cup
            Vector3 dc = pos - ground.Cup; dc.y = 0f;
            float d = dc.magnitude;
            if (d < CupRadius)
            {
                float hs = new Vector3(vel.x, 0, vel.z).magnitude;
                if (hs < 2.1f || (hs < 3.2f && d < CupRadius * 0.45f))
                {
                    mode = BallMode.Rest; vel = Vector3.zero; pos = ground.Cup + Vector3.down * 0.06f;
                    return SimEvent.Holed;
                }
                if (hs < 3.6f && !pinTouched)
                {
                    pinTouched = true;
                    float ang = Random.Range(25f, 55f) * (Random.value < 0.5f ? -1f : 1f);
                    vel = Quaternion.Euler(0f, ang, 0f) * vel * 0.6f;
                    return SimEvent.LipOut;
                }
            }

            if (time > 60f) { vel = Vector3.zero; mode = BallMode.Rest; return SimEvent.Stopped; }
            return SimEvent.None;
        }
    }

    /// <summary>Flat calm ground used for club calibration.</summary>
    public class FlatGround : IGround
    {
        public float GroundY(Vector3 p) { return 0f; }
        public Vector3 Normal(Vector3 p) { return Vector3.up; }
        public Surface SurfaceAt(Vector3 p) { return Surface.Fairway; }
        public Vector3 Cup { get { return new Vector3(1e6f, 0f, 1e6f); } }
        public bool UseObstacles { get { return false; } }
    }

    /// <summary>Wraps a course but ignores trees/pin - for the landing preview ring.</summary>
    public class PreviewGround : IGround
    {
        readonly IGround inner;
        public PreviewGround(IGround g) { inner = g; }
        public float GroundY(Vector3 p) { return inner.GroundY(p); }
        public Vector3 Normal(Vector3 p) { return inner.Normal(p); }
        public Surface SurfaceAt(Vector3 p) { return Surface.Fairway; }
        public Vector3 Cup { get { return new Vector3(1e6f, 0f, 1e6f); } }
        public bool UseObstacles { get { return false; } }
    }
}
