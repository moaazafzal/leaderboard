using System;
using System.Collections.Generic;

namespace AquaGames.League
{
    // No server: all opponents are generated on the device from the player id + week + tier.
    public class SimulatedBackend : ILeagueBackend
    {
        LeagueService svc;
        readonly Dictionary<string, List<LeagueBot>> cache = new Dictionary<string, List<LeagueBot>>();

        public LeagueStatus Status => LeagueStatus.Simulated;
        public string StatusText => "";
        public event Action Updated { add { } remove { } }

        public void Initialize(LeagueService service) => svc = service;
        public void Join(int week, int tier) { }
        public void ReportXp(int week, int xp, bool force) { }
        public void Refresh(bool force) { }

        public List<LeagueBot> Bots(int week, int tier)
        {
            string key = svc.Save.playerId + ":" + week + ":" + tier;
            if (!cache.TryGetValue(key, out var bots))
                cache[key] = bots = BotFactory.Create(key, svc.Config.groupSize - 1, tier, svc.PlayerPace, svc.Config, new[] { svc.PlayerName });
            return bots;
        }

        public List<LeagueEntry> Opponents(int week, int tier, DateTime at)
        {
            var bots = Bots(week, tier);
            var start = svc.WeekStart(week);
            var list = new List<LeagueEntry>(bots.Count);
            for (int i = 0; i < bots.Count; i++) list.Add(BotFactory.ToEntry(bots[i], i, start, at, svc.Config));
            return list;
        }

        public void FinalOpponents(int week, int tier, string groupId, Action<List<LeagueEntry>> done) =>
            done(Opponents(week, tier, svc.WeekEnd(week).AddSeconds(-1)));
    }
}
