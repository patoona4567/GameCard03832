using UnityEngine;

namespace PangGolf
{
    /// <summary>Immediate-mode HUD: shot gauge, wind, minimap, spin, club and scorecard.</summary>
    public class GolfHUD : MonoBehaviour
    {
        GolfGame g;
        bool ready;
        float W, H;

        Texture2D white, panel, panelLight, fill, ballTex, dotTex, arrowTex, ringTex, vignette;
        GUIStyle txt, txtSmall, txtBig, txtHuge, txtCenter, txtClub;

        public void Init(GolfGame game) { g = game; }

        void Setup()
        {
            white = ProcGen.Solid(Color.white);
            panel = ProcGen.Solid(new Color(0.04f, 0.07f, 0.16f, 0.62f));
            panelLight = ProcGen.Solid(new Color(1f, 1f, 1f, 0.12f));
            fill = ProcGen.HorizontalGradient(new Color(1f, 0.9f, 0.25f), new Color(1f, 0.25f, 0.15f));
            ballTex = ProcGen.Circle(128, new Color(0.97f, 0.97f, 0.98f), new Color(0.25f, 0.3f, 0.45f), 6f);
            dotTex = ProcGen.Circle(32, new Color(1f, 0.25f, 0.2f), new Color(0.45f, 0f, 0f), 4f);
            ringTex = ProcGen.Circle(128, new Color(0.04f, 0.07f, 0.16f, 0.62f), new Color(0.6f, 0.85f, 1f, 0.9f), 4f);
            arrowTex = ProcGen.Arrow(64, new Color(0.45f, 0.92f, 1f));
            vignette = ProcGen.VerticalGradient(new Color(0.02f, 0.05f, 0.15f, 0.2f), new Color(0.02f, 0.05f, 0.15f, 0.85f));

            txt = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false };
            txtSmall = new GUIStyle(txt) { fontSize = 14, fontStyle = FontStyle.Normal };
            txtBig = new GUIStyle(txt) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            txtHuge = new GUIStyle(txt) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
            txtCenter = new GUIStyle(txt) { alignment = TextAnchor.MiddleCenter };
            txtClub = new GUIStyle(txt) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (g == null || g.course == null) return;
            if (!ready) { Setup(); ready = true; }

            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            W = Screen.width / s;
            H = 720f;

            switch (g.state)
            {
                case GolfGame.State.Title: DrawTitle(); return;
                case GolfGame.State.Scorecard: DrawScorecard(false); break;
                case GolfGame.State.Final: DrawScorecard(true); break;
                default: DrawPlay(); break;
            }
            DrawMessages();
        }

        // ------------------------------------------------------------------ Screens

        void DrawTitle()
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), vignette);
            float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 3f);
            var big = new GUIStyle(txtHuge) { fontSize = Mathf.RoundToInt(96 * pulse) };
            Text(new Rect(0, 150, W, 120), "PANG GOLF", big, new Color(1f, 0.88f, 0.25f));
            Text(new Rect(0, 255, W, 40), "Fantasy golf  -  " + Holes.All.Length + " holes", txtBig, new Color(0.8f, 0.95f, 1f));
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
            Text(new Rect(0, 360, W, 40), "Press  ENTER  or  SPACE  to tee off", txtBig, new Color(1f, 1f, 1f, blink));

            string help =
                "SPACE   start swing  >  set power  >  hit the impact zone\n" +
                "LEFT / RIGHT  aim  (SHIFT = fine)        UP / DOWN  change club\n" +
                "W A S D  impact point: topspin / backspin / curve       X  reset\n" +
                "E  power shot      Q  Tomahawk / Spike / Cobra      V  landing view      C  green view";
            Text(new Rect(0, 450, W, 120), help, new GUIStyle(txtSmall) { alignment = TextAnchor.UpperCenter, fontSize = 16 }, new Color(0.9f, 0.95f, 1f));
        }

        void DrawPlay()
        {
            bool aiming = g.state == GolfGame.State.Aim || g.state == GolfGame.State.Swing;
            DrawHoleInfo();
            if (g.state == GolfGame.State.Intro) return;
            DrawWind();
            DrawMinimap();
            DrawClubPanel();
            if (aiming)
            {
                DrawImpactWidget();
                DrawGauge();
                DrawPinInfo();
                if (g.showHelp) DrawHelp();
            }
            else if (g.state == GolfGame.State.Flight)
            {
                Vector3 d = g.BallPos - g.course.Cup; d.y = 0f;
                Text(new Rect(0, H - 60, W, 30), "Pin " + Yards(d.magnitude) + "      (hold SPACE to speed up)", txtCenter, new Color(1f, 1f, 1f, 0.85f));
            }
        }

        void DrawHoleInfo()
        {
            Box(new Rect(12, 12, 290, 92));
            Text(new Rect(26, 16, 270, 30), "HOLE " + (g.holeIndex + 1) + "  /  PAR " + g.Hole.par, txt, new Color(1f, 0.9f, 0.4f));
            Text(new Rect(26, 42, 270, 24), g.Hole.name + "   " + Mathf.RoundToInt(g.course.LengthYards) + "y", txtSmall, Color.white);
            int done = g.TotalStrokes, par = g.TotalPar;
            string tot = par == 0 ? "E" : ToPar(done - par);
            Text(new Rect(26, 66, 270, 30), "Stroke " + (g.strokes + (g.state == GolfGame.State.Aim || g.state == GolfGame.State.Swing ? 1 : 0)) + "     Total " + tot, txt, Color.white);
        }

        void DrawWind()
        {
            Rect r = new Rect(W - 172, 12, 160, 120);
            Box(r);
            Rect c = new Rect(r.x + 10, r.y + 10, 100, 100);
            GUI.DrawTexture(c, ringTex);
            float spd = g.WindSpeed;
            if (spd > 0.01f)
            {
                Vector3 camF = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                camF.y = 0f;
                float ang = Vector3.SignedAngle(camF.normalized, g.wind / spd, Vector3.up);
                Matrix4x4 m = GUI.matrix;
                Rotate(ang, c.center);
                float sz = 40f + spd * 4f;
                GUI.DrawTexture(new Rect(c.center.x - sz * 0.5f, c.center.y - sz * 0.5f, sz, sz), arrowTex);
                GUI.matrix = m;
            }
            Text(new Rect(r.x + 112, r.y + 30, 44, 40), Mathf.RoundToInt(spd) + "m", new GUIStyle(txtBig) { fontSize = 24 }, WindColor(spd));
            Text(new Rect(r.x + 112, r.y + 64, 44, 24), "WIND", new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleCenter }, Color.white);
        }

        static Color WindColor(float s)
        {
            if (s < 3f) return new Color(0.6f, 1f, 0.6f);
            if (s < 6f) return new Color(1f, 0.95f, 0.4f);
            return new Color(1f, 0.45f, 0.35f);
        }

        void DrawMinimap()
        {
            var tex = g.course.minimap;
            if (tex == null) return;
            Rect r = new Rect(W - 172, 140, 160, 160);
            Box(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8));
            GUI.DrawTexture(r, tex);

            Vector2 bp = MapPoint(r, g.BallPos);
            Vector2 pp = MapPoint(r, g.course.Cup);
            if (g.state == GolfGame.State.Aim || g.state == GolfGame.State.Swing)
            {
                Vector2 tp = MapPoint(r, g.predicted);
                Line(bp, tp, 2f, new Color(1f, 0.9f, 0.3f, 0.9f));
                GUI.color = new Color(1f, 0.9f, 0.3f);
                GUI.DrawTexture(new Rect(tp.x - 4, tp.y - 4, 8, 8), ringTex);
                GUI.color = Color.white;
            }
            GUI.color = new Color(1f, 0.25f, 0.25f);
            GUI.DrawTexture(new Rect(pp.x - 2, pp.y - 9, 3, 10), white);
            GUI.DrawTexture(new Rect(pp.x + 1, pp.y - 9, 6, 4), white);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bp.x - 4, bp.y - 4, 8, 8), ballTex);
        }

        Vector2 MapPoint(Rect r, Vector3 w)
        {
            Vector2 uv = g.course.WorldToMap(w);
            uv.x = Mathf.Clamp01(uv.x); uv.y = Mathf.Clamp01(uv.y);
            return new Vector2(r.x + uv.x * r.width, r.y + (1f - uv.y) * r.height);
        }

        void DrawClubPanel()
        {
            Rect r = new Rect(12, H - 172, 230, 160);
            Box(r);
            var c = g.CurrentClub;
            Text(new Rect(r.x, r.y + 6, r.width, 50), c.name, txtClub, Color.white);
            Text(new Rect(r.x, r.y + 52, r.width, 24), "Max " + Mathf.RoundToInt(c.maxYards) + "y", txtCenter, new Color(0.8f, 0.92f, 1f));

            string lieText = GolfGame.SurfaceName(g.lie) + "  " + Mathf.RoundToInt(g.LieFactor * 100f) + "%";
            Color lieCol = g.LieFactor >= 0.999f ? new Color(0.6f, 1f, 0.6f) : g.LieFactor > 0.8f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.5f, 0.4f);
            Text(new Rect(r.x, r.y + 78, r.width, 22), lieText, new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }, lieCol);

            string shot = g.powerArmed ? GolfGame.SpecialName(g.special) : "NORMAL";
            Text(new Rect(r.x, r.y + 100, r.width, 24), shot, txtCenter, g.powerArmed ? new Color(1f, 0.55f, 0.25f) : new Color(0.85f, 0.85f, 0.85f));

            // Power gauge slots
            float sw = 52f, total = GolfGame.MaxPowerSlots * sw + (GolfGame.MaxPowerSlots - 1) * 6f;
            float x0 = r.x + (r.width - total) * 0.5f;
            for (int i = 0; i < GolfGame.MaxPowerSlots; i++)
            {
                Rect sr = new Rect(x0 + i * (sw + 6f), r.y + 130, sw, 14);
                GUI.DrawTexture(sr, panelLight);
                if (i < g.powerSlots)
                {
                    GUI.color = new Color(1f, 0.6f + 0.2f * Mathf.Sin(Time.time * 4f + i), 0.2f);
                    GUI.DrawTexture(new Rect(sr.x + 2, sr.y + 2, sr.width - 4, sr.height - 4), white);
                    GUI.color = Color.white;
                }
            }
        }

        void DrawImpactWidget()
        {
            if (g.CurrentClub.putter) return;
            Rect r = new Rect(W - 172, H - 172, 160, 160);
            Box(r);
            Rect b = new Rect(r.x + 25, r.y + 14, 110, 110);
            GUI.DrawTexture(b, ballTex);
            GUI.color = new Color(0.3f, 0.35f, 0.5f, 0.5f);
            GUI.DrawTexture(new Rect(b.center.x - 0.5f, b.y + 6, 1, b.height - 12), white);
            GUI.DrawTexture(new Rect(b.x + 6, b.center.y - 0.5f, b.width - 12, 1), white);
            GUI.color = Color.white;
            Vector2 p = new Vector2(b.center.x + g.impact.x * 44f, b.center.y - g.impact.y * 44f);
            GUI.DrawTexture(new Rect(p.x - 8, p.y - 8, 16, 16), dotTex);

            string spin = g.impact.y > 0.05f ? "Top " + Mathf.RoundToInt(g.impact.y * 10) : g.impact.y < -0.05f ? "Back " + Mathf.RoundToInt(-g.impact.y * 10) : "Spin 0";
            string curve = g.impact.x > 0.05f ? "R " + Mathf.RoundToInt(g.impact.x * 10) : g.impact.x < -0.05f ? "L " + Mathf.RoundToInt(-g.impact.x * 10) : "C 0";
            Text(new Rect(r.x, r.y + 128, r.width, 24), spin + "   " + curve, new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleCenter }, Color.white);
        }

        void DrawPinInfo()
        {
            float h = g.HeightToPin;
            string hs = (h >= 0 ? "up " : "down ") + Mathf.Abs(h).ToString("0.0") + "m";
            Rect r = new Rect(W * 0.5f - 170, 12, 340, 56);
            Box(r);
            Text(new Rect(r.x, r.y + 2, r.width, 30), "PIN  " + Yards(g.DistanceToPin) + "   " + hs, txtCenter, Color.white);
            Vector3 d = g.predicted - g.BallPos; d.y = 0f;
            Text(new Rect(r.x, r.y + 28, r.width, 24), "Max landing " + Yards(d.magnitude) + " (no wind)", new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleCenter }, new Color(1f, 0.92f, 0.4f));
        }

        void DrawGauge()
        {
            var ga = g.gauge;
            var club = g.CurrentClub;
            float gw = Mathf.Min(660f, W - 520f), gh = 30f;
            float gx = (W - gw) * 0.5f, gy = H - 74f;
            float lo = ShotGauge.MissLimit;
            System.Func<float, float> X = v => gx + (v - lo) / (1f - lo) * gw;

            Box(new Rect(gx - 12, gy - 40, gw + 24, gh + 60));
            GUI.DrawTexture(new Rect(gx, gy, gw, gh), panelLight);

            // Impact zones
            float zone = club.perfectZone * (g.powerArmed ? 0.6f : 1f);
            GUI.color = new Color(0.4f, 1f, 0.5f, 0.35f);
            GUI.DrawTexture(new Rect(X(-0.04f), gy, X(0.04f) - X(-0.04f), gh), white);
            GUI.color = new Color(0.3f, 1f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(X(-zone), gy - 3, Mathf.Max(3f, X(zone) - X(-zone)), gh + 6), white);
            GUI.color = Color.white;

            // Power fill
            float shown = ga.phase == ShotGauge.Phase.Power ? ga.marker : ga.phase == ShotGauge.Phase.Impact ? ga.power : 0f;
            if (shown > 0f)
                GUI.DrawTextureWithTexCoords(new Rect(X(0f), gy + 4, X(shown) - X(0f), gh - 8), fill, new Rect(0, 0, shown, 1));
            if (ga.phase == ShotGauge.Phase.Impact)
            {
                GUI.color = new Color(1f, 0.2f, 0.2f);
                GUI.DrawTexture(new Rect(X(ga.power) - 2, gy - 6, 4, gh + 12), white);
                GUI.color = Color.white;
            }

            // Ticks
            for (int i = 0; i <= 10; i++)
            {
                float v = i / 10f;
                float th = i % 5 == 0 ? gh : gh * 0.4f;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.DrawTexture(new Rect(X(v) - 0.5f, gy + gh - th, 1, th), white);
                GUI.color = Color.white;
                if (i % 5 == 0 && i > 0)
                    Text(new Rect(X(v) - 20, gy + gh, 40, 18), (i * 10).ToString(), new GUIStyle(txtSmall) { alignment = TextAnchor.UpperCenter, fontSize = 12 }, Color.white);
            }

            // Marker
            if (ga.Active)
            {
                float mx = X(ga.marker);
                GUI.DrawTexture(new Rect(mx - 3, gy - 8, 6, gh + 16), white);
                GUI.color = new Color(0.1f, 0.1f, 0.2f);
                GUI.DrawTexture(new Rect(mx - 1, gy - 6, 2, gh + 12), white);
                GUI.color = Color.white;
            }

            // Label
            string label;
            float p = ga.phase == ShotGauge.Phase.Power ? ga.marker : ga.phase == ShotGauge.Phase.Impact ? ga.power : 1f;
            float yards = club.maxYards * p * (club.putter ? 1f : g.LieFactor * g.ShotMultiplier);
            if (g.state == GolfGame.State.Aim)
                label = "SPACE to swing   -   " + club.name + " full power " + Mathf.RoundToInt(yards) + "y";
            else if (ga.phase == ShotGauge.Phase.Power)
                label = "POWER " + Mathf.RoundToInt(p * 100f) + "%   " + (club.putter ? yards.ToString("0.0") : Mathf.RoundToInt(yards).ToString()) + "y";
            else
                label = "IMPACT!   power " + Mathf.RoundToInt(p * 100f) + "%";
            Text(new Rect(gx, gy - 36, gw, 30), label, txtCenter, new Color(1f, 0.95f, 0.7f));
        }

        void DrawHelp()
        {
            Rect r = new Rect(12, 116, 250, 238);
            Box(r);
            string h =
                "CONTROLS  (H hide)\n" +
                "SPACE  swing / power / impact\n" +
                "ESC  cancel swing\n" +
                "LEFT RIGHT  aim (SHIFT fine)\n" +
                "UP DOWN  club\n" +
                "W / S  topspin / backspin\n" +
                "A / D  curve left / right\n" +
                "X  reset spin\n" +
                "E  power shot (uses gauge)\n" +
                "Q  Tomahawk / Spike / Cobra\n" +
                "V  landing view   C  green view";
            Text(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12), h, new GUIStyle(txtSmall) { fontSize = 13, wordWrap = true }, new Color(0.9f, 0.95f, 1f));
        }

        void DrawMessages()
        {
            if (g.bigTimer > 0f && !string.IsNullOrEmpty(g.bigText))
            {
                float age = Mathf.Clamp01((4f - g.bigTimer) * 4f);
                float pop = Mathf.Lerp(1.6f, 1f, Mathf.SmoothStep(0, 1, age));
                float alpha = Mathf.Clamp01(g.bigTimer * 2f);
                var st = new GUIStyle(txtHuge) { fontSize = Mathf.RoundToInt(70 * pop) };
                Color c = g.bigColor; c.a = alpha;
                Text(new Rect(0, H * 0.3f - 60, W, 120), g.bigText, st, c);
                if (!string.IsNullOrEmpty(g.subText))
                    Text(new Rect(0, H * 0.3f + 45, W, 40), g.subText, txtBig, new Color(1f, 1f, 1f, alpha));
            }
            if (g.toastTimer > 0f && !string.IsNullOrEmpty(g.toast))
            {
                float a = Mathf.Clamp01(g.toastTimer * 2f);
                Rect r = new Rect(W * 0.5f - 180, H * 0.55f, 360, 36);
                GUI.color = new Color(1, 1, 1, a);
                Box(r);
                GUI.color = Color.white;
                Text(r, g.toast, txtCenter, new Color(1f, 1f, 1f, a));
            }
        }

        void DrawScorecard(bool final)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), vignette);
            int n = Holes.All.Length;
            float cw = 90f, labelW = 120f;
            float tw = labelW + cw * (n + 1);
            float x0 = (W - tw) * 0.5f, y0 = 170f;
            Box(new Rect(x0 - 20, y0 - 80, tw + 40, 290));
            Text(new Rect(x0, y0 - 70, tw, 50), final ? "ROUND COMPLETE" : "SCORECARD", txtBig, new Color(1f, 0.9f, 0.4f));

            string[] rows = { "HOLE", "PAR", "SCORE" };
            for (int r = 0; r < 3; r++)
            {
                float y = y0 + r * 44f;
                Text(new Rect(x0, y, labelW, 40), rows[r], txt, new Color(0.8f, 0.9f, 1f));
                int totPar = 0, totScore = 0;
                for (int i = 0; i < n; i++)
                {
                    var h = Holes.All[i];
                    int sc = g.scores[i];
                    totPar += h.par; totScore += sc;
                    string cell = r == 0 ? (i + 1).ToString() : r == 1 ? h.par.ToString() : sc > 0 ? sc.ToString() : "-";
                    Color col = Color.white;
                    if (r == 2 && sc > 0) col = sc < h.par ? new Color(1f, 0.85f, 0.3f) : sc == h.par ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.55f);
                    Text(new Rect(x0 + labelW + i * cw, y, cw, 40), cell, txtCenter, col);
                }
                string tot = r == 0 ? "TOTAL" : r == 1 ? totPar.ToString() : g.TotalStrokes.ToString();
                Text(new Rect(x0 + labelW + n * cw, y, cw, 40), tot, txtCenter, new Color(1f, 0.95f, 0.7f));
            }

            int diff = g.TotalStrokes - g.TotalPar;
            Text(new Rect(x0, y0 + 140, tw, 40), "To par: " + (g.TotalPar == 0 ? "E" : ToPar(diff)), txtBig, diff < 0 ? new Color(1f, 0.85f, 0.3f) : Color.white);
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
            Text(new Rect(0, y0 + 230, W, 40), final ? "Press ENTER to play again" : "Press ENTER for the next hole", txtBig, new Color(1f, 1f, 1f, blink));
        }

        // ------------------------------------------------------------------ Helpers

        static string Yards(float metres) { return Mathf.RoundToInt(metres / Clubs.Yard) + "y"; }

        static string ToPar(int d) { return d == 0 ? "E" : d > 0 ? "+" + d : d.ToString(); }

        void Box(Rect r) { GUI.DrawTexture(r, panel); }

        // RotateAroundPivot works in screen space, so convert from our scaled GUI space first.
        static void Rotate(float angle, Vector2 pivot)
        {
            Vector3 sp = GUI.matrix.MultiplyPoint3x4(new Vector3(pivot.x, pivot.y, 0f));
            GUIUtility.RotateAroundPivot(angle, new Vector2(sp.x, sp.y));
        }

        void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            Rotate(ang, a);
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), white);
            GUI.color = Color.white;
            GUI.matrix = m;
        }

        static void Text(Rect r, string t, GUIStyle st, Color c)
        {
            Color old = st.normal.textColor;
            st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.75f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, st);
            st.normal.textColor = c;
            GUI.Label(r, t, st);
            st.normal.textColor = old;
        }
    }
}
