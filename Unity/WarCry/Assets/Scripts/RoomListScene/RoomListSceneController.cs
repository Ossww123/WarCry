using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using RoomListData;

/// <summary>
/// 방 목록 씬의 전체 흐름과 컴포넌트 간 조정을 담당하는 메인 컨트롤러
/// RoomService, RoomListUIManager 간의 상호작용을 관리하며
/// 씬 초기화, 헤드리스 모드 처리, 씬 전환, Mirror 서버 연결 등의 핵심 기능을 제공
/// </summary>
public class RoomListSceneController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Scene References")]
    [SerializeField] private RoomListUIManager roomListUIManager;

    [Header("Scene Transition")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";
    [SerializeField] private string waitingRoomSceneName = "WaitingRoomScene";
    [SerializeField] private float sceneTransitionDelay = 0.5f;

    [Header("Auto Refresh")]
    [SerializeField] private bool enableAutoRefresh = true;
    [SerializeField] private float autoRefreshInterval = 30f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string RoomServiceObjectName = "RoomService";
    private const string AuthManagerObjectName = "AuthManager";
    private const string GameNetworkManagerObjectName = "GameNetworkManager";

    // PlayerPrefs 키
    private const string CurrentMatchIdKey = "CurrentMatchId";
    private const string CurrentUserRoleKey = "CurrentUserRole";
    private const string MirrorServerIPKey = "MirrorServerIP";
    private const string MirrorServerPortKey = "MirrorServerPort";

    #endregion

    #region Private Fields

    // 컴포넌트 참조
    private RoomService roomService;
    private GameNetworkManager gameNetworkManager;

    // 씬 상태
    private bool isInitialized = false;
    private bool isSceneTransitioning = false;

    // 자동 새로고침
    private Coroutine autoRefreshCoroutine = null;

    // 연결 정보 저장
    private string lastMirrorIP;
    private int lastMirrorPort;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeRoomListScene();
    }

    private void Start()
    {
        if (!isInitialized)
            return;

        StartInitialRoomListLoad();
        StartAutoRefreshIfEnabled();
    }

    private void OnDestroy()
    {
        CleanupRoomListScene();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 RoomListScene 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 방 목록 씬 초기화
    /// </summary>
    private void InitializeRoomListScene()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RoomListScene 초기화 시작");

        if (!ValidateAuthentication())
            return;

        InitializeRequiredServices();
        ValidateSceneComponents();
        RegisterEventHandlers();
        FinalizeInitialization();
    }

    /// <summary>
    /// 인증 상태 검증
    /// </summary>
    /// <returns>인증이 유효하면 true</returns>
    private bool ValidateAuthentication()
    {
        if (AuthManager.Instance == null || string.IsNullOrEmpty(AuthManager.Instance.Token))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] JWT 토큰이 없습니다. 로그인이 필요합니다.");

            // 로그인 씬으로 전환
            SceneManager.LoadScene("LoginScene");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 필수 서비스 초기화
    /// </summary>
    private void InitializeRequiredServices()
    {
        EnsureAuthManagerExists();
        EnsureRoomServiceExists();
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
    /// RoomService 존재 확인 및 생성
    /// </summary>
    private void EnsureRoomServiceExists()
    {
        roomService = RoomService.Instance;

        if (roomService == null)
        {
            GameObject roomServiceObj = GameObject.Find(RoomServiceObjectName);
            if (roomServiceObj == null)
            {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RoomService 동적 생성");

                // 동적으로 생성
                roomServiceObj = new GameObject(RoomServiceObjectName);
                roomServiceObj.AddComponent<RoomService>();
                roomService = RoomService.Instance;
            }
            else
            {
                // 씬에 있는 RoomService 사용
                roomService = roomServiceObj.GetComponent<RoomService>();
            }
        }

        if (roomService == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] RoomService를 찾거나 생성할 수 없습니다!");
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
        if (roomListUIManager == null)
        {
            roomListUIManager = FindObjectOfType<RoomListUIManager>();

            if (roomListUIManager == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] RoomListUIManager를 찾을 수 없습니다!");
                return;
            }
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 컴포넌트 검증 완료");
    }

    /// <summary>
    /// 이벤트 핸들러 등록
    /// </summary>
    private void RegisterEventHandlers()
    {
        // RoomService 이벤트 구독
        RoomService.OnRoomListReceived += HandleRoomListReceived;
        RoomService.OnRoomCreated += HandleRoomCreated;
        RoomService.OnRoomJoined += HandleRoomJoined;

        // UI 이벤트 구독
        if (roomListUIManager != null)
        {
            roomListUIManager.OnCreateRoomRequested += HandleCreateRoomRequested;
            roomListUIManager.OnJoinRoomRequested += HandleJoinRoomRequested;
            roomListUIManager.OnRefreshRequested += HandleRefreshRequested;
            roomListUIManager.OnBackToMainMenuRequested += HandleBackToMainMenuRequested;
        }

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
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RoomListScene 초기화 완료");
    }

    /// <summary>
    /// 씬 정리 작업
    /// </summary>
    private void CleanupRoomListScene()
    {
        StopAutoRefresh();
        UnregisterEventHandlers();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RoomListScene 정리 완료");
    }

    /// <summary>
    /// 이벤트 핸들러 등록 해제
    /// </summary>
    private void UnregisterEventHandlers()
    {
        RoomService.OnRoomListReceived -= HandleRoomListReceived;
        RoomService.OnRoomCreated -= HandleRoomCreated;
        RoomService.OnRoomJoined -= HandleRoomJoined;

        if (roomListUIManager != null)
        {
            roomListUIManager.OnCreateRoomRequested -= HandleCreateRoomRequested;
            roomListUIManager.OnJoinRoomRequested -= HandleJoinRoomRequested;
            roomListUIManager.OnRefreshRequested -= HandleRefreshRequested;
            roomListUIManager.OnBackToMainMenuRequested -= HandleBackToMainMenuRequested;
        }
    }

    #endregion

    #region Initial Loading

    /// <summary>
    /// 초기 방 목록 로드 시작
    /// </summary>
    private void StartInitialRoomListLoad()
    {
        if (roomService != null && roomListUIManager != null)
        {
            roomListUIManager.StartRefreshAnimation();
            roomService.GetRoomListAsync();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 초기 방 목록 로드 시작");
        }
    }

    #endregion

    #region Auto Refresh Management

    /// <summary>
    /// 자동 새로고침 시작 (활성화된 경우)
    /// </summary>
    private void StartAutoRefreshIfEnabled()
    {
        if (enableAutoRefresh && autoRefreshInterval > 0)
        {
            autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 자동 새로고침 시작 - 간격: {autoRefreshInterval}초");
        }
    }

    /// <summary>
    /// 자동 새로고침 중지
    /// </summary>
    private void StopAutoRefresh()
    {
        if (autoRefreshCoroutine != null)
        {
            StopCoroutine(autoRefreshCoroutine);
            autoRefreshCoroutine = null;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 자동 새로고침 중지");
        }
    }

    /// <summary>
    /// 자동 새로고침 코루틴
    /// </summary>
    private IEnumerator AutoRefreshCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoRefreshInterval);

            if (roomService != null && !isSceneTransitioning)
            {
                roomService.GetRoomListAsync();

                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 자동 새로고침 실행");
            }
        }
    }

    #endregion

    #region Event Handlers - RoomService

    /// <summary>
    /// 방 목록 조회 완료 이벤트 처리
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="response">응답 데이터</param>
    /// <param name="message">결과 메시지</param>
    private void HandleRoomListReceived(bool success, MatchListApiResponse response, string message)
    {
        if (roomListUIManager == null)
            return;

        roomListUIManager.CompleteRefreshAnimation();

        if (success && response != null)
        {
            roomListUIManager.UpdateRoomList(response);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 업데이트 완료 - {response.matches?.Count ?? 0}개 방");
        }
        else
        {
            roomListUIManager.ShowErrorMessage(message);

            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 조회 실패: {message}");
        }
    }

    /// <summary>
    /// 방 생성 완료 이벤트 처리
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="response">응답 데이터</param>
    /// <param name="message">결과 메시지</param>
    private void HandleRoomCreated(bool success, CreateMatchApiResponse response, string message)
    {
        if (roomListUIManager == null)
            return;

        if (success && response != null)
        {
            // 사용자 역할을 HOST로 설정
            SaveUserRole(UserRole.Host);
            SaveConnectionInfo(response.matchId, response.serverIp, response.serverPort);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 성공 - 방 ID: {response.matchId}, IP: {response.serverIp}, Port: {response.serverPort}");

            // 방 목록 새로고침
            if (roomService != null)
            {
                roomService.GetRoomListAsync();
            }

            // Mirror 서버 연결 및 WaitingRoom으로 이동
            ConnectToMirrorServer(response.matchId, response.serverIp, response.serverPort);
        }
        else
        {
            roomListUIManager.ShowErrorMessage(message);

            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 실패: {message}");
        }
    }

    /// <summary>
    /// 방 입장 완료 이벤트 처리
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="response">응답 데이터</param>
    /// <param name="message">결과 메시지</param>
    private void HandleRoomJoined(bool success, JoinMatchApiResponse response, string message)
    {
        if (roomListUIManager == null)
            return;

        if (success && response != null)
        {
            // 서버에서 받은 역할 저장
            SaveUserRole(response.role);
            SaveConnectionInfo(response.matchId, response.serverIp, response.serverPort);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 성공 - 방 ID: {response.matchId}, 역할: {response.role}, IP: {response.serverIp}, Port: {response.serverPort}");

            // Mirror 서버 연결 및 WaitingRoom으로 이동
            ConnectToMirrorServer(response.matchId, response.serverIp, response.serverPort);
        }
        else
        {
            roomListUIManager.ShowErrorMessage(message);

            // 비밀번호 오류인 경우 다시 입력 프롬프트 표시
            if (message.Contains("비밀번호"))
            {
                // UI에서 마지막으로 시도한 방 ID를 다시 프롬프트로 표시
                // 이 부분은 UI 매니저에서 처리하거나 추가 로직이 필요할 수 있음
            }

            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 실패: {message}");
        }
    }

    #endregion

    #region Event Handlers - UI

    /// <summary>
    /// 방 생성 요청 이벤트 처리
    /// </summary>
    /// <param name="title">방 제목</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <param name="password">방 비밀번호</param>
    private void HandleCreateRoomRequested(string title, bool isPrivate, string password)
    {
        if (roomService != null)
        {
            if (roomListUIManager != null)
            {
                roomListUIManager.SetLoadingIndicatorVisible(true);
                roomListUIManager.UpdateStatusMessage("방을 생성하는 중...");
            }

            roomService.CreateRoomAsync(title, isPrivate, password);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 요청 - 제목: {title}, 비공개: {isPrivate}");
        }
    }

    /// <summary>
    /// 방 입장 요청 이벤트 처리
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <param name="password">방 비밀번호</param>
    private void HandleJoinRoomRequested(int matchId, string password)
    {
        if (roomService != null)
        {
            if (roomListUIManager != null)
            {
                roomListUIManager.SetLoadingIndicatorVisible(true);
                roomListUIManager.UpdateStatusMessage("방에 입장하는 중...");
            }

            roomService.JoinRoomAsync(matchId, password);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 요청 - 방 ID: {matchId}");
        }
    }

    /// <summary>
    /// 새로고침 요청 이벤트 처리
    /// </summary>
    private void HandleRefreshRequested()
    {
        if (roomService != null && roomListUIManager != null)
        {
            roomListUIManager.StartRefreshAnimation();
            roomService.GetRoomListAsync();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 수동 새로고침 요청");
        }
    }

    /// <summary>
    /// 메인 메뉴로 돌아가기 요청 이벤트 처리
    /// </summary>
    private void HandleBackToMainMenuRequested()
    {
        StartSceneTransition(mainMenuSceneName);
    }

    #endregion

    #region Mirror Connection Management

    /// <summary>
    /// Mirror 서버에 연결하고 WaitingRoom으로 이동
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <param name="serverIp">서버 IP</param>
    /// <param name="serverPort">서버 포트</param>
    private void ConnectToMirrorServer(int matchId, string serverIp, int serverPort)
    {
        if (gameNetworkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] GameNetworkManager를 찾을 수 없습니다!");
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Mirror 서버 연결 시도 - 방 ID: {matchId}, IP: {serverIp}, Port: {serverPort}");

        // Mirror 서버 연결 정보 설정
        gameNetworkManager.SetupClient(serverIp, serverPort);

        // WaitingRoomScene으로 전환 (Mirror 연결은 씬 로드 후 처리)
        StartSceneTransition(waitingRoomSceneName);
    }

    #endregion

    #region Data Persistence

    /// <summary>
    /// 사용자 역할 저장
    /// </summary>
    /// <param name="role">사용자 역할</param>
    private void SaveUserRole(string role)
    {
        PlayerPrefs.SetString(CurrentUserRoleKey, role);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 사용자 역할 저장: {role}");
    }

    /// <summary>
    /// 연결 정보 저장
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <param name="serverIp">서버 IP</param>
    /// <param name="serverPort">서버 포트</param>
    private void SaveConnectionInfo(int matchId, string serverIp, int serverPort)
    {
        PlayerPrefs.SetInt(CurrentMatchIdKey, matchId);
        PlayerPrefs.SetString(MirrorServerIPKey, serverIp);
        PlayerPrefs.SetInt(MirrorServerPortKey, serverPort);
        PlayerPrefs.Save();

        lastMirrorIP = serverIp;
        lastMirrorPort = serverPort;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 연결 정보 저장 - 방 ID: {matchId}, IP: {serverIp}, Port: {serverPort}");
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
        StopAutoRefresh();

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

        // 전환 지연 시간 대기
        yield return new WaitForSeconds(sceneTransitionDelay);

        // 씬 로드
        SceneManager.LoadScene(sceneName);
    }

    #endregion

    #region Public API - Development

    /// <summary>
    /// 강제 방 목록 새로고침 (개발/디버깅용)
    /// </summary>
    [ContextMenu("Force Refresh Room List")]
    public void ForceRefreshRoomList()
    {
        if (roomService != null)
        {
            roomService.GetRoomListAsync();
        }
    }

    /// <summary>
    /// 현재 씬 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Scene Status")]
    public void LogSceneStatus()
    {
        Debug.Log($"=== RoomListScene 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"씬 전환 중: {isSceneTransitioning}");
        Debug.Log($"자동 새로고침 활성화: {enableAutoRefresh}");
        Debug.Log($"자동 새로고침 실행 중: {autoRefreshCoroutine != null}");
        Debug.Log($"RoomService 사용 가능: {roomService != null}");
        Debug.Log($"RoomListUIManager 연결: {roomListUIManager != null}");
        Debug.Log($"GameNetworkManager 연결: {gameNetworkManager != null}");
        Debug.Log($"마지막 Mirror 연결 정보: {lastMirrorIP}:{lastMirrorPort}");
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
    /// RoomService 사용 가능 여부 확인
    /// </summary>
    /// <returns>RoomService가 사용 가능하면 true</returns>
    public bool IsRoomServiceAvailable()
    {
        return roomService != null && RoomService.Instance != null;
    }

    #endregion
}