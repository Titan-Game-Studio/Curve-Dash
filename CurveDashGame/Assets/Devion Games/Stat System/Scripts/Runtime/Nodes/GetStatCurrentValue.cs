using UnityEngine;

namespace DevionGames.Graphs
{
    [ComponentMenu("Stat System/Get Stat Current Value")]
    [System.Serializable]
    public class GetStatCurrentValue : StatNode
    {
        public override object OnRequestValue(Port port)
        {
            if (statValue == null)
            {
                return 0f;
            }
            return ((StatSystem.Attribute)statValue).CurrentValue;
        }
    }
}