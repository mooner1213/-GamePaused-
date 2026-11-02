using System.Collections.Generic;
using UnityEngine;
namespace PauseClimb
{
    public enum PauseSoundCue { Jump, Land, Bounce, Ending }
    /// <summary>
    /// 배경음과 사건 효과음만 생성·재생하고 직접 만든 음원을 해제합니다.
    /// AudioSource는 자동 준비하며 별도 오디오 파일 연결은 필요하지 않습니다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PauseSoundPlayer : MonoBehaviour
    {
        #region Variables
        private const int SAMPLE_RATE = 22050;

        [Header("도트 배경음")]
        [SerializeField, Tooltip("반복 배경음의 음 높이입니다. 단위는 Hz입니다.")] private float[] _backgroundNotes = {164.81f,196f,246.94f,196f,146.83f,174.61f,220f,174.61f};
        [SerializeField, Tooltip("배경음의 음표 한 개 길이입니다.")] private float _backgroundNoteSeconds = 0.6f;
        [SerializeField, Tooltip("배경음 파형의 기본 크기입니다. 전체 음량 설정과 곱해집니다.")] private float _backgroundLoudness = 0.12f;
        [Header("사건 효과음")]
        [SerializeField, Tooltip("도약 효과음의 음 높이입니다.")] private float[] _jumpNotes = {330f,494f};
        [SerializeField, Tooltip("착지 효과음의 음 높이입니다.")] private float[] _landNotes = {98f};
        [SerializeField, Tooltip("벽과 배 반동의 음 높이입니다.")] private float[] _bounceNotes = {180f,120f};
        [SerializeField, Tooltip("정상 어퍼컷 성공 음의 높이입니다.")] private float[] _endingNotes = {330f,440f,554f,660f};
        [SerializeField, Tooltip("순서대로 도약·착지·반동·엔딩의 음표 길이입니다.")] private Vector4 _noteSeconds = new Vector4(0.065f,0.07f,0.06f,0.18f);
        [SerializeField, Tooltip("순서대로 도약·착지·반동·엔딩의 파형 크기입니다.")] private Vector4 _effectLoudness = new Vector4(0.32f,0.22f,0.2f,0.25f);
        [SerializeField, Tooltip("음표가 끝날 때 소리가 줄어드는 감쇠율입니다.")] private float _toneDecay = 2f;
        private float _masterVolume = 1f;

        #endregion
        #region Reference
        private AudioSource _musicSource, _effectSource;
        private readonly List<AudioClip> _clips = new List<AudioClip>();
        private AudioClip[] _effects;
        #endregion
        #region Property
        public float MusicVolume => _musicSource == null ? 0f : _musicSource.volume;
        public float EffectsVolume => _effectSource == null ? 0f : _effectSource.volume;
        #endregion
        #region Unity Event Method
        // 컴포넌트 수명이 끝나면 직접 생성한 음원만 정리합니다.
        private void OnDestroy() { PauseAudioPreferences.Changed -= ApplyVolumes; ReleaseClips(); }

        #endregion
        #region Custom Method
        // 필요한 소리를 한 번 만들고 전체 음량을 적용합니다.
        public void Initialize(float volume)
        {
            _musicSource = GetComponent<AudioSource>(); _effectSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true; _musicSource.clip = MakeTone("탑의 리듬", _backgroundNotes, _backgroundNoteSeconds, _backgroundLoudness);
            _effects = new[] { MakeTone("도약",_jumpNotes,_noteSeconds.x,_effectLoudness.x),MakeTone("착지",_landNotes,_noteSeconds.y,_effectLoudness.y),MakeTone("반동",_bounceNotes,_noteSeconds.z,_effectLoudness.z),MakeTone("저주 해제",_endingNotes,_noteSeconds.w,_effectLoudness.w) };
            PauseAudioPreferences.Changed -= ApplyVolumes;
            PauseAudioPreferences.Changed += ApplyVolumes;
            SetVolume(volume); _musicSource.Play();
        }
        // 기존 전체 음량은 보존하고 BGM·SFX의 사용자 설정을 각각 곱합니다.
        public void SetVolume(float value) { _masterVolume = Mathf.Clamp01(value); ApplyVolumes(); }
        // 슬라이더 변경 사건이 생길 때만 소스 음량을 갱신합니다.
        private void ApplyVolumes()
        {
            if (_musicSource != null) _musicSource.volume = _masterVolume * PauseAudioPreferences.Music;
            if (_effectSource != null) _effectSource.volume = _masterVolume * PauseAudioPreferences.Effects;
        }
        // 관리 메뉴에서만 설정한 소리를 들을 수 있게 하고, ESC 점프 계획의 기존 음원 정지는 유지합니다.
        public void SetMenuPreview(bool isEnabled)
        {
            if (_musicSource != null) _musicSource.ignoreListenerPause = isEnabled;
            if (_effectSource != null) _effectSource.ignoreListenerPause = isEnabled;
        }
        // 호출자는 정지 여부를 확인하고 사건이 생긴 시점에만 재생합니다.
        public void Play(PauseSoundCue cue) { if (_effects != null) _effectSource.PlayOneShot(_effects[(int)cue]); }

        // 외부 음악 의존 없이 짧은 감쇠 사인파 음표를 만듭니다.
        private AudioClip MakeTone(string name,float[] notes,float seconds,float loudness)
        {
            int perNote = Mathf.CeilToInt(SAMPLE_RATE * seconds); float[] samples = new float[notes.Length * perNote];
            for (int n = 0; n < notes.Length; n++) for (int i = 0; i < perNote; i++)
                samples[n*perNote+i] = Mathf.Sin(2f*Mathf.PI*notes[n]*i/SAMPLE_RATE)*Mathf.Sin(Mathf.PI*i/perNote)*Mathf.Exp(-_toneDecay*i/perNote)*loudness;
            AudioClip clip = AudioClip.Create(name,samples.Length,1,SAMPLE_RATE,false); clip.SetData(samples,0); _clips.Add(clip); return clip;
        }
        // 프로젝트의 외부 음원은 삭제하지 않습니다.
        private void ReleaseClips() { foreach (AudioClip clip in _clips) if (clip != null) Destroy(clip); _clips.Clear(); }
        #endregion
    }
}
