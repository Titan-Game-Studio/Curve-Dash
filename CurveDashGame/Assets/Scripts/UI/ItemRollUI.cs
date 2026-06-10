using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// PoE-style "Roll" crafting popup. Opened from the item context menu (<see cref="ItemActionSheet"/>)
    /// for a bag equipment item. The four crafting orbs are shown as an action-bar-style horizontal strip
    /// of icon slots (orb icon + owned count); orbs that don't apply to the item's rarity are greyed out.
    /// Tapping an eligible orb spends it and re-rolls the item via <see cref="ItemRollService"/>, then shows
    /// the result in Devion's own item tooltip (the same detail window used elsewhere) rather than a
    /// bespoke panel. The strip stays up so the player can keep rolling. Self-instantiating.
    /// </summary>
    public class ItemRollUI : MonoBehaviour
    {
        private static ItemRollUI _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ItemRollUI]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ItemRollUI>();
        }

        /// <summary>Opens the roll popup for a bag item (no-op if the item can't be rolled).</summary>
        public static void OpenFor(Item item, ItemSlot slot)
        {
            if (_instance == null || !ItemRollService.CanRoll(item)) return;
            _instance.Open(item as CurveDashEquipmentAdapter, slot);
        }

        private Font _font;
        private GameObject _root;
        private RectTransform _bar;     // horizontal strip that holds the currency slots
        private Text _title;
        private Text _hint;             // transient message (e.g. roll failure reason)

        private CurveDashEquipmentAdapter _item;
        private ItemSlot _slot;
        private bool _changed;          // whether any roll happened (so we repaint the slot on close)
        private bool _detailShown;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();
            BuildUI();
            ShowRoot(false);
        }

        // ---------------------------------------------------------------- flow

        private void Open(CurveDashEquipmentAdapter item, ItemSlot slot)
        {
            if (item == null) return;
            _item = item;
            _slot = slot;
            _changed = false;
            ShowRoot(true);
            RebuildBar(null);
            ShowDetail(); // show Devion's detail window alongside the bar from the start
        }

        private void Close()
        {
            HideDetail();
            // Repaint the source slot so its rarity colour / name reflect any roll.
            if (_changed && _slot != null) _slot.ObservedItem = _slot.ObservedItem;
            ShowRoot(false);
            _item = null;
            _slot = null;
        }

        private void DoRoll(ItemRollService.RollCurrency c)
        {
            var result = ItemRollService.Roll(_item, c);
            if (result.Success)
            {
                _changed = true;
                RebuildBar(null);          // refresh counts / eligibility / title rarity
                ShowDetail();              // Devion's own detail window shows the new stats
            }
            else
            {
                RebuildBar(result.Message); // keep the strip, show why it didn't roll
            }
        }

        // ---------------------------------------------------------------- currency strip (action-bar style)

        private void RebuildBar(string message)
        {
            ClearBar();
            if (_item == null) { Close(); return; }

            var rarity = _item.EffectiveRarity;
            _title.text = $"<b>Roll</b>  <color={RarityHex(rarity)}>{EscapeRich(_item.DisplayName)}</color> <color=#9aa0aa>({rarity})</color>";

            foreach (ItemRollService.RollCurrency c in System.Enum.GetValues(typeof(ItemRollService.RollCurrency)))
                AddCurrencySlot(c);

            _hint.text = string.IsNullOrEmpty(message) ? "" : $"<color=#c98a8a>{EscapeRich(message)}</color>";
            _hint.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // One action-bar-style slot: a square frame with the orb icon, an owned-count badge and a short
        // name caption. Eligible orbs are tappable and lit; ineligible ones are dimmed and inert.
        private void AddCurrencySlot(ItemRollService.RollCurrency c)
        {
            var type = ItemRollService.ToCurrencyType(c);
            int count = ItemRollService.CountCurrency(type);
            bool eligible = ItemRollService.IsEligible(_item, c, out string reason);

            // Cell = vertical: [square slot button] + [name caption].
            var cell = new GameObject("Cell", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            cell.transform.SetParent(_bar, false);
            cell.GetComponent<LayoutElement>().preferredWidth = 132f;
            var cvlg = cell.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = 6f; cvlg.childAlignment = TextAnchor.UpperCenter;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;

            // Square slot.
            var slot = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(LayoutElement));
            slot.transform.SetParent(cell.transform, false);
            slot.GetComponent<LayoutElement>().minHeight = 124f;
            var slotImg = slot.GetComponent<Image>();
            slotImg.color = eligible ? new Color(0.16f, 0.18f, 0.14f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f);
            var outline = slot.GetComponent<Outline>();
            outline.effectColor = eligible ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(0.3f, 0.32f, 0.4f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var slotBtn = slot.GetComponent<Button>();
            if (eligible) { var captured = c; slotBtn.onClick.AddListener(() => DoRoll(captured)); }
            else slotBtn.interactable = false;

            // Orb icon (centred, inset).
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var irt = (RectTransform)iconGO.transform;
            irt.SetParent(slot.transform, false);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(12f, 12f); irt.offsetMax = new Vector2(-12f, -12f);
            var icon = iconGO.GetComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;
            var sprite = ItemRollService.GetCurrencyIcon(type);
            if (sprite != null) { icon.sprite = sprite; icon.color = eligible ? Color.white : new Color(1, 1, 1, 0.35f); }
            else icon.color = new Color(0.3f, 0.32f, 0.4f, eligible ? 1f : 0.35f);

            // Count badge (bottom-right corner).
            var badgeGO = new GameObject("Count", typeof(RectTransform), typeof(Text));
            var brt = (RectTransform)badgeGO.transform;
            brt.SetParent(slot.transform, false);
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(0f, 4f); brt.offsetMax = new Vector2(-8f, 0f);
            var badge = badgeGO.GetComponent<Text>();
            badge.font = _font; badge.fontSize = 30; badge.fontStyle = FontStyle.Bold;
            badge.alignment = TextAnchor.LowerRight; badge.raycastTarget = false;
            badge.color = count > 0 ? Color.white : new Color(0.6f, 0.4f, 0.4f, 1f);
            badge.text = "x" + count;
            var bo = badgeGO.AddComponent<Outline>(); bo.effectColor = new Color(0, 0, 0, 0.9f); bo.effectDistance = new Vector2(1.2f, -1.2f);

            // Name caption (or the reason it's unavailable).
            var capGO = new GameObject("Caption", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            capGO.transform.SetParent(cell.transform, false);
            capGO.GetComponent<LayoutElement>().minHeight = 44f;
            var cap = capGO.GetComponent<Text>();
            cap.font = _font; cap.fontSize = 19; cap.alignment = TextAnchor.UpperCenter; cap.supportRichText = true;
            cap.horizontalOverflow = HorizontalWrapMode.Wrap; cap.verticalOverflow = VerticalWrapMode.Overflow; cap.raycastTarget = false;
            string shortName = ItemRollService.DisplayName(c).Replace("Orb of ", "").Replace(" Orb", "");
            cap.text = eligible
                ? $"<color=#ffe9b0>{shortName}</color>"
                : $"<color=#8a8f9c>{shortName}</color>\n<size=16><color=#c98a8a>{reason}</color></size>";
        }

        // ---------------------------------------------------------------- Devion detail tooltip (result)

        private void ShowDetail()
        {
            var tt = InventoryManager.UI != null ? InventoryManager.UI.tooltip : null;
            if (tt == null || _item == null) return;

            DisableFollow(tt);
            string title = DevionGames.UnityTools.ColorString(_item.DisplayName, _item.Rarity.Color);
            tt.Show(title, _item.Description, _item.Icon, _item.GetPropertyInfo());

            // Devion's tooltip lives on a lower canvas; lift it above our dim so it's visible, and park it
            // in the upper-centre area (away from the bottom strip).
            RaiseTooltip(tt, true);
            PositionTooltipUpper(tt);
            _detailShown = true;
        }

        private void HideDetail()
        {
            if (!_detailShown) return;
            var tt = InventoryManager.UI != null ? InventoryManager.UI.tooltip : null;
            if (tt != null) { tt.Close(); RaiseTooltip(tt, false); RestoreFollow(tt); }
            _detailShown = false;
        }

        private const System.Reflection.BindingFlags _ttFlags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private static System.Reflection.FieldInfo _ttFollowField; // protected bool m_UpdatePosition
        private static System.Reflection.FieldInfo _ttActiveField; // private  bool _updatePosition
        private bool _ttFollowSaved, _ttFollowOverridden;

        private void DisableFollow(DevionGames.UIWidgets.Tooltip tt)
        {
            if (tt == null) return;
            var tType = typeof(DevionGames.UIWidgets.Tooltip);
            if (_ttFollowField == null) _ttFollowField = tType.GetField("m_UpdatePosition", _ttFlags);
            if (_ttActiveField == null) _ttActiveField = tType.GetField("_updatePosition", _ttFlags);
            if (_ttFollowField != null && !_ttFollowOverridden)
            {
                _ttFollowSaved = (bool)_ttFollowField.GetValue(tt);
                _ttFollowOverridden = true;
            }
            _ttFollowField?.SetValue(tt, false);
            _ttActiveField?.SetValue(tt, false);
        }

        private void RestoreFollow(DevionGames.UIWidgets.Tooltip tt)
        {
            if (!_ttFollowOverridden || tt == null || _ttFollowField == null) return;
            _ttFollowField.SetValue(tt, _ttFollowSaved);
            _ttFollowOverridden = false;
        }

        // Adds (once) a Canvas override on the tooltip so it draws above our dim while a roll session is open.
        private void RaiseTooltip(DevionGames.UIWidgets.Tooltip tt, bool raise)
        {
            if (tt == null) return;
            var canvas = tt.GetComponent<Canvas>();
            if (raise)
            {
                if (canvas == null) canvas = tt.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30650; // above our dim (30600), below the socket popup
            }
            else if (canvas != null)
            {
                canvas.overrideSorting = false;
            }
        }

        private void PositionTooltipUpper(DevionGames.UIWidgets.Tooltip tt)
        {
            var rt = tt.GetComponent<RectTransform>();
            var canvas = tt.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            Vector2 ttSize = rt.rect.size * scale;
            float x = Mathf.Clamp(Screen.width * 0.5f, ttSize.x * 0.5f + 16f, Screen.width - ttSize.x * 0.5f - 16f);
            float y = Mathf.Clamp(Screen.height * 0.62f, ttSize.y * 0.5f + 16f, Screen.height - ttSize.y * 0.5f - 16f);

            var canvasRT = canvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, new Vector2(x, y), cam, out var lp))
                tt.transform.position = canvas.transform.TransformPoint(lp);
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
            var canvasGO = new GameObject("ItemRollCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30600; // above the item action sheet (30500)
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Dim backdrop — tapping outside the strip closes the popup.
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(canvas.transform, false);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            dim.GetComponent<Button>().onClick.AddListener(Close);
            _root = dim;

            // Bottom-docked panel (like the action bar): title, the currency strip, a hint line, Close.
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(dim.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 40f);
            prt.sizeDelta = new Vector2(900f, 100f);
            panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);
            var pvlg = panel.GetComponent<VerticalLayoutGroup>();
            pvlg.padding = new RectOffset(28, 28, 22, 24);
            pvlg.spacing = 14f;
            pvlg.childControlWidth = true; pvlg.childControlHeight = true;
            pvlg.childForceExpandWidth = true; pvlg.childForceExpandHeight = false;
            pvlg.childAlignment = TextAnchor.UpperCenter;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = NewText(panel.transform, "", 30, TextAnchor.MiddleCenter);
            _title.raycastTarget = false;
            _title.gameObject.AddComponent<LayoutElement>().minHeight = 46f;

            // The action-bar-style horizontal strip of orb slots.
            var barGO = new GameObject("CurrencyBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            barGO.transform.SetParent(panel.transform, false);
            barGO.GetComponent<LayoutElement>().minHeight = 176f;
            var hlg = barGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            _bar = (RectTransform)barGO.transform;

            _hint = NewText(panel.transform, "", 22, TextAnchor.MiddleCenter);
            _hint.raycastTarget = false;
            _hint.gameObject.SetActive(false);

            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            close.transform.SetParent(panel.transform, false);
            close.GetComponent<Image>().color = new Color(0.26f, 0.28f, 0.34f, 1f);
            close.GetComponent<LayoutElement>().minHeight = 80f;
            close.GetComponent<Button>().onClick.AddListener(Close);
            var clbl = NewText(close.transform, "Close", 28, TextAnchor.MiddleCenter);
            var clrt = (RectTransform)clbl.transform;
            clrt.anchorMin = Vector2.zero; clrt.anchorMax = Vector2.one; clrt.offsetMin = Vector2.zero; clrt.offsetMax = Vector2.zero;
            clbl.fontStyle = FontStyle.Bold;
        }

        private void ShowRoot(bool show) { if (_root != null) _root.SetActive(show); }

        private void ClearBar()
        {
            if (_bar == null) return;
            for (int i = _bar.childCount - 1; i >= 0; i--) Destroy(_bar.GetChild(i).gameObject);
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

        private static string RarityHex(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:  return "#5a8cff";
                case ItemRarity.Rare:   return "#ffd900";
                case ItemRarity.Unique: return "#af6025";
                default:                return "#ffffff";
            }
        }

        private static string EscapeRich(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹");
    }
}
