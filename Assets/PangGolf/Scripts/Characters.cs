using UnityEngine;

namespace PangGolf
{
    public enum HairStyle { Short, Spiky, TwinTails, Long, Bald }
    public enum Accessory { None, Cap, Bow, Headband, Hat, FlatCap }

    /// <summary>A playable golfer: looks plus stats (1-5) that change how shots behave.</summary>
    public class CharacterDef
    {
        public string name, title, description;
        public Color skin, hair, top, bottom, shoes, accent;
        public HairStyle hairStyle;
        public Accessory accessory;
        public bool beard;
        public float scale = 1f, bulk = 1f;
        public int power = 3, control = 3, accuracy = 3, spin = 3, curve = 3;

        /// <summary>Distance multiplier for every club except the putter (+/-4% per point).</summary>
        public float PowerMul { get { return 1f + (power - 3) * 0.04f; } }
        /// <summary>Width of the PANGYA zone.</summary>
        public float ControlMul { get { return 0.7f + (control - 1) * 0.15f; } }
        /// <summary>How hard a missed impact punishes direction and power.</summary>
        public float MissMul { get { return 1.3f - (accuracy - 1) * 0.15f; } }
        /// <summary>Gauge speed - steadier players get a slower marker.</summary>
        public float GaugeSpeedMul { get { return 1.08f - (accuracy - 1) * 0.04f; } }
        public float SpinMul { get { return 0.7f + (spin - 1) * 0.15f; } }
        public float CurveMul { get { return 0.7f + (curve - 1) * 0.15f; } }
    }

    public static class Characters
    {
        public static readonly CharacterDef[] All =
        {
            new CharacterDef
            {
                name = "Kai", title = "The All-Rounder",
                description = "Cheerful rookie with no weak spots.\nGreat for learning every shot.",
                skin = new Color(1f, 0.82f, 0.68f), hair = new Color(0.35f, 0.2f, 0.1f),
                top = new Color(0.9f, 0.2f, 0.18f), bottom = new Color(0.95f, 0.95f, 0.92f),
                shoes = new Color(0.2f, 0.25f, 0.35f), accent = new Color(1f, 0.85f, 0.2f),
                hairStyle = HairStyle.Spiky, accessory = Accessory.None,
                power = 3, control = 3, accuracy = 3, spin = 3, curve = 3,
            },
            new CharacterDef
            {
                name = "Momo", title = "Spin Princess",
                description = "Makes the ball dance on the green.\nHuge backspin, wide PANGYA zone.",
                skin = new Color(1f, 0.86f, 0.76f), hair = new Color(1f, 0.55f, 0.72f),
                top = new Color(1f, 0.78f, 0.86f), bottom = new Color(0.98f, 0.98f, 1f),
                shoes = new Color(0.95f, 0.4f, 0.55f), accent = new Color(1f, 0.3f, 0.5f),
                hairStyle = HairStyle.TwinTails, accessory = Accessory.Bow,
                scale = 0.94f,
                power = 2, control = 4, accuracy = 3, spin = 5, curve = 3,
            },
            new CharacterDef
            {
                name = "Brock", title = "Power Hitter",
                description = "Bombs it past everyone off the tee.\nBut the PANGYA zone is tight.",
                skin = new Color(0.82f, 0.6f, 0.45f), hair = new Color(0.08f, 0.08f, 0.1f),
                top = new Color(0.15f, 0.4f, 0.9f), bottom = new Color(0.2f, 0.22f, 0.28f),
                shoes = new Color(0.95f, 0.95f, 0.95f), accent = new Color(1f, 0.3f, 0.2f),
                hairStyle = HairStyle.Short, accessory = Accessory.Headband,
                scale = 1.12f, bulk = 1.3f,
                power = 5, control = 2, accuracy = 2, spin = 3, curve = 2,
            },
            new CharacterDef
            {
                name = "Luna", title = "Curve Master",
                description = "Bends the ball around anything.\nDoglegs are her playground.",
                skin = new Color(1f, 0.88f, 0.8f), hair = new Color(0.85f, 0.88f, 0.98f),
                top = new Color(0.5f, 0.3f, 0.85f), bottom = new Color(0.25f, 0.18f, 0.45f),
                shoes = new Color(0.15f, 0.12f, 0.25f), accent = new Color(0.95f, 0.8f, 0.3f),
                hairStyle = HairStyle.Long, accessory = Accessory.Hat,
                power = 3, control = 3, accuracy = 3, spin = 2, curve = 5,
            },
            new CharacterDef
            {
                name = "Sensei Hiro", title = "Precision Pro",
                description = "Forty years on the tour.\nSteady gauge, rarely misses.",
                skin = new Color(0.95f, 0.78f, 0.64f), hair = new Color(0.82f, 0.82f, 0.8f),
                top = new Color(0.2f, 0.55f, 0.35f), bottom = new Color(0.85f, 0.78f, 0.6f),
                shoes = new Color(0.4f, 0.25f, 0.15f), accent = new Color(0.9f, 0.9f, 0.85f),
                hairStyle = HairStyle.Bald, accessory = Accessory.FlatCap, beard = true,
                scale = 0.98f,
                power = 2, control = 5, accuracy = 5, spin = 3, curve = 2,
            },
        };
    }
}
