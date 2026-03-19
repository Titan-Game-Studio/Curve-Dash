using System;
using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [Serializable]
    public class AudioSettings
    {
        [HideInInspector] public AudioClip BallHitCrystalSound;
        public float BallHitCrystalVolume = 1.0f;
        [HideInInspector] public AudioClip GameStartSound;
        [HideInInspector] public AudioClip BallFallSound;
        [HideInInspector] public AudioClip BallTurnSound;
        [HideInInspector] public AudioClip NextLevelSound;
        [HideInInspector] public List<AudioClip> BackgroundSounds;
    }
}