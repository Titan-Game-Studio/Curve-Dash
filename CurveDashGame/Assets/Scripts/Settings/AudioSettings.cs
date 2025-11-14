using System;
using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [Serializable]
    public class AudioSettings
    {
        public AudioClip BallHitCrystalSound;
        public float BallHitCrystalVolume = 1.0f;
        public AudioClip GameStartSound;
        public AudioClip BallFallSound;
        public AudioClip BallTurnSound;
        public AudioClip NextLevelSound;
        public List<AudioClip> BackgroundSounds;
    }
}