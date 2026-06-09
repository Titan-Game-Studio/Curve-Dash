using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.StatSystem;

namespace STG.CurveDash
{
    /// <summary>
    /// In-game character stats viewer built with runtime uGUI (so it uses the same EventSystem/input
    /// path as the rest of the game's working buttons — unlike IMGUI which can be unclickable under
    /// the new Input System). Self-instantiates at runtime; the panel sits in the top-right corner,
    /// one fifth of the screen height below the top. Toggle with the "Stats" button.
    /// </summary>
    public class CharacterStatsPanel : MonoBehaviour
    {
        private const string HandlerName = "Player Stats";

        private static readonly (string category, string[] names)[] Groups =
        {
            ("Resources",   new[] { "Heart", "Mana", "Shield", "Exp", "Free Points", "Level" }),
            ("Attributes",  new[] { "Strength", "Dexterity", "Intelligence" }),
            ("Offence",     new[] { "Min Damage", "Max Damage", "Attack Speed", "Critical Strike", "Critical Multiplier" }),
            ("Defence",     new[] { "Armor", "Evasion Rating", "Accuracy Rating", "Block Chance", "Life Regeneration" }),
            ("Resistances", new[] { "Fire Resistance", "Cold Resistance", "Lightning Resistance", "Chaos Resistance" }),
            ("Movement",    new[] { "Movement Speed" }),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<CharacterStatsPanel>() != null) return;
            var go = new GameObject("[CharacterStatsPanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<CharacterStatsPanel>();
        }

        private Font _font;
        private Text _label;
        private Text _buttonLabel;
        private GameObject _panel;
        private bool _open;
        private float _refresh;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();
            BuildUI();
            SetOpen(false);
        }

        private void Update()
        {
            if (!_open) return;
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f) { _refresh = 0.25f; RefreshText(); }
        }

        // ---------------------------------------------------------------- UI construction

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("StatsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // draw above the game's UI
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Toggle button — top-right corner.
            var btn = CreateButton(canvas.transform, "☰ Stats", Toggle);
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.sizeDelta = new Vector2(230f, 84f);
            brt.anchoredPosition = new Vector2(-24f, -24f);
            _buttonLabel = btn.GetComponentInChildren<Text>();

            // Panel — top-right, top edge at 1/5 (0.2) of screen height from the top (anchor y = 0.8).
            _panel = new GameObject("StatsPanel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            _panel.transform.SetParent(canvas.transform, false);
            var prt = _panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.8f);
            prt.pivot = new Vector2(1f, 1f);
            prt.sizeDelta = new Vector2(414f, 1180f); // ~2/3 of the previous width
            prt.anchoredPosition = new Vector2(-24f, 0f);
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.65f); // more translucent

            // Viewport (masked) inside the panel with a small inset border.
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(_panel.transform, false);
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(16f, 16f); vrt.offsetMax = new Vector2(-16f, -16f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // Content holder: a vertical layout with padding so text never touches (and gets clipped at)
            // the edges; sized to its content so the ScrollRect can scroll it.
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 16, 16);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The actual text lives in a child so the layout group can inset it.
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(content.transform, false);
            _label = textGO.GetComponent<Text>();
            _label.font = _font;
            _label.fontSize = 26;
            _label.color = Color.white;
            _label.supportRichText = true;
            _label.alignment = TextAnchor.UpperLeft;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            var scroll = _panel.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        private Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.85f, 0.95f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var labelGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var label = labelGO.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = 34;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        // ---------------------------------------------------------------- behaviour

        private void Toggle() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.SetActive(open);
            if (_buttonLabel != null) _buttonLabel.text = open ? "✕ Stats" : "☰ Stats";
            if (open) { _refresh = 0f; RefreshText(); }
        }

        private void RefreshText()
        {
            if (_label == null) return;
            var handler = StatsManager.GetStatsHandler(HandlerName);
            _sb.Clear();

            if (handler == null)
            {
                _sb.Append("<b>Character Stats</b>\n\n<color=#bbbbbb>Start a game to view stats.</color>");
                _label.text = _sb.ToString();
                return;
            }

            _sb.Append("<size=30><b>Character Stats</b></size>\n");

            // PoE-style summary up top: the two numbers that actually matter, derived so the player never
            // has to add stats up by hand.
            AppendSummary(handler);

            var shown = new HashSet<Stat>();
            foreach (var group in Groups)
            {
                bool header = false;
                foreach (var statName in group.names)
                {
                    var stat = handler.GetStat(statName);
                    if (stat == null) continue;
                    if (!header) { AppendHeader(group.category); header = true; }
                    shown.Add(stat);
                    AppendRow(stat);
                }
            }

            bool otherHeader = false;
            foreach (var stat in handler.m_Stats)
            {
                if (stat == null || shown.Contains(stat)) continue;
                if (!otherHeader) { AppendHeader("Other"); otherHeader = true; }
                AppendRow(stat);
            }

            _label.text = _sb.ToString();
        }

        // Derived headline numbers, PoE-style: DPS (avg hit × attack speed × crit factor) and Effective HP
        // (Life + Energy Shield). Computed from the live stat sheet so they always reflect gear + gems.
        private void AppendSummary(StatsHandler handler)
        {
            float min  = GetVal(handler, "Min Damage");
            float max  = GetVal(handler, "Max Damage");
            float spd  = GetVal(handler, "Attack Speed");
            float critC = GetVal(handler, "Critical Strike");      // %
            float critM = GetVal(handler, "Critical Multiplier");  // %, e.g. 150 = 150%
            if (critM <= 0f) critM = 150f;

            float avgHit = (min + max) * 0.5f;
            float critFactor = 1f + Mathf.Clamp01(critC / 100f) * (critM / 100f - 1f);
            float dps = avgHit * Mathf.Max(spd, 0f) * critFactor;

            float life   = GetVal(handler, "Heart");
            float shield = GetVal(handler, "Shield");
            float ehp    = life + shield;

            AppendHeader("Summary");
            _sb.Append("<color=#cfd2dc>DPS:</color>  <b><color=#ffd24d>").Append(dps.ToString("0")).Append("</color></b>\n");
            _sb.Append("<color=#cfd2dc>Effective HP:</color>  <b>").Append(ehp.ToString("0")).Append("</b>");
            if (shield > 0f)
                _sb.Append("  <size=20><color=#8a8f9c>(").Append(life.ToString("0")).Append(" + ")
                   .Append(shield.ToString("0")).Append(" ES)</color></size>");
            _sb.Append("\n");
        }

        private static float GetVal(StatsHandler handler, string name)
        {
            var stat = handler.GetStat(name);
            return stat != null ? stat.Value : 0f;
        }

        private void AppendHeader(string title)
        {
            _sb.Append("\n<color=#ffd24d><b>").Append(title).Append("</b></color>\n");
        }

        private void AppendRow(Stat stat)
        {
            string value;
            if (stat.Name == "Exp")
            {
                // Exp holds progress into the current level — show it as "x / threshold", where the
                // threshold is the EXP needed to advance FROM the current level (now level-dependent).
                var lvlStat = StatsManager.GetStatsHandler(HandlerName)?.GetStat("Level");
                int level = lvlStat != null ? Mathf.RoundToInt(lvlStat.Value) : 1;
                value = Mathf.RoundToInt(stat.Value) + " / " + PlayerStatService.ExpToNextLevel(level);
            }
            else if (stat is DevionGames.StatSystem.Attribute attr)
                value = Mathf.RoundToInt(attr.CurrentValue) + " / " + Mathf.RoundToInt(attr.Value);
            else
                value = FormatValue(stat.Name, stat.Value);

            _sb.Append("<color=#cfd2dc>").Append(stat.Name).Append(":</color>  <b>")
               .Append(value).Append("</b>\n");
        }

        private static string FormatValue(string statName, float value)
        {
            if (statName.Contains("Resistance") || statName == "Critical Strike" || statName == "Block Chance")
                return value.ToString("0.#") + "%";
            if (statName == "Critical Multiplier")
                return value.ToString("0") + "%";
            if (statName == "Attack Speed")
                return value.ToString("0.00");
            return value.ToString("0.##");
        }
    }
}
