using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AquaGames.League.Samples
{
    // Drop this on an empty GameObject in any scene and press Play: it builds a Canvas with buttons to
    // add XP, open the board, fast-forward a week and switch Simulated / Online / Hybrid.
    public class LeagueDemo : MonoBehaviour
    {
        public LeagueMode mode = LeagueMode.Simulated;
        public LeagueOrientation orientation = LeagueOrientation.Auto;
        public string playerName = "You";

        LeagueService league;
        RectTransform canvasRect;
        TextMeshProUGUI info;

        void Start()
        {
            if (!LeagueView.CheckTextMeshPro()) { enabled = false; return; }
            var cfg = LeagueConfig.Default();
            cfg.mode = mode;
            cfg.orientation = orientation;
            league = new LeagueService(cfg, new PlayerPrefsLeagueStorage("league_demo"));
            league.PlayerName = playerName;
            league.WeekFinished += r => Debug.Log($"[LeagueDemo] week finished: #{r.rank}, tier {r.tierBefore} → {r.tierAfter}");

            var canvasGo = new GameObject("LeagueDemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            bool land = Screen.width > Screen.height;
            scaler.referenceResolution = land ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = land ? 1 : 0;
            canvasRect = (RectTransform)canvasGo.transform;
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            info = MakeText(canvasRect, "", new Vector2(0.5f, 1), new Vector2(0, -80));
            MakeButton("+15 XP", new Vector2(0.5f, 0.75f), () => league.AddXP(15));
            MakeButton("+100 XP", new Vector2(0.5f, 0.65f), () => league.AddXP(100));
            MakeButton("Open league", new Vector2(0.5f, 0.55f), () => LeagueView.Open(canvasRect, league));
            MakeButton("Skip 1 day", new Vector2(0.5f, 0.45f), () => { league.DebugTimeOffset += System.TimeSpan.FromDays(1); league.Sync(); });
            MakeButton("Finish week", new Vector2(0.5f, 0.35f), () =>
            {
                league.DebugTimeOffset += league.TimeLeft + System.TimeSpan.FromMinutes(1);
                league.Sync();
                var r = league.PendingResult;
                if (r != null) LeagueView.ShowResult(canvasRect, r, league, null, null, r.rank <= 3 ? "Reward: a shiny trophy" : null);
            });
            MakeButton("Mode: next", new Vector2(0.5f, 0.25f), () => league.SetMode((LeagueMode)(((int)league.ActiveMode + 1) % 3)));
        }

        void Update()
        {
            if (league == null || info == null) return;
            info.text = $"{league.TierName} · {league.XP} XP · rank #{league.PlayerRank}\nmode {league.ActiveMode} ({league.Status}) · {LeagueView.Countdown(league.TimeLeft)}";
        }

        void OnApplicationPause(bool paused) { if (paused) league?.Flush(); }

        void MakeButton(string label, Vector2 anchor, System.Action click)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var r = (RectTransform)go.transform;
            r.SetParent(canvasRect, false);
            r.anchorMin = r.anchorMax = anchor; r.sizeDelta = new Vector2(460, 110);
            go.GetComponent<Image>().color = new Color(0.25f, 0.55f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => click());
            var t = MakeText(r, label, new Vector2(0.5f, 0.5f), Vector2.zero);
            t.rectTransform.sizeDelta = r.sizeDelta;
        }

        TextMeshProUGUI MakeText(RectTransform parent, string text, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject("text", typeof(RectTransform));
            var t = go.AddComponent<TextMeshProUGUI>();
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = pos; r.sizeDelta = new Vector2(1000, 140);
            t.text = text; t.fontSize = 40; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }
    }
}
