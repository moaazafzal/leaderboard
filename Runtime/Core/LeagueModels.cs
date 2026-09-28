using System;
using System.Collections.Generic;
using UnityEngine;

namespace AquaGames.League
{
    public enum Personality { Steady, Bursty, Weekend, Quitter, Grinder }

    // One simulated competitor. Everything is derived from a seed, so the same bot always has the same
    // name, avatar and schedule for a given group.
    public class LeagueBot
    {
        public string name;
        public Color avatarColor;
        public string avatarInitial;
        public Personality personality;
        public float xpPerDay;
        public float[] dayWeights;   // share of activity per day of the week
        public int seed;
    }

    public class LeagueEntry
    {
        public string id;            // player id (online) or "bot:<n>"
        public string name;
        public string avatarInitial;
        public Color avatarColor;
        public int xp;
        public int rank;             // 1-based
        public bool isPlayer;
        public bool isSimulated;
    }

    public enum Zone { Promotion, Safe, Demotion }

    public enum LeagueStatus { Simulated, Connecting, Online, Offline }

    public class WeekResult
    {
        public int week;
        public int tierBefore, tierAfter;
        public int rank, groupSize, xp;
        public bool promoted => tierAfter > tierBefore;
        public bool demoted => tierAfter < tierBefore;
    }

    // Persisted player state (same for every mode, so switching modes keeps tier and XP).
    [Serializable]
    public class LeagueSave
    {
        public string playerId;
        public string playerName = "You";
        public int tier;
        public int week = -1;           // week index the player is currently in
        public bool joined;             // earned XP this week (you join a group with your first XP, like Duolingo)
        public int xp;                  // XP this week
        public long joinedAtTicks;
        public string groupId;          // online group this week ("" when simulated)
        public int uploadedXp;          // last XP value confirmed by the server
        public List<float> recentDailyXp = new List<float>();   // player's pace in past weeks (for rubber-banding)
        public int bestTier, weeksPlayed, firstPlaces, promotions;
        public WeekResultData pendingResult;                    // shown once by the UI, then cleared
        public int finalizingWeek = -1;                         // week whose result is being fetched (online)
        public int finalizingTier, finalizingXp;
        public string finalizingGroup;
    }

    [Serializable]
    public class WeekResultData
    {
        public int week, tierBefore, tierAfter, rank, groupSize, xp;
        public bool valid;

        public WeekResult ToResult() => new WeekResult { week = week, tierBefore = tierBefore, tierAfter = tierAfter, rank = rank, groupSize = groupSize, xp = xp };
    }
}
