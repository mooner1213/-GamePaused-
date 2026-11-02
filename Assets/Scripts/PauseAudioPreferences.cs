using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>BGM과 효과음 설정을 분리해 저장하고 모든 활성 음원에 변경 사건을 전달합니다. Inspector 연결은 필요 없습니다.</summary>
    public static class PauseAudioPreferences
    {
        #region Variables
        private const string MUSIC_KEY = "PauseClimb.MusicVolume";
        private const string EFFECTS_KEY = "PauseClimb.EffectsVolume";
        private const float DEFAULT_VOLUME = 0.8f;
        private static float _music = PlayerPrefs.GetFloat(MUSIC_KEY, DEFAULT_VOLUME);
        private static float _effects = PlayerPrefs.GetFloat(EFFECTS_KEY, DEFAULT_VOLUME);
        #endregion
        #region Property
        public static float Music => _music;
        public static float Effects => _effects;
        public static event Action Changed;
        #endregion
        #region Custom Method
        // 드래그 중에는 메모리 설정과 실제 음원만 바꾸며 저장은 메뉴를 닫을 때 수행합니다.
        public static void SetMusic(float value) { _music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MUSIC_KEY, _music); Changed?.Invoke(); }
        // 효과음만 바꾸고 음악 설정은 유지합니다.
        public static void SetEffects(float value) { _effects = Mathf.Clamp01(value); PlayerPrefs.SetFloat(EFFECTS_KEY, _effects); Changed?.Invoke(); }
        // 씬 전환·메뉴 종료 시 사용자 설정을 디스크에 보존합니다.
        public static void Save() { PlayerPrefs.Save(); }
        #endregion
    }
}
