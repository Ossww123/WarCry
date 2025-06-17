using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Mirror;
using NetworkMessages;

/// <summary>
/// 대기실 씬의 전체 흐름과 컴포넌트 간 조정을 담당하는 메인 컨트롤러
/// WaitingRoomManager, WaitingRoomUIManager, 네트워크 연결 관리 간의 상호작용을 관리하며
/// 씬 초기화, 헤드리스 모드 처리, 씬 전환, Mirror 서버 연결 등의 핵심 기능을 제공
/// </summary>
public class WaitingRoomSceneController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Scene References")]
    [SerializeField] private WaitingRoomManager roomManager;
    [SerializeField] private WaitingRoomUIManager uiManager;
    [SerializeField] private WaitingRoomInitializer initializer;

    [Header("Scene Transition")]
    [SerializeField] private string roomListSceneName = "RoomListScene";
    [SerializeField] private string battleSceneName = "BattleScene";
    [SerializeField] private float sceneTransitionDelay = 0.5f;

    [Header("Network Settings")]
    [SerializeField] private float connectionTimeout = 10f;
    [SerializeField] private float networkInitializationDelay = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string GameNetworkManagerObjectName = "GameNetworkManager";
    private const string AuthManagerObjectName = "AuthManager";

    // PlayerPrefs 키
    private const string CurrentMatchIdKey = "CurrentMatchId";
    private const string CurrentUserRoleKey = "CurrentUserRole";
    private const string MirrorServerIPKey = "MirrorServerIP";
    private const string MirrorServerPortKey = "MirrorServerPort";

    #endregion

    #region Private Fields

    // 컴포넌트 참조
    private GameNetworkManager gameNetworkManager;

    // 씬 상태
    private bool isInitialized = false;
    private bool isSceneTransitioning = false;
    private bool isNetworkConnected = false;

    // 연결 정보
    private string serverIP;
    private int serverPort;
    private int matchId;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeWaitingRoomScene();
    }

    private void Start()
    {
        if (!isInitialized)
            return;

        StartCoroutine(InitializeNetworkConnection());
    }

    private void OnDestroy()
    {
        CleanupWaitingRoomScene();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 WaitingRoomScene 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 대기실 씬 초기화
    /// </summary>
    private void InitializeWaitingRoomScene()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomScene 초기화 시작");

        if (!ValidateConnectionInfo())
            return;

        InitializeRequiredServices();
        ValidateSceneComponents();
        RegisterEventHandlers();
        FinalizeInitialization();
    }

    /// <summary>
    /// 연결 정보 유효성 검증
    /// </summary>
    /// <returns>연결 정보가 유효하면 true</returns>
    private bool ValidateConnectionInfo()
    {
        matchId = PlayerPrefs.GetInt(CurrentMatchIdKey, -1);
        serverIP = PlayerPrefs.GetString(MirrorServerIPKey, "");
        serverPort = PlayerPrefs.GetInt(MirrorServerPortKey, 0);

        if (matchId == -1 || string.IsNullOrEmpty(serverIP) || serverPort == 0)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 연결 정보가 불완전합니다. 방 목록으로 돌아갑니다.");

            // 방 목록으로 즉시 전환
            SceneManager.LoadScene(roomListSceneName);
            return false;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 연결 정보 확인 완료 - 방 ID: {matchId}, IP: {serverIP}, Port: {serverPort}");

        return true;
    }

    /// <summary>
    /// 필수 서비스 초기화
    /// </summary>
    private void InitializeRequiredServices()
    {
        EnsureAuthManagerExists();
        EnsureGameNetworkManagerExists();
    }

    /// <summary>
    /// AuthManager 존재 확인
    /// </summary>
    private void EnsureAuthManagerExists()
    {
        if (AuthManager.Instance == null)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] AuthManager가 없습니다. LoginScene으로 이동합니다.");

            SceneManager.LoadScene("LoginScene");
        }
    }

    /// <summary>
    /// GameNetworkManager 존재 확인
    /// </summary>
    private void EnsureGameNetworkManagerExists()
    {
        gameNetworkManager = FindFirstObjectByType<GameNetworkManager>();

        if (gameNetworkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] GameNetworkManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 씬 컴포넌트 유효성 검증
    /// </summary>
    private void ValidateSceneComponents()
    {
        ValidateRoomManager();
        ValidateUIManager();
        ValidateInitializer();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 컴포넌트 검증 완료");
    }

    /// <summary>
    /// WaitingRoomManager 검증
    /// </summary>
    private void ValidateRoomManager()
    {
        if (roomManager == null)
        {
            roomManager = FindObjectOfType<WaitingRoomManager>();

            if (roomManager == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomManager를 찾을 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// WaitingRoomUIManager 검증
    /// </summary>
    private void ValidateUIManager()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<WaitingRoomUIManager>();

            if (uiManager == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomUIManager를 찾을 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// WaitingRoomInitializer 검증
    /// </summary>
    private void ValidateInitializer()
    {
        if (initializer == null)
        {
            initializer = FindObjectOfType<WaitingRoomInitializer>();

            if (initializer == null)
            {
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomInitializer를 찾을 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// 이벤트 핸들러 등록
    /// </summary>
    private void RegisterEventHandlers()
    {
        // GameNetworkManager 이벤트 구독
        GameNetworkManager.OnPlayerListUpdateReceived += HandlePlayerListUpdate;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이벤트 핸들러 등록 완료");
    }

    /// <summary>
    /// 초기화 완료 처리
    /// </summary>
    private void FinalizeInitialization()
    {
        isInitialized = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomScene 초기화 완료");
    }

    /// <summary>
    /// 씬 정리 작업
    /// </summary>
    private void CleanupWaitingRoomScene()
    {
        UnregisterEventHandlers();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomScene 정리 완료");
    }

    /// <summary>
    /// 이벤트 핸들러 등록 해제
    /// </summary>
    private void UnregisterEventHandlers()
    {
        GameNetworkManager.OnPlayerListUpdateReceived -= HandlePlayerListUpdate;
    }

    #endregion

    #region Network Connection Management

    /// <summary>
    /// 네트워크 연결 초기화
    /// </summary>
    private IEnumerator InitializeNetworkConnection()
    {
        // 다른 컴포넌트들의 초기화 대기
        yield return new WaitForSeconds(networkInitializationDelay);

        if (gameNetworkManager != null)
        {
            ConnectToMirrorServer();
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] GameNetworkManager가 없어 네트워크 연결을 시작할 수 없습니다!");
            StartSceneTransition(roomListSceneName);
        }
    }

    /// <summary>
    /// Mirror 서버에 연결
    /// </summary>
    private void ConnectToMirrorServer()
    {
        if (NetworkClient.active)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이미 네트워크에 연결되어 있습니다");

            isNetworkConnected = true;
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Mirror 서버 연결 시작: {serverIP}:{serverPort}");

        // 연결 설정 및 시작
        gameNetworkManager.SetupClient(serverIP, serverPort);
        gameNetworkManager.ConnectClient();

        // 연결 상태 모니터링 시작
        StartCoroutine(MonitorNetworkConnection());
    }

    /// <summary>
    /// 네트워크 연결 상태 모니터링
    /// </summary>
    private IEnumerator MonitorNetworkConnection()
    {
        float elapsed = 0f;

        while (elapsed < connectionTimeout && !NetworkClient.isConnected)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (NetworkClient.isConnected)
        {
            isNetworkConnected = true;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Mirror 서버 연결 성공");
        }
        else
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Mirror 서버 연결 실패 - 타임아웃");

            HandleNetworkConnectionFailure();
        }
    }

    /// <summary>
    /// 네트워크 연결 실패 처리
    /// </summary>
    private void HandleNetworkConnectionFailure()
    {
        Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 네트워크 연결에 실패했습니다. 방 목록으로 돌아갑니다.");
        StartSceneTransition(roomListSceneName);
    }

    /// <summary>
    /// 네트워크 연결 해제
    /// </summary>
    private void DisconnectFromMirrorServer()
    {
        if (NetworkClient.isConnected)
        {
            NetworkClient.Disconnect();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Mirror 서버 연결 해제됨");
        }

        isNetworkConnected = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// 플레이어 목록 업데이트 이벤트 처리
    /// </summary>
    /// <param name="message">플레이어 목록 업데이트 메시지</param>
    private void HandlePlayerListUpdate(PlayerListUpdateMessage message)
    {
        if (uiManager != null)
        {
            ValidateAndUpdatePlayerList(message);
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomUIManager를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 플레이어 목록 유효성 검증 및 업데이트
    /// </summary>
    /// <param name="message">플레이어 목록 메시지</param>
    private void ValidateAndUpdatePlayerList(PlayerListUpdateMessage message)
    {
        try
        {
            // 플레이어 목록에 실제 데이터가 있는지 확인
            if (message.playerNetIds != null && message.playerNetIds.Count > 0)
            {
                uiManager.UpdatePlayerList(
                    message.playerNetIds,
                    message.playerNames,
                    message.playerReadyStates,
                    message.playerColorIndices,
                    message.playerIsHost
                );

                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 성공: {message.playerNetIds.Count}명");
            }
            else
            {
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 빈 플레이어 목록 수신됨. 업데이트 건너뜀.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 중 오류 발생: {e.Message}");
        }
    }

    #endregion

    #region Public API - Room Management

    /// <summary>
    /// 방 나가기 요청 처리
    /// </summary>
    public void RequestLeaveRoom()
    {
        if (roomManager != null)
        {
            roomManager.LeaveRoom();
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomManager를 찾을 수 없습니다!");

            // 폴백: 직접 씬 전환
            StartSceneTransition(roomListSceneName);
        }
    }

    /// <summary>
    /// 게임 시작 요청 처리
    /// </summary>
    public void RequestStartGame()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 게임 시작 요청");

        // BattleScene으로 전환
        StartSceneTransition(battleSceneName);
    }

    #endregion

    #region Public API - Network Status

    /// <summary>
    /// 네트워크 연결 상태 확인
    /// </summary>
    /// <returns>연결되어 있으면 true</returns>
    public bool IsNetworkConnected()
    {
        return isNetworkConnected && NetworkClient.isConnected;
    }

    /// <summary>
    /// 강제 네트워크 연결 해제
    /// </summary>
    public void ForceDisconnect()
    {
        DisconnectFromMirrorServer();
    }

    #endregion

    #region Scene Transition

    /// <summary>
    /// 씬 전환 시작
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름</param>
    public void StartSceneTransition(string sceneName)
    {
        if (isSceneTransitioning)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환이 이미 진행 중입니다");
            return;
        }

        isSceneTransitioning = true;
        StartCoroutine(SceneTransitionCoroutine(sceneName));
    }

    /// <summary>
    /// 씬 전환 코루틴
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름</param>
    private IEnumerator SceneTransitionCoroutine(string sceneName)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환 시작: {sceneName}");

        // 네트워크 연결 해제 (방 목록으로 돌아가는 경우만)
        if (sceneName == roomListSceneName)
        {
            DisconnectFromMirrorServer();
        }

        // 전환 지연 시간 대기
        yield return new WaitForSeconds(sceneTransitionDelay);

        // 씬 로드
        SceneManager.LoadScene(sceneName);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 씬 초기화 완료 여부 확인
    /// </summary>
    /// <returns>초기화가 완료되었으면 true</returns>
    public bool IsSceneInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 현재 씬 전환 상태 확인
    /// </summary>
    /// <returns>씬 전환 중이면 true</returns>
    public bool IsSceneTransitioning()
    {
        return isSceneTransitioning;
    }

    /// <summary>
    /// 현재 방 ID 가져오기
    /// </summary>
    /// <returns>현재 방 ID</returns>
    public int GetCurrentMatchId()
    {
        return matchId;
    }

    /// <summary>
    /// 현재 사용자 역할 가져오기
    /// </summary>
    /// <returns>사용자 역할 ("HOST" 또는 "GUEST")</returns>
    public string GetCurrentUserRole()
    {
        return PlayerPrefs.GetString(CurrentUserRoleKey, "GUEST");
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 씬 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Scene Status")]
    public void LogSceneStatus()
    {
        Debug.Log($"=== WaitingRoomScene 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"씬 전환 중: {isSceneTransitioning}");
        Debug.Log($"네트워크 연결: {IsNetworkConnected()}");
        Debug.Log($"방 ID: {matchId}");
        Debug.Log($"서버 주소: {serverIP}:{serverPort}");
        Debug.Log($"사용자 역할: {GetCurrentUserRole()}");
        Debug.Log($"RoomManager 연결: {roomManager != null}");
        Debug.Log($"UIManager 연결: {uiManager != null}");
        Debug.Log($"GameNetworkManager 연결: {gameNetworkManager != null}");
    }

    #endregion
}