using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// Mobile-friendly item interaction: instead of a tap instantly equipping/replacing gear (easy to
    /// mis-tap) or a long-press tooltip, a single tap on an Inventory/Equipment item opens a big-button
    /// action sheet — Equip / Unequip, Details, Sockets, Drop — so every action is explicit.
    ///
    /// • Taps are captured through <see cref="ItemSlot.TapInterceptor"/> (a framework-typed hook in Devion,
    ///   so no assembly boundary is crossed). Only "Inventory" and "Equipment" are intercepted; the
    ///   Actionbar and shop/vendor containers keep their native behaviour (so skills still cast on tap).
    /// • "Details" simply shows Devion's own item tooltip (already polished) with a tap-anywhere to dismiss.
    /// • "Sockets" opens the gem socket popup; equip/unequip/use reuse Devion's own Use() flow.
    /// </summary>
    public class ItemActionSheet : MonoBehaviour
    {
        private static ItemActionSheet _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ItemActionSheet]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ItemActionSheet>();
        }

        private Font _font;
        private Canvas _canvas;
        private GameObject _root;       // dim backdrop + panel
        private GameObject _catcher;    // full-screen tap catcher used to dismiss Devion's tooltip
        private RectTransform _content;
        private Text _title;

        private ItemSlot _slot;
        private Item _item;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();
            BuildUI();
            ShowRoot(false);

            ItemSlot.TapInterceptor = OnSlotTapped;
        }

        private void OnDestroy()
        {
            if (ItemSlot.TapInterceptor == OnSlotTapped) ItemSlot.TapInterceptor = null;
        }

        // ---------------------------------------------------------------- tap routing

        // Returns true when we take over the tap (open the sheet); false lets Devion handle it normally.
        private bool OnSlotTapped(ItemSlot slot)
        {
            if (slot == null || slot.ObservedItem == null || slot.Container == null) return false;

            string container = slot.Container.Name;
            if (container != "Inventory" && container != "Equipment") return false; // never hijack Actionbar/shop

            Open(slot);
            return true;
        }

        private void Open(ItemSlot slot)
        {
            _slot = slot;
            _item = slot.ObservedItem;
            ShowRoot(true);
            Rebuild();
        }

        private void Close() => ShowRoot(false);

        // ---------------------------------------------------------------- menu

        private void Rebuild()
        {
            ClearContent();
            if (_item == null) { Close(); return; }

            bool inEquipment = _slot != null && _slot.Container != null && _slot.Container.Name == "Equipment";
            bool isEquippable = _item is CurveDashEquipmentAdapter;
            bool hasSockets = inEquipment && EquipmentSocketUI.HasSockets(_item);

            _title.text = $"<b>{EscapeRich(_item.DisplayName)}</b>";

            // Primary action — reuse Devion's own Use() (equip / unequip / consume).
            string primary = inEquipment ? "Unequip" : (isEquippable ? "Equip" : "Use");
            Button(primary, new Color(0.20f, 0.45f, 0.30f, 1f), () => { var s = _slot; Close(); if (s != null) s.Use(); });

            // Details — Devion's native tooltip is already great; just show it.
            Button("Details", new Color(0.22f, 0.30f, 0.48f, 1f), ShowDevionTooltip);

            if (hasSockets)
            {
                var it = _item;
                Button("Sockets", new Color(0.42f, 0.30f, 0.55f, 1f), () => { Close(); EquipmentSocketUI.OpenFor(it); });
            }

            if (!inEquipment && _item.IsDroppable)
            {
                var it = _item;
                Button("Drop", new Color(0.55f, 0.22f, 0.24f, 1f), () => { Close(); ItemContainer.RemoveItemCompletely(it); });
            }

            Button("Close", new Color(0.26f, 0.28f, 0.34f, 1f), Close);
        }

        // ---------------------------------------------------------------- Devion tooltip (Details)

        private void ShowDevionTooltip()
        {
            var tt = InventoryManager.UI != null ? InventoryManager.UI.tooltip : null;
            if (tt == null || _item == null) { Close(); return; }

            var slot = _slot;           // capture before we hide the sheet
            ShowRoot(false);            // hide our sheet so the (lower-canvas) tooltip is visible
            if (_catcher != null) _catcher.SetActive(true);

            tt.Show(DevionGames.UnityTools.ColorString(_item.DisplayName, _item.Rarity.Color),
                    _item.Description, _item.Icon, _item.GetPropertyInfo());

            // Devion's tooltip follows Input.mousePosition, which on a tap-driven (mobile) UI parks it
            // in a random corner. Pin it beside the tapped slot instead.
            PinTooltipNextToSlot(tt, slot);
        }

        private void DismissTooltip()
        {
            var tt = InventoryManager.UI != null ? InventoryManager.UI.tooltip : null;
            if (tt != null) tt.Close();
            RestoreTooltipFollow(tt);
            if (_catcher != null) _catcher.SetActive(false);
        }

        // ---------- tooltip positioning (place beside the slot, not under the mouse) ----------

        private const System.Reflection.BindingFlags _ttFlags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private static System.Reflection.FieldInfo _ttFollowField;   // protected bool m_UpdatePosition
        private static System.Reflection.FieldInfo _ttActiveField;   // private  bool _updatePosition
        private bool _ttFollowSaved;
        private bool _ttFollowOverridden;

        private void PinTooltipNextToSlot(DevionGames.UIWidgets.Tooltip tt, ItemSlot slot)
        {
            if (tt == null || slot == null) return;
            var rt = tt.GetComponent<RectTransform>();
            var canvas = tt.GetComponentInParent<Canvas>();
            var slotRT = slot.transform as RectTransform;
            if (rt == null || canvas == null || slotRT == null) return;

            // Stop the per-frame mouse-follow so our placement sticks; remember the value to restore it
            // (PC hover should keep following the cursor).
            var tType = typeof(DevionGames.UIWidgets.Tooltip);
            if (_ttFollowField == null) _ttFollowField = tType.GetField("m_UpdatePosition", _ttFlags);
            if (_ttActiveField == null) _ttActiveField = tType.GetField("_updatePosition", _ttFlags);
            if (_ttFollowField != null)
            {
                _ttFollowSaved = (bool)_ttFollowField.GetValue(tt);
                _ttFollowField.SetValue(tt, false);
                _ttFollowOverridden = true;
            }
            if (_ttActiveField != null) _ttActiveField.SetValue(tt, false);

            // Make sure the tooltip has its final size before we measure it.
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;

            // Slot bounds in screen pixels.
            var c = new Vector3[4];
            slotRT.GetWorldCorners(c);
            Vector2 slotBL = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 slotTR = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            float slotCenterX = (slotBL.x + slotTR.x) * 0.5f;
            float slotCenterY = (slotBL.y + slotTR.y) * 0.5f;

            // Tooltip box size in screen pixels (rect.size is in canvas units).
            Vector2 ttSize = rt.rect.size * scale;
            float halfW = ttSize.x * 0.5f;
            float halfH = ttSize.y * 0.5f;
            const float margin = 16f;

            // Prefer the side with more room: right of the slot when it sits in the left half, else left.
            float x = slotCenterX < Screen.width * 0.5f
                ? slotTR.x + margin + halfW
                : slotBL.x - margin - halfW;
            float y = slotCenterY;

            // Keep the whole box on screen.
            x = Mathf.Clamp(x, halfW + margin, Screen.width - halfW - margin);
            y = Mathf.Clamp(y, halfH + margin, Screen.height - halfH - margin);

            var canvasRT = canvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, new Vector2(x, y), cam, out var lp))
                tt.transform.position = canvas.transform.TransformPoint(lp);
        }

        private void RestoreTooltipFollow(DevionGames.UIWidgets.Tooltip tt)
        {
            if (!_ttFollowOverridden || tt == null || _ttFollowField == null) return;
            _ttFollowField.SetValue(tt, _ttFollowSaved);
            _ttFollowOverridden = false;
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
            var canvasGO = new GameObject("ItemActionSheetCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30500; // above slots; below the socket popup (31000)
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Tap catcher (separate from the sheet) to dismiss Devion's tooltip; transparent, off by default.
            _catcher = new GameObject("TooltipCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
            _catcher.transform.SetParent(_canvas.transform, false);
            var ctr = _catcher.GetComponent<RectTransform>();
            ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one;
            ctr.offsetMin = Vector2.zero; ctr.offsetMax = Vector2.zero;
            _catcher.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // nearly invisible but raycastable
            _catcher.GetComponent<Button>().onClick.AddListener(DismissTooltip);
            _catcher.SetActive(false);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(_canvas.transform, false);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            dim.GetComponent<Button>().onClick.AddListener(Close);
            _root = dim;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(dim.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 100f); // height driven by ContentSizeFitter
            panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);
            var pvlg = panel.GetComponent<VerticalLayoutGroup>();
            pvlg.padding = new RectOffset(30, 30, 24, 28);
            pvlg.spacing = 16f;
            pvlg.childControlWidth = true; pvlg.childControlHeight = true;
            pvlg.childForceExpandWidth = true; pvlg.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = NewText(panel.transform, "", 32, TextAnchor.MiddleCenter);
            _title.raycastTarget = false;
            _title.gameObject.AddComponent<LayoutElement>().minHeight = 56f;

            _content = (RectTransform)panel.transform; // rows are added straight into the vertical layout
        }

        private void ShowRoot(bool show)
        {
            if (_root != null) _root.SetActive(show);
        }

        private void ClearContent()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                if (child == _title.transform) continue;
                Destroy(child.gameObject);
            }
        }

        private Button Button(string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_content, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<LayoutElement>().minHeight = 92f; // large finger target
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var labelGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(16f, 0f); lrt.offsetMax = new Vector2(-16f, 0f);
            var label = labelGO.GetComponent<Text>();
            label.font = _font; label.text = text; label.fontSize = 30; label.fontStyle = FontStyle.Bold;
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.supportRichText = true;
            return go.GetComponent<Button>();
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

        private static string EscapeRich(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹");
    }
}
