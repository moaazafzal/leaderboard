#if LEAGUE_FIREBASE
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

namespace AquaGames.League.FirebaseBackend
{
    // Real weekly leagues on Firebase (Firestore + Anonymous Auth — both free on the Spark plan).
    //
    // Data model (prefix = LeagueConfig.collectionPrefix, default "league"):
    //   {prefix}_counters/{week}_{tier}                 { group: int, count: int }      → which group is filling up
    //   {prefix}_groups/{week}_{tier}_{group}/members/{uid}  { name, xp, color, updatedAt }
    //   {prefix}_users/{uid}                            { name, tier, week, groupId }  → player profile
    // Security rules: Firebase/firestore.rules (deploy them!).
    //
    // Mode Online: only real members are shown. Mode Hybrid: empty seats are filled with simulated players
    // seeded by the group id, so every member of a group sees the same simulated players.
    public class FirestoreBackend : ILeagueBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LeagueBackends.Register("firebase", _ => new FirestoreBackend());

        LeagueService svc;
        LeagueConfig cfg;
        FirebaseFirestore db;
        string uid;
        bool ready, joining, uploading, dirty;
        bool pendingJoin;
        float lastUpload = -999, lastRefresh = -999, lastFinalTry = -999;
        List<LeagueEntry> members = new List<LeagueEntry>();   // real members of the current group (incl. player)
        string membersGroup;
        List<LeagueBot> hybridBots;
        string hybridSeed;

        public LeagueStatus Status { get; private set; } = LeagueStatus.Connecting;
        public string StatusText => Status == LeagueStatus.Connecting ? "Connecting…" : Status == LeagueStatus.Offline ? "Offline · showing last known standings" : "";
        public event Action Updated;

        bool Hybrid => svc.ActiveMode == LeagueMode.Hybrid;
        string Counters => cfg.collectionPrefix + "_counters";
        string Groups => cfg.collectionPrefix + "_groups";
        string Users => cfg.collectionPrefix + "_users";

        public void Initialize(LeagueService service)
        {
            svc = service;
            cfg = service.Config;
            Ticker.Ensure(this);
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(dep =>
            {
                if (dep.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning("[League] Firebase not available: " + dep.Result);
                    Status = LeagueStatus.Offline; Updated?.Invoke();
                    return;
                }
                db = FirebaseFirestore.DefaultInstance;
                var auth = FirebaseAuth.DefaultInstance;
                if (auth.CurrentUser != null) OnSignedIn(auth.CurrentUser.UserId);
                else auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted || t.IsCanceled) { Debug.LogWarning("[League] sign-in failed: " + t.Exception); Status = LeagueStatus.Offline; Updated?.Invoke(); return; }
                    OnSignedIn(t.Result.User.UserId);
                });
            });
        }

        void OnSignedIn(string id)
        {
            uid = id;
            ready = true;
            Status = LeagueStatus.Online;
            if (pendingJoin || (svc.Save.joined && string.IsNullOrEmpty(svc.Save.groupId))) Join(svc.Save.week, svc.Save.tier);
            else Refresh(true);
            Updated?.Invoke();
        }

        // ---------------- join a group ----------------
        public void Join(int week, int tier)
        {
            if (!ready) { pendingJoin = true; return; }
            if (joining || !string.IsNullOrEmpty(svc.Save.groupId)) return;
            joining = true;
            pendingJoin = false;
            var counterRef = db.Collection(Counters).Document(week + "_" + tier);
            string name = svc.PlayerName;
            string color = "#" + ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(Mathf.Abs(BotFactory.Hash(uid) % 360) / 360f, 0.6f, 0.85f));
            db.RunTransactionAsync(async tx =>
            {
                var snap = await tx.GetSnapshotAsync(counterRef);
                long group = 0, count = 0;
                if (snap.Exists)
                {
                    snap.TryGetValue("group", out group);
                    snap.TryGetValue("count", out count);
                }
                if (count >= cfg.groupSize) { group++; count = 0; }
                count++;
                string gid = week + "_" + tier + "_" + group;
                tx.Set(counterRef, new Dictionary<string, object> { { "group", group }, { "count", count } });
                var memberRef = db.Collection(Groups).Document(gid).Collection("members").Document(uid);
                tx.Set(memberRef, new Dictionary<string, object> { { "name", name }, { "xp", 0 }, { "color", color }, { "updatedAt", FieldValue.ServerTimestamp } });
                return gid;
            }).ContinueWithOnMainThread(t =>
            {
                joining = false;
                if (t.IsFaulted || t.IsCanceled) { Debug.LogWarning("[League] join failed: " + t.Exception); pendingJoin = true; return; }
                if (svc.Save.week != week) return;   // week rolled over meanwhile
                svc.Save.groupId = t.Result;
                svc.Save.uploadedXp = 0;
                svc.Persist();
                db.Collection(Users).Document(uid).SetAsync(new Dictionary<string, object>
                {
                    { "name", name }, { "tier", tier }, { "week", week }, { "groupId", t.Result }, { "updatedAt", FieldValue.ServerTimestamp },
                }, SetOptions.MergeAll);
                dirty = true;
                Upload();
                Refresh(true);
            });
        }

        // ---------------- XP upload (debounced, capped per write for the security rules) ----------------
        public void ReportXp(int week, int xp, bool force)
        {
            dirty = true;
            if (force || Time.realtimeSinceStartup - lastUpload >= cfg.uploadDebounceSeconds) Upload();
        }

        void Upload()
        {
            if (!ready || uploading || !dirty || string.IsNullOrEmpty(svc.Save.groupId) || !svc.Save.joined) return;
            int target = Math.Min(svc.Save.xp, svc.Save.uploadedXp + cfg.maxXpPerUpload);
            if (target <= svc.Save.uploadedXp && svc.Save.xp == svc.Save.uploadedXp) { dirty = false; return; }
            uploading = true;
            lastUpload = Time.realtimeSinceStartup;
            string gid = svc.Save.groupId;
            db.Collection(Groups).Document(gid).Collection("members").Document(uid).SetAsync(new Dictionary<string, object>
            {
                { "xp", target }, { "name", svc.PlayerName }, { "updatedAt", FieldValue.ServerTimestamp },
            }, SetOptions.MergeAll).ContinueWithOnMainThread(t =>
            {
                uploading = false;
                if (t.IsFaulted || t.IsCanceled) { Status = LeagueStatus.Offline; return; }
                Status = LeagueStatus.Online;
                if (svc.Save.groupId != gid) return;
                svc.Save.uploadedXp = target;
                svc.Persist();
                dirty = svc.Save.xp > svc.Save.uploadedXp;
                if (dirty) Upload();                       // more than one capped step behind
            });
        }

        // ---------------- standings ----------------
        public void Refresh(bool force)
        {
            if (!ready) return;
            if (pendingJoin && svc.Save.joined) Join(svc.Save.week, svc.Save.tier);
            if (dirty) Upload();
            string gid = svc.Save.groupId;
            if (string.IsNullOrEmpty(gid)) return;
            if (!force && Time.realtimeSinceStartup - lastRefresh < cfg.refreshSeconds) return;
            lastRefresh = Time.realtimeSinceStartup;
            FetchMembers(gid, list =>
            {
                if (list == null) { Status = LeagueStatus.Offline; Updated?.Invoke(); return; }
                Status = LeagueStatus.Online;
                members = list;
                membersGroup = gid;
                Updated?.Invoke();
            });
        }

        void FetchMembers(string gid, Action<List<LeagueEntry>> done)
        {
            db.Collection(Groups).Document(gid).Collection("members").OrderByDescending("xp").Limit(cfg.groupSize)
              .GetSnapshotAsync().ContinueWithOnMainThread(t =>
              {
                  if (t.IsFaulted || t.IsCanceled) { done(null); return; }
                  var list = new List<LeagueEntry>();
                  foreach (var d in t.Result.Documents)
                  {
                      d.TryGetValue("name", out string name);
                      d.TryGetValue("xp", out long xp);
                      d.TryGetValue("color", out string hex);
                      ColorUtility.TryParseHtmlString(hex ?? "#5B8DEF", out var c);
                      list.Add(new LeagueEntry { id = d.Id, name = string.IsNullOrEmpty(name) ? "Player" : name, avatarInitial = BotFactory.Initial(name), avatarColor = c, xp = (int)xp });
                  }
                  done(list);
              });
        }

        public List<LeagueEntry> Opponents(int week, int tier, DateTime at)
        {
            var list = new List<LeagueEntry>();
            string gid = svc.Save.groupId;
            if (!string.IsNullOrEmpty(gid) && membersGroup == gid)
                foreach (var m in members) if (m.id != uid) list.Add(m);
            if (Hybrid) AddBots(list, string.IsNullOrEmpty(gid) ? svc.Save.playerId + ":" + week + ":" + tier : gid, tier, svc.WeekStart(week), at);
            return list;
        }

        void AddBots(List<LeagueEntry> list, string seed, int tier, DateTime weekStart, DateTime at)
        {
            int seats = cfg.groupSize - 1 - list.Count;
            if (seats <= 0) return;
            if (hybridBots == null || hybridSeed != seed || hybridBots.Count < seats)
            {
                var names = new List<string> { svc.PlayerName };
                foreach (var e in list) names.Add(e.name);
                hybridBots = BotFactory.Create(seed, cfg.groupSize - 1, tier, svc.PlayerPace, cfg, names);
                hybridSeed = seed;
            }
            for (int i = 0; i < seats; i++) list.Add(BotFactory.ToEntry(hybridBots[i], i, weekStart, at, cfg));
        }

        // ---------------- final results of a finished week ----------------
        public void FinalOpponents(int week, int tier, string groupId, Action<List<LeagueEntry>> done)
        {
            var end = svc.WeekEnd(week).AddSeconds(-1);
            if (string.IsNullOrEmpty(groupId))
            {
                // joined while offline and never got a group: score against simulated players
                var sim = new List<LeagueEntry>();
                AddBots(sim, svc.Save.playerId + ":" + week + ":" + tier, tier, svc.WeekStart(week), end);
                done(sim);
                return;
            }
            if (!ready || Time.realtimeSinceStartup - lastFinalTry < 15f) { done(null); return; }
            lastFinalTry = Time.realtimeSinceStartup;
            FetchMembers(groupId, list =>
            {
                if (list == null) { done(null); return; }
                var opp = list.FindAll(m => m.id != uid);
                if (Hybrid) AddBots(opp, groupId, tier, svc.WeekStart(week), end);
                done(opp);
            });
        }

        // Drives debounced uploads and retries without needing the game to call anything.
        class Ticker : MonoBehaviour
        {
            FirestoreBackend backend;
            float t;

            public static void Ensure(FirestoreBackend b)
            {
                var go = new GameObject("[LeagueFirestore]");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                go.AddComponent<Ticker>().backend = b;
            }

            void Update()
            {
                t += Time.unscaledDeltaTime;
                if (t < 5f) return;
                t = 0;
                if (backend.dirty && Time.realtimeSinceStartup - backend.lastUpload >= backend.cfg.uploadDebounceSeconds) backend.Upload();
                if (backend.pendingJoin && backend.svc.Save.joined) backend.Join(backend.svc.Save.week, backend.svc.Save.tier);
                if (backend.svc.ResultPending) backend.svc.Sync();
            }

            void OnApplicationPause(bool paused) { if (paused) { backend.dirty = true; backend.Upload(); } }
        }
    }
}
#endif
