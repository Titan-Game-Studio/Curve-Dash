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

        // A clone of Devion's own tooltip, reused as the second "Equipped" details box for comparison.
        private DevionGames.UIWidgets.Tooltip _compareTooltip;
        // Width (canvas units) both tooltips use in compare mode so the pair fits side by side.
        private float _compareWidth = 300f;

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
            bool inEquipment = slot != null && slot.Container != null && slot.Container.Name == "Equipment";
            ShowRoot(false);            // hide our sheet so the (lower-canvas) tooltip is visible
            if (_catcher != null) _catcher.SetActive(true);

            // When inspecting a BAG item, also show the currently-equipped item of the same slot in a
            // SECOND Devion tooltip (a clone of Devion's own tooltip) so the player can compare. Skipped
            // when nothing comparable is equipped, or when inspecting an equipped item itself.
            Item equipped = inEquipment ? null : FindEquippedCounterpart(_item);
            string title = DevionGames.UnityTools.ColorString(_item.DisplayName, _item.Rarity.Color);

            if (equipped != null)
            {
                // Two Devion tooltips side by side: equipped on the left, inspected item on the right.
                // Narrow both so the pair fits the screen without overlapping.
                _compareWidth = ComputeCompareWidth(tt);
                tt.Show(title, _item.Description, _item.Icon, _item.GetPropertyInfo(), _compareWidth, true);
                EnsureTooltipFollowDisabled(tt);
                PlaceTooltipHalf(tt, leftHalf: false);
                ShowEquippedCompare(equipped);
            }
            else
            {
                tt.Show(title, _item.Description, _item.Icon, _item.GetPropertyInfo());
                HideCompareTooltip();
                // Devion's tooltip follows Input.mousePosition, which on a tap-driven (mobile) UI parks it
                // in a random corner. Pin it beside the tapped slot instead.
                PinTooltipNextToSlot(tt, slot);
            }
        }

        private void DismissTooltip()
        {
            var tt = InventoryManager.UI != null ? InventoryManager.UI.tooltip : null;
            if (tt != null) tt.Close();
            RestoreTooltipFollow(tt);
            HideCompareTooltip();
            if (_catcher != null) _catcher.SetActive(false);
        }

        // ---------------------------------------------------------------- equipped-item comparison

        // Finds the currently-equipped item that occupies the same logical slot as the inspected bag item,
        // so its details can be shown side-by-side. Returns null when nothing comparable is equipped.
        private Item FindEquippedCounterpart(Item bagItem)
        {
            if (!(bagItem is CurveDashEquipmentAdapter bagAdapter) || bagAdapter.OriginalEquipmentData == null)
                return null;

            string key = CompareKey(bagAdapter.OriginalEquipmentData);
            if (key == null) return null; // not an equippable kind we compare (gem/currency/flask/etc.)

            var equipment = DevionGames.UIWidgets.WidgetUtility.Find<ItemContainer>("Equipment");
            if (equipment == null) return null;

            foreach (var s in equipment.Slots)
            {
                if (s == null || s.IsEmpty || s.ObservedItem == null || s.ObservedItem == bagItem) continue;
                if (s.ObservedItem is CurveDashEquipmentAdapter eqAdapter
                    && eqAdapter.OriginalEquipmentData != null
                    && CompareKey(eqAdapter.OriginalEquipmentData) == key)
                {
                    return s.ObservedItem;
                }
            }
            return null;
        }

        // Groups items into comparable slot categories. Armor compares within its own slot; rings compare to
        // any equipped ring. Returns null for items that have no meaningful "equipped counterpart".
        private static string CompareKey(ItemData data)
        {
            switch (data)
            {
                case WeaponData _:    return "weapon";
                case OffHandData _:   return "offhand";
                case ArmorItemData a: return "armor:" + a.Slot;
                case RingItemData _:  return "ring";
                case AmuletItemData _:return "amulet";
                case BeltItemData _:  return "belt";
                default:              return null;
            }
        }

        // Shows the equipped item in the cloned Devion tooltip. The very first time, the clone has just
        // been Instantiated and its slot cache (built in the widget's Start/OnStart) isn't ready yet, so we
        // defer the first Show by one frame; afterwards it's reused immediately.
        private void ShowEquippedCompare(Item equipped)
        {
            var source = InventoryManager.UI != null ? InventoryManager.UI.tooltip : null;
            bool firstCreate = _compareTooltip == null;
            var clone = GetOrCreateCompareTooltip(source);
            if (clone == null) return;

            if (firstCreate)
                StartCoroutine(ShowCompareNextFrame(equipped));
            else
                DriveCompareTooltip(clone, equipped);
        }

        private System.Collections.IEnumerator ShowCompareNextFrame(Item equipped)
        {
            yield return null; // let the clone's Start/OnStart run so its slot cache is initialized
            // Only show if the comparison view is still up (the tooltip dismiss catcher is active).
            if (_compareTooltip != null && _catcher != null && _catcher.activeSelf)
                DriveCompareTooltip(_compareTooltip, equipped);
        }

        private void DriveCompareTooltip(DevionGames.UIWidgets.Tooltip clone, Item equipped)
        {
            if (clone == null || equipped == null) return;
            ForceDisableFollow(clone); // ours is positioned manually, never mouse-follows

            // Title is just the item name (same as the inspected tooltip); the "Equipped" tag moves to the
            // bottom as the last line so both titles read consistently.
            var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>(
                equipped.GetPropertyInfo());
            pairs.Add(new System.Collections.Generic.KeyValuePair<string, string>("", "")); // spacer
            pairs.Add(new System.Collections.Generic.KeyValuePair<string, string>(
                "<color=#9aa0aa><i>Equipped</i></color>", ""));

            clone.Show(DevionGames.UnityTools.ColorString(equipped.DisplayName, equipped.Rarity.Color),
                       equipped.Description, equipped.Icon, pairs, _compareWidth, true);
            PlaceTooltipHalf(clone, leftHalf: true); // left of centre, beside the inspected item's tooltip
        }

        // Lazily clones Devion's tooltip the first time, then reuses it. Returns null if there is no
        // source tooltip to clone.
        private DevionGames.UIWidgets.Tooltip GetOrCreateCompareTooltip(DevionGames.UIWidgets.Tooltip source)
        {
            if (_compareTooltip != null) return _compareTooltip;
            if (source == null) return null;

            var cloneGo = Instantiate(source.gameObject, source.transform.parent);
            cloneGo.name = "CompareTooltip(Clone)"; // distinct name so WidgetUtility.Find never returns it
            _compareTooltip = cloneGo.GetComponent<DevionGames.UIWidgets.Tooltip>();
            SanitizeClonedSlots(_compareTooltip);
            return _compareTooltip;
        }

        // We clone the live tooltip AFTER it has shown the inspected item, so its runtime StringPairSlot
        // rows (the inspected item's stats) get copied into the clone and would linger as duplicate content.
        // Remove every cloned slot row except the prefab template; Devion rebuilds rows cleanly on Show().
        private void SanitizeClonedSlots(DevionGames.UIWidgets.Tooltip clone)
        {
            if (clone == null) return;
            var field = typeof(DevionGames.UIWidgets.Tooltip).GetField("m_SlotPrefab", _ttFlags);
            var template = field?.GetValue(clone) as Component;
            if (template == null) return;

            var parent = template.transform.parent;
            if (parent == null) return;

            var slotType = template.GetType();
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.gameObject == template.gameObject) continue;       // keep the template
                if (child.GetComponent(slotType) != null) Destroy(child.gameObject);
            }
        }

        private void HideCompareTooltip()
        {
            if (_compareTooltip != null) _compareTooltip.Close();
        }

        // Width (canvas units) for each tooltip in compare mode: half the canvas width minus a gap/margins,
        // clamped so it stays readable and never wider than Devion's default. Guarantees the two boxes fit
        // side by side without overlapping.
        private float ComputeCompareWidth(DevionGames.UIWidgets.Tooltip tt)
        {
            var canvas = tt != null ? tt.GetComponentInParent<Canvas>() : null;
            float scale = (canvas == null || canvas.scaleFactor <= 0f) ? 1f : canvas.scaleFactor;
            float canvasWidthUnits = Screen.width / scale;
            float w = (canvasWidthUnits - 96f) * 0.5f; // 96 units ≈ centre gap + side margins
            return Mathf.Clamp(w, 220f, 300f);
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

            // Stop the per-frame mouse-follow so our placement sticks (restored on dismiss for PC hover).
            EnsureTooltipFollowDisabled(tt);

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

        // Disables Devion's per-frame mouse-follow (remembering the value so RestoreTooltipFollow can undo it).
        private void EnsureTooltipFollowDisabled(DevionGames.UIWidgets.Tooltip tt)
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

        // Disables mouse-follow on the clone outright (we own it and always position it manually, so no
        // need to remember/restore its previous value).
        private void ForceDisableFollow(DevionGames.UIWidgets.Tooltip tt)
        {
            if (tt == null) return;
            var tType = typeof(DevionGames.UIWidgets.Tooltip);
            if (_ttFollowField == null) _ttFollowField = tType.GetField("m_UpdatePosition", _ttFlags);
            if (_ttActiveField == null) _ttActiveField = tType.GetField("_updatePosition", _ttFlags);
            _ttFollowField?.SetValue(tt, false);
            _ttActiveField?.SetValue(tt, false);
        }

        // Places a tooltip just to the left or right of screen centre, so the equipped clone and the
        // inspected item's tooltip sit directly next to each other (each offset from centre by its own
        // half-width plus half the gap). TOP edges are aligned to a common line so the two boxes line up
        // at the top regardless of differing heights. Clamped fully on screen.
        private void PlaceTooltipHalf(DevionGames.UIWidgets.Tooltip tt, bool leftHalf)
        {
            if (tt == null) return;
            var rt = tt.GetComponent<RectTransform>();
            var canvas = tt.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            Vector2 ttSize = rt.rect.size * scale;
            float halfW = ttSize.x * 0.5f;
            float halfH = ttSize.y * 0.5f;
            const float margin = 16f;
            const float gap = 28f;
            const float topFraction = 0.85f; // shared top edge for both tooltips (near the top of the screen)

            float cx = Screen.width * 0.5f;
            float x = leftHalf ? cx - gap * 0.5f - halfW : cx + gap * 0.5f + halfW;
            float y = Screen.height * topFraction - halfH; // centre placed so the TOP edge sits on the shared line

            x = Mathf.Clamp(x, halfW + margin, Screen.width  - halfW - margin);
            y = Mathf.Clamp(y, halfH + margin, Screen.height - halfH - margin);

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
