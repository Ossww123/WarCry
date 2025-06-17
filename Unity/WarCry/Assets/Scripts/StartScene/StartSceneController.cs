using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 시작 씬의 인트로 시퀀스와 사용자 상호작용을 관리하는 컨트롤러
/// 카메라 애니메이션, 팀 소개 시퀀스, 로고 연출, 패럴랙스 효과를 통해 게임의 첫 인상을 제공하며
/// 헤드리스 모드 지원과 시퀀스 스킵 기능을 포함
/// </summary>
public class StartSceneController : MonoBehaviour
{
    #region Inspector Fields

    [Header("World Canvas (World Space)")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private GameObject blueTeam;
    [SerializeField] private GameObject redTeam;
    [SerializeField] private GameObject middleFight;

    [Header("Mask Canvas (Screen Space - Camera)")]
    [SerializeField] private Canvas maskCanvas;
    [SerializeField] private Image blackTopMask;
    [SerializeField] private Image blackBottomMask;
    [SerializeField] private float letterboxHeight = 150f;

    [Header("UI Canvas (Screen Space - Overlay)")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private CanvasGroup uiCanvasGroup;
    [SerializeField] private Image blackBackground;
    [SerializeField] private Image flashImage;
    [SerializeField] private GameObject logoObject;
    [SerializeField] private GameObject startText;

    [Header("Camera Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform leftFocusPoint;
    [SerializeField] private Transform rightFocusPoint;
    [SerializeField] private Transform centerFocusPoint;
    [SerializeField] private Transform finalCameraPosition;
    [SerializeField] private float cameraOffset = 5f;

    [Header("Animation Timing")]
    [SerializeField] private float initialDelay = 1.0f;
    [SerializeField] private float panDuration = 2.0f;
    [SerializeField] private float pauseDuration = 0.5f;
    [SerializeField] private float revealDuration = 1.0f;
    [SerializeField] private float logoRevealDelay = 0.5f;
    [SerializeField] private float textBlinkSpeed = 0.8f;
    [SerializeField] private float sceneTransitionDuration = 1.0f;

    [Header("Logo Animation")]
    [SerializeField] private float logoScaleDuration = 0.5f;
    [SerializeField] private AnimationCurve logoScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Flash Effect")]
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Camera Shake Effect")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Parallax Effect")]
    [SerializeField] private float parallaxStrength = 0.1f;

    [Header("Audio")]
    [SerializeField] private int clashSoundIndex = 0;
    [SerializeField] private int startConfirmSoundIndex = 1;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const float TeamSequenceFOV = 10f;
    private const float DefaultFOV = 30f;
    private const string NextSceneName = "LoginScene";

    #endregion

    #region Private Fields

    // 상태 관리
    private bool introComplete = false;
    private bool hasStarted = false;
    private bool isSkipping = false;

    // 원본 Transform 정보 저장
    private Vector3 blueTeamOriginalPos;
    private Vector3 redTeamOriginalPos;
    private Vector3 middleFightOriginalPos;
    private Vector3 logoOriginalScale;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeStartScene();
    }

    private void Update()
    {
        if (introComplete)
        {
            HandlePostIntroInput();
        }
        else if (!isSkipping && Input.anyKeyDown)
        {
            SkipIntroSequence();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 StartSceneController 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 시작 씬 초기화
    /// </summary>
    private void InitializeStartScene()
    {
        SaveOriginalTransforms();
        SetupInitialState();
        StartCoroutine(PlayIntroSequence());

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] StartScene 초기화 완료");
    }

    /// <summary>
    /// 모든 오브젝트의 원본 Transform 정보 저장
    /// </summary>
    private void SaveOriginalTransforms()
    {
        if (blueTeam) blueTeamOriginalPos = blueTeam.transform.localPosition;
        if (redTeam) redTeamOriginalPos = redTeam.transform.localPosition;
        if (middleFight) middleFightOriginalPos = middleFight.transform.localPosition;
        if (logoObject) logoOriginalScale = logoObject.transform.localScale;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 원본 Transform 정보 저장 완료");
    }

    /// <summary>
    /// UI 및 카메라 초기 상태 설정
    /// </summary>
    private void SetupInitialState()
    {
        SetupUIInitialState();
        SetupCameraInitialState();
        SetupCanvasSettings();
    }

    /// <summary>
    /// UI 요소들의 초기 상태 설정
    /// </summary>
    private void SetupUIInitialState()
    {
        if (uiCanvasGroup) uiCanvasGroup.alpha = 1f;
        if (blackBackground) blackBackground.color = Color.black;

        SetupLetterbox(false);

        if (logoObject)
        {
            logoObject.SetActive(false);
            logoObject.transform.localScale = Vector3.zero;
        }

        if (startText) startText.SetActive(false);
        if (flashImage) flashImage.color = new Color(1, 1, 1, 0);
    }

    /// <summary>
    /// 카메라 초기 상태 설정
    /// </summary>
    private void SetupCameraInitialState()
    {
        if (mainCamera && leftFocusPoint)
        {
            mainCamera.transform.position = new Vector3(
                leftFocusPoint.position.x,
                leftFocusPoint.position.y,
                mainCamera.transform.position.z
            );
        }
    }

    /// <summary>
    /// 캔버스 설정 초기화
    /// </summary>
    private void SetupCanvasSettings()
    {
        if (maskCanvas)
        {
            maskCanvas.worldCamera = mainCamera;
        }
    }

    #endregion

    #region Intro Sequence Management

    /// <summary>
    /// 인트로 시퀀스 스킵 처리
    /// </summary>
    private void SkipIntroSequence()
    {
        isSkipping = true;
        StopAllCoroutines();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 인트로 시퀀스 스킵됨");

        SetupSkippedState();
        PlayClashSound();
        StartCoroutine(BlinkText());

        introComplete = true;
    }

    /// <summary>
    /// 스킵 후 최종 상태 설정
    /// </summary>
    private void SetupSkippedState()
    {
        // 카메라 및 UI 상태 복원
        if (mainCamera)
        {
            mainCamera.fieldOfView = DefaultFOV;
            if (finalCameraPosition)
                mainCamera.transform.position = finalCameraPosition.position;
        }

        if (uiCanvasGroup) uiCanvasGroup.alpha = 1f;

        SetupLetterbox(false);

        if (blackBackground) blackBackground.color = new Color(0, 0, 0, 0);
        if (flashImage) flashImage.color = new Color(1, 1, 1, 0);

        // 로고 및 텍스트 표시
        if (logoObject)
        {
            logoObject.SetActive(true);
            logoObject.transform.localScale = logoOriginalScale;
        }

        if (startText) startText.SetActive(true);
    }

    /// <summary>
    /// 전체 인트로 시퀀스 재생
    /// </summary>
    private IEnumerator PlayIntroSequence()
    {
        if (isSkipping) yield break;

        yield return new WaitForSeconds(initialDelay);

        // 블루팀 시퀀스
        yield return StartCoroutine(ShowTeamSequence(true));
        yield return new WaitForSeconds(pauseDuration);

        // 레드팀 시퀀스
        yield return StartCoroutine(ShowTeamSequence(false));
        yield return new WaitForSeconds(pauseDuration);

        // 중앙 충돌 연출
        yield return StartCoroutine(ShowMiddleClash());

        // 로고 애니메이션
        yield return StartCoroutine(AnimateLogo());

        // 시작 텍스트 표시
        yield return StartCoroutine(ShowStartText());

        introComplete = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 인트로 시퀀스 완료");
    }

    #endregion

    #region Team Sequence Animation

    /// <summary>
    /// 팀별 소개 시퀀스 재생
    /// </summary>
    /// <param name="isBlueTeam">블루팀 여부 (false면 레드팀)</param>
    private IEnumerator ShowTeamSequence(bool isBlueTeam)
    {
        if (isSkipping) yield break;

        var cameraPositions = CalculateTeamCameraPositions(isBlueTeam);
        float originalFOV = ApplyTeamSequenceFOV();

        yield return StartCoroutine(ExecuteTeamSequence(cameraPositions));

        RestoreCameraFOV(originalFOV);
    }

    /// <summary>
    /// 팀 시퀀스용 카메라 위치 계산
    /// </summary>
    /// <param name="isBlueTeam">블루팀 여부</param>
    /// <returns>시작 위치와 목표 위치</returns>
    private (Vector3 startPos, Vector3 targetPos) CalculateTeamCameraPositions(bool isBlueTeam)
    {
        Transform focusPoint = isBlueTeam ? leftFocusPoint : rightFocusPoint;
        float offset = cameraOffset;

        Vector3 startPos, targetPos;

        if (isBlueTeam)
        {
            startPos = new Vector3(focusPoint.position.x - offset, focusPoint.position.y, mainCamera.transform.position.z);
            targetPos = new Vector3(focusPoint.position.x + offset, focusPoint.position.y, mainCamera.transform.position.z);
        }
        else
        {
            startPos = new Vector3(focusPoint.position.x + offset, focusPoint.position.y, mainCamera.transform.position.z);
            targetPos = new Vector3(focusPoint.position.x - offset, focusPoint.position.y, mainCamera.transform.position.z);
        }

        return (startPos, targetPos);
    }

    /// <summary>
    /// 팀 시퀀스용 FOV 적용
    /// </summary>
    /// <returns>원본 FOV 값</returns>
    private float ApplyTeamSequenceFOV()
    {
        float originalFOV = mainCamera.fieldOfView;
        mainCamera.fieldOfView = TeamSequenceFOV;
        return originalFOV;
    }

    /// <summary>
    /// 카메라 FOV 복원
    /// </summary>
    /// <param name="originalFOV">복원할 FOV 값</param>
    private void RestoreCameraFOV(float originalFOV)
    {
        mainCamera.fieldOfView = originalFOV;
    }

    /// <summary>
    /// 팀 시퀀스 실행 (페이드 + 카메라 이동)
    /// </summary>
    /// <param name="positions">카메라 위치 정보</param>
    private IEnumerator ExecuteTeamSequence((Vector3 startPos, Vector3 targetPos) positions)
    {
        if (blackBackground) blackBackground.color = Color.black;
        if (mainCamera) mainCamera.transform.position = positions.startPos;

        SetupLetterbox(true);

        yield return StartCoroutine(FadeFromBlackToLetterbox(revealDuration * 0.5f));
        yield return StartCoroutine(SmoothCameraMove(positions.startPos, positions.targetPos, panDuration));
        yield return StartCoroutine(FadeFromLetterboxToBlack(revealDuration * 0.5f));
    }

    #endregion

    #region Fade Effects

    /// <summary>
    /// 검은 화면에서 레터박스로 페이드 아웃
    /// </summary>
    /// <param name="duration">페이드 지속 시간</param>
    private IEnumerator FadeFromBlackToLetterbox(float duration)
    {
        if (isSkipping) yield break;

        yield return StartCoroutine(FadeBlackBackground(1f, 0f, duration));
    }

    /// <summary>
    /// 레터박스에서 검은 화면으로 페이드 인
    /// </summary>
    /// <param name="duration">페이드 지속 시간</param>
    private IEnumerator FadeFromLetterboxToBlack(float duration)
    {
        if (isSkipping) yield break;

        yield return StartCoroutine(FadeBlackBackground(0f, 1f, duration));
    }

    /// <summary>
    /// 검은 배경 알파값 페이드 처리
    /// </summary>
    /// <param name="startAlpha">시작 알파값</param>
    /// <param name="targetAlpha">목표 알파값</param>
    /// <param name="duration">페이드 지속 시간</param>
    private IEnumerator FadeBlackBackground(float startAlpha, float targetAlpha, float duration)
    {
        if (!blackBackground) yield break;

        float elapsed = 0;

        while (elapsed < duration && !isSkipping)
        {
            float t = elapsed / duration;
            Color color = blackBackground.color;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            blackBackground.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정확한 최종 상태 설정
        if (blackBackground)
        {
            Color finalColor = blackBackground.color;
            finalColor.a = targetAlpha;
            blackBackground.color = finalColor;
        }
    }

    #endregion

    #region Middle Clash Sequence

    /// <summary>
    /// 중앙 충돌 시퀀스 재생
    /// </summary>
    private IEnumerator ShowMiddleClash()
    {
        if (isSkipping) yield break;

        SetupMiddleClashScene();
        PlayClashEffects();

        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(MoveCameraToFinalPosition());
    }

    /// <summary>
    /// 중앙 충돌 씬 설정
    /// </summary>
    private void SetupMiddleClashScene()
    {
        if (mainCamera && centerFocusPoint)
        {
            mainCamera.transform.position = new Vector3(
                centerFocusPoint.position.x,
                centerFocusPoint.position.y,
                mainCamera.transform.position.z
            );
        }

        SetupLetterbox(false);

        if (blackBackground) blackBackground.color = new Color(0, 0, 0, 0);
    }

    /// <summary>
    /// 충돌 효과 재생 (사운드, 플래시, 쉐이크)
    /// </summary>
    private void PlayClashEffects()
    {
        PlayClashSound();
        StartCoroutine(FlashEffect());

        if (mainCamera)
        {
            Vector3 originalCameraPos = mainCamera.transform.position;
            StartCoroutine(CameraShakeEffect(originalCameraPos));
        }
    }

    /// <summary>
    /// 카메라를 최종 위치로 이동
    /// </summary>
    private IEnumerator MoveCameraToFinalPosition()
    {
        if (!mainCamera || !finalCameraPosition || isSkipping) yield break;

        Vector3 startPos = mainCamera.transform.position;
        Vector3 targetPos = finalCameraPosition.position;

        float elapsed = 0;
        while (elapsed < revealDuration && !isSkipping)
        {
            float t = elapsed / revealDuration;
            mainCamera.transform.position = Vector3.Lerp(
                startPos,
                targetPos,
                Mathf.SmoothStep(0, 1, t)
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = targetPos;
    }

    #endregion

    #region Visual Effects

    /// <summary>
    /// 화면 플래시 효과
    /// </summary>
    private IEnumerator FlashEffect()
    {
        if (!flashImage || isSkipping) yield break;

        flashImage.color = new Color(1, 1, 1, 1);
        float elapsed = 0;

        while (elapsed < flashDuration && !isSkipping)
        {
            float t = elapsed / flashDuration;
            float alpha = flashCurve.Evaluate(t);
            flashImage.color = new Color(1, 1, 1, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        flashImage.color = new Color(1, 1, 1, 0);
    }

    /// <summary>
    /// 카메라 쉐이크 효과
    /// </summary>
    /// <param name="originalPosition">원본 카메라 위치</param>
    private IEnumerator CameraShakeEffect(Vector3 originalPosition)
    {
        if (!mainCamera || isSkipping) yield break;

        float elapsed = 0;

        while (elapsed < shakeDuration && !isSkipping)
        {
            float strength = shakeIntensity * (1 - (elapsed / shakeDuration));
            mainCamera.transform.position = originalPosition + Random.insideUnitSphere * strength;
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = originalPosition;
    }

    #endregion

    #region Logo and Text Animation

    /// <summary>
    /// 로고 애니메이션 재생
    /// </summary>
    private IEnumerator AnimateLogo()
    {
        if (isSkipping) yield break;

        yield return new WaitForSeconds(logoRevealDelay);

        if (!logoObject) yield break;

        logoObject.SetActive(true);

        float elapsed = 0;
        while (elapsed < logoScaleDuration && !isSkipping)
        {
            float t = elapsed / logoScaleDuration;
            float curveValue = logoScaleCurve.Evaluate(t);

            logoObject.transform.localScale = Vector3.Lerp(
                Vector3.zero,
                logoOriginalScale,
                curveValue
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        logoObject.transform.localScale = logoOriginalScale;
    }

    /// <summary>
    /// 시작 텍스트 표시 및 깜빡임 시작
    /// </summary>
    private IEnumerator ShowStartText()
    {
        if (isSkipping) yield break;

        if (startText)
        {
            startText.SetActive(true);
            StartCoroutine(BlinkText());
        }

        yield return null;
    }

    /// <summary>
    /// 텍스트 깜빡임 효과
    /// </summary>
    private IEnumerator BlinkText()
    {
        if (!startText) yield break;

        Component textComponent = GetTextComponent();
        if (textComponent == null) yield break;

        while (!hasStarted)
        {
            ToggleTextVisibility(textComponent);
            yield return new WaitForSeconds(textBlinkSpeed);
        }
    }

    #endregion

    #region Camera Movement

    /// <summary>
    /// 부드러운 카메라 이동
    /// </summary>
    /// <param name="startPos">시작 위치</param>
    /// <param name="targetPos">목표 위치</param>
    /// <param name="duration">이동 지속 시간</param>
    private IEnumerator SmoothCameraMove(Vector3 startPos, Vector3 targetPos, float duration)
    {
        if (!mainCamera || isSkipping) yield break;

        float elapsed = 0;
        Vector3 currentVelocity = Vector3.zero;

        while (elapsed < duration)
        {
            mainCamera.transform.position = Vector3.SmoothDamp(
                mainCamera.transform.position,
                targetPos,
                ref currentVelocity,
                duration - elapsed,
                Mathf.Infinity,
                Time.deltaTime
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = targetPos;
    }

    #endregion

    #region Parallax Effect

    /// <summary>
    /// 마우스 위치 기반 패럴랙스 효과 적용
    /// </summary>
    private void ApplyParallaxEffect()
    {
        Vector2 offset = CalculateMouseOffset();

        ApplyTeamParallax(offset);
    }

    /// <summary>
    /// 마우스 위치 기반 오프셋 계산
    /// </summary>
    /// <returns>정규화된 마우스 오프셋</returns>
    private Vector2 CalculateMouseOffset()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        return (mousePos - screenCenter) / screenCenter;
    }

    /// <summary>
    /// 팀 오브젝트들에 패럴랙스 효과 적용
    /// </summary>
    /// <param name="offset">마우스 오프셋</param>
    private void ApplyTeamParallax(Vector2 offset)
    {
        if (blueTeam)
        {
            blueTeam.transform.localPosition = blueTeamOriginalPos + new Vector3(
                offset.x * -parallaxStrength,
                offset.y * parallaxStrength,
                0
            );
        }

        if (redTeam)
        {
            redTeam.transform.localPosition = redTeamOriginalPos + new Vector3(
                offset.x * parallaxStrength,
                offset.y * parallaxStrength,
                0
            );
        }

        if (middleFight)
        {
            middleFight.transform.localPosition = middleFightOriginalPos + new Vector3(
                offset.x * (parallaxStrength * 0.5f),
                offset.y * (parallaxStrength * 0.5f),
                0
            );
        }
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// 인트로 완료 후 입력 처리
    /// </summary>
    private void HandlePostIntroInput()
    {
        ApplyParallaxEffect();

        if (!hasStarted && Input.anyKeyDown)
        {
            StartSceneTransition();
        }
    }

    /// <summary>
    /// 씬 전환 시작
    /// </summary>
    private void StartSceneTransition()
    {
        hasStarted = true;

        PlayStartConfirmSound();
        StopAllCoroutines();

        StartCoroutine(FadeOutAndLoadScene(NextSceneName));

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환 시작: {NextSceneName}");
    }

    #endregion

    #region Scene Transition

    /// <summary>
    /// 페이드 아웃 후 다음 씬 로드
    /// </summary>
    /// <param name="sceneName">로드할 씬 이름</param>
    private IEnumerator FadeOutAndLoadScene(string sceneName)
    {
        yield return StartCoroutine(FadeToBlack());
        yield return new WaitForSeconds(0.3f);

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 검은 화면으로 페이드 아웃
    /// </summary>
    private IEnumerator FadeToBlack()
    {
        if (!blackBackground) yield break;

        blackBackground.gameObject.SetActive(true);
        Color color = blackBackground.color;
        color.a = 0f;
        blackBackground.color = color;

        float elapsed = 0;
        while (elapsed < sceneTransitionDuration)
        {
            float t = elapsed / sceneTransitionDuration;
            color.a = Mathf.Lerp(0f, 1f, t);
            blackBackground.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        blackBackground.color = new Color(0f, 0f, 0f, 1f);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 레터박스 설정
    /// </summary>
    /// <param name="show">레터박스 표시 여부</param>
    private void SetupLetterbox(bool show)
    {
        if (blackTopMask && blackBottomMask)
        {
            blackTopMask.gameObject.SetActive(show);
            blackBottomMask.gameObject.SetActive(show);
        }

        if (show)
        {
            ConfigureLetterboxMasks();
        }
    }

    /// <summary>
    /// 레터박스 마스크 위치 및 크기 설정
    /// </summary>
    private void ConfigureLetterboxMasks()
    {
        if (!blackTopMask || !blackBottomMask) return;

        RectTransform topRect = blackTopMask.rectTransform;
        RectTransform bottomRect = blackBottomMask.rectTransform;

        // 위 마스크 설정
        topRect.anchorMin = new Vector2(0, 0.5f);
        topRect.anchorMax = new Vector2(1, 1);
        topRect.offsetMin = new Vector2(0, letterboxHeight / 2);
        topRect.offsetMax = new Vector2(0, 0);

        // 아래 마스크 설정
        bottomRect.anchorMin = new Vector2(0, 0);
        bottomRect.anchorMax = new Vector2(1, 0.5f);
        bottomRect.offsetMin = new Vector2(0, 0);
        bottomRect.offsetMax = new Vector2(0, -letterboxHeight / 2);
    }

    /// <summary>
    /// 텍스트 컴포넌트 가져오기 (Text 또는 TextMeshPro)
    /// </summary>
    /// <returns>찾은 텍스트 컴포넌트</returns>
    private Component GetTextComponent()
    {
        Component textComponent = startText.GetComponent<Text>();
        if (textComponent == null)
        {
            textComponent = startText.GetComponent<TMPro.TextMeshProUGUI>();
        }

        return textComponent;
    }

    /// <summary>
    /// 텍스트 가시성 토글
    /// </summary>
    /// <param name="textComponent">토글할 텍스트 컴포넌트</param>
    private void ToggleTextVisibility(Component textComponent)
    {
        if (textComponent is Text text)
        {
            text.enabled = !text.enabled;
        }
        else if (textComponent is TMPro.TextMeshProUGUI tmpText)
        {
            tmpText.enabled = !tmpText.enabled;
        }
    }

    /// <summary>
    /// 충돌 사운드 재생
    /// </summary>
    private void PlayClashSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(clashSoundIndex);
        }
    }

    /// <summary>
    /// 시작 확인 사운드 재생
    /// </summary>
    private void PlayStartConfirmSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(startConfirmSoundIndex);
        }
    }

    #endregion
}