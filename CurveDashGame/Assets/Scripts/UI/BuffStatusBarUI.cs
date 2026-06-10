using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace STG.CurveDash
{
    /// <summary>
    /// Shows the player's currently-active self-buffs as a row of small icon cells (half the size of an
    /// action-bar slot) docked directly under the health/mana/shield bars of the top-left "Player Stats"
    /// HUD panel. Auras render as persistent icons with an element-coloured border; flask effects render
    /// with a radial dark sweep that grows as the effect expires plus a tiny seconds countdown.
    ///
    /// Reads <see cref="ActiveBuffTracker"/> (kept live by <see cref="AuraSystem"/> and
    /// <see cref="BeltFlaskService"/>), self-instantiates at runtime and parents its row to the existing
    /// HUD panel non-destructively — so it needs no scene wiring and follows the panel's show/hide.
    /// </summary>
    public class BuffStatusBarUI : MonoBehaviour
    {
        private const string PanelName = "Player Stats";   // the top-left vitals HUD panel
        private const float CellSize = 26f;                // ~half of a 50px action-bar icon
        private const float Spacing = 3f;
        private const float RowLeft = 60f;                 // align with the bars' left edge (past the avatar)
        private const float RowTop = -65.5f;               // just below the Player Stats panel's bottom edge

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<BuffStatusBarUI>() != null) return;
            var go = new GameObject("[BuffStatusBarUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<BuffStatusBarUI>();
        }

        private sealed class Cell
        {
            public GameObject Root;
            public Image Background;
            public Image Icon;
            public Image Radial;        // flask depletion sweep over the icon (hidden for auras)
            public Image TimeBarFill;   // bottom depleting time bar (hidden for auras)
            public Text Countdown;      // flask seconds remaining (hidden for auras)
            public Outline Border;
        }

        private Font _font;
        private Sprite _whiteSprite;
        private RectTransform _row;
        private readonly List<Cell> _cells = new List<Cell>();

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private void LateUpdate()
        {
            // (Re)acquire the HUD panel each frame until found — it's a scene object that may not exist yet
            // at the very first frames, and is rebuilt across scene reloads.
            if (_row == null && !EnsureRow()) return;
            if (_row == null) return;

            // Only auras are shown in this HUD row. Flask effects live in the Stats panel's "Active Buffs".
            int needed = ActiveBuffTracker.Auras.Count;

            // Grow the pool to fit, then drive each cell; hide the surplus.
            while (_cells.Count < needed) _cells.Add(CreateCell(_cells.Count));

            int i = 0;
            foreach (var aura in ActiveBuffTracker.Auras)
            {
                ApplyAura(_cells[i], aura);
                i++;
            }
            for (; i < _cells.Count; i++)
                if (_cells[i].Root.activeSelf) _cells[i].Root.SetActive(false);
        }

        // Locates the "Player Stats" HUD panel and parents an empty horizontal row to it, anchored top-left
        // and positioned just under the bars. Returns false until the panel exists.
        private bool EnsureRow()
        {
            // The vitals HUD ("Player Stats") is a plain panel under "Main UI" — locate it by name/parent.
            Transform panelT = null;
            foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == PanelName && t.parent != null && t.parent.name == "Main UI") { panelT = t; break; }
            if (panelT == null) return false;

            var go = new GameObject("BuffStatusRow", typeof(RectTransform));
            _row = (RectTransform)go.transform;
            _row.SetParent(panelT, false);
            _row.anchorMin = _row.anchorMax = new Vector2(0f, 1f); // top-left of the panel
            _row.pivot = new Vector2(0f, 1f);
            _row.anchoredPosition = new Vector2(RowLeft, RowTop);
            _row.sizeDelta = new Vector2(CellSize, CellSize);
            _row.SetAsLastSibling();
            return true;
        }

        private Cell CreateCell(int index)
        {
            var rootGO = new GameObject("Buff " + index, typeof(RectTransform), typeof(Image));
            var rrt = (RectTransform)rootGO.transform;
            rrt.SetParent(_row, false);
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.sizeDelta = new Vector2(CellSize, CellSize);
            rrt.anchoredPosition = new Vector2(index * (CellSize + Spacing), 0f);

            var bg = rootGO.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            var border = rootGO.AddComponent<Outline>();
            border.effectDistance = new Vector2(1.2f, -1.2f);

            // Icon, inset 1px inside the cell.
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var irt = (RectTransform)iconGO.transform;
            irt.SetParent(rrt, false);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(1.5f, 1.5f); irt.offsetMax = new Vector2(-1.5f, -1.5f);
            var icon = iconGO.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // Radial dark sweep for flask depletion, over the icon.
            var radialGO = new GameObject("Radial", typeof(RectTransform), typeof(Image));
            var rdrt = (RectTransform)radialGO.transform;
            rdrt.SetParent(rrt, false);
            rdrt.anchorMin = Vector2.zero; rdrt.anchorMax = Vector2.one;
            rdrt.offsetMin = Vector2.zero; rdrt.offsetMax = Vector2.zero;
            var radial = radialGO.GetComponent<Image>();
            radial.sprite = _whiteSprite;
            radial.type = Image.Type.Filled;
            radial.fillMethod = Image.FillMethod.Radial360;
            radial.fillOrigin = (int)Image.Origin360.Top;
            radial.fillClockwise = false; // dark wedge grows clockwise-from-top as time elapses
            radial.color = new Color(0f, 0f, 0f, 0.45f);
            radial.raycastTarget = false;

            // Depleting time bar across the bottom of the cell: a dark track with a coloured fill that
            // shrinks left-to-right as the effect runs out — the clearest "time remaining" readout.
            var trackGO = new GameObject("TimeBarTrack", typeof(RectTransform), typeof(Image));
            var tkrt = (RectTransform)trackGO.transform;
            tkrt.SetParent(rrt, false);
            tkrt.anchorMin = new Vector2(0f, 0f); tkrt.anchorMax = new Vector2(1f, 0f);
            tkrt.pivot = new Vector2(0.5f, 0f);
            tkrt.offsetMin = new Vector2(1.5f, 1.5f); tkrt.offsetMax = new Vector2(-1.5f, 5.5f); // ~4px tall strip
            trackGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            trackGO.GetComponent<Image>().raycastTarget = false;

            var fillGO = new GameObject("TimeBarFill", typeof(RectTransform), typeof(Image));
            var flrt = (RectTransform)fillGO.transform;
            flrt.SetParent(tkrt, false);
            flrt.anchorMin = Vector2.zero; flrt.anchorMax = Vector2.one;
            flrt.offsetMin = Vector2.zero; flrt.offsetMax = Vector2.zero;
            var timeFill = fillGO.GetComponent<Image>();
            timeFill.sprite = _whiteSprite;
            timeFill.type = Image.Type.Filled;
            timeFill.fillMethod = Image.FillMethod.Horizontal;
            timeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            timeFill.raycastTarget = false;

            // Seconds countdown, centred, with a black outline so it reads on any icon.
            var textGO = new GameObject("Count", typeof(RectTransform), typeof(Text));
            var trt = (RectTransform)textGO.transform;
            trt.SetParent(rrt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(0f, 3f); trt.offsetMax = Vector2.zero; // sit above the time bar
            var text = textGO.GetComponent<Text>();
            text.font = _font;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = 18;
            text.raycastTarget = false;
            text.supportRichText = false;
            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            textOutline.effectDistance = new Vector2(1.1f, -1.1f);

            return new Cell { Root = rootGO, Background = bg, Icon = icon, Radial = radial, TimeBarFill = timeFill, Countdown = text, Border = border };
        }

        private void ApplyAura(Cell cell, ActiveBuffTracker.AuraBuff aura)
        {
            cell.Root.SetActive(true);
            SetIcon(cell, aura.Icon);   // the skill ability's own icon (e.g. Hatred), not a gem frame
            Color tint = ElementColor(aura.Element);
            cell.Border.effectColor = WithAlpha(tint, 0.95f);

            // Auras loop: show the current cast's remaining time, refilling on each recast.
            float remainFrac = aura.Duration > 0.0001f
                ? Mathf.Clamp01(aura.Remaining / aura.Duration)
                : 0f;

            // Radial dark wedge over the icon grows as the cast elapses (full icon at start, dark near recast).
            cell.Radial.gameObject.SetActive(true);
            cell.Radial.fillAmount = 1f - remainFrac;

            // Bottom time bar depletes with the remaining fraction, tinted to the aura's element.
            cell.TimeBarFill.transform.parent.gameObject.SetActive(true);
            cell.TimeBarFill.fillAmount = remainFrac;
            cell.TimeBarFill.color = WithAlpha(tint, 0.95f);

            // Exact seconds remaining (e.g. "5", or "0.6" in the last second).
            cell.Countdown.gameObject.SetActive(aura.Remaining > 0f);
            cell.Countdown.text = aura.Remaining >= 1f
                ? aura.Remaining.ToString("0")
                : aura.Remaining.ToString("0.0");
        }

        // Sets the cell icon to the buff's sprite, or a neutral fallback box when no sprite exists.
        private void SetIcon(Cell cell, Sprite sprite)
        {
            if (sprite != null)
            {
                cell.Icon.sprite = sprite;
                cell.Icon.color = Color.white;
            }
            else
            {
                cell.Icon.sprite = _whiteSprite;
                cell.Icon.color = new Color(0.25f, 0.28f, 0.36f, 1f);
            }
        }

        private static Color ElementColor(PoEElementType element)
        {
            switch (element)
            {
                case PoEElementType.Fire:      return new Color(1f, 0.42f, 0.29f);
                case PoEElementType.Cold:      return new Color(0.48f, 0.82f, 1f);
                case PoEElementType.Lightning: return new Color(1f, 0.89f, 0.30f);
                case PoEElementType.Chaos:     return new Color(0.78f, 0.49f, 1f);
                default:                       return new Color(0.81f, 0.82f, 0.86f); // Physical
            }
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
