using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// Runtime debug viewer of equipped gear as a nested, collapsible dropdown tree:
    ///
    ///   [-] Iron Sword (Weapon)
    ///       [-] Stats (3)
    ///           • +5–12 Phys
    ///           • +8 Fire
    ///       [+] Gems (2)
    ///
    /// Every foldout (item, and the Stats / Gems sub-groups) toggles independently; stat lines are kept
    /// terse (short stat label + value, rolled affixes tinted). Rows are rebuilt only when the equipped
    /// set/affixes change. Self-instantiates top-left (never overlaps <see cref="CharacterStatsPanel"/>).
    /// </summary>
    public class GemTreePanel : MonoBehaviour
    {
        // A row in the tree: a clickable foldout (item / Stats / Gems) or a plain leaf line.
        private class Node
        {
            public string key;
            public GameObject go;
            public Text headerText;   // foldouts only
            public string headerLabel;
            public bool isFoldout;
            public bool expanded;
            public int depth;
            public readonly List<Node> children = new List<Node>();
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

        private readonly List<Node> _roots = new List<Node>();
        private readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool>(); // by node key, default expanded
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

            var btn = CreateButton(canvas.transform, "☰ Gems", new Color(0.55f, 0.35f, 0.85f, 0.95f), Toggle);
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(230f, 84f);
            brt.anchoredPosition = new Vector2(24f, -24f);
            _buttonLabel = btn.GetComponentInChildren<Text>();

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

            var title = NewText(_content, "<size=30><b>Gems / Abilities</b></size>", 26, TextAnchor.UpperLeft);
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

        private void ConfigText(Text t, int size, TextAnchor anchor)
        {
            t.font = _font; t.fontSize = size; t.color = Color.white; t.supportRichText = true;
            t.alignment = anchor; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private Text NewText(Transform parent, string text, int size, TextAnchor anchor)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            ConfigText(t, size, anchor);
            t.text = text;
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

        // ---------------------------------------------------------------- tree build / refresh

        private void RefreshTree()
        {
            var pv = GetPlayerView();
            var container = pv != null ? FindEquipmentContainer() : null;

            string signature = BuildSignature(pv, container);
            if (signature == _lastSignature) return;
            _lastSignature = signature;

            ClearRows();

            if (pv == null || container == null)
            {
                var msg = NewText(_content, "\n<color=#bbbbbb>Start a game and equip gear to view sockets.</color>", 24, TextAnchor.UpperLeft);
                msg.raycastTarget = false;
                return;
            }

            bool any = false;
            foreach (var slot in container.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                if (!(slot.ObservedItem is CurveDashEquipmentAdapter adapter) || adapter.OriginalEquipmentData == null) continue;
                BuildItemNode(pv, adapter);
                any = true;
            }

            if (!any)
            {
                var msg = NewText(_content, "\n<color=#bbbbbb>No equipment found in the Equipment container.</color>", 24, TextAnchor.UpperLeft);
                msg.raycastTarget = false;
            }

            ApplyVisibility();
        }

        private void ClearRows()
        {
            _roots.Clear();
            for (int i = _content.childCount - 1; i >= 1; i--) // keep the title (index 0)
                Destroy(_content.GetChild(i).gameObject);
        }

        private void BuildItemNode(PlayerView pv, CurveDashEquipmentAdapter adapter)
        {
            var data = adapter.OriginalEquipmentData;
            var stats = ResolveStats(adapter);
            var abilities = ResolveAbilities(pv, data);
            int gemCount = abilities != null ? abilities.Count : 0;
            string baseKey = $"{SlotLabel(data)}::{data.name}";

            string itemLabel = $"<color=#ffd24d><b>{EscapeRich(data.ItemName)}</b></color>" +
                               $" <size=20><color=#8a8f9c>({SlotLabel(data)})</color></size>";
            var itemNode = CreateFoldout(baseKey, itemLabel, 0, boxed: true);
            _roots.Add(itemNode);

            // Stats sub-dropdown.
            var statsNode = CreateFoldout(baseKey + "/stats", $"Stats <color=#8a8f9c>({stats.Count})</color>", 1, boxed: false);
            itemNode.children.Add(statsNode);
            if (stats.Count == 0) statsNode.children.Add(CreateLeaf("<color=#777777>(none)</color>", 2));
            else foreach (var line in stats) statsNode.children.Add(CreateLeaf(line, 2));

            // Gems sub-dropdown.
            var gemsNode = CreateFoldout(baseKey + "/gems", $"Gems <color=#8a8f9c>({gemCount})</color>", 1, boxed: false);
            itemNode.children.Add(gemsNode);
            if (gemCount == 0) gemsNode.children.Add(CreateLeaf("<color=#777777>(none)</color>", 2));
            else foreach (var ab in abilities) gemsNode.children.Add(CreateLeaf(AbilityLabel(ab), 2));
        }

        private Node CreateFoldout(string key, string label, int depth, bool boxed)
        {
            var node = new Node { key = key, isFoldout = true, headerLabel = label, depth = depth, expanded = GetExpanded(key) };

            if (boxed)
            {
                var go = new GameObject("Foldout", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_content, false);
                go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.26f, 0.95f);
                go.GetComponent<LayoutElement>().minHeight = 46f;

                var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
                txtGO.transform.SetParent(go.transform, false);
                var rt = txtGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(12f, 0f); rt.offsetMax = new Vector2(-8f, 0f);
                node.headerText = txtGO.GetComponent<Text>();
                ConfigText(node.headerText, 25, TextAnchor.MiddleLeft);
                node.headerText.raycastTarget = false;
                node.go = go;
                go.GetComponent<Button>().onClick.AddListener(() => ToggleNode(node));
            }
            else
            {
                var go = new GameObject("Foldout", typeof(RectTransform), typeof(Text), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_content, false);
                go.GetComponent<LayoutElement>().minHeight = 36f;
                node.headerText = go.GetComponent<Text>();
                ConfigText(node.headerText, 24, TextAnchor.MiddleLeft);
                node.headerText.raycastTarget = true; // the Button uses this graphic to receive clicks
                node.go = go;
                go.GetComponent<Button>().onClick.AddListener(() => ToggleNode(node));
            }
            return node;
        }

        private Node CreateLeaf(string text, int depth)
        {
            var go = new GameObject("Leaf", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_content, false);
            var t = go.GetComponent<Text>();
            ConfigText(t, 23, TextAnchor.UpperLeft);
            t.raycastTarget = false;
            t.text = Indent(depth) + "• " + text;
            return new Node { go = go, isFoldout = false, depth = depth };
        }

        private void ToggleNode(Node node)
        {
            node.expanded = !node.expanded;
            _expanded[node.key] = node.expanded;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            foreach (var root in _roots) ApplyNode(root, true);
        }

        private void ApplyNode(Node n, bool parentVisible)
        {
            if (n.go != null) n.go.SetActive(parentVisible);
            if (n.isFoldout && n.headerText != null)
            {
                string arrow = n.expanded ? "<color=#9bff9b>[-]</color> " : "<color=#ffb37f>[+]</color> ";
                n.headerText.text = Indent(n.depth) + arrow + n.headerLabel;
            }
            bool childVisible = parentVisible && (!n.isFoldout || n.expanded);
            foreach (var c in n.children) ApplyNode(c, childVisible);
        }

        private static string Indent(int depth) => depth <= 0 ? "" : new string(' ', depth * 4);

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
                if (adapter.RolledAffixes != null)
                    foreach (var m in adapter.RolledAffixes)
                        if (m != null) _sig.Append(m.Type).Append(m.Value).Append(';');
                _sig.Append("}|");
            }
            return _sig.ToString();
        }

        // Concise item stats: base weapon damage, projected typed/universal stats, and rolled affixes.
        private static List<string> ResolveStats(CurveDashEquipmentAdapter adapter)
        {
            var lines = new List<string>();
            var data = adapter.OriginalEquipmentData;

            if (data is WeaponData w)
                lines.Add($"<color=#cdd6e0>+{Mathf.RoundToInt(w.BaseMinDamage)}–{Mathf.RoundToInt(w.BaseMaxDamage)} Dmg</color>");

            var baseMods = data.GetStatModifiers();
            if (baseMods != null)
                foreach (var m in baseMods)
                    if (m != null) lines.Add(FormatStatMod(m, false));

            if (adapter.RolledAffixes != null)
                foreach (var m in adapter.RolledAffixes)
                    if (m != null) lines.Add(FormatStatMod(m, true));

            return lines;
        }

        // Terse line: "+8 Fire" (rolled affixes tinted, no verbose affix name / stat wording).
        private static string FormatStatMod(StatModifier m, bool rolled)
        {
            string v = (m.Value >= 0 ? "+" : "") + m.Value.ToString("0.#");
            string col = rolled ? "#b99cff" : "#cdd6e0";
            return $"<color={col}>{v} {ShortStat(m.Type)}</color>";
        }

        private static string ShortStat(StatType t)
        {
            switch (t)
            {
                case StatType.AddedPhysicalDamage:      return "Phys";
                case StatType.AddedFireDamage:          return "Fire";
                case StatType.AddedColdDamage:          return "Cold";
                case StatType.IncreasedPhysicalDamage:  return "Phys%";
                case StatType.IncreasedAttackSpeed:     return "AtkSpd%";
                case StatType.IncreasedCriticalChance:  return "Crit%";
                case StatType.AddedCriticalMultiplier:  return "CritMulti";
                case StatType.LifeStealPercentage:      return "LifeSteal%";
                case StatType.KnockbackForce:           return "Knockback";
                case StatType.AddedLife:                return "Life";
                case StatType.AddedMana:                return "Mana";
                case StatType.AddedArmour:              return "Armour";
                case StatType.AddedEvasion:             return "Evasion";
                case StatType.AddedAccuracy:            return "Accuracy";
                case StatType.AddedStrength:            return "Str";
                case StatType.AddedDexterity:           return "Dex";
                case StatType.AddedIntelligence:        return "Int";
                case StatType.AddedMovementSpeed:       return "MoveSpd%";
                case StatType.AddedLifeRegen:           return "LifeRegen";
                case StatType.AddedBlockChance:         return "Block%";
                case StatType.AddedFireResistance:      return "FireRes";
                case StatType.AddedColdResistance:      return "ColdRes";
                case StatType.AddedLightningResistance: return "LightRes";
                case StatType.AddedChaosResistance:     return "ChaosRes";
                case StatType.AddedAllResistances:      return "AllRes";
                default:                                return t.ToString();
            }
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
