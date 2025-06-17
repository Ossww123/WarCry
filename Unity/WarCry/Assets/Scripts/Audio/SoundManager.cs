using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 씬 이름 열거형
/// 게임 내 모든 씬을 정의하고 BGM 매핑에 사용
/// </summary>
public enum SceneName
{
    StartScene,
    LoginScene,
    MainMenuScene,
    RoomListScene,
    WaitingRoomScene,
    BattleScene,
    ResultScene
}

/// <summary>
/// 게임 전반의 오디오 관리를 담당하는 싱글톤 매니저
/// BGM 자동 전환, SFX 풀링, 3D 사운드, 볼륨 설정 관리 등을 처리하며
/// 헤드리스 모드 지원과 씬 기반 자동 BGM 전환 기능을 제공
/// </summary>
public class SoundManager : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// SoundManager 싱글톤 인스턴스
    /// </summary>
    public static SoundManager Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;

    [Header("Audio Clips")]
    [SerializeField] private List<AudioClip> bgmClips;
    [SerializeField] private List<AudioClip> sfxClips;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 1.0f;
    [SerializeField] private int sfxPoolSize = 5;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const float MinVolumeValue = 0.0001f;
    private const float MaxVolumeValue = 1.0f;
    private const float DefaultVolume = 0.7f;
    private const float VolumeToDBMultiplier = 20f;

    // PlayerPrefs 키
    private const string MasterVolumeKey = "MasterVolume";
    private const string BGMVolumeKey = "BGMVolume";
    private const string SFXVolumeKey = "SFXVolume";
    private const string VoiceVolumeKey = "VoiceVolume";

    // Audio Mixer 파라미터
    private const string MasterVolumeParam = "MasterVolume";
    private const string BGMVolumeParam = "BGMVolume";
    private const string SFXVolumeParam = "SFXVolume";
    private const string VoiceVolumeParam = "VoiceVolume";

    #endregion

    #region Private Fields

    // BGM 관리
    private Dictionary<SceneName, int> sceneBgmMap = new Dictionary<SceneName, int>();
    private int currentBgmIndex = -1;
    private Coroutine bgmFadeCoroutine = null;

    // SFX 풀링
    private List<AudioSource> sfxPool;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeSingleton();
    }

    private void OnDestroy()
    {
        CleanupSoundManager();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 헤드리스 모드 검증 및 처리
    /// </summary>
    /// <returns>헤드리스 모드인 경우 true</returns>
    private bool ValidateHeadlessMode()
    {
        if (Application.isBatchMode)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 서버 모드에서 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글톤 인스턴스 초기화
    /// </summary>
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSoundManager();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SoundManager 인스턴스 생성됨");
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] SoundManager 인스턴스가 이미 존재합니다. 중복 인스턴스를 제거합니다.");

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// SoundManager 전체 초기화
    /// </summary>
    private void InitializeSoundManager()
    {
        InitializeSceneBGMMapping();
        LoadVolumeSettings();
        InitializeSFXPool();
        RegisterSceneEvents();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SoundManager 초기화 완료");
    }

    /// <summary>
    /// 씬별 BGM 매핑 초기화
    /// </summary>
    private void InitializeSceneBGMMapping()
    {
        sceneBgmMap.Clear();

        sceneBgmMap.Add(SceneName.StartScene, 0);
        sceneBgmMap.Add(SceneName.LoginScene, 0);
        sceneBgmMap.Add(SceneName.MainMenuScene, 0);
        sceneBgmMap.Add(SceneName.RoomListScene, 0);
        sceneBgmMap.Add(SceneName.WaitingRoomScene, 3);
        sceneBgmMap.Add(SceneName.BattleScene, 4);
        sceneBgmMap.Add(SceneName.ResultScene, 4);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬별 BGM 매핑 초기화 완료: {sceneBgmMap.Count}개 씬");
    }

    /// <summary>
    /// 저장된 볼륨 설정 로드 및 적용
    /// </summary>
    private void LoadVolumeSettings()
    {
        float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);
        float bgmVolume = PlayerPrefs.GetFloat(BGMVolumeKey, DefaultVolume);
        float sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, DefaultVolume);
        float voiceVolume = PlayerPrefs.GetFloat(VoiceVolumeKey, DefaultVolume);

        SetMasterVolume(masterVolume);
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
        SetVoiceVolume(voiceVolume);

        StartCoroutine(ReapplyVolumeSettingsAfterFrame(masterVolume, bgmVolume, sfxVolume, voiceVolume));

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 볼륨 설정 로드 완료 - Master: {masterVolume:F2}, BGM: {bgmVolume:F2}, SFX: {sfxVolume:F2}, Voice: {voiceVolume:F2}");
    }

    /// <summary>
    /// AudioMixer 설정이 확실히 적용되도록 1프레임 후 볼륨을 재적용
    /// </summary>
    private IEnumerator ReapplyVolumeSettingsAfterFrame(float master, float bgm, float sfx, float voice)
    {
        yield return null; // 1프레임 대기

        SetMasterVolume(master);
        SetBGMVolume(bgm);
        SetSFXVolume(sfx);
        SetVoiceVolume(voice);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 볼륨 설정 재적용 완료");
    }

    /// <summary>
    /// SFX 오브젝트 풀 초기화
    /// </summary>
    private void InitializeSFXPool()
    {
        sfxPool = new List<AudioSource>();

        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource source = CreateSFXPoolObject(i);
            sfxPool.Add(source);
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SFX 풀 초기화 완료: {sfxPoolSize}개 AudioSource");
    }

    /// <summary>
    /// SFX 풀용 AudioSource 오브젝트 생성
    /// </summary>
    /// <param name="index">풀 인덱스</param>
    /// <returns>생성된 AudioSource</returns>
    private AudioSource CreateSFXPoolObject(int index)
    {
        GameObject sfxObj = new GameObject($"SFX_{index}");
        sfxObj.transform.SetParent(transform);

        AudioSource source = sfxObj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;

        return source;
    }

    /// <summary>
    /// 씬 이벤트 등록
    /// </summary>
    private void RegisterSceneEvents()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// SoundManager 정리 작업
    /// </summary>
    private void CleanupSoundManager()
    {
        UnregisterSceneEvents();
        StopAllCoroutines();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SoundManager 정리 완료");
    }

    /// <summary>
    /// 씬 이벤트 등록 해제
    /// </summary>
    private void UnregisterSceneEvents()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region Scene Events

    /// <summary>
    /// 씬 로드 완료 시 자동 BGM 전환
    /// </summary>
    /// <param name="scene">로드된 씬</param>
    /// <param name="mode">씬 로드 모드</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (System.Enum.TryParse(scene.name, out SceneName sceneName))
        {
            PlaySceneBGM(sceneName);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 로드됨: {sceneName}, BGM 자동 전환 시도");
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 알 수 없는 씬 이름: {scene.name}");
        }
    }

    #endregion

    #region Public API - BGM Control

    /// <summary>
    /// BGM 재생 (인덱스 기반)
    /// </summary>
    /// <param name="index">BGM 클립 인덱스</param>
    /// <param name="withFade">페이드 효과 사용 여부</param>
    public void PlayBGM(int index, bool withFade = true)
    {
        if (!ValidateBGMIndex(index) || index == currentBgmIndex)
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] BGM 재생 요청: {index} (페이드: {withFade})");

        if (withFade && bgmSource.isPlaying)
        {
            StartBGMFade(index);
        }
        else
        {
            PlayBGMDirectly(index);
        }
    }

    /// <summary>
    /// 씬에 맞는 BGM 재생
    /// </summary>
    /// <param name="sceneName">씬 이름</param>
    public void PlaySceneBGM(SceneName sceneName)
    {
        if (sceneBgmMap.TryGetValue(sceneName, out int bgmIndex))
        {
            PlayBGM(bgmIndex);
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 씬 {sceneName}에 대한 BGM 매핑이 없습니다");
        }
    }

    #endregion

    #region Public API - SFX Control

    /// <summary>
    /// 2D 효과음 재생
    /// </summary>
    /// <param name="index">SFX 클립 인덱스</param>
    public void PlaySFX(int index)
    {
        if (!ValidateSFXIndex(index))
            return;

        AudioSource source = GetAvailableSFXSource();
        source.clip = sfxClips[index];
        source.spatialBlend = 0.0f; // 2D 사운드
        source.PlayOneShot(sfxClips[index]);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SFX 재생: {index}");
    }

    /// <summary>
    /// 3D 위치에서 효과음 재생
    /// </summary>
    /// <param name="index">SFX 클립 인덱스</param>
    /// <param name="position">재생 위치</param>
    /// <param name="minDistance">최소 거리</param>
    /// <param name="maxDistance">최대 거리</param>
    public void Play3DSFX(int index, Vector3 position, float minDistance = 5f, float maxDistance = 20f)
    {
        if (!ValidateSFXIndex(index))
            return;

        AudioSource source = GetAvailableSFXSource();
        Configure3DSFXSource(source, index, position, minDistance, maxDistance);
        source.Play();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 3D SFX 재생: {index} at {position}");
    }

    /// <summary>
    /// 모든 효과음 중지
    /// </summary>
    public void StopAllSFX()
    {
        foreach (var source in sfxPool)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 모든 SFX 중지됨");
    }

    #endregion

    #region Public API - Voice Control

    /// <summary>
    /// 음성 명령 재생
    /// </summary>
    /// <param name="voiceClip">재생할 음성 클립</param>
    public void PlayVoiceCommand(AudioClip voiceClip)
    {
        if (!ValidateVoiceClip(voiceClip))
            return;

        voiceSource.clip = voiceClip;
        voiceSource.Play();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 음성 명령 재생: {voiceClip.name}");
    }

    #endregion

    #region Public API - Volume Control

    /// <summary>
    /// 마스터 볼륨 설정
    /// </summary>
    /// <param name="volume">볼륨 값 (0-1)</param>
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume(MasterVolumeParam, volume);
        PlayerPrefs.SetFloat(MasterVolumeKey, volume);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 마스터 볼륨 설정: {volume:F2}");
    }

    /// <summary>
    /// BGM 볼륨 설정
    /// </summary>
    /// <param name="volume">볼륨 값 (0-1)</param>
    public void SetBGMVolume(float volume)
    {
        SetMixerVolume(BGMVolumeParam, volume);
        PlayerPrefs.SetFloat(BGMVolumeKey, volume);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] BGM 볼륨 설정: {volume:F2}");
    }

    /// <summary>
    /// SFX 볼륨 설정
    /// </summary>
    /// <param name="volume">볼륨 값 (0-1)</param>
    public void SetSFXVolume(float volume)
    {
        SetMixerVolume(SFXVolumeParam, volume);
        PlayerPrefs.SetFloat(SFXVolumeKey, volume);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SFX 볼륨 설정: {volume:F2}");
    }

    /// <summary>
    /// 음성 볼륨 설정
    /// </summary>
    /// <param name="volume">볼륨 값 (0-1)</param>
    public void SetVoiceVolume(float volume)
    {
        SetMixerVolume(VoiceVolumeParam, volume);
        PlayerPrefs.SetFloat(VoiceVolumeKey, volume);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 음성 볼륨 설정: {volume:F2}");
    }

    /// <summary>
    /// 음소거 토글
    /// </summary>
    /// <param name="isMuted">음소거 여부</param>
    public void ToggleMute(bool isMuted)
    {
        float muteValue = isMuted ? MinVolumeValue : PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);
        SetMasterVolume(muteValue);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 음소거 토글: {isMuted}");
    }

    #endregion

    #region Public API - Pause Control

    /// <summary>
    /// 모든 사운드 일시정지
    /// </summary>
    public void PauseAllSounds()
    {
        if (bgmSource.isPlaying) bgmSource.Pause();
        if (voiceSource.isPlaying) voiceSource.Pause();

        foreach (var source in sfxPool)
        {
            if (source.isPlaying) source.Pause();
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 모든 사운드 일시정지됨");
    }

    /// <summary>
    /// 모든 사운드 재개
    /// </summary>
    public void ResumeAllSounds()
    {
        if (!bgmSource.isPlaying) bgmSource.UnPause();
        if (!voiceSource.isPlaying) voiceSource.UnPause();

        foreach (var source in sfxPool)
        {
            if (!source.isPlaying) source.UnPause();
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 모든 사운드 재개됨");
    }

    #endregion

    #region BGM Management

    /// <summary>
    /// BGM 페이드 시작
    /// </summary>
    /// <param name="newIndex">새로운 BGM 인덱스</param>
    private void StartBGMFade(int newIndex)
    {
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }

        bgmFadeCoroutine = StartCoroutine(FadeBGM(newIndex));
    }

    /// <summary>
    /// BGM 직접 재생 (페이드 없음)
    /// </summary>
    /// <param name="index">BGM 인덱스</param>
    private void PlayBGMDirectly(int index)
    {
        bgmSource.clip = bgmClips[index];
        bgmSource.loop = true;
        bgmSource.Play();
        currentBgmIndex = index;
    }

    /// <summary>
    /// BGM 페이드 효과 코루틴
    /// </summary>
    /// <param name="newIndex">새로운 BGM 인덱스</param>
    private IEnumerator FadeBGM(int newIndex)
    {
        try
        {
            float startVolume = bgmSource.volume;

            // 페이드 아웃
            yield return StartCoroutine(FadeAudioSource(bgmSource, startVolume, 0f));

            // BGM 변경
            bgmSource.Stop();
            bgmSource.clip = bgmClips[newIndex];
            bgmSource.Play();
            currentBgmIndex = newIndex;

            // 페이드 인
            yield return StartCoroutine(FadeAudioSource(bgmSource, 0f, startVolume));
        }
        finally
        {
            bgmFadeCoroutine = null;
        }
    }

    /// <summary>
    /// AudioSource 볼륨 페이드 코루틴
    /// </summary>
    /// <param name="source">대상 AudioSource</param>
    /// <param name="startVolume">시작 볼륨</param>
    /// <param name="targetVolume">목표 볼륨</param>
    private IEnumerator FadeAudioSource(AudioSource source, float startVolume, float targetVolume)
    {
        source.volume = startVolume;

        while (Mathf.Abs(source.volume - targetVolume) > 0.01f)
        {
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.deltaTime * fadeSpeed);
            yield return null;
        }

        source.volume = targetVolume;
    }

    #endregion

    #region SFX Management

    /// <summary>
    /// 사용 가능한 SFX AudioSource 가져오기
    /// </summary>
    /// <returns>사용 가능한 AudioSource</returns>
    private AudioSource GetAvailableSFXSource()
    {
        foreach (var source in sfxPool)
        {
            if (!source.isPlaying) return source;
        }

        // 모두 재생 중이면 첫 번째 소스 재사용
        return sfxPool[0];
    }

    /// <summary>
    /// 3D SFX AudioSource 설정
    /// </summary>
    /// <param name="source">설정할 AudioSource</param>
    /// <param name="index">SFX 인덱스</param>
    /// <param name="position">재생 위치</param>
    /// <param name="minDistance">최소 거리</param>
    /// <param name="maxDistance">최대 거리</param>
    private void Configure3DSFXSource(AudioSource source, int index, Vector3 position, float minDistance, float maxDistance)
    {
        source.clip = sfxClips[index];
        source.transform.position = position;
        source.spatialBlend = 1.0f; // 완전 3D 사운드
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
    }

    #endregion

    #region Volume Management

    /// <summary>
    /// AudioMixer 볼륨 설정 (dB 변환)
    /// </summary>
    /// <param name="parameter">믹서 파라미터 이름</param>
    /// <param name="value">볼륨 값 (0-1)</param>
    private void SetMixerVolume(string parameter, float value)
    {
        float clampedVolume = Mathf.Clamp(value, MinVolumeValue, MaxVolumeValue);
        float dbValue = Mathf.Log10(clampedVolume) * VolumeToDBMultiplier;

        audioMixer.SetFloat(parameter, dbValue);

        audioMixer.SetFloat(parameter, dbValue);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] AudioMixer {parameter} 설정: {dbValue:F2}dB (원본값: {value:F2})");
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// BGM 인덱스 유효성 검증
    /// </summary>
    /// <param name="index">검증할 인덱스</param>
    /// <returns>유효한 인덱스면 true</returns>
    private bool ValidateBGMIndex(int index)
    {
        if (index < 0 || index >= bgmClips.Count)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 BGM 인덱스: {index} (범위: 0-{bgmClips.Count - 1})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// SFX 인덱스 유효성 검증
    /// </summary>
    /// <param name="index">검증할 인덱스</param>
    /// <returns>유효한 인덱스면 true</returns>
    private bool ValidateSFXIndex(int index)
    {
        if (index < 0 || index >= sfxClips.Count)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 SFX 인덱스: {index} (범위: 0-{sfxClips.Count - 1})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 음성 클립 유효성 검증
    /// </summary>
    /// <param name="voiceClip">검증할 음성 클립</param>
    /// <returns>유효한 클립이면 true</returns>
    private bool ValidateVoiceClip(AudioClip voiceClip)
    {
        if (voiceClip == null)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 음성 클립이 null입니다");
            return false;
        }

        return true;
    }

    #endregion
}