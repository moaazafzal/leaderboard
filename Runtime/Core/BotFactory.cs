using System;
using System.Collections.Generic;
using UnityEngine;

namespace AquaGames.League
{
    // Deterministic simulated players. Same seed → same bots, same schedules, same scores at the same time,
    // so every device (and every app launch) sees an identical group.
    public static class BotFactory
    {
        // count bots for a group. seed identifies the group (player id + week + tier when simulated,
        // the online group id in Hybrid mode so all members of that group see the same bots).
        public static List<LeagueBot> Create(string seed, int count, int tier, float playerPace, LeagueConfig cfg, ICollection<string> takenNames = null)
        {
            var rng = new System.Random(Hash(seed));
            var bots = new List<LeagueBot>(count);
            var used = new HashSet<string>(takenNames ?? Array.Empty<string>());
            float tierPace = cfg.baseXpPerDay * Mathf.Pow(cfg.tierXpGrowth, tier);
            for (int i = 0; i < count; i++)
            {
                string name;
                int guard = 0;
                do name = NamePool.Pick(rng); while (!used.Add(name) && ++guard < 50);
                var b = new LeagueBot
                {
                    name = name,
                    avatarInitial = Initial(name),
                    avatarColor = Color.HSVToRGB((float)rng.NextDouble(), 0.55f + (float)rng.NextDouble() * 0.3f, 0.85f),
                    seed = rng.Next(),
                };
                double r = rng.NextDouble();
                b.personality = r < cfg.inactiveShare ? Personality.Quitter
                              : r < 0.45 ? Personality.Steady
                              : r < 0.65 ? Personality.Bursty
                              : r < 0.85 ? Personality.Weekend
                              : Personality.Grinder;
                // log-normal spread around the tier pace, pulled towards the player's own pace
                float pace = tierPace * Mathf.Exp((float)Gaussian(rng) * 0.45f);
                if (playerPace > 0) pace = Mathf.Lerp(pace, playerPace * (0.5f + (float)rng.NextDouble() * 1.1f), cfg.rubberBand);
                if (b.personality == Personality.Grinder) pace *= 1.6f;
                b.xpPerDay = pace;
                b.dayWeights = DayWeights(b.personality, rng, cfg);
                bots.Add(b);
            }
            return bots;
        }

        static float[] DayWeights(Personality p, System.Random rng, LeagueConfig cfg)
        {
            int n = cfg.weekLengthDays;
            var w = new float[n];
            for (int d = 0; d < n; d++)
            {
                int dow = ((int)cfg.weekStartsOn + d) % 7;
                bool weekend = dow == 0 || dow == 6;
                w[d] = p switch
                {
                    Personality.Steady => 0.8f + (float)rng.NextDouble() * 0.4f,
                    Personality.Bursty => rng.NextDouble() < 0.45 ? 0f : 1.2f + (float)rng.NextDouble() * 1.3f,
                    Personality.Weekend => weekend ? 2.2f + (float)rng.NextDouble() : 0.25f * (float)rng.NextDouble(),
                    Personality.Quitter => d < 1 + rng.Next(2) ? 0.6f + (float)rng.NextDouble() : 0f,
                    _ => 0.9f + (float)rng.NextDouble() * 0.5f,
                };
            }
            return w;
        }

        // XP a bot has at time `at` in a week that started at weekStart. Finished days count fully,
        // today counts by how much of the bot's ~4 playing hours have passed. Never decreases.
        public static int Xp(LeagueBot b, DateTime weekStart, DateTime at, LeagueConfig cfg)
        {
            double days = (at - weekStart).TotalDays;
            if (days <= 0) return 0;
            days = Math.Min(days, cfg.weekLengthDays);
            var rng = new System.Random(b.seed);
            double total = 0;
            for (int d = 0; d < cfg.weekLengthDays; d++)
            {
                double dayXp = b.xpPerDay * b.dayWeights[d] * (0.55 + rng.NextDouble() * 0.9);
                double startHour = 7 + rng.NextDouble() * 10;
                if (d + 1 <= days) { total += dayXp; continue; }
                if (d >= days) break;
                double hour = (days - d) * 24;
                total += dayXp * Mathf.Clamp01((float)((hour - startHour) / 4.0));
                break;
            }
            int step = Math.Max(1, cfg.xpStep);
            return (int)(Math.Floor(total / step) * step);
        }

        public static LeagueEntry ToEntry(LeagueBot b, int index, DateTime weekStart, DateTime at, LeagueConfig cfg) => new LeagueEntry
        {
            id = "bot:" + index,
            name = b.name,
            avatarInitial = b.avatarInitial,
            avatarColor = b.avatarColor,
            xp = Xp(b, weekStart, at, cfg),
            isSimulated = true,
        };

        public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : char.ToUpperInvariant(name[0]).ToString();

        public static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (var c in s ?? "") h = h * 31 + c;
                return h;
            }
        }

        static double Gaussian(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
