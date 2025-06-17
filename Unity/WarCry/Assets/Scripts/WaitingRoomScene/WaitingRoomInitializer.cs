using UnityEngine;
using System.Collections;

/// <summary>
/// 대기실 씬의 초기화 도우미 클래스
/// PlayerInfo와 UI 매니저 간의 연결을 중계하고, 안전한 초기화 순서를 보장
/// WaitingRoomSceneController와 연동하여 씬 초기화 과정을 지원
/// </summary>
public class WaitingRoomInitializer : MonoBehaviour
{
    #region Inspector Fields

    [Header("Initialization Settings")]
    [SerializeField] private float initializationDelay = 0.2f;
    [SerializeField] private float uiInitializationRetryInterval = 0.1f;
    [SerializeField] private int maxRetryAttempts = 50;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Private Fields

    // 초기화 상태
    private bool isInitialized = false;
    private WaitingRoomSceneController sceneController;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeWaitingRoomHelper();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 WaitingRoomInitializer 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 대기실 초기화 헬퍼 시작
    /// </summary>
    private void InitializeWaitingRoomHelper()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 대기실 초기화 헬퍼 시작");

        FindSceneController();
        StartCoroutine(InitializeWithDelay());
    }

    /// <summary>
    /// 씬 컨트롤러 찾기
    /// </summary>
    private void FindSceneController()
    {
        sceneController = FindObjectOfType<WaitingRoomSceneController>();

        if (sceneController == null)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomSceneController를 찾을 수 없습니다. 독립 모드로 동작합니다.");
        }
    }

    /// <summary>
    /// 지연 후 초기화 시작
    /// </summary>
    private IEnumerator InitializeWithDelay()
    {
        // 모든 컴포넌트의 Start 메서드가 실행될 시간 부여
        yield return new WaitForSeconds(initializationDelay);

        // 씬 컨트롤러 초기화 대기
        yield return StartCoroutine(WaitForSceneControllerInitialization());

        isInitialized = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 대기실 초기화 헬퍼 준비 완료");
    }

    /// <summary>
    /// 씬 컨트롤러 초기화 대기
    /// </summary>
    private IEnumerator WaitForSceneControllerInitialization()
    {
        if (sceneController == null) yield break;

        float waitTime = 0f;
        while (!sceneController.IsSceneInitialized() && waitTime < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (verboseLogging)
        {
            if (sceneController.IsSceneInitialized())
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 컨트롤러 초기화 완료 대기 성공");
            else
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 씬 컨트롤러 초기화 대기 타임아웃");
        }
    }

    #endregion

    #region Public API - PlayerInfo Integration

    /// <summary>
    /// PlayerInfo에서 사용할 UI 초기화 메서드
    /// </summary>
    /// <param name="localPlayer">로컬 플레이어 정보</param>
    public void InitializePlayerUI(PlayerInfo localPlayer)
    {
        if (localPlayer == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerInfo가 null입니다!");
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 UI 초기화 요청: {localPlayer.playerName}");

        if (isInitialized)
        {
            InitializeUIDirectly(localPlayer);
        }
        else
        {
            StartCoroutine(InitializeUIWithRetry(localPlayer));
        }
    }

    #endregion

    #region Private Methods - UI Initialization

    /// <summary>
    /// UI 직접 초기화
    /// </summary>
    /// <param name="localPlayer">로컬 플레이어 정보</param>
    private void InitializeUIDirectly(PlayerInfo localPlayer)
    {
        WaitingRoomUIManager uiManager = FindUIManager();

        if (uiManager != null)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] UI 매니저 발견, 플레이어 UI 초기화: {localPlayer.playerName}");

            uiManager.InitializeForPlayer(localPlayer);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomUIManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 재시도를 통한 UI 초기화
    /// </summary>
    /// <param name="localPlayer">로컬 플레이어 정보</param>
    private IEnumerator InitializeUIWithRetry(PlayerInfo localPlayer)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] UI 초기화 지연 - 초기화 완료 대기 중");

        int retryCount = 0;

        // 초기화 완료 대기
        while (!isInitialized && retryCount < maxRetryAttempts)
        {
            yield return new WaitForSeconds(uiInitializationRetryInterval);
            retryCount++;
        }

        if (!isInitialized)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] UI 초기화 대기 타임아웃 (재시도 {retryCount}회)");
            yield break;
        }

        // UI 매니저 찾기 및 초기화
        WaitingRoomUIManager uiManager = FindUIManager();

        if (uiManager != null)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 UI 초기화 재시도 성공: {localPlayer.playerName}");

            uiManager.InitializeForPlayer(localPlayer);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] UI 초기화 실패: UI 매니저를 찾을 수 없습니다");
        }
    }

    /// <summary>
    /// UI 매니저 찾기
    /// </summary>
    /// <returns>찾은 UI 매니저 (없으면 null)</returns>
    private WaitingRoomUIManager FindUIManager()
    {
        return FindObjectOfType<WaitingRoomUIManager>();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 초기화 완료 여부 확인
    /// </summary>
    /// <returns>초기화가 완료되었으면 true</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 씬 컨트롤러 연결 상태 확인
    /// </summary>
    /// <returns>씬 컨트롤러가 연결되어 있으면 true</returns>
    public bool HasSceneController()
    {
        return sceneController != null;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 초기화 헬퍼 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Initializer Status")]
    public void LogInitializerStatus()
    {
        Debug.Log($"=== WaitingRoomInitializer 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"씬 컨트롤러 연결: {HasSceneController()}");
        Debug.Log($"최대 재시도 횟수: {maxRetryAttempts}");
        Debug.Log($"초기화 지연: {initializationDelay}초");
    }

    #endregion
}