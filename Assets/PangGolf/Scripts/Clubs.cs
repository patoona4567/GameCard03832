using UnityEngine;

namespace PangGolf
{
    public class Club
    {
        public readonly string name;
        public readonly float maxYards;
        public readonly float launch;   // degrees
        public readonly float spin;     // base backspin
        public readonly bool putter;
        public readonly float perfectZone; // half width of the PANGYA zone (gauge units)

        float baseSpeed;
        float[] carryTable; // carry fraction for speed fraction i*Step

        const float Step = 0.05f;
        const int Steps = 30; // up to 150% speed

        public Club(string name, float maxYards, float launch, float spin, float perfectZone, bool putter = false)
        {
            this.name = name; this.maxYards = maxYards; this.launch = launch; this.spin = spin;
            this.perfectZone = perfectZone; this.putter = putter;
        }

        public float MaxMetres { get { return maxYards * Clubs.Yard; } }

        /// <summary>Launch speed (m/s) that carries <paramref name="frac"/> of the club's max distance on flat calm ground.</summary>
        public float SpeedFor(float frac)
        {
            if (putter) return Mathf.Sqrt(2f * SurfaceProps.For(Surface.Green).roll * BallSim.G * MaxMetres * Mathf.Max(0f, frac));
            if (carryTable == null) Calibrate();
            frac = Mathf.Max(0f, frac);
            for (int i = 1; i < carryTable.Length; i++)
            {
                if (carryTable[i] >= frac)
                {
                    float t = Mathf.InverseLerp(carryTable[i - 1], carryTable[i], frac);
                    return baseSpeed * Mathf.Lerp((i - 1) * Step, i * Step, t);
                }
            }
            return baseSpeed * Steps * Step;
        }

        void Calibrate()
        {
            var sim = new BallSim(new FlatGround());
            float target = MaxMetres;
            float lo = 5f, hi = 160f;
            for (int i = 0; i < 32; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Carry(sim, mid) < target) lo = mid; else hi = mid;
            }
            baseSpeed = lo;
            carryTable = new float[Steps + 1];
            for (int i = 0; i <= Steps; i++)
                carryTable[i] = i == 0 ? 0f : Carry(sim, baseSpeed * i * Step) / target;
        }

        float Carry(BallSim sim, float speed)
        {
            sim.Launch(new Vector3(0f, BallSim.Radius, 0f), new ShotParams
            {
                dir = Vector3.forward, speed = speed, launchDeg = launch, back = spin, liftSpin = spin, type = ShotType.Normal
            });
            for (int i = 0; i < 240 * 20; i++)
            {
                sim.Step(BallSim.StepDt, Vector3.zero);
                if (sim.landed) break;
            }
            return sim.firstLand.z;
        }
    }

    public static class Clubs
    {
        public const float Yard = 0.9144f;

        public static readonly Club[] All =
        {
            new Club("1W", 230, 13.5f, 0.35f, 0.010f),
            new Club("2W", 210, 14.0f, 0.40f, 0.011f),
            new Club("3W", 195, 14.5f, 0.45f, 0.012f),
            new Club("2I", 180, 16.0f, 0.55f, 0.013f),
            new Club("3I", 170, 17.5f, 0.60f, 0.013f),
            new Club("4I", 160, 19.0f, 0.65f, 0.014f),
            new Club("5I", 150, 21.0f, 0.70f, 0.014f),
            new Club("6I", 140, 23.0f, 0.78f, 0.015f),
            new Club("7I", 130, 25.5f, 0.85f, 0.015f),
            new Club("8I", 120, 28.5f, 0.92f, 0.016f),
            new Club("9I", 110, 31.5f, 1.00f, 0.017f),
            new Club("PW", 95, 36.0f, 1.10f, 0.018f),
            new Club("SW", 75, 42.0f, 1.20f, 0.020f),
            new Club("PT", 30, 0f, 0f, 0.030f, true),
        };

        public static int Putter { get { return All.Length - 1; } }
        public static int SandWedge { get { return All.Length - 2; } }

        /// <summary>Smallest club that can reach the given distance.</summary>
        public static int BestFor(float metres)
        {
            for (int i = SandWedge; i >= 0; i--)
                if (All[i].MaxMetres >= metres) return i;
            return 0;
        }
    }
}
