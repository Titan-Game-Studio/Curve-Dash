using UnityEngine;
using UnityEngine.UI;

namespace STG.CurveDash
{
    /// <summary>
    /// Prefab view for one filled gem socket row in the Equipment socket popup: a label and the compact
    /// Remove (✕) / Replace (↻) icon buttons, all on a single horizontal line. The prefab lives at
    /// <c>Assets/Resources/UI/SocketRow.prefab</c>; <see cref="EquipmentSocketUI"/> instantiates it, sets
    /// <see cref="Label"/> and wires the two buttons per gem.
    /// </summary>
    public class SocketRowView : MonoBehaviour
    {
        public Text Label;
        public Button RemoveButton;
        public Button ReplaceButton;
    }
}
