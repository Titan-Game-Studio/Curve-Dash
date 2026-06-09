using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// PoE-style gem sockets surfaced inside the Devion "Equipment" window, designed for touch / mobile:
    ///   • draws a socket-pip strip on every equipped item slot (● = gem, tinted by its socket colour;
    ///     ○ = empty) so the player can see at a glance how many sockets a piece has and which are filled;
    ///   • a tap-driven popup — opened by tapping the pip strip — to read each gem's stats, remove a gem
    ///     (→ bag), or socket / replace a gem chosen
    ///     from the bag. No hover and no drag (both unreliable on mobile): everything is plain taps.
    ///
    /// Self-bootstraps and finds the live <see cref="PlayerView"/> / Devion containers at runtime, so it
    /// needs no scene wiring. Gem mutations are delegated to PlayerView (UnsocketGem / SocketGemIntoSlot /
    /// ReplaceGemAtSlot) which owns the runtime sockets and persistence.
    /// </summary>
    public class EquipmentSocketUI : MonoBehaviour
    {
        private static EquipmentSocketUI _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[EquipmentSocketUI]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<EquipmentSocketUI>();
        }

        // ---------------------------------------------------------------- public API (Devion ItemSlot hook)

        /// <summary>True if this Devion item maps to game gear that has at least one socket.</summary>
        public static bool HasSockets(Item item)
        {
            return item is CurveDashEquipmentAdapter a && a.OriginalEquipmentData != null
                   && GetMaxSockets(a.OriginalEquipmentData) > 0;
        }

        /// <summary>Opens the socket popup for the given Devion equipment item (called by the context menu).</summary>
        public static void OpenFor(Item item)
        {
            if (_instance == null) Bootstrap();
            if (_instance != null) _instance.OpenPopup(item as CurveDashEquipmentAdapter);
        }

        // ---------------------------------------------------------------- state

        private enum Mode { Sockets, Picker }

        private Font _font;
        private Canvas _canvas;
        private GameObject _popup;
        private RectTransform _content;
        private Text _titleText;

        private PlayerView _cachedPlayer;

        private Mode _mode = Mode.Sockets;
        private CurveDashEquipmentAdapter _target;        // item whose sockets we are editing
        private EquipmentSlot _targetSlot;
        private AbilityData _pickerReplaceAbility;         // null → socket an empty slot; else → replace this gem

        private readonly StringBuilder _sb = new StringBuilder(64);

        private SocketRowView _rowPrefab;   // Resources/UI/SocketRow — Text + ✕ + ↻ on one row
        private bool _rowPrefabTried;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();
            BuildUI();
            ShowPopup(false);
        }

        // ---------------------------------------------------------------- lookups

        private PlayerView GetPlayerView()
        {
            if (_cachedPlayer == null) _cachedPlayer = FindAnyObjectByType<PlayerView>();
            return _cachedPlayer;
        }

        private static ItemContainer GetInventoryContainer()
        {
            var found = DevionGames.UIWidgets.WidgetUtility.FindAll<ItemContainer>("Inventory");
            return (found != null && found.Length > 0) ? found[0] : null;
        }

        // ---------------------------------------------------------------- popup scaffold

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("EquipmentSocketCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 31000; // above Devion UI and the GemTreePanel overlay
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen dim backdrop that also closes the popup when tapped outside.
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(_canvas.transform, false);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            dim.GetComponent<Button>().onClick.AddListener(() => ShowPopup(false));

            _popup = new GameObject("SocketPopup", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            _popup.transform.SetParent(dim.transform, false);
            var prt = _popup.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760f, 1180f);
            prt.anchoredPosition = Vector2.zero;
            _popup.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);

            // Header (title + close button).
            _titleText = NewText(_popup.transform, "", 34, TextAnchor.UpperLeft);
            var hrt = _titleText.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.offsetMin = new Vector2(28f, -84f); hrt.offsetMax = new Vector2(-110f, -24f);
            _titleText.raycastTarget = false;

            var close = CreateButton(_popup.transform, "✕", new Color(0.5f, 0.2f, 0.22f, 1f), () => ShowPopup(false));
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.sizeDelta = new Vector2(72f, 72f);
            crt.anchoredPosition = new Vector2(-20f, -20f);

            // Scrollable content body.
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(_popup.transform, false);
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(20f, 20f); vrt.offsetMax = new Vector2(-20f, -100f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewport.transform, false);
            _content = contentGO.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 12, 12); // generous side padding so rich-text rows never clip at the mask edge
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = _popup.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = _content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        // ---------------------------------------------------------------- popup behaviour

        private void OpenPopup(CurveDashEquipmentAdapter adapter)
        {
            if (adapter == null || adapter.OriginalEquipmentData == null) return;
            if (GetMaxSockets(adapter.OriginalEquipmentData) <= 0) return;

            _target = adapter;
            _targetSlot = GetItemSlot(adapter.OriginalEquipmentData);
            _mode = Mode.Sockets;
            _pickerReplaceAbility = null;
            ShowPopup(true);
            Rebuild();
        }

        private void ShowPopup(bool show)
        {
            if (_popup != null) _popup.transform.parent.gameObject.SetActive(show); // toggle the dim backdrop too
        }

        private void Rebuild()
        {
            ClearContent();
            if (_target == null || _target.OriginalEquipmentData == null) { ShowPopup(false); return; }

            if (_mode == Mode.Sockets) BuildSocketsView();
            else BuildPickerView();
        }

        private void BuildSocketsView()
        {
            var pv = GetPlayerView();
            var data = _target.OriginalEquipmentData;
            int max = GetMaxSockets(data);
            var runtime = pv != null ? GetRuntimeGems(pv, data, _targetSlot) : new List<AbilityData>();

            _titleText.text = $"<b>{EscapeRich(data.ItemName)}</b>  <size=24><color=#8a8f9c>Sockets {runtime.Count}/{max}</color></size>  {SocketPips(pv, runtime, max)}";

            // Detailed character DPS up top — gem/support changes here pay off in this number.
            BuildDpsSection();

            // Filled sockets: one row each — gem label + Remove (✕) + Replace (↻) on a single line.
            for (int i = 0; i < runtime.Count; i++)
            {
                var ab = runtime[i];
                var gem = pv != null ? pv.FindGemByAbility(ab) : null;
                var capturedAbility = ab;
                AddFilledSocketRow(gem, ab,
                    () => DoRemove(capturedAbility),
                    () => OpenPicker(capturedAbility));
            }

            // Empty sockets: a Socket action each.
            for (int i = runtime.Count; i < max; i++)
            {
                var rowGO = new GameObject("Empty", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                rowGO.transform.SetParent(_content, false);
                rowGO.GetComponent<LayoutElement>().minHeight = 70f;
                var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10f; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
                hlg.childControlWidth = true; hlg.childControlHeight = true;

                NewText(rowGO.transform, "<color=#888888>○ Empty socket</color>", 26, TextAnchor.MiddleLeft).raycastTarget = false;
                CreateButton(rowGO.transform, "＋ Socket gem", new Color(0.25f, 0.5f, 0.32f, 1f), () => OpenPicker(null));
            }
        }

        // Adds one filled-socket row. Uses the SocketRow prefab (Text + ✕ + ↻ on one line) when present,
        // otherwise builds the same single-row layout in code so the popup still works without the asset.
        private void AddFilledSocketRow(GemItemData gem, AbilityData ab,
            UnityEngine.Events.UnityAction onRemove, UnityEngine.Events.UnityAction onReplace)
        {
            var stats = GemStatLines(ab);
            string summary = stats.Count > 0
                ? "  <size=20><color=#9bb0c4>" + string.Join(" · ", stats.ToArray()) + "</color></size>"
                : "";
            string label = $"{ColorPip(gem)} {AbilityLabel(ab)}{summary}";

            var prefab = GetRowPrefab();
            if (prefab != null)
            {
                var row = Instantiate(prefab, _content);
                if (row.Label != null) row.Label.text = label;
                if (row.RemoveButton != null) { row.RemoveButton.onClick.RemoveAllListeners(); row.RemoveButton.onClick.AddListener(onRemove); }
                if (row.ReplaceButton != null) { row.ReplaceButton.onClick.RemoveAllListeners(); row.ReplaceButton.onClick.AddListener(onReplace); }
                return;
            }

            // Code fallback: gem label expands, two compact icon buttons sit at the right of the same row.
            var rowGO = new GameObject("SocketRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGO.transform.SetParent(_content, false);
            rowGO.GetComponent<LayoutElement>().minHeight = 84f;
            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childAlignment = TextAnchor.MiddleLeft;

            var lbl = NewText(rowGO.transform, label, 26, TextAnchor.MiddleLeft);
            lbl.raycastTarget = false;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            IconButton(rowGO.transform, "✕", new Color(0.62f, 0.22f, 0.26f, 1f), onRemove);
            IconButton(rowGO.transform, "↻", new Color(0.30f, 0.34f, 0.55f, 1f), onReplace);
        }

        private SocketRowView GetRowPrefab()
        {
            if (!_rowPrefabTried)
            {
                _rowPrefabTried = true;
                _rowPrefab = Resources.Load<SocketRowView>("UI/SocketRow");
            }
            return _rowPrefab;
        }

        private void BuildPickerView()
        {
            var bag = CollectBagGems();
            _titleText.text = _pickerReplaceAbility != null
                ? "<b>Choose a gem to replace</b>"
                : "<b>Choose a gem to socket</b>";

            CreateButton(_content, "‹ Back", new Color(0.28f, 0.3f, 0.36f, 1f), () => { _mode = Mode.Sockets; _pickerReplaceAbility = null; Rebuild(); });

            if (bag.Count == 0)
            {
                NewText(_content, "\n<color=#bbbbbb>No gems in your bag.</color>", 26, TextAnchor.UpperLeft).raycastTarget = false;
                return;
            }

            foreach (var entry in bag)
            {
                var gem = entry.Gem;
                var capturedInstance = entry.Instance;
                var capturedGem = gem;

                string label = $"{ColorPip(gem)}  {AbilityLabel(gem.EmbeddedAbility)}";
                var statLines = GemStatLines(gem.EmbeddedAbility);
                if (statLines.Count > 0) label += "  <size=20><color=#9bb0c4>" + string.Join(" · ", statLines.ToArray()) + "</color></size>";

                CreateButton(_content, label, new Color(0.16f, 0.18f, 0.26f, 1f), () => DoSocketFromBag(capturedGem, capturedInstance));
            }
        }

        // ---------------------------------------------------------------- actions

        private void DoRemove(AbilityData ability)
        {
            var pv = GetPlayerView();
            if (pv != null && pv.UnsocketGem(_targetSlot, ability)) Rebuild();
        }

        private void OpenPicker(AbilityData replaceAbility)
        {
            _pickerReplaceAbility = replaceAbility;
            _mode = Mode.Picker;
            Rebuild();
        }

        private void DoSocketFromBag(GemItemData gem, Item instance)
        {
            var pv = GetPlayerView();
            if (pv == null || gem == null) return;

            bool ok = _pickerReplaceAbility != null
                ? pv.ReplaceGemAtSlot(_targetSlot, _pickerReplaceAbility, gem)
                : pv.SocketGemIntoSlot(_targetSlot, gem);

            if (ok)
            {
                // Consume the gem we just socketed from the bag.
                var inv = GetInventoryContainer();
                if (inv != null && instance != null) inv.RemoveItem(instance);
            }

            _mode = Mode.Sockets;
            _pickerReplaceAbility = null;
            Rebuild();
        }

        private struct BagGem { public Item Instance; public GemItemData Gem; }

        private List<BagGem> CollectBagGems()
        {
            var list = new List<BagGem>();
            var inv = GetInventoryContainer();
            if (inv == null) return list;

            foreach (var slot in inv.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                if (slot.ObservedItem is CurveDashItemAdapter adapter && adapter.OriginalItemData is GemItemData gem && gem.EmbeddedAbility != null)
                    list.Add(new BagGem { Instance = slot.ObservedItem, Gem = gem });
            }
            return list;
        }

        // ---------------------------------------------------------------- UI helpers

        private void ClearContent()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
        }

        private Button CreateButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<LayoutElement>().minHeight = 66f;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var labelGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(14f, 0f); lrt.offsetMax = new Vector2(-14f, 0f);
            var label = labelGO.GetComponent<Text>();
            label.font = _font; label.text = text; label.fontSize = 26;
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
            label.supportRichText = true; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return go.GetComponent<Button>();
        }

        // A compact, fixed-width square icon button (for the per-gem Remove / Replace actions).
        private Button IconButton(Transform parent, string glyph, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var btn = CreateButton(parent, glyph, color, onClick);
            var le = btn.GetComponent<LayoutElement>();
            le.preferredWidth = 96f; le.flexibleWidth = 0f; le.minWidth = 96f;
            var label = btn.GetComponentInChildren<Text>();
            if (label != null) label.fontSize = 38;
            return btn;
        }

        private Text NewText(Transform parent, string text, int size, TextAnchor anchor)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = Color.white; t.supportRichText = true;
            t.alignment = anchor; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        // ---------------------------------------------------------------- socket data / formatting

        private static EquipmentSlot GetItemSlot(ItemData data)
        {
            switch (data)
            {
                case WeaponData _:    return EquipmentSlot.MainHand;
                case OffHandData _:   return EquipmentSlot.OffHand;
                case ArmorItemData a: return a.Slot;
                default:              return EquipmentSlot.MainHand;
            }
        }

        private static int GetMaxSockets(ItemData data)
        {
            switch (data)
            {
                case WeaponData w:    return w.MaxSockets;
                case OffHandData o:   return o.MaxSockets;
                case ArmorItemData a: return a.MaxSockets;
                default:              return 0;
            }
        }

        private static List<AbilityData> GetRuntimeGems(PlayerView pv, ItemData data, EquipmentSlot slot)
        {
            var list = new List<AbilityData>();
            if (data is WeaponData)
            {
                if (pv.CurrentWeaponInstance != null && pv.CurrentWeaponInstance.BaseData == data)
                    foreach (var a in pv.CurrentWeaponInstance.DynamicAbilities)
                        if (a != null) list.Add(a);
            }
            else
            {
                foreach (var a in pv.GetArmorRuntimeSockets(slot))
                    if (a != null) list.Add(a);
            }
            return list;
        }

        // Character DPS breakdown, computed from the live stat sheet (same model as CharacterStatsPanel)
        // so the player sees the concrete payoff of socketing / swapping a gem without leaving the popup.
        private void BuildDpsSection()
        {
            var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
            if (handler == null) return;

            float min   = StatVal(handler, "Min Damage");
            float max   = StatVal(handler, "Max Damage");
            float spd   = StatVal(handler, "Attack Speed");
            float critC = StatVal(handler, "Critical Strike");      // %
            float critM = StatVal(handler, "Critical Multiplier");  // %
            if (critM <= 0f) critM = 150f;

            float avg = (min + max) * 0.5f;
            float critFactor = 1f + Mathf.Clamp01(critC / 100f) * (critM / 100f - 1f);
            float dps = avg * Mathf.Max(spd, 0f) * critFactor;

            NewText(_content, $"<b><color=#ffd24d>DPS  {dps:0}</color></b>", 30, TextAnchor.UpperLeft).raycastTarget = false;
            NewText(_content, $"<color=#9bb0c4>Hit {min:0}–{max:0} (avg {avg:0}) × {spd:0.00} aps</color>", 22, TextAnchor.UpperLeft).raycastTarget = false;
            NewText(_content, $"<color=#9bb0c4>Crit {critC:0.#}% · multi {critM:0}% → ×{critFactor:0.00}</color>", 22, TextAnchor.UpperLeft).raycastTarget = false;
            NewText(_content, "<color=#3a3f4a>────────────</color>", 20, TextAnchor.UpperLeft).raycastTarget = false;
        }

        private static float StatVal(DevionGames.StatSystem.StatsHandler handler, string name)
        {
            var s = handler.GetStat(name);
            return s != null ? s.Value : 0f;
        }

        private string SocketPips(PlayerView pv, List<AbilityData> runtime, int max)
        {
            if (max <= 0) return "";
            _sb.Clear();
            for (int i = 0; i < max; i++)
            {
                if (i < runtime.Count) _sb.Append(ColorPip(pv != null ? pv.FindGemByAbility(runtime[i]) : null));
                else _sb.Append("<color=#555555>○</color>");
            }
            return _sb.ToString();
        }

        private static string ColorPip(GemItemData gem)
        {
            Color c = gem != null ? gem.SocketColor : new Color(0.4f, 0.9f, 0.4f);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>●</color>";
        }

        private static List<string> GemStatLines(AbilityData ab)
        {
            var lines = new List<string>();
            if (ab == null) return lines;

            if (ab is PoEAbility poe)
            {
                lines.Add($"{poe.SkillType} · {poe.Element}");
                var parts = new List<string>();
                if (Mathf.Abs(poe.DamageMultiplier - 1f) > 0.001f) parts.Add($"Dmg ×{poe.DamageMultiplier:0.##}");
                if (poe.AddedFlatDamage != 0f) parts.Add($"+{poe.AddedFlatDamage:0.#} flat");
                if (poe.CriticalChanceBonus != 0f) parts.Add($"+{poe.CriticalChanceBonus:0.#}% crit");
                if (poe.ProjectileCount > 1) parts.Add($"{poe.ProjectileCount} proj");
                if (poe.SkillType == PoEAbilityType.Aura) parts.Add($"{poe.Duration:0.#}s");
                if (parts.Count > 0) lines.Add(string.Join(", ", parts.ToArray()));
            }
            else if (ab is SupportAbilityData sup)
            {
                if (sup.Modifiers != null)
                    foreach (var m in sup.Modifiers)
                        if (m != null) lines.Add(SupportModLabel(m));
                if (lines.Count == 0 && !string.IsNullOrEmpty(sup.SupportDescription))
                    lines.Add(EscapeRich(sup.SupportDescription));
            }
            return lines;
        }

        private static string SupportModLabel(SupportModifier m)
        {
            string val = m.Form == SupportForm.Flat
                ? (m.Value >= 0 ? "+" : "") + m.Value.ToString("0.#")
                : (m.Value >= 0 ? "+" : "") + m.Value.ToString("0.#") + "%";
            return $"{m.Form} {m.Stat} {val}";
        }

        private static string AbilityLabel(AbilityData ab)
        {
            if (ab == null) return "<color=#777777>(empty)</color>";
            if (ab is SupportAbilityData support)
                return "<color=#7fd1ff>[S]</color> " + EscapeRich(support.AbilityName);
            if (ab is PoEAbility poe)
            {
                bool aura = poe.SkillType == PoEAbilityType.Aura;
                string tag = aura ? "[Aura]" : "[A]";
                string col = aura ? "#c79bff" : "#9bff9b";
                return $"<color={col}>{tag}</color> {EscapeRich(poe.AbilityName)}";
            }
            return EscapeRich(ab.AbilityName);
        }

        private static string EscapeRich(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹");
    }
}
