using UnityEngine;

namespace DevionGames.Graphs
{
    [ComponentMenu("Stat System/Get Stat Value")]
    [System.Serializable]
    public class GetStatValue : StatNode
    {
        public override object OnRequestValue(Port port)
        {
            if (statValue == null)
            {
                return 0f;
            }
            return statValue.Value;
        }
    }
}