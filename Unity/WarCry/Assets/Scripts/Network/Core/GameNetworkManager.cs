using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NetworkMessages;
using System;

/// <summary>
/// 게임의 핵심 네트워크 매니저
/// Mirror NetworkManager를 상속받아 멀티플레이어 게임의 네트워크 기능을 관리하며,
/// 각 전문 매니저들에게 기능을 위임하는 중앙 허브 역할을 담당
/// </summary>
public class GameNetworkManager : NetworkManager
{
    #region Inspector Fields

    [Header("Core Settings")]
    [SerializeField] private bool verboseLogging = true;

    [Header("Battle References")]
    [SerializeField] private BattleUIManager battleUIManager;

    #endregion

    #region Events

    /// <summary>
    /// 플레이어 목록 업데이트 메시지 수신 시 발생하는 이벤트
    /// </summary>
    public static event Action<PlayerListUpdateMessage> OnPlayerListUpdateReceived;

    #endregion

    #region Private Fields

    // 매니저 참조
    private ServerManager serverManager;
    private NetworkSceneManager networkSceneManager;

    // 배틀 상태
    private bool battleStarted = false;

    #endregion

    #region Unity Lifecycle

    public override void Awake()
    {
        base.Awake();

        // Mirror 설정
        autoCreatePlayer = false;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] GameNetworkManager 초기화 시작");

        ValidateConfiguration();
        InitializeAllManagers();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 필수 컴포넌트 및 설정을 검증
    /// </summary>
    private void ValidateConfiguration()
    {
        if (playerPrefab != null && playerPrefab.GetComponent<PlayerInfo>() == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerPrefab에 PlayerInfo 컴포넌트가 필요합니다!");
        }
    }

    /// <summary>
    /// 모든 하위 매니저들을 초기화
    /// </summary>
    private void InitializeAllManagers()
    {
        InitializeNetworkManagers();
        InitializePlayerManagers();
        InitializeServerManager();
        InitializeSceneManager();
    }

    /// <summary>
    /// 네트워크 연결 및 메시지 처리 매니저 초기화
    /// </summary>
    private void InitializeNetworkManagers()
    {
        var connectionManager = GetComponent<NetworkConnectionManager>();
        connectionManager?.Initialize(this);

        var messageHandler = GetComponent<NetworkMessageHandler>();
        messageHandler?.Initialize();
    }

    /// <summary>
    /// 플레이어 관련 매니저 초기화
    /// </summary>
    private void InitializePlayerManagers()
    {
        var spawnManager = GetComponent<PlayerSpawnManager>();
        spawnManager?.Initialize(this);
    }

    /// <summary>
    /// 서버 관리 매니저 초기화
    /// </summary>
    private void InitializeServerManager()
    {
        serverManager = GetComponent<ServerManager>();
        if (serverManager != null)
        {
            serverManager.Initialize(this);
        }
        else if (verboseLogging)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] ServerManager 컴포넌트를 찾을 수 없습니다");
        }
    }

    /// <summary>
    /// 씬 전환 매니저 초기화
    /// </summary>
    private void InitializeSceneManager()
    {
        networkSceneManager = GetComponent<NetworkSceneManager>();
        if (networkSceneManager != null)
        {
            networkSceneManager.Initialize(this);
        }
        else if (verboseLogging)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] NetworkSceneManager 컴포넌트를 찾을 수 없습니다");
        }
    }

    #endregion

    #region Server Events

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (verboseLogging)
        {
            var spawnablePrefabs = spawnPrefabs.Select(p => p.name).ToList();
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 시작됨. 등록된 프리팹: {string.Join(", ", spawnablePrefabs)}");
        }

        RegisterServerMessageHandlers();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        GetComponent<NetworkConnectionManager>()?.OnServerConnect(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GetComponent<PlayerSpawnManager>()?.OnServerAddPlayer(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        GetComponent<NetworkConnectionManager>()?.OnServerDisconnect(conn);
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        networkSceneManager?.OnServerSceneChanged(sceneName);
    }

    #endregion

    #region Client Events

    public override void OnStartClient()
    {
        base.OnStartClient();
        GetComponent<NetworkConnectionManager>()?.OnStartClient();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        GetComponent<NetworkConnectionManager>()?.OnClientConnect();
    }

    #endregion

    #region Message Handlers

    /// <summary>
    /// 서버 메시지 핸들러 등록
    /// </summary>
    private void RegisterServerMessageHandlers()
    {
        NetworkServer.RegisterHandler<PlayerSpawnRequest>(OnPlayerSpawnRequested);
        NetworkServer.RegisterHandler<HostTransferMessage>(OnHostTransferRequest);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 메시지 핸들러 등록 완료");
    }

    /// <summary>
    /// 플레이어 스폰 요청 처리
    /// </summary>
    private void OnPlayerSpawnRequested(NetworkConnection conn, PlayerSpawnRequest msg)
    {
        var spawnManager = GetComponent<PlayerSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.HandlePlayerSpawnRequest(conn, msg);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerSpawnManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 호스트 권한 이전 요청 처리
    /// </summary>
    private void OnHostTransferRequest(NetworkConnection conn, HostTransferMessage msg)
    {
        PlayerManager.Instance?.TransferHostRole(msg.newHostNetId);
    }

    /// <summary>
    /// 플레이어 목록 업데이트 이벤트 트리거
    /// </summary>
    public static void TriggerPlayerListUpdate(PlayerListUpdateMessage message)
    {
        OnPlayerListUpdateReceived?.Invoke(message);
    }

    #endregion

    #region Public API - Connection Management

    /// <summary>
    /// 클라이언트 연결 설정 (IP 및 포트)
    /// </summary>
    public void SetupClient(string ip, int port)
    {
        GetComponent<NetworkConnectionManager>()?.SetupClient(ip, port);
    }

    /// <summary>
    /// 호스트 모드로 서버 시작
    /// </summary>
    public void StartHostMode()
    {
        GetComponent<NetworkConnectionManager>()?.StartHostMode();
    }

    /// <summary>
    /// 클라이언트로 서버에 연결
    /// </summary>
    public void ConnectClient()
    {
        GetComponent<NetworkConnectionManager>()?.ConnectClient();
    }

    #endregion

    #region Public API - Player Management

    /// <summary>
    /// 모든 플레이어의 준비 상태를 확인하고 게임 시작
    /// </summary>
    public void CheckAllPlayersReadyAndStartGame()
    {
        PlayerManager.Instance?.CheckAllPlayersReadyAndStartGame();
    }

    /// <summary>
    /// 호스트 권한 이전 요청
    /// </summary>
    public void RequestHostTransfer(uint newHostNetId)
    {
        if (NetworkServer.active)
        {
            PlayerManager.Instance?.TransferHostRole(newHostNetId);
        }
        else if (NetworkClient.active)
        {
            NetworkClient.connection.Send(new HostTransferMessage { newHostNetId = newHostNetId });
        }
    }

    #endregion

    #region Public API - Scene Management

    /// <summary>
    /// 씬 전환 시작 (로딩 씬을 거쳐 목표 씬으로 이동)
    /// </summary>
    public void StartSceneTransition(string sceneName)
    {
        networkSceneManager?.StartSceneTransition(sceneName);
    }

    /// <summary>
    /// 지정된 시간 후 메인 메뉴로 복귀
    /// </summary>
    public void ScheduleReturnToMainMenu(float delay)
    {
        networkSceneManager?.ScheduleReturnToMainMenu(delay);
    }

    #endregion

    #region Battle Management

    /// <summary>
    /// 모든 플레이어의 배틀 준비 상태를 확인하고 배틀 시작
    /// </summary>
    public void CheckAllBattleReady()
    {
        if (!NetworkServer.active || battleStarted)
            return;

        var allPlayers = GetAllConnectedPlayers();

        if (!ValidateBattleRequirements(allPlayers))
            return;

        if (AllPlayersReady(allPlayers))
        {
            StartBattle(allPlayers);
        }
    }

    /// <summary>
    /// 연결된 모든 플레이어 정보 가져오기
    /// </summary>
    private List<PlayerInfo> GetAllConnectedPlayers()
    {
        return NetworkServer.connections.Values
            .Where(c => c.identity != null)
            .Select(c => c.identity.GetComponent<PlayerInfo>())
            .Where(p => p != null)
            .ToList();
    }

    /// <summary>
    /// 배틀 시작 요구사항 검증
    /// </summary>
    private bool ValidateBattleRequirements(List<PlayerInfo> players)
    {
        const int MinPlayersRequired = 2;

        if (players.Count < MinPlayersRequired)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 수 부족: {players.Count}/{MinPlayersRequired}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 모든 플레이어의 배틀 준비 상태 확인
    /// </summary>
    private bool AllPlayersReady(List<PlayerInfo> players)
    {
        if (verboseLogging)
        {
            foreach (var player in players)
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {player.playerName}: 준비 상태 = {player.isBattleReady}");
            }
        }

        return players.All(p => p.isBattleReady);
    }

    /// <summary>
    /// 배틀 시작 처리
    /// </summary>
    private void StartBattle(List<PlayerInfo> players)
    {
        battleStarted = true;

        // BattleSceneManager 우선 시도
        var battleManager = FindObjectOfType<BattleSceneManager>();
        if (battleManager != null)
        {
            battleManager.SetBattlePhase(BattleSceneManager.BattlePhase.BattleStarting);
            return;
        }

        // 폴백: 기존 방식
        StartBattleFallback(players);
    }

    /// <summary>
    /// 배틀 시작 폴백 처리 (BattleSceneManager가 없는 경우)
    /// </summary>
    private void StartBattleFallback(List<PlayerInfo> players)
    {
        var controller = FindFirstObjectByType<BattleController>();
        if (controller != null && NetworkServer.active)
        {
            controller.StartBattle();
        }

        // 모든 플레이어에게 배틀 시작 알림
        foreach (var player in players)
        {
            player.RpcStartBattle();
        }
    }

    #endregion
}