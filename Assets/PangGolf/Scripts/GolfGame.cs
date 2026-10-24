using UnityEngine;
using UnityEngine.Rendering;

namespace PangGolf
{
    /// <summary>
    /// Main game controller: hole flow, aiming, the shot gauge, ball simulation and scoring.
    /// Drop it on an empty GameObject (or just press Play - <see cref="Bootstrap"/> adds it).
    /// </summary>
    public class GolfGame : MonoBehaviour
    {
        public enum State { Title, Intro, Aim, Swing, Flight, Settle, HoleDone, Scorecard, Final }

        public const int MaxPowerSlots = 3;
        public const int StrokeLimitOverPar = 5;

        public State state = State.Title;
        public Course course;
        public BallSim sim;
        public GolfCamera cam;
        public Sfx sfx;
        public Fx fx;

        public int holeIndex;
        public int strokes;
        public int[] scores;
        public int clubIndex;
        public float aimYaw;
        public Vector2 impact;              // x: curve (-1 left .. 1 right), y: spin (+1 top .. -1 back)
        public bool powerArmed;
        public ShotType special = ShotType.Power;
        public int powerSlots = 1;
        public Vector3 wind;
        public Surface lie = Surface.Tee;
        public Vector3 predicted;
        public readonly ShotGauge gauge = new ShotGauge();
        public bool showHelp = true;
        public float lastShotYards;

        // On-screen messages
        public string bigText, subText;
        public Color bigColor = Color.white;
        public float bigTimer;
        public string toast;
        public float toastTimer;

        Transform ball;
        TrailRenderer trail;
        LineRenderer ring;
        GreenGrid grid;
        BallSim preview;
        float roughFactor = 0.9f;
        float stateTimer;
        float simAcc;
        float spinRepeat;
        Vector3 shotStart;
        bool shotFromGreen;
        System.Action afterSettle;

        public Club CurrentClub { get { return Clubs.All[clubIndex]; } }
        public HoleDef Hole { get { return course.hole; } }
        public Vector3 BallPos { get { return sim != null ? sim.pos : Vector3.zero; } }
        public Vector3 BallVel { get { return sim != null ? sim.vel : Vector3.zero; } }
        public Vector3 AimDir { get { return Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward; } }
        public float WindSpeed { get { return wind.magnitude; } }
        public float DistanceToPin { get { Vector3 d = course.Cup - BallPos; d.y = 0f; return d.magnitude; } }
        public float HeightToPin { get { return course.Cup.y - (BallPos.y - BallSim.Radius); } }
        public int TotalStrokes { get { int t = 0; for (int i = 0; i < scores.Length; i++) t += scores[i]; return t; } }
        public int TotalPar { get { int t = 0; for (int i = 0; i < scores.Length; i++) if (scores[i] > 0) t += Holes.All[i].par; return t; } }

        public float LieFactor
        {
            get
            {
                switch (lie)
                {
                    case Surface.Rough: return roughFactor;
                    case Surface.Bunker: return clubIndex >= Clubs.SandWedge - 1 && !CurrentClub.putter ? 0.88f : 0.7f;
                    default: return 1f;
                }
            }
        }

        public float ShotMultiplier
        {
            get
            {
                if (!powerArmed) return 1f;
                switch (special)
                {
                    case ShotType.Tomahawk: return 1.12f;
                    case ShotType.Spike: return 1.2f;
                    case ShotType.Cobra: return 1.15f;
                    default: return 1.1f;
                }
            }
        }

        // ------------------------------------------------------------------ Setup

        void Awake()
        {
            DisableSceneDefaults();
            WorldEnv.Build(transform);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            var c = camGo.AddComponent<Camera>();
            c.fieldOfView = 55f;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 3500f;
            c.allowHDR = true;
            c.allowMSAA = true;
            c.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            cam = camGo.AddComponent<GolfCamera>();
            cam.Init(this);

            sfx = new GameObject("Sfx").AddComponent<Sfx>();
            sfx.transform.SetParent(transform, false);
            fx = new GameObject("Fx").AddComponent<Fx>();
            fx.transform.SetParent(transform, false);

            BuildBall();
            BuildRing();
            gameObject.AddComponent<GolfHUD>().Init(this);

            foreach (var club in Clubs.All) club.SpeedFor(1f); // calibrate up front

            scores = new int[Holes.All.Length];
            LoadHole(0);
            state = State.Title;
            cam.mode = GolfCamera.Mode.Title;
            cam.Snap();
        }

        static void DisableSceneDefaults()
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.enabled = false;
        }

        void BuildBall()
        {
            var go = ProcGen.Prim(PrimitiveType.Sphere, transform, ProcGen.Lit(Color.white, 0.75f));
            go.name = "Ball";
            go.transform.localScale = Vector3.one * (BallSim.Radius * 2f);
            ball = go.transform;

            trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = ProcGen.Unlit(Color.white);
            trail.time = 1.6f;
            trail.minVertexDistance = 0.3f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.14f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.9f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
        }

        void BuildRing()
        {
            var go = new GameObject("TargetRing");
            go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = ProcGen.Unlit(Color.white);
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.18f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.startColor = ring.endColor = new Color(1f, 0.92f, 0.25f, 0.95f);
        }

        void LoadHole(int index)
        {
            holeIndex = index;
            if (course != null) Destroy(course.gameObject);
            course = Course.Build(Holes.All[index], transform);
            sim = new BallSim(course);
            preview = new BallSim(new PreviewGround(course));
            grid = GreenGrid.Build(course);
            strokes = 0;
            lie = Surface.Tee;
            PlaceBall(course.TeePos);
            NewWind();
            AutoClub();
            AutoAim();
        }

        void PlaceBall(Vector3 p)
        {
            p.y = course.GroundY(p) + BallSim.Radius;
            sim.pos = p;
            sim.vel = Vector3.zero;
            sim.mode = BallMode.Rest;
            ball.position = p;
            trail.Clear();
            trail.emitting = false;
        }

        void NewWind()
        {
            float max = Hole.windMax;
            float s = Mathf.Round(Random.Range(0f, max));
            float a = Random.Range(0f, 360f);
            wind = Quaternion.Euler(0f, a, 0f) * Vector3.forward * s;
            course.SetWind(wind);
        }

        void AutoClub()
        {
            if (lie == Surface.Green) { clubIndex = Clubs.Putter; return; }
            clubIndex = Clubs.BestFor(DistanceToPin * 0.98f);
            if (lie == Surface.Bunker && DistanceToPin < 70f) clubIndex = Clubs.SandWedge;
        }

        void AutoAim()
        {
            Vector3 b = BallPos;
            Vector3 target = course.Cup;
            float reach = CurrentClub.MaxMetres * 1.05f;
            if (!CurrentClub.putter && DistanceToPin > reach)
            {
                float t;
                course.ClosestOnPath(b, out t);
                for (float u = t; u <= 1f; u += 0.005f)
                {
                    Vector3 p = course.PointOnPath(u);
                    Vector3 d = p - b; d.y = 0f;
                    if (d.magnitude >= CurrentClub.MaxMetres * 0.97f) { target = p; break; }
                }
            }
            Vector3 dir = target - b; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) aimYaw = Quaternion.LookRotation(dir).eulerAngles.y;
        }

        // ------------------------------------------------------------------ Flow

        void StartRound()
        {
            for (int i = 0; i < scores.Length; i++) scores[i] = 0;
            powerSlots = 1;
            BeginHole(0);
        }

        void BeginHole(int index)
        {
            LoadHole(index);
            state = State.Intro;
            stateTimer = 0f;
            cam.mode = GolfCamera.Mode.Flyover;
            cam.flyT = 0f;
            cam.Snap();
            Big("HOLE " + (index + 1), Hole.name + "   PAR " + Hole.par + "   " + Mathf.RoundToInt(course.LengthYards) + "y", new Color(1f, 0.95f, 0.6f), 4.5f);
        }

        void PrepareShot(bool newWind = true)
        {
            lie = course.SurfaceAt(BallPos);
            if (lie == Surface.Rough) roughFactor = Mathf.Round(Random.Range(0.85f, 0.95f) * 100f) / 100f;
            if (newWind) NewWind();
            powerArmed = false;
            impact = Vector2.zero;
            AutoClub();
            AutoAim();
            gauge.Reset();
            state = State.Aim;
            cam.mode = GolfCamera.Mode.Aim;
            trail.emitting = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (bigTimer > 0f) bigTimer -= dt;
            if (toastTimer > 0f) toastTimer -= dt;
            stateTimer += dt;

            switch (state)
            {
                case State.Title:
                    if (GInput.Down(GKey.Confirm)) StartRound();
                    break;
                case State.Intro:
                    cam.flyT = Mathf.Clamp01(stateTimer / 4.5f);
                    if (stateTimer > 5f || (stateTimer > 0.3f && GInput.Down(GKey.Confirm)))
                    {
                        bigTimer = Mathf.Min(bigTimer, 0.3f);
                        PrepareShot(false);
                        cam.Snap();
                    }
                    break;
                case State.Aim:
                    UpdateAim(dt);
                    break;
                case State.Swing:
                    UpdateSwing(dt);
                    break;
                case State.Flight:
                    UpdateFlight(dt);
                    break;
                case State.Settle:
                    if (stateTimer > 1.6f && afterSettle != null)
                    {
                        var a = afterSettle;
                        afterSettle = null;
                        a();
                    }
                    break;
                case State.HoleDone:
                    if (stateTimer > 4f || (stateTimer > 1f && GInput.Down(GKey.Confirm)))
                    {
                        state = State.Scorecard;
                        stateTimer = 0f;
                    }
                    break;
                case State.Scorecard:
                    if (stateTimer > 0.4f && GInput.Down(GKey.Confirm))
                    {
                        if (holeIndex + 1 < Holes.All.Length) BeginHole(holeIndex + 1);
                        else { state = State.Final; stateTimer = 0f; }
                    }
                    break;
                case State.Final:
                    if (stateTimer > 1f && (GInput.Down(GKey.Confirm) || GInput.Down(GKey.Restart))) StartRound();
                    break;
            }

            UpdateRing();
            grid.SetVisible((state == State.Aim || state == State.Swing) &&
                            Vector3.Distance(BallPos, course.GreenCenter) < Hole.greenRadius + 30f);
            if (state != State.Flight) ball.position = sim.pos;
        }

        // ------------------------------------------------------------------ Aiming

        void UpdateAim(float dt)
        {
            bool putt = CurrentClub.putter;
            bool fine = GInput.Held(GKey.Fine);
            float turn = putt ? (fine ? 3f : 22f) : (fine ? 5f : 38f);
            if (GInput.Held(GKey.AimLeft)) aimYaw -= turn * dt;
            if (GInput.Held(GKey.AimRight)) aimYaw += turn * dt;

            if (GInput.Down(GKey.ClubUp) && clubIndex > 0) { clubIndex--; sfx.Tick(); OnClubChanged(); }
            if (GInput.Down(GKey.ClubDown) && clubIndex < Clubs.All.Length - 1) { clubIndex++; sfx.Tick(); OnClubChanged(); }

            if (!CurrentClub.putter) UpdateSpinInput(dt);

            if (GInput.Down(GKey.PowerShot) && !CurrentClub.putter)
            {
                if (powerArmed) { powerArmed = false; sfx.Tick(); }
                else if (powerSlots > 0) { powerArmed = true; special = ShotType.Power; sfx.PowerUp(); }
                else Toast("Power gauge empty - hit a PANGYA to charge it");
            }
            if (GInput.Down(GKey.SpecialShot) && powerArmed)
            {
                special = special == ShotType.Power ? ShotType.Tomahawk
                        : special == ShotType.Tomahawk ? ShotType.Spike
                        : special == ShotType.Spike ? ShotType.Cobra : ShotType.Power;
                sfx.Tick();
            }

            if (GInput.Down(GKey.ViewLanding))
                cam.mode = cam.mode == GolfCamera.Mode.Landing ? GolfCamera.Mode.Aim : GolfCamera.Mode.Landing;
            if (GInput.Down(GKey.ViewGreen))
                cam.mode = cam.mode == GolfCamera.Mode.GreenTop ? GolfCamera.Mode.Aim : GolfCamera.Mode.GreenTop;
            if (GInput.Down(GKey.Help)) showHelp = !showHelp;

            UpdatePrediction();

            if (GInput.Down(GKey.Shot))
            {
                cam.mode = GolfCamera.Mode.Aim;
                gauge.Begin(CurrentClub.putter ? 0.75f : 0.9f);
                state = State.Swing;
                stateTimer = 0f;
                sfx.Tick();
            }
        }

        void OnClubChanged()
        {
            if (CurrentClub.putter) { powerArmed = false; impact = Vector2.zero; }
        }

        void UpdateSpinInput(float dt)
        {
            Vector2 dir = Vector2.zero;
            if (GInput.Held(GKey.SpinUp)) dir.y += 1f;
            if (GInput.Held(GKey.SpinDown)) dir.y -= 1f;
            if (GInput.Held(GKey.SpinLeft)) dir.x -= 1f;
            if (GInput.Held(GKey.SpinRight)) dir.x += 1f;
            bool pressed = GInput.Down(GKey.SpinUp) || GInput.Down(GKey.SpinDown) || GInput.Down(GKey.SpinLeft) || GInput.Down(GKey.SpinRight);
            if (dir != Vector2.zero)
            {
                spinRepeat -= dt;
                if (pressed) spinRepeat = 0.35f;
                if (pressed || spinRepeat <= 0f)
                {
                    if (!pressed) spinRepeat = 0.07f;
                    impact += dir * 0.1f;
                    if (impact.magnitude > 1f) impact = impact.normalized;
                    impact = new Vector2(Mathf.Round(impact.x * 10f) / 10f, Mathf.Round(impact.y * 10f) / 10f);
                }
            }
            if (GInput.Down(GKey.SpinReset)) impact = Vector2.zero;
        }

        ShotParams BuildShot(float power, float yaw, float extraSide, bool includeLie)
        {
            Club c = CurrentClub;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (c.putter)
                return new ShotParams { dir = dir, speed = c.SpeedFor(power), putt = true, type = ShotType.Normal };

            ShotType type = powerArmed ? special : ShotType.Normal;
            float frac = power * LieFactor * ShotMultiplier;
            float launch = c.launch;
            if (type == ShotType.Tomahawk) launch += 10f;
            else if (type == ShotType.Spike) launch -= 2f;
            else if (type == ShotType.Cobra) launch -= 6f;

            var sp = new ShotParams
            {
                dir = dir,
                speed = c.SpeedFor(frac),
                launchDeg = launch,
                back = c.spin - impact.y * 0.9f,
                liftSpin = c.spin - impact.y * 0.25f,
                side = impact.x * 0.6f + extraSide,
                type = type,
                expectedCarry = c.MaxMetres * frac,
            };

            if (includeLie)
            {
                // Uneven lies: ball above/below the feet curves, uphill/downhill changes the launch.
                Vector3 n = course.Normal(BallPos);
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                sp.side += Vector3.Dot(n, right) * 4f;
                sp.launchDeg = Mathf.Max(2f, sp.launchDeg - Vector3.Dot(n, dir) * 30f);
            }
            return sp;
        }

        void UpdatePrediction()
        {
            Club c = CurrentClub;
            if (c.putter)
            {
                Vector3 p = BallPos + AimDir * c.MaxMetres;
                p.y = course.GroundY(p);
                predicted = p;
                return;
            }
            var sp = BuildShot(1f, aimYaw, 0f, false);
            preview.Launch(BallPos, sp);
            for (int i = 0; i < 240 * 25 && !preview.landed; i++) preview.Step(BallSim.StepDt, Vector3.zero);
            predicted = preview.landed ? preview.firstLand : preview.pos;
        }

        void UpdateRing()
        {
            bool show = state == State.Aim || state == State.Swing;
            ring.enabled = show;
            if (!show) return;
            float r = (CurrentClub.putter ? 0.9f : 3f) * (1f + 0.06f * Mathf.Sin(Time.time * 4f));
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                Vector3 p = predicted + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                p.y = course.GroundY(p) + 0.12f;
                ring.SetPosition(i, p);
            }
            Color col = powerArmed ? new Color(1f, 0.4f, 0.2f, 0.95f) : new Color(1f, 0.92f, 0.25f, 0.95f);
            ring.startColor = ring.endColor = col;
        }

        // ------------------------------------------------------------------ Swing

        void UpdateSwing(float dt)
        {
            if (GInput.Down(GKey.Back)) { gauge.Reset(); state = State.Aim; return; }
            if (GInput.Down(GKey.Shot)) { gauge.Press(); sfx.Tick(); }
            gauge.Tick(dt);
            if (gauge.phase == ShotGauge.Phase.Cancelled) { gauge.Reset(); state = State.Aim; return; }
            if (gauge.phase == ShotGauge.Phase.Done) FireShot(gauge.power, gauge.accuracy);
        }

        void FireShot(float power, float accuracy)
        {
            Club c = CurrentClub;
            float zone = c.perfectZone * (powerArmed ? 0.6f : 1f);
            bool perfect = Mathf.Abs(accuracy) <= zone;
            float miss = Mathf.Max(0f, Mathf.Abs(accuracy) - zone);
            float sign = Mathf.Sign(accuracy); // + = early (pull/hook left), - = late (push/slice right)

            float yawErr = -sign * miss * (c.putter ? 35f : 100f);
            float sideErr = c.putter ? 0f : -sign * miss * 3f;
            float p = power * (1f - miss * 1.5f);

            var sp = BuildShot(p, aimYaw + yawErr, sideErr, true);
            shotStart = BallPos;
            shotFromGreen = lie == Surface.Green;
            sim.Launch(BallPos, sp);
            strokes++;
            simAcc = 0f;

            if (powerArmed) { powerSlots = Mathf.Max(0, powerSlots - 1); }

            if (c.putter)
            {
                sfx.Putt();
            }
            else if (perfect)
            {
                sfx.Perfect();
                fx.Sparkle(BallPos);
                Big("PANGYA!", powerArmed ? SpecialName(special) : "", new Color(0.5f, 1f, 1f), 1.6f);
                if (powerSlots < MaxPowerSlots) powerSlots++;
            }
            else
            {
                if (miss < 0.03f) { sfx.Hit(p); Toast("Nice shot"); }
                else { sfx.Miss(); Toast(sign > 0 ? "Too early - hook!" : "Too late - slice!"); }
            }
            if (lie == Surface.Bunker) fx.Sand(BallPos);
            else if (!c.putter) fx.Dust(BallPos);

            powerArmed = false;
            trail.Clear();
            trail.emitting = !c.putter;
            gauge.Reset();
            state = State.Flight;
            stateTimer = 0f;
            cam.mode = GolfCamera.Mode.Follow;
        }

        public static string SpecialName(ShotType t)
        {
            switch (t)
            {
                case ShotType.Tomahawk: return "TOMAHAWK";
                case ShotType.Spike: return "SPIKE";
                case ShotType.Cobra: return "COBRA";
                case ShotType.Power: return "POWER SHOT";
                default: return "NORMAL";
            }
        }

        // ------------------------------------------------------------------ Flight

        void UpdateFlight(float dt)
        {
            float speed = GInput.Held(GKey.Shot) && stateTimer > 0.4f ? 3f : 1f; // hold Space to fast-forward
            simAcc += dt * speed;
            int steps = 0;
            while (simAcc >= BallSim.StepDt && steps < 4000 && state == State.Flight)
            {
                simAcc -= BallSim.StepDt;
                steps++;
                Vector3 vBefore = sim.vel;
                var ev = sim.Step(BallSim.StepDt, wind);
                switch (ev)
                {
                    case SimEvent.Bounce:
                        sfx.Bounce(Mathf.Abs(vBefore.y));
                        if (sim.lastSurface == Surface.Bunker) fx.Sand(sim.pos);
                        else if (sim.bounces == 1) fx.Dust(sim.pos);
                        break;
                    case SimEvent.TreeHit: sfx.Bounce(8f); Toast("Hit a tree!"); break;
                    case SimEvent.PinHit: sfx.Bounce(6f); Toast("Hit the pin!"); break;
                    case SimEvent.LipOut: sfx.Bounce(4f); Toast("Lip out!"); break;
                    case SimEvent.Holed: OnHoled(); break;
                    case SimEvent.Water: OnPenalty(true); break;
                    case SimEvent.OutOfBounds: OnPenalty(false); break;
                    case SimEvent.Stopped: OnStopped(); break;
                }
            }
            ball.position = sim.pos;
            if (sim.mode == BallMode.Roll) trail.emitting = false;
        }

        void OnStopped()
        {
            Vector3 d = sim.pos - shotStart; d.y = 0f;
            lastShotYards = d.magnitude / Clubs.Yard;
            var s = course.SurfaceAt(sim.pos);
            Toast(Mathf.RoundToInt(lastShotYards) + "y  -  " + SurfaceName(s));
            Settle(() =>
            {
                if (strokes >= Hole.par + StrokeLimitOverPar) { GiveUp(); return; }
                PrepareShot();
            });
        }

        void OnPenalty(bool water)
        {
            strokes++;
            if (water) { fx.Water(sim.pos); sfx.Splash(); Big("WATER HAZARD", "+1 stroke penalty", new Color(0.4f, 0.75f, 1f), 2f); }
            else { sfx.Miss(); Big("OUT OF BOUNDS", "+1 stroke penalty", new Color(1f, 0.5f, 0.4f), 2f); }
            Vector3 drop = course.DropPoint(sim.pos);
            Settle(() =>
            {
                if (strokes >= Hole.par + StrokeLimitOverPar) { GiveUp(); return; }
                PlaceBall(drop);
                PrepareShot();
            });
        }

        void OnHoled()
        {
            ball.position = sim.pos;
            trail.emitting = false;
            sfx.Cup();
            scores[holeIndex] = strokes;
            int diff = strokes - Hole.par;
            string name = ScoreName(strokes, Hole.par);
            string sub = !shotFromGreen && strokes > 1 ? "CHIP-IN!" : "";
            Color col = diff < 0 ? new Color(1f, 0.85f, 0.2f) : diff == 0 ? new Color(0.6f, 1f, 0.6f) : new Color(0.85f, 0.85f, 1f);
            if (diff < 0 || strokes == 1)
            {
                fx.Confetti(course.Cup);
                sfx.Cheer();
                if (powerSlots < MaxPowerSlots) powerSlots++;
            }
            Big(name, sub, col, 4f);
            state = State.HoleDone;
            stateTimer = 0f;
            cam.mode = GolfCamera.Mode.Orbit;
        }

        void GiveUp()
        {
            scores[holeIndex] = Hole.par + StrokeLimitOverPar;
            Big("GIVE UP", "Stroke limit reached", new Color(0.8f, 0.8f, 0.8f), 3f);
            state = State.HoleDone;
            stateTimer = 0f;
            cam.mode = GolfCamera.Mode.Orbit;
        }

        void Settle(System.Action then)
        {
            afterSettle = then;
            state = State.Settle;
            stateTimer = 0f;
        }

        // ------------------------------------------------------------------ Helpers

        public void Big(string text, string sub, Color c, float time)
        {
            bigText = text; subText = sub; bigColor = c; bigTimer = time;
        }

        public void Toast(string text)
        {
            toast = text; toastTimer = 2.2f;
        }

        public static string ScoreName(int strokes, int par)
        {
            if (strokes == 1) return "HOLE IN ONE!!";
            switch (strokes - par)
            {
                case -4: return "CONDOR!!";
                case -3: return "ALBATROSS!!";
                case -2: return "EAGLE!";
                case -1: return "BIRDIE!";
                case 0: return "PAR";
                case 1: return "BOGEY";
                case 2: return "DOUBLE BOGEY";
                case 3: return "TRIPLE BOGEY";
                default: return "+" + (strokes - par);
            }
        }

        public static string SurfaceName(Surface s)
        {
            switch (s)
            {
                case Surface.Tee: return "Tee";
                case Surface.Fairway: return "Fairway";
                case Surface.Rough: return "Rough";
                case Surface.Green: return "Green";
                case Surface.Bunker: return "Bunker";
                case Surface.Water: return "Water";
                default: return "OB";
            }
        }
    }
}
