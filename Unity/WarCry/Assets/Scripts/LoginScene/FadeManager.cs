using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

/// <summary>
/// 씬 전환 시 페이드 인/아웃 효과를 관리하는 매니저
/// CanvasGroup을 사용하여 부드러운 알파 전환과 UI 인터랙션 제어를 제공하며
/// 헤드리스 모드 지원과 다양한 페이드 옵션을 포함
/// </summary>
public class FadeManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Auto Fade")]
    [SerializeField] private bool autoFadeInOnStart = true;
    [SerializeField] private float initialDelay = 0f;

    [Header("UI Interaction")]
    [SerializeField] private bool blockRaycastsDuringFade = true;
    [SerializeField] private bool disableInteractionDuringFade = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Events

    /// <summary>
    /// 페이드 인 시작 시 발생하는 이벤트
    /// </summary>
    public static event Action OnFadeInStarted;

    /// <summary>
    /// 페이드 인 완료 시 발생하는 이벤트
    /// </summary>
    public static event Action OnFadeInCompleted;

    /// <summary>
    /// 페이드 아웃 시작 시 발생하는 이벤트
    /// </summary>
    public static event Action OnFadeOutStarted;

    /// <summary>
    /// 페이드 아웃 완료 시 발생하는 이벤트
    /// </summary>
    public static event Action OnFadeOutCompleted;

    #endregion

    #region Constants

    private const float MinAlpha = 0f;
    private const float MaxAlpha = 1f;
    private const float DefaultFadeDuration = 1.5f;

    #endregion

    #region Private Fields

    // 페이드 상태
    private bool isFading = false;
    private Coroutine currentFadeCoroutine = null;

    // 초기 상태 저장
    private bool originalBlocksRaycasts;
    private bool originalInteractable;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        ValidateComponents();
        SaveInitialState();
    }

    private void Start()
    {
        if (autoFadeInOnStart)
        {
            StartAutoFadeIn();
        }
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 FadeManager 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 필수 컴포넌트 유효성 검증
    /// </summary>
    private void ValidateComponents()
    {
        if (fadeGroup == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] FadeGroup이 설정되지 않았습니다!");
            return;
        }

        if (fadeDuration <= 0f)
        {
            fadeDuration = DefaultFadeDuration;

            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 fadeDuration 값. 기본값으로 설정: {DefaultFadeDuration}");
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] FadeManager 컴포넌트 검증 완료");
    }

    /// <summary>
    /// 초기 상태 저장
    /// </summary>
    private void SaveInitialState()
    {
        if (fadeGroup == null) return;

        originalBlocksRaycasts = fadeGroup.blocksRaycasts;
        originalInteractable = fadeGroup.interactable;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 초기 상태 저장 - BlocksRaycasts: {originalBlocksRaycasts}, Interactable: {originalInteractable}");
    }

    /// <summary>
    /// 자동 페이드 인 시작
    /// </summary>
    private void StartAutoFadeIn()
    {
        if (initialDelay > 0f)
        {
            StartCoroutine(DelayedAutoFadeIn());
        }
        else
        {
            FadeIn();
        }
    }

    /// <summary>
    /// 지연 후 자동 페이드 인
    /// </summary>
    private IEnumerator DelayedAutoFadeIn()
    {
        yield return new WaitForSeconds(initialDelay);
        FadeIn();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 페이드 인 시작 (불투명 → 투명)
    /// </summary>
    /// <param name="duration">페이드 지속 시간 (기본값 사용 시 null)</param>
    /// <param name="onComplete">완료 시 콜백</param>
    public void FadeIn(float? duration = null, Action onComplete = null)
    {
        float fadeTime = duration ?? fadeDuration;
        StartFadeCoroutine(MaxAlpha, MinAlpha, fadeTime, FadeType.In, onComplete);
    }

    /// <summary>
    /// 페이드 아웃 시작 (투명 → 불투명)
    /// </summary>
    /// <param name="duration">페이드 지속 시간 (기본값 사용 시 null)</param>
    /// <param name="onComplete">완료 시 콜백</param>
    public void FadeOut(float? duration = null, Action onComplete = null)
    {
        float fadeTime = duration ?? fadeDuration;
        StartFadeCoroutine(MinAlpha, MaxAlpha, fadeTime, FadeType.Out, onComplete);
    }

    /// <summary>
    /// 즉시 페이드 인 (애니메이션 없음)
    /// </summary>
    public void FadeInImmediate()
    {
        SetAlphaImmediate(MinAlpha);
        RestoreInteraction();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 즉시 페이드 인 완료");
    }

    /// <summary>
    /// 즉시 페이드 아웃 (애니메이션 없음)
    /// </summary>
    public void FadeOutImmediate()
    {
        SetAlphaImmediate(MaxAlpha);
        BlockInteraction();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 즉시 페이드 아웃 완료");
    }

    /// <summary>
    /// 현재 페이드 중단
    /// </summary>
    public void StopFade()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = null;
            isFading = false;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 페이드 중단됨");
        }
    }

    /// <summary>
    /// 특정 알파값으로 즉시 설정
    /// </summary>
    /// <param name="alpha">설정할 알파값 (0-1)</param>
    public void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        SetAlphaImmediate(alpha);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 알파값 설정: {alpha:F2}");
    }

    #endregion

    #region Fade Coroutines

    /// <summary>
    /// 페이드 타입 열거형
    /// </summary>
    private enum FadeType
    {
        In,
        Out
    }

    /// <summary>
    /// 페이드 코루틴 시작
    /// </summary>
    /// <param name="startAlpha">시작 알파값</param>
    /// <param name="endAlpha">목표 알파값</param>
    /// <param name="duration">페이드 지속 시간</param>
    /// <param name="fadeType">페이드 타입</param>
    /// <param name="onComplete">완료 콜백</param>
    private void StartFadeCoroutine(float startAlpha, float endAlpha, float duration, FadeType fadeType, Action onComplete)
    {
        if (isFading)
        {
            StopFade();
        }

        if (fadeGroup == null)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] FadeGroup이 없어 페이드를 실행할 수 없습니다");

            onComplete?.Invoke();
            return;
        }

        currentFadeCoroutine = StartCoroutine(FadeCoroutine(startAlpha, endAlpha, duration, fadeType, onComplete));
    }

    /// <summary>
    /// 페이드 애니메이션 코루틴
    /// </summary>
    /// <param name="startAlpha">시작 알파값</param>
    /// <param name="endAlpha">목표 알파값</param>
    /// <param name="duration">페이드 지속 시간</param>
    /// <param name="fadeType">페이드 타입</param>
    /// <param name="onComplete">완료 콜백</param>
    private IEnumerator FadeCoroutine(float startAlpha, float endAlpha, float duration, FadeType fadeType, Action onComplete)
    {
        isFading = true;

        // 페이드 시작 처리
        HandleFadeStart(fadeType, startAlpha);

        // 페이드 애니메이션
        yield return StartCoroutine(AnimateFade(startAlpha, endAlpha, duration));

        // 페이드 완료 처리
        HandleFadeComplete(fadeType, endAlpha);

        // 정리
        isFading = false;
        currentFadeCoroutine = null;

        // 완료 콜백 호출
        onComplete?.Invoke();
    }

    /// <summary>
    /// 페이드 애니메이션 실행
    /// </summary>
    /// <param name="startAlpha">시작 알파값</param>
    /// <param name="endAlpha">목표 알파값</param>
    /// <param name="duration">지속 시간</param>
    private IEnumerator AnimateFade(float startAlpha, float endAlpha, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            float curveValue = fadeCurve.Evaluate(progress);
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);

            fadeGroup.alpha = currentAlpha;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 정확한 최종값 설정
        fadeGroup.alpha = endAlpha;
    }

    #endregion

    #region Fade Event Handling

    /// <summary>
    /// 페이드 시작 처리
    /// </summary>
    /// <param name="fadeType">페이드 타입</param>
    /// <param name="startAlpha">시작 알파값</param>
    private void HandleFadeStart(FadeType fadeType, float startAlpha)
    {
        fadeGroup.alpha = startAlpha;

        if (fadeType == FadeType.In)
        {
            TriggerFadeInStarted();

            if (disableInteractionDuringFade)
                BlockInteraction();
        }
        else
        {
            TriggerFadeOutStarted();

            if (disableInteractionDuringFade)
                BlockInteraction();
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {fadeType} 페이드 시작 - 시작 알파: {startAlpha:F2}");
    }

    /// <summary>
    /// 페이드 완료 처리
    /// </summary>
    /// <param name="fadeType">페이드 타입</param>
    /// <param name="endAlpha">최종 알파값</param>
    private void HandleFadeComplete(FadeType fadeType, float endAlpha)
    {
        if (fadeType == FadeType.In)
        {
            RestoreInteraction();
            TriggerFadeInCompleted();
        }
        else
        {
            RestoreInteraction();
            TriggerFadeOutCompleted();
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {fadeType} 페이드 완료 - 최종 알파: {endAlpha:F2}");
    }

    #endregion

    #region UI Interaction Control

    /// <summary>
    /// UI 상호작용 차단
    /// </summary>
    private void BlockInteraction()
    {
        if (fadeGroup == null) return;

        if (blockRaycastsDuringFade)
            fadeGroup.blocksRaycasts = true;

        if (disableInteractionDuringFade)
            fadeGroup.interactable = false;
    }

    /// <summary>
    /// UI 상호작용 복원
    /// </summary>
    private void RestoreInteraction()
    {
        if (fadeGroup == null) return;

        fadeGroup.blocksRaycasts = originalBlocksRaycasts;
        fadeGroup.interactable = originalInteractable;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 즉시 알파값 설정
    /// </summary>
    /// <param name="alpha">설정할 알파값</param>
    private void SetAlphaImmediate(float alpha)
    {
        if (fadeGroup != null)
        {
            fadeGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    /// <summary>
    /// 현재 페이드 진행 여부 확인
    /// </summary>
    /// <returns>페이드 중이면 true</returns>
    public bool IsFading()
    {
        return isFading;
    }

    /// <summary>
    /// 현재 알파값 가져오기
    /// </summary>
    /// <returns>현재 알파값 (0-1)</returns>
    public float GetCurrentAlpha()
    {
        return fadeGroup != null ? fadeGroup.alpha : 0f;
    }

    /// <summary>
    /// 페이드 완전히 투명한지 확인
    /// </summary>
    /// <returns>완전히 투명하면 true</returns>
    public bool IsFullyTransparent()
    {
        return Mathf.Approximately(GetCurrentAlpha(), MinAlpha);
    }

    /// <summary>
    /// 페이드 완전히 불투명한지 확인
    /// </summary>
    /// <returns>완전히 불투명하면 true</returns>
    public bool IsFullyOpaque()
    {
        return Mathf.Approximately(GetCurrentAlpha(), MaxAlpha);
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 페이드 인 시작 이벤트 발생
    /// </summary>
    private void TriggerFadeInStarted()
    {
        OnFadeInStarted?.Invoke();
    }

    /// <summary>
    /// 페이드 인 완료 이벤트 발생
    /// </summary>
    private void TriggerFadeInCompleted()
    {
        OnFadeInCompleted?.Invoke();
    }

    /// <summary>
    /// 페이드 아웃 시작 이벤트 발생
    /// </summary>
    private void TriggerFadeOutStarted()
    {
        OnFadeOutStarted?.Invoke();
    }

    /// <summary>
    /// 페이드 아웃 완료 이벤트 발생
    /// </summary>
    private void TriggerFadeOutCompleted()
    {
        OnFadeOutCompleted?.Invoke();
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 페이드 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Fade Status")]
    public void LogFadeStatus()
    {
        Debug.Log($"=== FadeManager 상태 정보 ===");
        Debug.Log($"페이드 진행 중: {isFading}");
        Debug.Log($"현재 알파값: {GetCurrentAlpha():F2}");
        Debug.Log($"페이드 지속 시간: {fadeDuration}");
        Debug.Log($"자동 페이드 인: {autoFadeInOnStart}");
        Debug.Log($"레이캐스트 차단: {blockRaycastsDuringFade}");
        Debug.Log($"상호작용 비활성화: {disableInteractionDuringFade}");
    }

    #endregion
}