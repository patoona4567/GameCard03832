using UnityEngine;

namespace PangGolf
{
    /// <summary>
    /// Pangya style 3-click gauge:
    /// 1st press starts the marker, 2nd press sets power, 3rd press sets impact
    /// accuracy as the marker sweeps back through the impact zone at 0.
    /// </summary>
    public class ShotGauge
    {
        public enum Phase { Idle, Power, Impact, Done, Cancelled }

        public const float MissLimit = -0.16f;

        public Phase phase = Phase.Idle;
        public float marker;      // MissLimit .. 1
        public float power;       // 0 .. 1
        public float accuracy;    // signed distance from 0 at impact (+ = early)
        public float speed = 0.85f;
        int dir = 1;

        public bool Active { get { return phase == Phase.Power || phase == Phase.Impact; } }

        public void Begin(float gaugeSpeed)
        {
            speed = gaugeSpeed;
            phase = Phase.Power;
            marker = 0f;
            power = 0f;
            accuracy = 0f;
            dir = 1;
        }

        public void Reset() { phase = Phase.Idle; marker = 0f; power = 0f; }

        public void Tick(float dt)
        {
            if (phase == Phase.Power)
            {
                marker += dir * speed * dt;
                if (marker >= 1f) { marker = 1f; dir = -1; }
                if (dir < 0 && marker <= 0f) { marker = 0f; phase = Phase.Cancelled; }
            }
            else if (phase == Phase.Impact)
            {
                marker -= speed * 1.3f * dt;
                if (marker <= MissLimit) { marker = MissLimit; accuracy = MissLimit; phase = Phase.Done; }
            }
        }

        public void Press()
        {
            if (phase == Phase.Power)
            {
                power = Mathf.Max(0.02f, marker);
                phase = Phase.Impact;
            }
            else if (phase == Phase.Impact)
            {
                accuracy = marker;
                phase = Phase.Done;
            }
        }
    }
}
