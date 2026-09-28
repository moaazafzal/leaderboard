using System;
using UnityEngine;

namespace AquaGames.League
{
    public enum LeagueMode
    {
        Simulated,   // all 29 opponents are generated on the device, no server, works offline
        Online,      // real players only, from an online backend (e.g. Firebase)
        Hybrid,      // real players first; empty seats in the group are filled with simulated players
    }

    public enum LeagueOrientation { Auto, Portrait, Landscape }

    // Everything tunable about the league. Start from LeagueConfig.Default() and change what you need.
    [Serializable]
    public class LeagueConfig
    {
        [Header("Mode")]
        [Tooltip("Simulated / Online / Hybrid. Switch at any time; the player's tier and XP carry over.")]
        public LeagueMode mode = LeagueMode.Simulated;
        [Tooltip("Registered backend id used by Online and Hybrid (\"firebase\" ships with the module).")]
        public string onlineBackend = "firebase";
        [Tooltip("If the online backend is missing or fails to connect, keep working in Simulated mode.")]
        public bool fallbackToSimulated = true;

        [Header("League structure")]
        [Tooltip("Tier names, lowest first (Duolingo has 10).")]
        public string[] tiers = { "Bronze", "Silver", "Gold", "Sapphire", "Ruby", "Emerald", "Amethyst", "Pearl", "Obsidian", "Diamond" };
        [Tooltip("Badge colour per tier (same length as tiers).")]
        public Color[] tierColors =
        {
            new Color(0.80f, 0.50f, 0.25f), new Color(0.72f, 0.76f, 0.82f), new Color(1.00f, 0.78f, 0.15f),
            new Color(0.25f, 0.50f, 1.00f), new Color(0.95f, 0.20f, 0.35f), new Color(0.20f, 0.80f, 0.45f),
            new Color(0.62f, 0.35f, 0.95f), new Color(0.95f, 0.88f, 0.95f), new Color(0.25f, 0.20f, 0.35f),
            new Color(0.45f, 0.90f, 1.00f),
        };
        public int groupSize = 30;              // players per group, including the real player
        public int promoteCount = 7;            // top N move up (none from the top tier)
        public int demoteCount = 5;             // bottom N move down (none from the bottom tier)
        public int weekLengthDays = 7;
        public DayOfWeek weekStartsOn = DayOfWeek.Monday;   // UTC

        [Header("Simulated players")]
        [Tooltip("Typical XP a simulated player earns per active day in the lowest tier.")]
        public float baseXpPerDay = 60f;
        [Tooltip("Each tier up multiplies simulated activity by this.")]
        public float tierXpGrowth = 1.22f;
        [Tooltip("0..1: how much simulated players scale towards the player's own recent pace (keeps races close).")]
        public float rubberBand = 0.55f;
        [Tooltip("Share of simulated players that barely play (real groups always have a few).")]
        public float inactiveShare = 0.12f;
        [Tooltip("XP values are rounded to this step (e.g. 5 or 10 like lesson XP).")]
        public int xpStep = 5;

        [Header("Online")]
        [Tooltip("Prefix for all database collections, so several games can share one Firebase project.")]
        public string collectionPrefix = "league";
        [Tooltip("Seconds between standings refreshes while the board is open (each refresh = ~30 document reads).")]
        public float refreshSeconds = 60f;
        [Tooltip("XP is uploaded at most this often (plus on board open and app pause) to save writes.")]
        public float uploadDebounceSeconds = 20f;
        [Tooltip("Security rules reject a single upload that adds more XP than this (anti-cheat). Keep in sync with firestore.rules.")]
        public int maxXpPerUpload = 500;

        [Header("Presentation")]
        public string leagueWord = "League";
        public LeagueOrientation orientation = LeagueOrientation.Auto;
        [Tooltip("Show a small note under the list when simulated players are present.")]
        public bool showSimulatedNote = false;
        public string simulatedNote = "Practice league · some players are simulated";

        public int TierCount => tiers.Length;

        public static LeagueConfig Default() => new LeagueConfig();
    }
}
