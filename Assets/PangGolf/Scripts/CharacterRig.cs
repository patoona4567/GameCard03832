using UnityEngine;

namespace PangGolf
{
    /// <summary>
    /// Chibi golfer built from primitives with a procedural swing.
    /// Local space: feet at y=0, facing +z (towards the ball), target on the character's left (-x).
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        enum Anim { Idle, Address, Backswing, Swing, Cheer, Sad }

        const float ShoulderY = 0.95f;
        const float PlaneTilt = 60f; // combined with the 15 degree torso lean the club reaches the ground
        public const float StanceDistance = 1.07f;

        public CharacterDef def;

        Transform model, body, torso, head, swing;
        Transform[] eyes;
        Anim anim = Anim.Idle;
        float angle, target, animT, startAngle, blinkT = 2f, practiceT = 3f;
        bool putt, practice;

        public static CharacterRig Create(Transform parent, CharacterDef d)
        {
            var go = new GameObject("Golfer");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CharacterRig>();
            rig.Build(d);
            return rig;
        }

        // ------------------------------------------------------------------ Build

        public void Build(CharacterDef d)
        {
            def = d;
            if (model != null) Destroy(model.gameObject);
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * d.scale;

            var skin = ProcGen.Lit(d.skin, 0.25f);
            var hair = ProcGen.Lit(d.hair, 0.35f);
            var top = ProcGen.Lit(d.top, 0.2f);
            var bottom = ProcGen.Lit(d.bottom, 0.15f);
            var shoes = ProcGen.Lit(d.shoes, 0.5f);
            var accent = ProcGen.Lit(d.accent, 0.4f);
            var black = ProcGen.Lit(new Color(0.05f, 0.05f, 0.08f), 0.9f);
            var white = ProcGen.Emissive(Color.white, new Color(0.6f, 0.6f, 0.6f));
            var blush = ProcGen.Lit(new Color(1f, 0.55f, 0.6f), 0.1f);
            var metal = ProcGen.Lit(new Color(0.8f, 0.82f, 0.86f), 0.9f, 0.9f);
            var grip = ProcGen.Lit(new Color(0.1f, 0.1f, 0.12f), 0.3f);

            body = new GameObject("Body").transform;
            body.SetParent(model, false);

            // Legs and shoes
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = P(PrimitiveType.Capsule, body, bottom, new Vector3(0.11f * s * d.bulk, 0.25f, 0f), new Vector3(0.15f, 0.2f, 0.15f) * d.bulk);
                leg.localRotation = Quaternion.Euler(0, 0, 4f * s);
                P(PrimitiveType.Sphere, body, shoes, new Vector3(0.12f * s * d.bulk, 0.05f, 0.05f), new Vector3(0.15f, 0.1f, 0.24f) * d.bulk);
            }

            // Torso (pivot at the hips so it can twist)
            torso = new GameObject("Torso").transform;
            torso.SetParent(body, false);
            torso.localPosition = new Vector3(0f, 0.46f, 0f);
            P(PrimitiveType.Capsule, torso, top, new Vector3(0f, 0.25f, 0f), new Vector3(0.38f * d.bulk, 0.24f, 0.28f * d.bulk));
            P(PrimitiveType.Cylinder, torso, bottom, new Vector3(0f, 0.04f, 0f), new Vector3(0.36f * d.bulk, 0.05f, 0.27f * d.bulk)); // belt line
            P(PrimitiveType.Sphere, torso, accent, new Vector3(0f, 0.38f, 0.12f * d.bulk), new Vector3(0.12f, 0.06f, 0.05f)); // collar

            // Head
            head = new GameObject("Head").transform;
            head.SetParent(torso, false);
            head.localPosition = new Vector3(0f, 0.6f, 0f);
            P(PrimitiveType.Sphere, head, skin, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.48f, 0.47f));
            eyes = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = P(PrimitiveType.Sphere, head, black, new Vector3(0.095f * s, 0.21f, 0.215f), new Vector3(0.075f, 0.1f, 0.04f));
                P(PrimitiveType.Sphere, eye, white, new Vector3(0.18f, 0.22f, 0.4f), Vector3.one * 0.35f);
                eyes[s < 0 ? 0 : 1] = eye;
                P(PrimitiveType.Sphere, head, blush, new Vector3(0.15f * s, 0.13f, 0.2f), new Vector3(0.08f, 0.04f, 0.02f));
                P(PrimitiveType.Sphere, head, skin, new Vector3(0.245f * s, 0.18f, 0f), new Vector3(0.06f, 0.1f, 0.07f)); // ears
            }
            P(PrimitiveType.Sphere, head, ProcGen.Lit(new Color(0.8f, 0.35f, 0.35f), 0.3f), new Vector3(0f, 0.1f, 0.225f), new Vector3(0.07f, 0.025f, 0.02f));
            BuildHair(d, hair);
            BuildAccessory(d, accent, hair, black);
            if (d.beard)
            {
                P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.04f, 0.15f), new Vector3(0.3f, 0.2f, 0.2f));
                P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.12f, 0.225f), new Vector3(0.16f, 0.035f, 0.03f)); // moustache
            }

            // Arms + club live on a tilted swing plane at the shoulders
            var plane = new GameObject("SwingPlane").transform;
            plane.SetParent(torso, false);
            plane.localPosition = new Vector3(0f, ShoulderY - 0.46f, 0.05f);
            plane.localRotation = Quaternion.Euler(-PlaneTilt, 0f, 0f);
            swing = new GameObject("Swing").transform;
            swing.SetParent(plane, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = P(PrimitiveType.Capsule, swing, top, new Vector3(0.07f * s * d.bulk, -0.21f, 0f), new Vector3(0.1f, 0.22f, 0.1f) * d.bulk);
                arm.localRotation = Quaternion.Euler(0f, 0f, -18f * s);
            }
            P(PrimitiveType.Sphere, swing, skin, new Vector3(0f, -0.44f, 0f), Vector3.one * 0.12f * d.bulk);
            P(PrimitiveType.Cylinder, swing, grip, new Vector3(0f, -0.5f, 0f), new Vector3(0.035f, 0.1f, 0.035f));
            P(PrimitiveType.Cylinder, swing, metal, new Vector3(0f, -0.88f, 0f), new Vector3(0.022f, 0.38f, 0.022f));
            P(PrimitiveType.Cube, swing, metal, new Vector3(-0.04f, -1.27f, 0.03f), new Vector3(0.14f, 0.06f, 0.08f));

            angle = target = 5f;
            Apply(0f);
        }

        void BuildHair(CharacterDef d, Material hair)
        {
            switch (d.hairStyle)
            {
                case HairStyle.Bald:
                    for (int s = -1; s <= 1; s += 2)
                        P(PrimitiveType.Sphere, head, hair, new Vector3(0.2f * s, 0.2f, -0.06f), new Vector3(0.12f, 0.18f, 0.3f));
                    return;
                case HairStyle.Spiky:
                {
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.28f, -0.03f), new Vector3(0.53f, 0.42f, 0.5f));
                    var cone = ProcGen.Cone(8);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        var spike = ProcGen.MeshObject("Spike", cone, head, hair).transform;
                        spike.localPosition = new Vector3(Mathf.Cos(a) * 0.12f, 0.4f, Mathf.Sin(a) * 0.12f - 0.04f);
                        spike.localRotation = Quaternion.Euler(Mathf.Sin(a) * -35f - 10f, 0f, Mathf.Cos(a) * 35f);
                        spike.localScale = new Vector3(0.08f, 0.2f, 0.08f);
                    }
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.36f, 0.16f), new Vector3(0.36f, 0.14f, 0.16f)); // fringe
                    return;
                }
                case HairStyle.TwinTails:
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.27f, -0.03f), new Vector3(0.53f, 0.42f, 0.5f));
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.36f, 0.16f), new Vector3(0.4f, 0.12f, 0.16f));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var tail = P(PrimitiveType.Capsule, head, hair, new Vector3(0.3f * s, 0.12f, -0.05f), new Vector3(0.16f, 0.2f, 0.16f));
                        tail.localRotation = Quaternion.Euler(0f, 0f, 20f * s);
                    }
                    return;
                case HairStyle.Long:
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.27f, -0.03f), new Vector3(0.54f, 0.43f, 0.5f));
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.36f, 0.16f), new Vector3(0.42f, 0.12f, 0.16f));
                    P(PrimitiveType.Capsule, head, hair, new Vector3(0f, 0.02f, -0.16f), new Vector3(0.44f, 0.3f, 0.2f));
                    return;
                default:
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.27f, -0.03f), new Vector3(0.52f, 0.4f, 0.5f));
                    P(PrimitiveType.Sphere, head, hair, new Vector3(0f, 0.35f, 0.15f), new Vector3(0.38f, 0.12f, 0.16f));
                    return;
            }
        }

        void BuildAccessory(CharacterDef d, Material accent, Material hair, Material black)
        {
            var top = ProcGen.Lit(d.top, 0.25f);
            switch (d.accessory)
            {
                case Accessory.Cap:
                    P(PrimitiveType.Sphere, head, top, new Vector3(0f, 0.33f, 0f), new Vector3(0.52f, 0.3f, 0.5f));
                    P(PrimitiveType.Cube, head, top, new Vector3(0f, 0.32f, 0.25f), new Vector3(0.36f, 0.03f, 0.22f));
                    break;
                case Accessory.FlatCap:
                    P(PrimitiveType.Sphere, head, accent, new Vector3(0f, 0.36f, 0.02f), new Vector3(0.54f, 0.22f, 0.54f));
                    P(PrimitiveType.Cube, head, accent, new Vector3(0f, 0.33f, 0.24f), new Vector3(0.34f, 0.03f, 0.14f));
                    break;
                case Accessory.Bow:
                    for (int s = -1; s <= 1; s += 2)
                        P(PrimitiveType.Sphere, head, accent, new Vector3(0.09f * s + 0.12f, 0.48f, -0.02f), new Vector3(0.14f, 0.1f, 0.06f));
                    P(PrimitiveType.Sphere, head, accent, new Vector3(0.12f, 0.48f, -0.02f), Vector3.one * 0.06f);
                    break;
                case Accessory.Headband:
                    P(PrimitiveType.Cylinder, head, accent, new Vector3(0f, 0.3f, 0f), new Vector3(0.52f, 0.035f, 0.5f));
                    break;
                case Accessory.Hat:
                    P(PrimitiveType.Cylinder, head, top, new Vector3(0f, 0.4f, 0f), new Vector3(0.75f, 0.015f, 0.75f));
                    P(PrimitiveType.Cylinder, head, top, new Vector3(0f, 0.5f, 0f), new Vector3(0.42f, 0.11f, 0.42f));
                    P(PrimitiveType.Cylinder, head, accent, new Vector3(0f, 0.43f, 0f), new Vector3(0.43f, 0.025f, 0.43f));
                    break;
            }
        }

        Transform P(PrimitiveType type, Transform parent, Material m, Vector3 pos, Vector3 scale)
        {
            var go = ProcGen.Prim(type, parent, m);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            return go.transform;
        }

        // ------------------------------------------------------------------ Control

        /// <summary>Stand beside the ball for a right-handed swing towards <paramref name="aimDir"/>.</summary>
        public void Place(Vector3 ball, Vector3 aimDir, IGround ground)
        {
            Vector3 right = Vector3.Cross(Vector3.up, aimDir).normalized;
            float s = def != null ? def.scale : 1f;
            Vector3 p = ball - right * StanceDistance * s;
            p.y = Mathf.Min(ground.GroundY(p), ball.y - BallSim.Radius + 0.05f);
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(right, Vector3.up);
        }

        public void Face(Vector3 position, Vector3 lookAt)
        {
            transform.position = position;
            Vector3 d = lookAt - position; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        public void Address(bool putter) { putt = putter; practice = false; anim = Anim.Address; }
        public void Idle() { anim = Anim.Idle; practiceT = 2.5f; }

        /// <summary>Backswing height follows the power gauge (0..1).</summary>
        public void Backswing(float power)
        {
            anim = Anim.Backswing;
            practice = false;
            target = putt ? 6f + power * 55f : 12f + power * 230f;
        }

        public void Swing()
        {
            anim = Anim.Swing;
            animT = 0f;
            startAngle = angle;
        }

        public void Cheer() { anim = Anim.Cheer; animT = 0f; }
        public void Sad() { anim = Anim.Sad; animT = 0f; }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            animT += dt;
            float bob = 0f, twist = 0f, lean = 15f, headPitch = 18f, jump = 0f;

            switch (anim)
            {
                case Anim.Idle:
                    lean = 3f; headPitch = 0f;
                    practiceT -= dt;
                    target = 5f + Mathf.Sin(t * 1.5f) * 4f;
                    if (practiceT < 0f) { Swing(); putt = false; practice = true; practiceT = 5f; }
                    angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-dt * 6f));
                    break;
                case Anim.Address:
                    target = 4f + Mathf.Sin(t * 2.2f) * 4f; // waggle
                    angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-dt * 8f));
                    break;
                case Anim.Backswing:
                    angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-dt * 14f));
                    break;
                case Anim.Swing:
                {
                    float down = putt ? 0.18f : 0.09f, follow = putt ? 0.35f : 0.4f;
                    float end = putt ? -45f : -235f;
                    if (animT < down) angle = Mathf.Lerp(startAngle, 0f, (animT / down) * (animT / down));
                    else angle = Mathf.Lerp(0f, end, 1f - Mathf.Pow(1f - Mathf.Clamp01((animT - down) / follow), 3f));
                    if (practice && animT > 1.4f) { practice = false; Idle(); } // practice swing in the showcase
                    break;
                }
                case Anim.Cheer:
                    lean = -5f; headPitch = -15f;
                    jump = Mathf.Abs(Mathf.Sin(animT * 7f)) * 0.3f * Mathf.Clamp01(3f - animT);
                    angle = Mathf.Lerp(angle, 175f + Mathf.Sin(animT * 12f) * 20f, 1f - Mathf.Exp(-dt * 10f));
                    break;
                case Anim.Sad:
                    lean = 20f; headPitch = 35f; bob = -0.04f;
                    angle = Mathf.Lerp(angle, -8f, 1f - Mathf.Exp(-dt * 5f));
                    break;
            }

            if (anim == Anim.Backswing || anim == Anim.Swing) twist = Mathf.Clamp(angle * 0.22f, -50f, 45f);
            bob += Mathf.Sin(t * 2.4f) * 0.008f;
            Apply(twist);
            body.localPosition = new Vector3(0f, bob + jump, 0f);
            torso.localRotation = Quaternion.Euler(lean, twist, 0f);
            head.localRotation = Quaternion.Euler(headPitch, -twist * 0.8f, 0f);

            // Blink
            blinkT -= dt;
            float eyeY = blinkT < 0.12f ? 0.15f : 1f;
            if (blinkT < 0f) blinkT = Random.Range(2f, 4.5f);
            if (anim == Anim.Cheer) eyeY = 0.35f; // happy squint
            foreach (var e in eyes) e.localScale = new Vector3(0.075f, 0.1f * eyeY, 0.04f);
        }

        void Apply(float twist)
        {
            if (swing != null) swing.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
