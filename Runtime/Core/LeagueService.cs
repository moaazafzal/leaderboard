using System;
using System.Collections.Generic;
using UnityEngine;

namespace AquaGames.League
{
    // Duolingo-style weekly league. One instance per app.
    //
    //   var league = new LeagueService(LeagueConfig.Default());   // Simulated by default
    //   league.AddXP(10);                                          // whenever the player earns XP
    //   league.WeekFinished += r => GiveReward(r);                 // or poll league.PendingResult
    //   LeagueView.Open(parentRect, league);                       // show the board
    //   league.SetMode(LeagueMode.Hybrid);                         // switch fake ↔ real at any time
    //
    // The service owns the week clock, the player's tier/XP and promotion rules; the backend supplies opponents.
    public class LeagueService
    {
        public LeagueConfig Config { get; }
        public LeagueSave Save { get; private set; }
        public ILeagueBackend Backend { get; private set; }
        public LeagueMode ActiveMode { get; private set; }
        public event Action Changed;
        public event Action<WeekResult> WeekFinished;

        readonly ILeagueStorage storage;
        readonly Func<DateTime> utcNow;
        public TimeSpan DebugTimeOffset;   // fast-forward the clock for testing

        static readonly DateTime Anchor = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // a Monday

        public LeagueService(LeagueConfig config = null, ILeagueStorage storage = null, Func<DateTime> utcNow = null)
        {
            Config = config ?? LeagueConfig.Default();
            this.storage = storage ?? new PlayerPrefsLeagueStorage();
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            Save = this.storage.Load() ?? new LeagueSave();
            if (string.IsNullOrEmpty(Save.playerId)) Save.playerId = Guid.NewGuid().ToString("N");
            SetMode(Config.mode);
        }

        // ---------------- mode ----------------
        // Switch between Simulated / Online / Hybrid at runtime. Tier, XP and history are kept.
        public void SetMode(LeagueMode mode)
        {
            if (Backend != null) Backend.Updated -= OnBackendUpdated;
            ActiveMode = mode;
            Config.mode = mode;
            ILeagueBackend b = null;
            if (mode != LeagueMode.Simulated)
            {
                b = LeagueBackends.Create(Config.onlineBackend, this);
                if (b == null)
                {
                    Debug.LogWarning($"[League] backend '{Config.onlineBackend}' is not installed (see Documentation/LEADERBOARD.md). " +
                                     (Config.fallbackToSimulated ? "Using Simulated mode." : "League disabled."));
                    if (Config.fallbackToSimulated) { ActiveMode = LeagueMode.Simulated; b = new SimulatedBackend(); }
                }
            }
            Backend = b ?? new SimulatedBackend();
            Backend.Updated += OnBackendUpdated;
            Backend.Initialize(this);
            Sync();
            if (Save.joined) Backend.Join(Save.week, Save.tier);
            Changed?.Invoke();
        }

        void OnBackendUpdated() { Sync(); Changed?.Invoke(); }

        public LeagueStatus Status => Backend.Status;
        public string StatusText => Backend.StatusText;

        // ---------------- clock ----------------
        public DateTime Now => utcNow() + DebugTimeOffset;
        int StartOffsetDays => ((int)Config.weekStartsOn - (int)DayOfWeek.Monday + 7) % 7;
        public int WeekIndex(DateTime t) => (int)Math.Floor(((t - Anchor).TotalDays - StartOffsetDays) / Config.weekLengthDays);
        public DateTime WeekStart(int week) => Anchor.AddDays(StartOffsetDays + (double)week * Config.weekLengthDays);
        public DateTime WeekEnd(int week) => WeekStart(week + 1);
        public int CurrentWeek => WeekIndex(Now);
        public TimeSpan TimeLeft => WeekEnd(CurrentWeek) - Now;

        // ---------------- player ----------------
        public int Tier => Save.tier;
        public string TierNameOf(int tier) => Config.tiers[Mathf.Clamp(tier, 0, Config.TierCount - 1)] + " " + Config.leagueWord;
        public string TierName => TierNameOf(Save.tier);
        public bool Joined { get { Sync(); return Save.joined; } }
        public int XP { get { Sync(); return Save.xp; } }
        public bool ResultPending => Save.finalizingWeek >= 0;

        public string PlayerName
        {
            get => Save.playerName;
            set { Save.playerName = string.IsNullOrWhiteSpace(value) ? "You" : value.Trim(); Persist(); Backend.ReportXp(Save.week, Save.xp, true); }
        }

        // Average XP/day in recent weeks (simulated players pace themselves against it).
        public float PlayerPace
        {
            get
            {
                if (Save.recentDailyXp.Count == 0) return 0;
                float s = 0; foreach (var p in Save.recentDailyXp) s += p;
                return s / Save.recentDailyXp.Count;
            }
        }

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            Sync();
            if (!Save.joined)
            {
                Save.joined = true;
                Save.joinedAtTicks = Now.Ticks;
                Backend.Join(Save.week, Save.tier);
            }
            Save.xp += amount;
            Persist();
            Backend.ReportXp(Save.week, Save.xp, false);
            Changed?.Invoke();
        }

        // Call from OnApplicationPause(true) so pending XP reaches the server.
        public void Flush()
        {
            if (Save.joined) Backend.ReportXp(Save.week, Save.xp, true);
            Persist();
        }

        public void Refresh(bool force = false) { Sync(); Backend.Refresh(force); }

        // ---------------- week rollover ----------------
        public void Sync()
        {
            int cur = CurrentWeek;
            if (Save.finalizingWeek >= 0) TryFinalize();
            if (Save.week == cur) return;
            if (Save.week >= 0 && Save.joined)
            {
                // remember what to score; online results may arrive later (or on the next launch)
                Save.finalizingWeek = Save.week;
                Save.finalizingTier = Save.tier;
                Save.finalizingXp = Save.xp;
                Save.finalizingGroup = Save.groupId;
                Save.recentDailyXp.Add(Save.xp / (float)Config.weekLengthDays);
                if (Save.recentDailyXp.Count > 4) Save.recentDailyXp.RemoveAt(0);
            }
            Save.week = cur;
            Save.joined = false;
            Save.xp = 0;
            Save.uploadedXp = 0;
            Save.groupId = "";
            Persist();
            if (Save.finalizingWeek >= 0) TryFinalize();
            Changed?.Invoke();
        }

        bool finalizing;

        void TryFinalize()
        {
            if (finalizing || Save.finalizingWeek < 0) return;
            finalizing = true;
            int week = Save.finalizingWeek, tier = Save.finalizingTier, xp = Save.finalizingXp;
            Backend.FinalOpponents(week, tier, Save.finalizingGroup, opponents =>
            {
                finalizing = false;
                if (opponents == null || Save.finalizingWeek != week) return;   // offline: retry on next Sync
                var list = Rank(opponents, xp);
                int rank = list.Find(e => e.isPlayer).rank;
                var zone = ZoneOf(rank, tier);
                int after = zone == Zone.Promotion ? tier + 1 : zone == Zone.Demotion ? tier - 1 : tier;
                var r = new WeekResult { week = week, tierBefore = tier, tierAfter = after, rank = rank, groupSize = list.Count, xp = xp };
                Save.pendingResult = new WeekResultData { valid = true, week = week, tierBefore = tier, tierAfter = after, rank = rank, groupSize = list.Count, xp = xp };
                Save.tier = after;
                Save.bestTier = Mathf.Max(Save.bestTier, after);
                Save.weeksPlayed++;
                if (rank == 1) Save.firstPlaces++;
                if (r.promoted) Save.promotions++;
                Save.finalizingWeek = -1;
                Persist();
                WeekFinished?.Invoke(r);
                Changed?.Invoke();
            });
        }

        public WeekResult PendingResult => Save.pendingResult != null && Save.pendingResult.valid ? Save.pendingResult.ToResult() : null;

        public void ClearPendingResult()
        {
            if (Save.pendingResult == null) return;
            Save.pendingResult.valid = false;
            Persist();
        }

        // ---------------- standings ----------------
        public List<LeagueEntry> Standings()
        {
            Sync();
            return Rank(Backend.Opponents(Save.week, Save.tier, Now), Save.xp);
        }

        public int PlayerRank => Standings().Find(e => e.isPlayer).rank;

        public Zone ZoneOf(int rank, int tier)
        {
            if (rank <= Config.promoteCount && tier < Config.TierCount - 1) return Zone.Promotion;
            if (rank > Config.groupSize - Config.demoteCount && tier > 0) return Zone.Demotion;
            return Zone.Safe;
        }

        List<LeagueEntry> Rank(List<LeagueEntry> opponents, int playerXp)
        {
            var list = new List<LeagueEntry>(opponents.Count + 1);
            foreach (var o in opponents) if (o.id != Save.playerId) list.Add(o);
            list.Add(new LeagueEntry
            {
                id = Save.playerId, name = Save.playerName, avatarInitial = BotFactory.Initial(Save.playerName),
                avatarColor = new Color(0.35f, 0.8f, 0.3f), xp = playerXp, isPlayer = true,
            });
            // ties: the player wins ties (feels fair, keeps order stable)
            list.Sort((a, b) => b.xp != a.xp ? b.xp.CompareTo(a.xp) : (a.isPlayer ? -1 : b.isPlayer ? 1 : string.CompareOrdinal(a.name, b.name)));
            for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
            return list;
        }

        public bool HasSimulatedPlayers => Standings().Exists(e => e.isSimulated);

        public void Persist() => storage.Save(Save);
    }
}
