using System;
using System.Collections.Generic;

namespace AquaGames.League
{
    // Where opponents come from. LeagueService owns the week clock, the player's tier/XP and the
    // promotion rules; a backend only supplies the other members of the player's group.
    //
    // Ships with: SimulatedBackend (built in) and FirestoreBackend (Firebase add-on, define LEAGUE_FIREBASE).
    // To add another (PlayFab, your own REST server…): implement this interface and register it with
    // LeagueBackends.Register("myid", svc => new MyBackend(svc)), then set config.onlineBackend = "myid".
    public interface ILeagueBackend
    {
        LeagueStatus Status { get; }
        string StatusText { get; }

        // Called once when the service starts.
        void Initialize(LeagueService service);

        // Player earned their first XP this week: put them in a group (online: find/create a group of groupSize).
        void Join(int week, int tier);

        // Player's weekly XP changed (backends may debounce uploads). force = upload now (board open, app pause).
        void ReportXp(int week, int xp, bool force);

        // Other members of the player's current group at time `at` (not including the player).
        List<LeagueEntry> Opponents(int week, int tier, DateTime at);

        // Final standings of a finished week (not including the player). Online backends call back later.
        void FinalOpponents(int week, int tier, string groupId, Action<List<LeagueEntry>> done);

        // Board opened / timer tick: refresh remote data if needed.
        void Refresh(bool force);

        // Fired when opponents changed (remote update) so the UI can redraw.
        event Action Updated;
    }

    // Registry so optional backends (in their own assemblies) can plug in without the core referencing them.
    public static class LeagueBackends
    {
        static readonly Dictionary<string, Func<LeagueService, ILeagueBackend>> factories = new Dictionary<string, Func<LeagueService, ILeagueBackend>>();

        public static void Register(string id, Func<LeagueService, ILeagueBackend> factory) => factories[id] = factory;
        public static bool Has(string id) => factories.ContainsKey(id);
        public static IEnumerable<string> Ids => factories.Keys;

        public static ILeagueBackend Create(string id, LeagueService svc) => factories.TryGetValue(id, out var f) ? f(svc) : null;
    }
}
