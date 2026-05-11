using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Curve-Dash/Catalogs/Audio Mapping Catalog")]
    public class AudioMappingCatalog : ScriptableObject
    {
        public List<AudioMapping> AudioAssets;
    }
}

