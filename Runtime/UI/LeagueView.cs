using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AquaGames.League
{
    // Duolingo-style league board, built entirely in code inside any RectTransform
    // (a full-screen rect under a Canvas, ideally with a CanvasScaler at 1080 wide).
    //
    //   LeagueView.Open(popupLayer, league, theme);           // board
    //   LeagueView.ShowResult(popupLayer, result, league, theme, onClosed);   // end-of-week popup
    public class LeagueView : MonoBehaviour
    {
        LeagueService svc;
        LeagueTheme th;
        RectTransform listContent, root;
        TextMeshProUGUI timerT, titleT, subT, statusT, noteT, meT;
        bool landscape;
        ScrollRect scroll;
        Action onClose;
        float refresh;
        bool scrolled;
        const float RowH = 116, Gap = 10;

        // TextMeshPro needs its Essential Resources (default font + shaders) unless a font is supplied.
        public static bool CheckTextMeshPro(LeagueTheme theme = null)
        {
            if (theme?.font != null || TMP_Settings.defaultFontAsset != null) return true;
            Debug.LogError("[League] TextMeshPro Essential Resources are missing. In Unity: Window → TextMeshPro → " +
                           "Import TMP Essential Resources (or pass a font in LeagueTheme.font).");
            return false;
        }

        public static LeagueView Open(RectTransform parent, LeagueService service, LeagueTheme theme = null, Action onClose = null)
        {
            if (!CheckTextMeshPro(theme)) return null;
            service.Sync();
            var go = new GameObject("LeagueView", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<LeagueView>();
            v.svc = service; v.th = theme ?? new LeagueTheme(); v.onClose = onClose;
            v.Build();
            return v;
        }

        public void Close()
        {
            svc.Changed -= OnChanged;
            Destroy(gameObject);
            onClose?.Invoke();
        }

        void OnDestroy() { if (svc != null) svc.Changed -= OnChanged; }
        void OnChanged() => Rebuild();

        float S(float v) => v * th.scale;
        bool Rib => th.ribbonSprite != null;

        void Build()
        {
            root = (RectTransform)transform;
            Stretch(root);
            var pr = ((RectTransform)root.parent).rect;
            var o = svc.Config.orientation;
            landscape = o == LeagueOrientation.Landscape || (o == LeagueOrientation.Auto && pr.width > pr.height);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = th.dim;
            float pl = landscape ? S(60) : S(30), pt = landscape ? S(40) : S(110), pb = landscape ? S(40) : S(90);
            if (th.panelBorder.a > 0)
            {
                var border = Img(root, th.panelSprite ?? LeagueTheme.Rounded(32), th.panelBorder);
                float bw = S(th.borderWidth);
                Stretch(border.rectTransform, pl - bw, pt - bw, pl - bw, pb - bw);
            }
            var panel = Img(root, th.panelSprite ?? LeagueTheme.Rounded(32), th.panel);
            Stretch(panel.rectTransform, pl, pt, pl, pb);

            // header: top band in portrait, left column in landscape
            var header = Img(panel.rectTransform, th.panelSprite ?? LeagueTheme.Rounded(32), th.header);
            var hr = header.rectTransform;
            if (landscape)
            {
                hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(0.38f, 1); hr.pivot = new Vector2(0.5f, 1);
                hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
            }
            else
            {
                hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1);
                hr.sizeDelta = new Vector2(0, S(Rib ? 400 : 360)); hr.anchoredPosition = Vector2.zero;
            }
            BuildBadges(hr);
            float w = landscape ? S(620) : S(900);
            if (th.ribbonSprite != null)
            {
                var rib = Img(hr, th.ribbonSprite, Color.white);
                Place(rib.rectTransform, new Vector2(0.5f, 1), new Vector2(Mathf.Min(w, S(640)), S(128)), new Vector2(0, -S(148)));
                titleT = Label(rib.rectTransform, "", 52, th.ribbonText, TextAlignmentOptions.Center);
                if (th.titleMaterial != null) titleT.fontSharedMaterial = th.titleMaterial;
                Stretch(titleT.rectTransform, S(60), S(8), S(60), S(34));
            }
            else
            {
                titleT = Label(hr, "", 58, th.headerText, TextAlignmentOptions.Center);
                if (th.titleMaterial != null) titleT.fontSharedMaterial = th.titleMaterial;
                Place(titleT.rectTransform, new Vector2(0.5f, 1), new Vector2(w, S(70)), new Vector2(0, -S(175)));
            }
            subT = Label(hr, "", 34, th.textDim, TextAlignmentOptions.Center);
            subT.textWrappingMode = TextWrappingModes.Normal;
            Place(subT.rectTransform, new Vector2(0.5f, 1), new Vector2(w, S(landscape ? 90 : 50)), new Vector2(0, -S(Rib ? 292 : 240)));
            timerT = Label(hr, "", 34, th.timerColor, TextAlignmentOptions.Center);
            Place(timerT.rectTransform, new Vector2(0.5f, 1), new Vector2(w, S(50)), new Vector2(0, -S(landscape ? (Rib ? 382 : 330) : (Rib ? 338 : 290))));
            statusT = Label(hr, "", 26, th.textDim, TextAlignmentOptions.Center);
            Place(statusT.rectTransform, landscape ? new Vector2(0.5f, 0) : new Vector2(0.5f, 1), new Vector2(w, S(40)), landscape ? new Vector2(0, S(24)) : new Vector2(0, -S(Rib ? 372 : 330)));
            if (landscape)
            {
                // player summary card (portrait shows the highlighted row instead)
                var card = Img(hr, th.rowSprite ?? LeagueTheme.Rounded(24), th.playerRow);
                Place(card.rectTransform, new Vector2(0.5f, 1), new Vector2(S(560), S(150)), new Vector2(0, -S(Rib ? 450 : 410)), new Vector2(0.5f, 1));
                meT = Label(card.rectTransform, "", 40, th.playerRowText, TextAlignmentOptions.Center);
                meT.textWrappingMode = TextWrappingModes.Normal;
                Stretch(meT.rectTransform, S(20), S(10), S(20), S(10));
            }

            // close
            var close = Img(panel.rectTransform, th.closeSprite ?? LeagueTheme.Circle(), th.closeColor);
            Place(close.rectTransform, new Vector2(1, 1), new Vector2(S(92), S(92)), new Vector2(S(24), S(24)), new Vector2(1, 1));
            var cb = close.gameObject.AddComponent<Button>();
            cb.onClick.AddListener(Close);
            if (th.closeIcon != null)
            {
                var ic = Img(close.rectTransform, th.closeIcon, Color.white);
                ic.preserveAspect = true; ic.type = Image.Type.Simple; ic.raycastTarget = false;
                Stretch(ic.rectTransform, S(20), S(20), S(20), S(20));
            }
            else
            {
                var x = Label(close.rectTransform, "X", 46, Color.white, TextAlignmentOptions.Center);
                Stretch(x.rectTransform);
            }

            // list: under the header in portrait, right column in landscape
            var view = new GameObject("list", typeof(RectTransform)).GetComponent<RectTransform>();
            view.SetParent(panel.rectTransform, false);
            float bottom = svc.Config.showSimulatedNote ? S(70) : S(20);
            if (landscape)
            {
                view.anchorMin = new Vector2(0.39f, 0); view.anchorMax = new Vector2(1, 1);
                view.offsetMin = new Vector2(S(10), bottom); view.offsetMax = new Vector2(-S(20), -S(20));
            }
            else Stretch(view, S(20), S(Rib ? 420 : 380), S(20), bottom);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40;
            listContent = new GameObject("content", typeof(RectTransform)).GetComponent<RectTransform>();
            listContent.SetParent(view, false);
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(0.5f, 1);
            scroll.content = listContent; scroll.viewport = view;

            if (svc.Config.showSimulatedNote)
            {
                noteT = Label(panel.rectTransform, svc.Config.simulatedNote, 26, th.textDim, TextAlignmentOptions.Center);
                Place(noteT.rectTransform, landscape ? new Vector2(0.69f, 0) : new Vector2(0.5f, 0), new Vector2(S(900), S(40)), new Vector2(0, S(16)), new Vector2(0.5f, 0));
            }

            svc.Changed += OnChanged;
            svc.Refresh(true);
            Rebuild();
        }

        void BuildBadges(RectTransform hr)
        {
            int n = svc.Config.TierCount, cur = svc.Tier;
            float spacing = S(96), maxX = landscape ? S(280) : S(520);
            for (int i = 0; i < n; i++)
            {
                bool isCur = i == cur, locked = i > cur;
                float size = isCur ? S(Rib ? 118 : 150) : S(74);
                var b = Img(hr, LeagueTheme.Shield(), locked ? th.lockedBadge : svc.Config.tierColors[i % svc.Config.tierColors.Length]);
                // keep the current badge centred, others spread left/right
                Place(b.rectTransform, new Vector2(0.5f, 1), new Vector2(size, size * 1.1f), new Vector2((i - cur) * spacing + (i < cur ? -S(40) : i > cur ? S(40) : 0), -S(isCur ? 18 : 55)), new Vector2(0.5f, 1));
                if (Mathf.Abs((i - cur) * spacing) > maxX) b.gameObject.SetActive(false);
                if (locked)
                {
                    var l = Label(b.rectTransform, "?", 30, Color.white, TextAlignmentOptions.Center);
                    Stretch(l.rectTransform);
                }
            }
        }

        void Rebuild()
        {
            var cfg = svc.Config;
            titleT.text = svc.TierName;
            timerT.text = Countdown(svc.TimeLeft);
            statusT.text = svc.ResultPending ? "Calculating last week's results…" : svc.StatusText;
            for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);

            if (!svc.Joined)
            {
                subT.text = $"Top {cfg.promoteCount} advance to the next league";
                var card = Img(listContent, th.rowSprite ?? LeagueTheme.Rounded(24), th.row);
                Place(card.rectTransform, new Vector2(0.5f, 1), new Vector2(S(900), S(260)), new Vector2(0, -S(20)), new Vector2(0.5f, 1));
                var t = Label(card.rectTransform, "Play to join this week's league!\n<size=70%>Earn XP to be placed with 29 other players.</size>", 42, th.rowText, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.Normal;
                Stretch(t.rectTransform, S(30), S(20), S(30), S(20));
                listContent.sizeDelta = new Vector2(0, S(300));
                return;
            }

            var list = svc.Standings();
            int tier = svc.Tier;
            bool canPromote = tier < cfg.TierCount - 1, canDemote = tier > 0;
            subT.text = canPromote ? $"Top {cfg.promoteCount} advance to the next league" : "Top league: finish #1 to be the champion";
            float y = -S(8);
            int demoteFrom = cfg.groupSize - cfg.demoteCount + 1;
            RectTransform playerRow = null;
            foreach (var e in list)
            {
                if (canDemote && e.rank == demoteFrom) { y = Divider(y, "DEMOTION ZONE", th.demotion); }
                var row = Row(e, y, svc.ZoneOf(e.rank, tier));
                if (e.isPlayer) playerRow = row;
                y -= S(RowH + Gap);
                if (canPromote && e.rank == cfg.promoteCount) { y = Divider(y, "PROMOTION ZONE", th.promotion); }
            }
            listContent.sizeDelta = new Vector2(0, -y + S(10));
            if (noteT != null) noteT.gameObject.SetActive(list.Exists(e => e.isSimulated));
            if (meT != null)
            {
                var me = list.Find(e => e.isPlayer);
                var zone = svc.ZoneOf(me.rank, tier);
                string z = zone == Zone.Promotion ? "<color=#8FFF8F>Promotion zone</color>" : zone == Zone.Demotion ? "<color=#FF9090>Demotion zone</color>" : "Safe";
                meT.text = $"You are <b>#{me.rank}</b> · {me.xp} XP\n<size=75%>{z}</size>";
            }
            if (!scrolled && playerRow != null)
            {
                scrolled = true;
                Canvas.ForceUpdateCanvases();
                float viewH = ((RectTransform)scroll.viewport).rect.height;
                float target = -playerRow.anchoredPosition.y - viewH * 0.4f;
                float max = listContent.rect.height - viewH;
                if (max > 0) scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / max);
            }
        }

        // Zone label with a coloured line on each side (no special glyphs needed from the font).
        float Divider(float y, string text, Color c)
        {
            var t = Label(listContent, text, 30, c, TextAlignmentOptions.Center);
            Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(S(420), S(56)), new Vector2(0, y - S(4)), new Vector2(0.5f, 1));
            foreach (float side in new[] { -1f, 1f })
            {
                var line = Img(listContent, null, c);
                Place(line.rectTransform, new Vector2(0.5f, 1), new Vector2(S(220), S(5)), new Vector2(side * S(330), y - S(30)), new Vector2(0.5f, 1));
            }
            return y - S(66);
        }

        RectTransform Row(LeagueEntry e, float y, Zone zone)
        {
            var bg = Img(listContent, th.rowSprite ?? LeagueTheme.Rounded(24), e.isPlayer ? th.playerRow : th.row);
            var r = bg.rectTransform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1);
            r.sizeDelta = new Vector2(-S(10), S(RowH)); r.anchoredPosition = new Vector2(0, y);

            // rank: medals for the top 3, zone-coloured number otherwise
            if (e.rank <= 3)
            {
                var m = Img(r, LeagueTheme.Circle(), th.medals[e.rank - 1]);
                Place(m.rectTransform, new Vector2(0, 0.5f), new Vector2(S(64), S(64)), new Vector2(S(24), 0), new Vector2(0, 0.5f));
                var rt = Label(m.rectTransform, e.rank.ToString(), 34, new Color(0.2f, 0.15f, 0.05f), TextAlignmentOptions.Center);
                Stretch(rt.rectTransform);
            }
            else
            {
                var rc = zone == Zone.Promotion ? th.promotion : zone == Zone.Demotion ? th.demotion : e.isPlayer ? th.playerRowText : th.rowTextDim;
                var rt = Label(r, e.rank.ToString(), 38, rc, TextAlignmentOptions.Center);
                Place(rt.rectTransform, new Vector2(0, 0.5f), new Vector2(S(70), S(60)), new Vector2(S(20), 0), new Vector2(0, 0.5f));
            }

            var av = Img(r, LeagueTheme.Circle(), e.avatarColor);
            Place(av.rectTransform, new Vector2(0, 0.5f), new Vector2(S(80), S(80)), new Vector2(S(110), 0), new Vector2(0, 0.5f));
            var ai = Label(av.rectTransform, e.avatarInitial, 40, Color.white, TextAlignmentOptions.Center);
            Stretch(ai.rectTransform);

            var nm = Label(r, e.isPlayer ? e.name + "  <size=70%>(you)</size>" : e.name, 40, e.isPlayer ? th.playerRowText : th.rowText, TextAlignmentOptions.Left);
            nm.overflowMode = TextOverflowModes.Ellipsis;
            Place(nm.rectTransform, new Vector2(0, 0.5f), new Vector2(S(500), S(60)), new Vector2(S(215), 0), new Vector2(0, 0.5f));
            var xp = Label(r, e.xp + " XP", 38, e.isPlayer ? th.playerRowText : th.rowTextDim, TextAlignmentOptions.Right);
            Place(xp.rectTransform, new Vector2(1, 0.5f), new Vector2(S(220), S(60)), new Vector2(-S(26), 0), new Vector2(1, 0.5f));
            return r;
        }

        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0) return;
            refresh = 5f;               // bots keep earning while the board is open
            svc.Refresh(false);         // online: re-fetch standings every refreshSeconds
            if (svc.Joined) Rebuild();
            else timerT.text = Countdown(svc.TimeLeft);
        }

        public static string Countdown(TimeSpan t)
        {
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays} day{((int)t.TotalDays == 1 ? "" : "s")} left";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m left";
            return $"{Math.Max(0, t.Minutes)}m left";
        }

        // ---------------- end-of-week result ----------------
        public static GameObject ShowResult(RectTransform parent, WeekResult r, LeagueService svc, LeagueTheme theme = null, Action onClosed = null, string rewardText = null)
        {
            var th = theme ?? new LeagueTheme();
            if (!CheckTextMeshPro(th)) return null;
            var go = new GameObject("LeagueResult", typeof(RectTransform));
            var root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            Stretch(root);
            go.AddComponent<Image>().color = th.dim;
            var size = new Vector2(860 * th.scale, 900 * th.scale);
            if (th.panelBorder.a > 0)
            {
                var border = Img(root, th.panelSprite ?? LeagueTheme.Rounded(32), th.panelBorder);
                Place(border.rectTransform, new Vector2(0.5f, 0.5f), size + Vector2.one * 2 * th.borderWidth * th.scale, Vector2.zero);
            }
            var panel = Img(root, th.panelSprite ?? LeagueTheme.Rounded(32), th.panel);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), size, Vector2.zero);
            var cfg = svc.Config;
            var badge = Img(panel.rectTransform, LeagueTheme.Shield(), cfg.tierColors[r.tierAfter % cfg.tierColors.Length]);
            Place(badge.rectTransform, new Vector2(0.5f, 1), new Vector2(220 * th.scale, 240 * th.scale), new Vector2(0, -40 * th.scale), new Vector2(0.5f, 1));
            string headline = r.promoted ? "PROMOTED!" : r.demoted ? "Demoted" : "Week complete!";
            string tierName = cfg.tiers[r.tierAfter] + " " + cfg.leagueWord;
            string detail = r.promoted ? $"You finished #{r.rank} and moved up to the {tierName}!"
                          : r.demoted ? $"You finished #{r.rank}. You're in the {tierName} this week — climb back up!"
                          : $"You finished #{r.rank} and stay in the {tierName}.";
            TextMeshProUGUI h;
            if (th.ribbonSprite != null)
            {
                var rib = Img(panel.rectTransform, th.ribbonSprite, Color.white);
                Place(rib.rectTransform, new Vector2(0.5f, 1), new Vector2(680 * th.scale, 130 * th.scale), new Vector2(0, -290 * th.scale), new Vector2(0.5f, 1));
                h = Label(th, rib.rectTransform, headline, 54, th.ribbonText, TextAlignmentOptions.Center);
                Stretch(h.rectTransform, 60 * th.scale, 8 * th.scale, 60 * th.scale, 34 * th.scale);
            }
            else
            {
                h = Label(th, panel.rectTransform, headline, 64, r.demoted ? th.demotion : th.medals[0], TextAlignmentOptions.Center);
                Place(h.rectTransform, new Vector2(0.5f, 1), new Vector2(800 * th.scale, 90 * th.scale), new Vector2(0, -300 * th.scale), new Vector2(0.5f, 1));
            }
            if (th.titleMaterial != null) h.fontSharedMaterial = th.titleMaterial;
            var d = Label(th, panel.rectTransform, detail + $"\n<size=80%>{r.xp} XP this week</size>" + (string.IsNullOrEmpty(rewardText) ? "" : $"\n\n<b>{rewardText}</b>"), 38, th.rowText, TextAlignmentOptions.Center);
            d.textWrappingMode = TextWrappingModes.Normal;
            Place(d.rectTransform, new Vector2(0.5f, 1), new Vector2(760 * th.scale, 300 * th.scale), new Vector2(0, -440 * th.scale), new Vector2(0.5f, 1));
            var btn = Img(panel.rectTransform, th.buttonSprite ?? LeagueTheme.Rounded(30), th.button);
            Place(btn.rectTransform, new Vector2(0.5f, 0), new Vector2(420 * th.scale, 130 * th.scale), new Vector2(0, 40 * th.scale), new Vector2(0.5f, 0));
            var bl = Label(th, btn.rectTransform, "CONTINUE", 48, th.buttonText, TextAlignmentOptions.Center);
            if (th.titleMaterial != null) bl.fontSharedMaterial = th.titleMaterial;
            Stretch(bl.rectTransform, 0, 0, 0, th.buttonSprite != null ? 12 * th.scale : 0);
            btn.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                svc.ClearPendingResult();
                Destroy(go);
                onClosed?.Invoke();
            });
            return go;
        }

        // ---------------- tiny UI helpers (module has no dependency on the game's UI kit) ----------------
        static Image Img(RectTransform parent, Sprite s, Color c)
        {
            var go = new GameObject("img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            if (s != null && s.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        TextMeshProUGUI Label(RectTransform parent, string text, float size, Color c, TextAlignmentOptions align) => Label(th, parent, text, size, c, align);

        static TextMeshProUGUI Label(LeagueTheme th, RectTransform parent, string text, float size, Color c, TextAlignmentOptions align)
        {
            var go = new GameObject("text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (th.font != null) t.font = th.font;
            if (th.fontMaterial != null) t.fontSharedMaterial = th.fontMaterial;
            t.text = text;
            t.fontSize = size * th.scale;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform r, float l = 0, float t = 0, float rr = 0, float b = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot ?? anchor;
            r.sizeDelta = size;
            r.anchoredPosition = pos;
        }
    }
}
