using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// Runtime debug viewer that shows the equipped gear and its socketed gems/abilities as a collapsible
    /// dropdown tree. Each item is a clickable header row ([-] expanded / [+] collapsed); clicking folds
    /// its gem children in/out. Per-item expand state persists, and the rows are only rebuilt when the
    /// equipped set actually changes (no per-frame flicker).
    ///
    ///   [-] Iron Sword (Weapon)
    ///       |_ [Active] Cleave (Melee/Physical)
    ///       |_ [Support] Melee Physical Damage
    ///   [+] Iron Helm (Head)
    ///
    /// Self-instantiates (sibling to <see cref="CharacterStatsPanel"/>), docks top-left so it never
    /// overlaps the stats panel. Pure uGUI so it shares the game's EventSystem/input path.
    /// </summary>
    public class GemTreePanel : MonoBehaviour
    {
        // One equipped item plus the runtime objects that render it, so a toggle needn't rebuild the tree.
        private class TreeItem
        {
            public string key;
            public Text headerText;
            public string headerLabel; // header text without the [+]/[-] prefix
            public readonly List<GameObject> children = new List<GameObject>();
            public bool expanded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GemTreePanel>() != null) return;
            var go = new GameObject("[GemTreePanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<GemTreePanel>();
        }

        private Font _font;
        private Text _buttonLabel;
        private GameObject _panel;
        private RectTransform _content;
        private bool _open;
        private float _refresh;
        private PlayerView _cachedPlayer;
        private string _lastSignature;

        private readonly List<TreeItem> _items = new List<TreeItem>();
        // Expand state survives rebuilds (keyed by item identity). Default = expanded.
        private readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool>();
        private readonly StringBuilder _sig = new StringBuilder(256);

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
            if (_refresh <= 0f) { _refresh = 0.3f; RefreshTree(); }
        }

        // ---------------------------------------------------------------- UI scaffold

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("GemTreeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Toggle button — top-LEFT (the stats button lives top-right).
            var btn = CreateButton(canvas.transform, "☰ Gems", new Color(0.55f, 0.35f, 0.85f, 0.95f), Toggle);
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(230f, 84f);
            brt.anchoredPosition = new Vector2(24f, -24f);
            _buttonLabel = btn.GetComponentInChildren<Text>();

            // Panel — top-LEFT, top edge at 1/5 of screen height from the top (anchor y = 0.8).
            _panel = new GameObject("GemTreePanel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            _panel.transform.SetParent(canvas.transform, false);
            var prt = _panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.8f);
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(480f, 1180f);
            prt.anchoredPosition = new Vector2(24f, 0f);
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.7f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(_panel.transform, false);
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(14f, 14f); vrt.offsetMax = new Vector2(-14f, -14f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewport.transform, false);
            _content = contentGO.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = _panel.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // A persistent title row at the top.
            var title = CreateText(_content, "<size=30><b>Gems / Abilities</b></size>", 26);
            title.raycastTarget = false;
        }

        private Button CreateButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var labelGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var label = labelGO.GetComponent<Text>();
            label.font = _font; label.text = text; label.fontSize = 34; label.fontStyle = FontStyle.Bold;
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        private Text CreateText(Transform parent, string text, int size)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = Color.white;
            t.supportRichText = true; t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---------------------------------------------------------------- behaviour

        private void Toggle() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.SetActive(open);
            if (_buttonLabel != null) _buttonLabel.text = open ? "✕ Gems" : "☰ Gems";
            if (open) { _refresh = 0f; _lastSignature = null; RefreshTree(); }
        }

        private PlayerView GetPlayerView()
        {
            if (_cachedPlayer == null) _cachedPlayer = FindAnyObjectByType<PlayerView>();
            return _cachedPlayer;
        }

        private static ItemContainer FindEquipmentContainer()
        {
            var found = DevionGames.UIWidgets.WidgetUtility.FindAll<ItemContainer>("Equipment");
            return (found != null && found.Length > 0) ? found[0] : null;
        }

        private bool GetExpanded(string key) => !_expanded.TryGetValue(key, out bool v) || v; // default expanded

        private void RefreshTree()
        {
            var pv = GetPlayerView();
            var container = pv != null ? FindEquipmentContainer() : null;

            // Build a signature of the equipped items + their gems; only rebuild when it changes.
            string signature = BuildSignature(pv, container);
            if (signature == _lastSignature) return;
            _lastSignature = signature;

            ClearRows();

            if (pv == null || container == null)
            {
                var msg = CreateText(_content, "\n<color=#bbbbbb>Start a game and equip gear to view sockets.</color>", 24);
                msg.raycastTarget = false;
                return;
            }

            bool any = false;
            foreach (var slot in container.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                if (!(slot.ObservedItem is CurveDashEquipmentAdapter adapter) || adapter.OriginalEquipmentData == null) continue;

                var data = adapter.OriginalEquipmentData;
                BuildItemRows(pv, $"{SlotLabel(data)}::{data.name}", adapter);
                any = true;
            }

            if (!any)
            {
                var msg = CreateText(_content, "\n<color=#bbbbbb>No equipment found in the Equipment container.</color>", 24);
                msg.raycastTarget = false;
            }
        }

        private void ClearRows()
        {
            _items.Clear();
            // Keep the title row (index 0); destroy everything created afterwards.
            for (int i = _content.childCount - 1; i >= 1; i--)
                Destroy(_content.GetChild(i).gameObject);
        }

        private void BuildItemRows(PlayerView pv, string key, CurveDashEquipmentAdapter adapter)
        {
            var data = adapter.OriginalEquipmentData;
            var abilities = ResolveAbilities(pv, data);
            var stats = ResolveStats(adapter);
            int gemCount = abilities != null ? abilities.Count : 0;

            var item = new TreeItem { key = key, expanded = GetExpanded(key) };
            item.headerLabel = $"<color=#ffd24d><b>{EscapeRich(data.ItemName)}</b></color>" +
                               $" <size=20><color=#8a8f9c>({SlotLabel(data)} · {stats.Count} stat{(stats.Count == 1 ? "" : "s")} · {gemCount} gem{(gemCount == 1 ? "" : "s")})</color></size>";

            // Header (clickable dropdown row).
            var headerGO = new GameObject("ItemHeader", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            headerGO.transform.SetParent(_content, false);
            headerGO.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.26f, 0.95f);
            headerGO.GetComponent<LayoutElement>().minHeight = 46f;

            var htGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            htGO.transform.SetParent(headerGO.transform, false);
            var hrt = htGO.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(12f, 0f); hrt.offsetMax = new Vector2(-8f, 0f);
            item.headerText = htGO.GetComponent<Text>();
            item.headerText.font = _font; item.headerText.fontSize = 25; item.headerText.color = Color.white;
            item.headerText.supportRichText = true; item.headerText.alignment = TextAnchor.MiddleLeft;
            item.headerText.horizontalOverflow = HorizontalWrapMode.Wrap; item.headerText.verticalOverflow = VerticalWrapMode.Overflow;
            item.headerText.raycastTarget = false;

            headerGO.GetComponent<Button>().onClick.AddListener(() => ToggleItem(item));

            // --- Stats section ---
            item.children.Add(CreateChild("   <b><color=#cfd2dc>Stats</color></b>"));
            if (stats.Count == 0)
                item.children.Add(CreateChild("       • <color=#777777>(none)</color>"));
            else
                foreach (var line in stats)
                    item.children.Add(CreateChild("       • " + line));

            // --- Gems section ---
            item.children.Add(CreateChild("   <b><color=#cfd2dc>Gems</color></b>"));
            if (gemCount == 0)
                item.children.Add(CreateChild("       • <color=#777777>(none socketed)</color>"));
            else
                foreach (var ab in abilities)
                    item.children.Add(CreateChild("       • " + AbilityLabel(ab)));

            _items.Add(item);
            ApplyItemState(item);
        }

        private GameObject CreateChild(string text)
        {
            var t = CreateText(_content, text, 23);
            t.raycastTarget = false;
            return t.gameObject;
        }

        private void ToggleItem(TreeItem item)
        {
            item.expanded = !item.expanded;
            _expanded[item.key] = item.expanded;
            ApplyItemState(item);
        }

        // Reflects an item's expand state onto its header arrow and child visibility.
        private void ApplyItemState(TreeItem item)
        {
            string arrow = item.expanded ? "<color=#9bff9b>[-]</color> " : "<color=#ffb37f>[+]</color> ";
            if (item.headerText != null) item.headerText.text = arrow + item.headerLabel;
            foreach (var child in item.children)
                if (child != null) child.SetActive(item.expanded);
        }

        // ---------------------------------------------------------------- data

        private string BuildSignature(PlayerView pv, ItemContainer container)
        {
            _sig.Clear();
            if (pv == null || container == null) return "none";
            foreach (var slot in container.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                if (!(slot.ObservedItem is CurveDashEquipmentAdapter adapter) || adapter.OriginalEquipmentData == null) continue;
                var data = adapter.OriginalEquipmentData;
                _sig.Append(data.name).Append('{');
                foreach (var ab in ResolveAbilities(pv, data))
                    _sig.Append(ab != null ? ab.AbilityName : "?").Append(',');
                // Rolled affixes affect the displayed stats → include them so the tree rebuilds on change.
                if (adapter.RolledAffixes != null)
                    foreach (var m in adapter.RolledAffixes)
                        if (m != null) _sig.Append(m.Type).Append(m.Value).Append(';');
                _sig.Append("}|");
            }
            return _sig.ToString();
        }

        // Item stats for display: base weapon damage, projected typed/universal stats, and rolled affixes.
        private static List<string> ResolveStats(CurveDashEquipmentAdapter adapter)
        {
            var lines = new List<string>();
            var data = adapter.OriginalEquipmentData;

            if (data is WeaponData w)
                lines.Add($"+{Mathf.RoundToInt(w.BaseMinDamage)}–{Mathf.RoundToInt(w.BaseMaxDamage)} Base Damage");

            var baseMods = data.GetStatModifiers();
            if (baseMods != null)
                foreach (var m in baseMods)
                    if (m != null) lines.Add(FormatStatMod(m, false));

            if (adapter.RolledAffixes != null)
                foreach (var m in adapter.RolledAffixes)
                    if (m != null) lines.Add(FormatStatMod(m, true));

            return lines;
        }

        private static string FormatStatMod(StatModifier m, bool rolled)
        {
            string v = (m.Value >= 0 ? "+" : "") + m.Value.ToString("0.#");
            string label = $"<color=#cdd6e0>{v} {PrettyStat(m.Type)}</color>";
            // Rolled loot affixes also show their affix name in a magic-ish colour.
            return (rolled && !string.IsNullOrEmpty(m.AffixName))
                ? $"<color=#b99cff>{EscapeRich(m.AffixName)}</color> {label}"
                : label;
        }

        // "AddedPhysicalDamage" -> "Added Physical Damage" (spaces before capitals).
        private static string PrettyStat(StatType t)
        {
            string s = t.ToString();
            var sb = new StringBuilder(s.Length + 6);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static List<AbilityData> ResolveAbilities(PlayerView pv, ItemData data)
        {
            var list = new List<AbilityData>();
            if (data is WeaponData)
            {
                if (pv.CurrentWeaponInstance != null && pv.CurrentWeaponInstance.BaseData == data)
                {
                    var a = pv.CurrentWeaponInstance.GetAbilities();
                    if (a != null) AddUnique(list, a);
                }
            }
            else if (data is ArmorItemData armor)
            {
                if (armor.Abilities != null) AddUnique(list, armor.Abilities);
                AddUnique(list, pv.GetArmorRuntimeSockets(armor.Slot));
            }
            else if (data is OffHandData off)
            {
                if (off.Abilities != null) AddUnique(list, off.Abilities);
            }
            return list;
        }

        private static void AddUnique(List<AbilityData> into, IEnumerable<AbilityData> src)
        {
            foreach (var ab in src)
                if (ab != null && !into.Contains(ab)) into.Add(ab);
        }

        private static string AbilityLabel(AbilityData ab)
        {
            if (ab == null) return "<color=#777777>(empty socket)</color>";
            if (ab is SupportAbilityData support)
                return "<color=#7fd1ff>[Support]</color> " + EscapeRich(support.AbilityName);
            if (ab is PoEAbility poe)
            {
                bool aura = poe.SkillType == PoEAbilityType.Aura;
                string tag = aura ? "[Aura]" : "[Active]";
                string col = aura ? "#c79bff" : "#9bff9b";
                return $"<color={col}>{tag}</color> {EscapeRich(poe.AbilityName)} " +
                       $"<size=18><color=#8a8f9c>({poe.SkillType}/{poe.Element})</color></size>";
            }
            return EscapeRich(ab.AbilityName);
        }

        private static string SlotLabel(ItemData data)
        {
            switch (data)
            {
                case WeaponData _:     return "Weapon";
                case OffHandData off:  return "Off-Hand: " + off.SubType;
                case ArmorItemData a:  return a.Slot.ToString();
                case AmuletItemData _: return "Amulet";
                case RingItemData r:   return r.Slot.ToString();
                case BeltItemData _:   return "Belt";
                default:               return data.Type.ToString();
            }
        }

        private static string EscapeRich(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹");
    }
}
