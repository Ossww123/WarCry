using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 옵션 패널의 UI 및 설정 관리를 담당하는 컨트롤러
/// 볼륨 설정, 화면 설정, 패널 애니메이션 등을 처리하며
/// SoundManager와 연동하여 실시간 설정 변경을 제공
/// </summary>
public class OptionPanelController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;

    [Header("Display Settings")]
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Test Audio")]
    [SerializeField] private Button testSFXButton;
    [SerializeField] private int testSFXIndex = 0;

    [Header("Panel Animation")]
    [SerializeField] private RectTransform optionPanelRect;
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private Vector2 hiddenPosition = new Vector2(0, 800);
    [SerializeField] private Vector2 visiblePosition = new Vector2(0, 0);

    [Header("Overlay Effect")]
    [SerializeField] private Image blackOverlayImage;
    [SerializeField] private float overlayMaxAlpha = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    // PlayerPrefs 키
    private const string MasterVolumeKey = "MasterVolume";
    private const string BGMVolumeKey = "BGMVolume";
    private const string SFXVolumeKey = "SFXVolume";
    private const string VoiceVolumeKey = "VoiceVolume";
    private const string FullscreenKey = "Fullscreen";

    // 기본값
    private const float DefaultMasterVolume = 1.0f;
    private const float DefaultBGMVolume = 0.7f;
    private const float DefaultSFXVolume = 1.0f;
    private const float DefaultVoiceVolume = 1.0f;

    #endregion

    #region Private Fields

    private Coroutine currentAnimationCoroutine;
    private bool isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeOptionPanel();
    }

    private void Start()
    {
        SetInitialPanelPosition();
    }

    private void OnDestroy()
    {
        CleanupOptionPanel();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 OptionPanelController 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 옵션 패널 초기화
    /// </summary>
    private void InitializeOptionPanel()
    {
        ValidateComponents();
        ApplySavedSettingsToSoundManager(); // 게임 시작 시 저장된 설정 적용
        InitializeUI();

        isInitialized = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] OptionPanelController 초기화 완료");
    }

    /// <summary>
    /// 필수 컴포넌트 유효성 검증
    /// </summary>
    private void ValidateComponents()
    {
        if (optionPanelRect == null)
        {
            optionPanelRect = GetComponent<RectTransform>();
        }

        if (SoundManager.Instance == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] SoundManager 인스턴스를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// UI 요소 초기화
    /// </summary>
    private void InitializeUI()
    {
        InitializeVolumeSliders();
        InitializeDisplaySettings();
        InitializeTestButton();
        SetupEventListeners();
    }

    /// <summary>
    /// 볼륨 슬라이더 초기화
    /// </summary>
    private void InitializeVolumeSliders()
    {
        if (masterSlider != null)
        {
            masterSlider.value = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        }

        if (bgmSlider != null)
        {
            bgmSlider.value = PlayerPrefs.GetFloat(BGMVolumeKey, DefaultBGMVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat(SFXVolumeKey, DefaultSFXVolume);
        }

        if (voiceSlider != null)
        {
            voiceSlider.value = PlayerPrefs.GetFloat(VoiceVolumeKey, DefaultVoiceVolume);
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 볼륨 슬라이더 초기화 완료");
    }

    /// <summary>
    /// 화면 설정 초기화
    /// </summary>
    private void InitializeDisplaySettings()
    {
        if (fullscreenToggle != null)
        {
            bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            Screen.fullScreen = savedFullscreen;
            fullscreenToggle.isOn = savedFullscreen;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전체화면 설정 초기화: {savedFullscreen}");
        }
    }

    /// <summary>
    /// 테스트 버튼 초기화
    /// </summary>
    private void InitializeTestButton()
    {
        if (testSFXButton != null)
        {
            testSFXButton.onClick.AddListener(PlayTestSound);
        }
    }

    /// <summary>
    /// 이벤트 리스너 설정
    /// </summary>
    private void SetupEventListeners()
    {
        if (SoundManager.Instance == null)
            return;

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(SoundManager.Instance.SetMasterVolume);

        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(SoundManager.Instance.SetBGMVolume);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(SoundManager.Instance.SetSFXVolume);

        if (voiceSlider != null)
            voiceSlider.onValueChanged.AddListener(SoundManager.Instance.SetVoiceVolume);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    /// <summary>
    /// 패널 초기 위치 설정
    /// </summary>
    private void SetInitialPanelPosition()
    {
        if (optionPanelRect != null)
        {
            optionPanelRect.anchoredPosition = hiddenPosition;
        }
    }

    /// <summary>
    /// 옵션 패널 정리 작업
    /// </summary>
    private void CleanupOptionPanel()
    {
        RemoveEventListeners();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] OptionPanelController 정리 완료");
    }

    /// <summary>
    /// 이벤트 리스너 제거
    /// </summary>
    private void RemoveEventListeners()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveAllListeners();
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveAllListeners();
        if (voiceSlider != null) voiceSlider.onValueChanged.RemoveAllListeners();
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveAllListeners();
        if (testSFXButton != null) testSFXButton.onClick.RemoveAllListeners();
    }

    #endregion

    #region Public API - Panel Control

    /// <summary>
    /// 옵션 패널 표시
    /// </summary>
    public void ShowPanel()
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 패널이 아직 초기화되지 않았습니다");
            return;
        }

        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
        }

        gameObject.SetActive(true);
        currentAnimationCoroutine = StartCoroutine(MovePanelCoroutine(visiblePosition));

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 옵션 패널 표시 시작");
    }

    /// <summary>
    /// 옵션 패널 숨기기
    /// </summary>
    public void HidePanel()
    {
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
        }

        currentAnimationCoroutine = StartCoroutine(MovePanelCoroutine(hiddenPosition, () => gameObject.SetActive(false)));

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 옵션 패널 숨기기 시작");
    }

    #endregion

    #region Panel Animation

    /// <summary>
    /// 패널 이동 애니메이션 코루틴
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <param name="onComplete">완료 시 호출할 콜백</param>
    private IEnumerator MovePanelCoroutine(Vector2 targetPosition, System.Action onComplete = null)
    {
        Vector2 startPosition = optionPanelRect.anchoredPosition;
        float elapsedTime = 0f;

        // 오버레이 알파값 계산
        float startAlpha = (targetPosition == visiblePosition) ? 0f : overlayMaxAlpha;
        float targetAlpha = (targetPosition == visiblePosition) ? overlayMaxAlpha : 0f;

        // 오버레이 활성화
        if (blackOverlayImage != null)
        {
            blackOverlayImage.gameObject.SetActive(true);
        }

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / transitionDuration);

            // 패널 위치 애니메이션
            optionPanelRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, normalizedTime);

            // 오버레이 알파 애니메이션
            if (blackOverlayImage != null)
            {
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, normalizedTime);
                Color overlayColor = blackOverlayImage.color;
                overlayColor.a = currentAlpha;
                blackOverlayImage.color = overlayColor;
            }

            yield return null;
        }

        // 최종 위치 설정
        optionPanelRect.anchoredPosition = targetPosition;

        // 오버레이 최종 처리
        if (blackOverlayImage != null)
        {
            if (targetAlpha == 0f)
            {
                blackOverlayImage.gameObject.SetActive(false);
            }
            else
            {
                Color finalColor = blackOverlayImage.color;
                finalColor.a = targetAlpha;
                blackOverlayImage.color = finalColor;
            }
        }

        // 완료 콜백 실행
        onComplete?.Invoke();

        currentAnimationCoroutine = null;
    }

    #endregion

    #region Settings Management

    /// <summary>
    /// 저장된 설정을 SoundManager에 적용
    /// 게임 시작 시 호출되어 이전 세션의 설정을 복원
    /// </summary>
    private void ApplySavedSettingsToSoundManager()
    {
        if (SoundManager.Instance == null)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] SoundManager가 없어 저장된 설정을 적용할 수 없습니다");
            return;
        }

        // 저장된 볼륨 값들을 SoundManager에 적용
        float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        float bgmVolume = PlayerPrefs.GetFloat(BGMVolumeKey, DefaultBGMVolume);
        float sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, DefaultSFXVolume);
        float voiceVolume = PlayerPrefs.GetFloat(VoiceVolumeKey, DefaultVoiceVolume);

        SoundManager.Instance.SetMasterVolume(masterVolume);
        SoundManager.Instance.SetBGMVolume(bgmVolume);
        SoundManager.Instance.SetSFXVolume(sfxVolume);
        SoundManager.Instance.SetVoiceVolume(voiceVolume);

        if (verboseLogging)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 저장된 볼륨 설정 적용 완료");
            Debug.Log($"  - Master: {masterVolume:F2}, BGM: {bgmVolume:F2}, SFX: {sfxVolume:F2}, Voice: {voiceVolume:F2}");
        }
    }

    /// <summary>
    /// 전체화면 설정 변경
    /// </summary>
    /// <param name="isFullscreen">전체화면 여부</param>
    private void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전체화면 설정 변경: {isFullscreen}");
    }

    /// <summary>
    /// 테스트 사운드 재생
    /// </summary>
    private void PlayTestSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(testSFXIndex);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 테스트 사운드 재생: {testSFXIndex}");
        }
    }

    /// <summary>
    /// 슬라이더 변경 시 효과음 재생
    /// </summary>
    public void PlaySliderChangedSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(testSFXIndex);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 패널 표시 상태 확인
    /// </summary>
    /// <returns>패널이 표시되고 있으면 true</returns>
    public bool IsPanelVisible()
    {
        return gameObject.activeInHierarchy &&
               Mathf.Approximately(optionPanelRect.anchoredPosition.y, visiblePosition.y);
    }

    /// <summary>
    /// 패널 애니메이션 진행 중 여부 확인
    /// </summary>
    /// <returns>애니메이션 진행 중이면 true</returns>
    public bool IsAnimating()
    {
        return currentAnimationCoroutine != null;
    }

    /// <summary>
    /// 모든 설정을 기본값으로 재설정
    /// </summary>
    public void ResetToDefaults()
    {
        if (masterSlider != null) masterSlider.value = DefaultMasterVolume;
        if (bgmSlider != null) bgmSlider.value = DefaultBGMVolume;
        if (sfxSlider != null) sfxSlider.value = DefaultSFXVolume;
        if (voiceSlider != null) voiceSlider.value = DefaultVoiceVolume;
        if (fullscreenToggle != null) fullscreenToggle.isOn = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 모든 설정을 기본값으로 재설정");
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 옵션 패널 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log OptionPanel Status")]
    public void LogOptionPanelStatus()
    {
        Debug.Log($"=== OptionPanel 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"패널 표시 중: {IsPanelVisible()}");
        Debug.Log($"애니메이션 진행 중: {IsAnimating()}");
        Debug.Log($"현재 위치: {optionPanelRect.anchoredPosition}");
        Debug.Log($"SoundManager 연결: {SoundManager.Instance != null}");
    }

    #endregion
}