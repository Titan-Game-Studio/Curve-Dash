using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// Runtime debug viewer that draws the equipped gear and its socketed gems/abilities as a tree:
    ///
    ///   Weapon: Iron Sword
    ///     |_ [Active] Cleave (Melee/Physical)
    ///     |_ [Support] Melee Physical Damage
    ///   Head: Iron Helm
    ///     |_ (no gems)
    ///
    /// Self-instantiates at runtime (sibling to <see cref="CharacterStatsPanel"/>), sits on the LEFT
    /// edge so it never overlaps the stats panel, and refreshes a few times a second while open.
    /// Pure uGUI so it uses the same EventSystem/input path as the rest of the game's working buttons.
    /// </summary>
    public class GemTreePanel : MonoBehaviour
    {
        // Armor slots scanned for socketed abilities (mirrors PlayerView.GetEquippedArmorAbilities order).
        private static readonly EquipmentSlot[] ArmorSlots =
            { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GemTreePanel>() != null) return;
            var go = new GameObject("[GemTreePanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<GemTreePanel>();
        }

        private Font _font;
        private Text _label;
        private Text _buttonLabel;
        private GameObject _panel;
        private bool _open;
        private float _refresh;
        private PlayerView _cachedPlayer;
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
            if (_refresh <= 0f) { _refresh = 0.3f; RefreshText(); }
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
            var canvasGO = new GameObject("GemTreeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Toggle button — top-LEFT corner (the stats button lives top-right).
            var btn = CreateButton(canvas.transform, "☰ Gems", Toggle);
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(230f, 84f);
            brt.anchoredPosition = new Vector2(24f, -24f);
            _buttonLabel = btn.GetComponentInChildren<Text>();

            // Panel — top-LEFT, top edge at 1/5 (0.2) of screen height from the top (anchor y = 0.8).
            _panel = new GameObject("GemTreePanel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            _panel.transform.SetParent(canvas.transform, false);
            var prt = _panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.8f);
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(460f, 1180f);
            prt.anchoredPosition = new Vector2(24f, 0f);
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.65f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(_panel.transform, false);
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(16f, 16f); vrt.offsetMax = new Vector2(-16f, -16f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

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
            go.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.85f, 0.95f);
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
            if (_buttonLabel != null) _buttonLabel.text = open ? "✕ Gems" : "☰ Gems";
            if (open) { _refresh = 0f; RefreshText(); }
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

        private void RefreshText()
        {
            if (_label == null) return;
            _sb.Clear();
            _sb.Append("<size=30><b>Gems / Abilities</b></size>\n");

            var pv = GetPlayerView();
            var container = FindEquipmentContainer();
            if (pv == null || container == null)
            {
                _sb.Append("\n<color=#bbbbbb>Start a game and equip gear to view sockets.</color>");
                _label.text = _sb.ToString();
                return;
            }

            bool anyItem = false;
            foreach (var slot in container.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                if (!(slot.ObservedItem is CurveDashEquipmentAdapter adapter) || adapter.OriginalEquipmentData == null) continue;

                anyItem = true;
                AppendItemNode(adapter.OriginalEquipmentData, ResolveAbilities(pv, adapter.OriginalEquipmentData));
            }

            if (!anyItem)
                _sb.Append("\n<color=#bbbbbb>No equipment found in the Equipment container.</color>");

            _label.text = _sb.ToString();
        }

        // Resolves the socketed abilities for an equipped item from the live runtime state.
        private static List<AbilityData> ResolveAbilities(PlayerView pv, ItemData data)
        {
            var list = new List<AbilityData>();

            if (data is WeaponData)
            {
                // The weapon's gems live on the runtime WeaponInstance (active skill + support gems).
                if (pv.CurrentWeaponInstance != null && pv.CurrentWeaponInstance.BaseData == data)
                {
                    var a = pv.CurrentWeaponInstance.GetAbilities();
                    if (a != null) AddUnique(list, a);
                }
            }
            else if (data is ArmorItemData armor)
            {
                if (armor.Abilities != null) AddUnique(list, armor.Abilities);            // static (asset)
                AddUnique(list, pv.GetArmorRuntimeSockets(armor.Slot));                    // runtime sockets
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

        private void AppendItemNode(ItemData data, List<AbilityData> abilities)
        {
            _sb.Append("\n<color=#ffd24d><b>").Append(EscapeRich(data.ItemName)).Append("</b></color>")
               .Append(" <size=20><color=#8a8f9c>(").Append(SlotLabel(data)).Append(")</color></size>\n");

            if (abilities == null || abilities.Count == 0)
            {
                _sb.Append("  |_ <color=#777777>(no gems socketed)</color>\n");
                return;
            }

            foreach (var ab in abilities)
                _sb.Append("  |_ ").Append(AbilityLabel(ab)).Append('\n');
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
                case WeaponData _:      return "Weapon";
                case OffHandData off:   return "Off-Hand: " + off.SubType;
                case ArmorItemData a:   return a.Slot.ToString();
                case AmuletItemData _:  return "Amulet";
                case RingItemData r:    return r.Slot.ToString();
                case BeltItemData _:    return "Belt";
                default:                return data.Type.ToString();
            }
        }

        // Strips '<' so item/ability names can never break the rich-text markup.
        private static string EscapeRich(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹");
    }
}
