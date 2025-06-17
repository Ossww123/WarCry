using Mirror;
using UnityEngine;
using System.Collections;

/// <summary>
/// 네트워크 씬 전환 관리를 담당하는 매니저
/// 로딩 씬을 통한 안전한 씬 전환, 메인 메뉴 복귀, 직접 씬 전환 기능을 제공
/// </summary>
public class NetworkSceneManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Scene Transition Settings")]
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private float defaultLoadingDelay = 3f;

    #endregion

    #region Constants

    private const string LoadingSceneName = "LoadingScene";
    private const string MainMenuSceneName = "MainMenuScene";

    #endregion

    #region Private Fields

    private string targetScene = "";
    private GameNetworkManager networkManager;

    #endregion

    #region Initialization

    /// <summary>
    /// NetworkSceneManager 초기화
    /// </summary>
    /// <param name="manager">참조할 GameNetworkManager 인스턴스</param>
    public void Initialize(GameNetworkManager manager)
    {
        networkManager = manager;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkSceneManager 초기화 완료");
    }

    #endregion

    #region Public API

    /// <summary>
    /// 로딩 씬을 통한 안전한 씬 전환 시작
    /// </summary>
    /// <param name="sceneName">전환할 목표 씬 이름</param>
    public void StartSceneTransition(string sceneName)
    {
        if (!ValidateSceneTransition(sceneName))
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환 시작: {sceneName}");

        targetScene = sceneName;
        networkManager.ServerChangeScene(LoadingSceneName);
    }

    /// <summary>
    /// 직접 씬 전환 (로딩 씬 없이)
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름</param>
    public void ChangeSceneDirectly(string sceneName)
    {
        if (!ValidateSceneTransition(sceneName))
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 직접 씬 전환: {sceneName}");

        networkManager.ServerChangeScene(sceneName);
    }

    /// <summary>
    /// 지정된 시간 후 메인 메뉴로 복귀 예약
    /// </summary>
    /// <param name="delay">복귀까지의 지연 시간(초)</param>
    public void ScheduleReturnToMainMenu(float delay)
    {
        if (!ValidateServerState())
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {delay}초 후 메인 메뉴 복귀 예약됨");

        StartCoroutine(ReturnToMainMenuAfterDelay(delay));
    }

    #endregion

    #region Scene Change Events

    /// <summary>
    /// 서버 씬 변경 완료 시 호출 (GameNetworkManager에서 호출)
    /// </summary>
    /// <param name="sceneName">변경된 씬 이름</param>
    public void OnServerSceneChanged(string sceneName)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 변경 완료: {sceneName}");

        // 로딩 씬이 로드되고 목표 씬이 설정된 경우 목표 씬으로 전환
        if (IsLoadingSceneWithTarget(sceneName))
        {
            StartCoroutine(LoadTargetSceneAsync());
        }
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 씬 전환 가능 상태 검증
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름</param>
    /// <returns>전환 가능하면 true</returns>
    private bool ValidateSceneTransition(string sceneName)
    {
        if (!ValidateServerState())
            return false;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 씬 이름이 비어있습니다!");
            return false;
        }

        if (networkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetworkManager가 초기화되지 않았습니다!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 서버 활성화 상태 검증
    /// </summary>
    /// <returns>서버가 활성화되어 있으면 true</returns>
    private bool ValidateServerState()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버가 비활성화 상태입니다");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 로딩 씬에서 목표 씬이 설정된 상태인지 확인
    /// </summary>
    /// <param name="sceneName">현재 씬 이름</param>
    /// <returns>로딩 씬이고 목표 씬이 설정되어 있으면 true</returns>
    private bool IsLoadingSceneWithTarget(string sceneName)
    {
        return sceneName == LoadingSceneName && !string.IsNullOrEmpty(targetScene);
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 로딩 대기 후 목표 씬으로 비동기 전환
    /// </summary>
    private IEnumerator LoadTargetSceneAsync()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {LoadingSceneName}에서 {targetScene}로 전환 준비 중...");

        // 로딩 지연 시간 대기 (리소스 로드 시뮬레이션)
        yield return new WaitForSeconds(defaultLoadingDelay);

        // 목표 씬으로 전환
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {targetScene}으로 전환 시작");

        networkManager.ServerChangeScene(targetScene);

        // 목표 씬 초기화
        targetScene = "";
    }

    /// <summary>
    /// 지연 후 메인 메뉴로 복귀
    /// </summary>
    /// <param name="delay">지연 시간(초)</param>
    private IEnumerator ReturnToMainMenuAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 메인 메뉴로 복귀 실행");

        if (ValidateServerState())
        {
            networkManager.ServerChangeScene(MainMenuSceneName);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 현재 목표 씬 정보 반환 (디버깅용)
    /// </summary>
    /// <returns>현재 설정된 목표 씬 이름</returns>
    public string GetCurrentTargetScene()
    {
        return targetScene;
    }

    /// <summary>
    /// 씬 전환 진행 중 여부 확인
    /// </summary>
    /// <returns>목표 씬이 설정되어 있으면 true</returns>
    public bool IsTransitionInProgress()
    {
        return !string.IsNullOrEmpty(targetScene);
    }

    #endregion
}