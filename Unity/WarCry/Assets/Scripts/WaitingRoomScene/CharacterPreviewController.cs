using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 프리뷰 및 시각 효과 관리를 담당하는 컨트롤러
/// 캐릭터 머티리얼 변경, 파티클 효과 재생, 프리뷰 애니메이션 등을 처리하며
/// 색상 변경 시 실시간 캐릭터 프리뷰 업데이트와 시각적 피드백을 제공
/// </summary>
public class CharacterPreviewController : MonoBehaviour
{
    #region Events

    /// <summary>
    /// 캐릭터 프리뷰 업데이트 완료 시 발생하는 이벤트 (색상 인덱스)
    /// </summary>
    public static event System.Action<int> OnPreviewUpdated;

    #endregion

    #region Inspector Fields

    [Header("Character Preview")]
    [SerializeField] private GameObject characterPreview;
    [SerializeField] private Material[] characterMaterials;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem tornadoParticle;
    [SerializeField] private float particlePlayDuration = 1.5f;
    [SerializeField] private bool useColoredParticles = true;

    [Header("Color Configuration")]
    [SerializeField] private Color[] particleColors = PalettesManager.colors;

    [Header("Animation Settings")]
    [SerializeField] private bool enablePreviewAnimation = true;
    [SerializeField] private float rotationSpeed = 0f;
    [SerializeField] private float bobHeight = 0f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("Material Isolation")]
    [SerializeField] private bool isolateParticleMaterials = true;
    [SerializeField] private bool excludeParticleRenderers = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Private Fields

    // 상태 관리
    private bool isInitialized = false;
    private int currentColorIndex = 0;

    // 애니메이션 관리
    private Coroutine animationCoroutine;
    private Vector3 originalPosition;
    private Vector3 originalRotation;

    // 머티리얼 관리
    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    private List<Renderer> characterRenderers = new List<Renderer>();

    // 파티클 관리
    private Coroutine particleStopCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        ValidateReferences();
    }

    private void Start()
    {
        InitializeCharacterPreview();
    }

    private void OnDestroy()
    {
        CleanupCharacterPreview();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 CharacterPreviewController 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 필수 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (characterPreview == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] CharacterPreview가 설정되지 않았습니다!");
            return;
        }

        if (characterMaterials == null || characterMaterials.Length == 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] CharacterMaterials가 설정되지 않았습니다!");
            return;
        }

        if (particleColors == null || particleColors.Length == 0)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] ParticleColors가 설정되지 않았습니다. 기본 색상을 사용합니다.");
        }
    }

    /// <summary>
    /// 캐릭터 프리뷰 초기화
    /// </summary>
    public void InitializeCharacterPreview()
    {
        if (isInitialized)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰가 이미 초기화되었습니다");
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰 초기화 시작");

        InitializeCharacterRenderers();
        SaveOriginalTransform();
        InitializeParticleSystem();
        StartPreviewAnimation();
        FinalizeInitialization();
    }

    /// <summary>
    /// 캐릭터 렌더러 초기화
    /// </summary>
    private void InitializeCharacterRenderers()
    {
        if (characterPreview == null) return;

        characterRenderers.Clear();
        originalMaterials.Clear();

        Renderer[] allRenderers = characterPreview.GetComponentsInChildren<Renderer>(includeInactive: true);

        foreach (Renderer renderer in allRenderers)
        {
            if (ShouldIncludeRenderer(renderer))
            {
                characterRenderers.Add(renderer);
                originalMaterials[renderer] = renderer.material;
            }
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 렌더러 초기화 완료: {characterRenderers.Count}개");
    }

    /// <summary>
    /// 렌더러 포함 여부 결정
    /// </summary>
    /// <param name="renderer">확인할 렌더러</param>
    /// <returns>포함해야 하면 true</returns>
    private bool ShouldIncludeRenderer(Renderer renderer)
    {
        // ParticleSystem의 Renderer는 제외
        if (excludeParticleRenderers && renderer.GetComponent<ParticleSystemRenderer>() != null)
        {
            return false;
        }

        // MeshRenderer 또는 SkinnedMeshRenderer만 포함
        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }

    /// <summary>
    /// 원본 Transform 정보 저장
    /// </summary>
    private void SaveOriginalTransform()
    {
        if (characterPreview == null) return;

        originalPosition = characterPreview.transform.localPosition;
        originalRotation = characterPreview.transform.localEulerAngles;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 원본 Transform 저장 완료");
    }

    /// <summary>
    /// 파티클 시스템 초기화
    /// </summary>
    private void InitializeParticleSystem()
    {
        if (tornadoParticle == null) return;

        if (isolateParticleMaterials)
        {
            IsolateParticleMaterials();
        }

        // 초기 상태에서는 파티클 중지
        tornadoParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 파티클 시스템 초기화 완료");
    }

    /// <summary>
    /// 파티클 머티리얼 분리
    /// </summary>
    private void IsolateParticleMaterials()
    {
        Renderer[] particleRenderers = tornadoParticle.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in particleRenderers)
        {
            if (renderer.material != null)
            {
                renderer.material = new Material(renderer.material);
            }
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 파티클 머티리얼 분리 완료: {particleRenderers.Length}개");
    }

    /// <summary>
    /// 프리뷰 애니메이션 시작
    /// </summary>
    private void StartPreviewAnimation()
    {
        if (enablePreviewAnimation && characterPreview != null)
        {
            animationCoroutine = StartCoroutine(PreviewAnimationCoroutine());

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 프리뷰 애니메이션 시작");
        }
    }

    /// <summary>
    /// 초기화 완료 처리
    /// </summary>
    private void FinalizeInitialization()
    {
        isInitialized = true;

        // 기본 머티리얼 적용
        UpdateCharacterMaterial(currentColorIndex);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰 초기화 완료");
    }

    /// <summary>
    /// 캐릭터 프리뷰 정리
    /// </summary>
    private void CleanupCharacterPreview()
    {
        StopPreviewAnimation();
        StopAllParticleEffects();
        RestoreOriginalMaterials();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰 정리 완료");
    }

    #endregion

    #region Public API - Material Management

    /// <summary>
    /// 캐릭터 머티리얼 업데이트
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    public void UpdateCharacterMaterial(int colorIndex)
    {
        if (!ValidateColorIndex(colorIndex))
            return;

        currentColorIndex = colorIndex;
        ApplyMaterialToRenderers(colorIndex);
        TriggerPreviewUpdated(colorIndex);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 머티리얼 업데이트: 색상 {colorIndex}");
    }

    /// <summary>
    /// 색상 변경과 동시에 파티클 효과 재생
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    public void UpdateCharacterWithEffect(int colorIndex)
    {
        UpdateCharacterMaterial(colorIndex);
        PlayColorChangeParticle(colorIndex);
    }

    #endregion

    #region Public API - Particle Effects

    /// <summary>
    /// 색상 변경 파티클 효과 재생
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    public void PlayColorChangeParticle(int colorIndex)
    {
        if (tornadoParticle == null) return;

        ConfigureParticleColor(colorIndex);
        PlayParticleEffect();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 변경 파티클 재생: 색상 {colorIndex}");
    }

    /// <summary>
    /// 파티클 효과 즉시 중지
    /// </summary>
    public void StopParticleEffect()
    {
        if (tornadoParticle != null)
        {
            tornadoParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (particleStopCoroutine != null)
        {
            StopCoroutine(particleStopCoroutine);
            particleStopCoroutine = null;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 파티클 효과 중지");
    }

    #endregion

    #region Public API - Animation Control

    /// <summary>
    /// 프리뷰 애니메이션 활성화/비활성화
    /// </summary>
    /// <param name="enabled">애니메이션 활성화 여부</param>
    public void SetPreviewAnimationEnabled(bool enabled)
    {
        enablePreviewAnimation = enabled;

        if (enabled)
        {
            StartPreviewAnimation();
        }
        else
        {
            StopPreviewAnimation();
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 프리뷰 애니메이션: {(enabled ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 애니메이션 속도 설정
    /// </summary>
    /// <param name="newRotationSpeed">회전 속도</param>
    /// <param name="newBobSpeed">부유 속도</param>
    public void SetAnimationSpeed(float newRotationSpeed, float newBobSpeed)
    {
        rotationSpeed = newRotationSpeed;
        bobSpeed = newBobSpeed;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 애니메이션 속도 변경 - 회전: {rotationSpeed}, 부유: {bobSpeed}");
    }

    #endregion

    #region Private Methods - Material Management

    /// <summary>
    /// 렌더러들에 머티리얼 적용
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    private void ApplyMaterialToRenderers(int colorIndex)
    {
        Material targetMaterial = GetValidMaterial(colorIndex);

        foreach (Renderer renderer in characterRenderers)
        {
            if (renderer != null)
            {
                renderer.material = targetMaterial;
            }
        }
    }

    /// <summary>
    /// 유효한 머티리얼 가져오기
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <returns>유효한 머티리얼</returns>
    private Material GetValidMaterial(int colorIndex)
    {
        if (colorIndex >= 0 && colorIndex < characterMaterials.Length)
        {
            return characterMaterials[colorIndex];
        }

        if (verboseLogging)
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 머티리얼 인덱스 {colorIndex}가 범위를 벗어났습니다. 기본값(0)으로 설정합니다.");

        return characterMaterials[0];
    }

    /// <summary>
    /// 원본 머티리얼 복원
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.material = kvp.Value;
            }
        }
    }

    #endregion

    #region Private Methods - Particle Management

    /// <summary>
    /// 파티클 색상 설정
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    private void ConfigureParticleColor(int colorIndex)
    {
        if (!useColoredParticles || tornadoParticle == null) return;

        Color particleColor = GetValidParticleColor(colorIndex);
        var main = tornadoParticle.main;
        main.startColor = particleColor;
    }

    /// <summary>
    /// 유효한 파티클 색상 가져오기
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <returns>유효한 파티클 색상</returns>
    private Color GetValidParticleColor(int colorIndex)
    {
        if (particleColors != null && colorIndex >= 0 && colorIndex < particleColors.Length)
        {
            return particleColors[colorIndex];
        }

        // 폴백: 기본 흰색
        return Color.white;
    }

    /// <summary>
    /// 파티클 효과 재생
    /// </summary>
    private void PlayParticleEffect()
    {
        if (tornadoParticle == null) return;

        // 이전 정지 코루틴 중단
        if (particleStopCoroutine != null)
        {
            StopCoroutine(particleStopCoroutine);
        }

        // 파티클 재생
        tornadoParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        tornadoParticle.Play();

        // 자동 정지 설정
        particleStopCoroutine = StartCoroutine(StopParticleAfterDelay(particlePlayDuration));
    }

    /// <summary>
    /// 지연 후 파티클 정지
    /// </summary>
    /// <param name="delay">지연 시간</param>
    private IEnumerator StopParticleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (tornadoParticle != null)
        {
            tornadoParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        particleStopCoroutine = null;
    }

    /// <summary>
    /// 모든 파티클 효과 중지
    /// </summary>
    private void StopAllParticleEffects()
    {
        StopParticleEffect();
    }

    #endregion

    #region Private Methods - Animation

    /// <summary>
    /// 프리뷰 애니메이션 코루틴
    /// </summary>
    private IEnumerator PreviewAnimationCoroutine()
    {
        if (characterPreview == null) yield break;

        float time = 0f;

        while (enablePreviewAnimation)
        {
            time += Time.deltaTime;

            // 회전 애니메이션
            ApplyRotationAnimation(time);

            // 부유 애니메이션
            ApplyBobAnimation(time);

            yield return null;
        }
    }

    /// <summary>
    /// 회전 애니메이션 적용
    /// </summary>
    /// <param name="time">경과 시간</param>
    private void ApplyRotationAnimation(float time)
    {
        if (characterPreview == null) return;

        Vector3 rotation = originalRotation;
        rotation.y += time * rotationSpeed;
        characterPreview.transform.localEulerAngles = rotation;
    }

    /// <summary>
    /// 부유 애니메이션 적용
    /// </summary>
    /// <param name="time">경과 시간</param>
    private void ApplyBobAnimation(float time)
    {
        if (characterPreview == null) return;

        Vector3 position = originalPosition;
        position.y += Mathf.Sin(time * bobSpeed) * bobHeight;
        characterPreview.transform.localPosition = position;
    }

    /// <summary>
    /// 프리뷰 애니메이션 중지
    /// </summary>
    private void StopPreviewAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        // 원본 위치 복원
        RestoreOriginalTransform();
    }

    /// <summary>
    /// 원본 Transform 복원
    /// </summary>
    private void RestoreOriginalTransform()
    {
        if (characterPreview == null) return;

        characterPreview.transform.localPosition = originalPosition;
        characterPreview.transform.localEulerAngles = originalRotation;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 색상 인덱스 유효성 검증
    /// </summary>
    /// <param name="colorIndex">검증할 색상 인덱스</param>
    /// <returns>유효한 인덱스면 true</returns>
    private bool ValidateColorIndex(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= characterMaterials.Length)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 유효하지 않은 색상 인덱스: {colorIndex} (범위: 0-{characterMaterials.Length - 1})");
            return false;
        }

        if (!isInitialized)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰가 초기화되지 않았습니다");
            return false;
        }

        return true;
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 프리뷰 업데이트 완료 이벤트 발생
    /// </summary>
    /// <param name="colorIndex">업데이트된 색상 인덱스</param>
    private void TriggerPreviewUpdated(int colorIndex)
    {
        OnPreviewUpdated?.Invoke(colorIndex);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 현재 색상 인덱스 반환
    /// </summary>
    /// <returns>현재 색상 인덱스</returns>
    public int GetCurrentColorIndex()
    {
        return currentColorIndex;
    }

    /// <summary>
    /// 초기화 완료 여부 확인
    /// </summary>
    /// <returns>초기화가 완료되었으면 true</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 사용 가능한 머티리얼 수 반환
    /// </summary>
    /// <returns>사용 가능한 머티리얼 수</returns>
    public int GetAvailableMaterialCount()
    {
        return characterMaterials?.Length ?? 0;
    }

    /// <summary>
    /// 애니메이션 활성화 상태 확인
    /// </summary>
    /// <returns>애니메이션이 활성화되어 있으면 true</returns>
    public bool IsAnimationEnabled()
    {
        return enablePreviewAnimation && animationCoroutine != null;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 캐릭터 프리뷰 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Character Preview Status")]
    public void LogCharacterPreviewStatus()
    {
        Debug.Log($"=== CharacterPreviewController 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"현재 색상: {currentColorIndex}");
        Debug.Log($"애니메이션 활성화: {IsAnimationEnabled()}");
        Debug.Log($"캐릭터 렌더러 수: {characterRenderers.Count}");
        Debug.Log($"사용 가능한 머티리얼 수: {GetAvailableMaterialCount()}");
        Debug.Log($"파티클 시스템: {(tornadoParticle != null ? "설정됨" : "없음")}");
    }

    #endregion
}