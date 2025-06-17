using Mirror;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using NetworkMessages;

/// <summary>
/// 플레이어 관리를 담당하는 싱글톤 매니저
/// 플레이어 목록 관리, 팀 할당, 호스트 권한 이전, 게임 시작 조건 검증을 처리
/// </summary>
public class PlayerManager : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// PlayerManager 싱글톤 인스턴스
    /// </summary>
    public static PlayerManager Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Player Settings")]
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private float playerListUpdateDelay = 0.5f;

    #endregion

    #region Constants

    private const int MinPlayersRequired = 2;
    private const float DisconnectUpdateDelay = 0.2f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeSingleton();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 싱글톤 인스턴스 초기화
    /// </summary>
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] PlayerManager 인스턴스 생성됨");
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] PlayerManager 인스턴스가 이미 존재합니다. 중복 인스턴스를 제거합니다.");
            Destroy(gameObject);
        }
    }

    #endregion

    #region Player List Management

    /// <summary>
    /// 플레이어 스폰 후 플레이어 목록 업데이트
    /// </summary>
    public void UpdatePlayerListAfterSpawn()
    {
        StartCoroutine(SendPlayerListAfterDelay(playerListUpdateDelay));
    }

    /// <summary>
    /// 플레이어 연결 해제 후 플레이어 목록 업데이트
    /// </summary>
    public void UpdatePlayerListAfterDisconnect()
    {
        StartCoroutine(SendPlayerListAfterDelay(DisconnectUpdateDelay));
    }

    /// <summary>
    /// 모든 클라이언트에게 플레이어 목록 전송
    /// </summary>
    public void SendPlayerListToAll()
    {
        if (!ValidateServerState())
            return;

        var playerInfos = GetAllValidPlayers();

        if (playerInfos.Length == 0)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 전송할 플레이어가 없습니다");
            return;
        }

        var message = CreatePlayerListMessage(playerInfos);
        NetworkServer.SendToAll(message);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 전송 완료: {playerInfos.Length}명");
    }

    /// <summary>
    /// 지연 후 플레이어 목록 전송
    /// </summary>
    /// <param name="delay">지연 시간(초)</param>
    private IEnumerator SendPlayerListAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SendPlayerListToAll();
    }

    #endregion

    #region Team Management

    /// <summary>
    /// 플레이어들에게 팀 할당
    /// 첫 번째 플레이어는 Left 팀, 두 번째 플레이어는 Right 팀에 배정
    /// </summary>
    public void AssignTeams()
    {
        var allPlayers = GetAllValidPlayers();

        if (allPlayers.Length < MinPlayersRequired)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 팀 할당을 위한 플레이어 수가 부족합니다: {allPlayers.Length}/{MinPlayersRequired}");
            return;
        }

        allPlayers[0].teamId = TeamIndex.Left;
        allPlayers[1].teamId = TeamIndex.Right;

        if (verboseLogging)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 팀 할당 완료:");
            Debug.Log($"  - {allPlayers[0].playerName} → Left 팀");
            Debug.Log($"  - {allPlayers[1].playerName} → Right 팀");
        }
    }

    #endregion

    #region Host Management

    /// <summary>
    /// 호스트 권한을 다른 플레이어에게 이전
    /// </summary>
    /// <param name="newHostNetId">새 호스트가 될 플레이어의 NetworkIdentity ID</param>
    public void TransferHostRole(uint newHostNetId)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버가 비활성화 상태에서 호스트 이전 시도됨");
            return;
        }

        var players = GetAllValidPlayers();

        // 모든 플레이어의 호스트 권한 제거
        foreach (var player in players)
        {
            player.isHost = false;
        }

        // 새 호스트 설정
        var newHost = players.FirstOrDefault(p => p.netId == newHostNetId);
        if (newHost != null)
        {
            newHost.isHost = true;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 호스트 권한 이전 완료: {newHost.playerName}");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetId {newHostNetId}에 해당하는 플레이어를 찾을 수 없습니다");
        }

        SendPlayerListToAll();
    }

    #endregion

    #region Game State Management

    /// <summary>
    /// 모든 플레이어의 준비 상태를 확인하고 게임 시작
    /// </summary>
    public void CheckAllPlayersReadyAndStartGame()
    {
        if (!AllPlayersReadyAndValid())
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 모든 플레이어 준비 완료! 게임 시작");

        AssignTeams();
        RequestSceneTransition("BattleScene");
    }

    /// <summary>
    /// 모든 플레이어의 준비 상태 및 유효성 검증
    /// </summary>
    /// <returns>모든 플레이어가 준비되고 유효하면 true</returns>
    public bool AllPlayersReadyAndValid()
    {
        var allPlayers = GetAllValidPlayers();

        if (!ValidatePlayerCount(allPlayers))
            return false;

        if (!ValidatePlayersReady(allPlayers))
            return false;

        if (!ValidatePlayersValid(allPlayers))
            return false;

        return true;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 서버 상태 검증
    /// </summary>
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
    /// 플레이어 수 검증
    /// </summary>
    private bool ValidatePlayerCount(PlayerInfo[] players)
    {
        if (players.Length < MinPlayersRequired)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 수 부족: {players.Length}/{MinPlayersRequired}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 모든 플레이어의 준비 상태 검증
    /// </summary>
    private bool ValidatePlayersReady(PlayerInfo[] players)
    {
        bool allReady = players.All(p => p.isReady);

        if (!allReady)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 일부 플레이어가 준비되지 않았습니다");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 모든 플레이어의 유효성 검증
    /// </summary>
    private bool ValidatePlayersValid(PlayerInfo[] players)
    {
        bool allValid = players.All(p => IsCharacterValid(p));

        if (!allValid)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 일부 플레이어가 유효하지 않습니다");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 개별 플레이어의 캐릭터 유효성 검사
    /// </summary>
    /// <param name="player">검사할 플레이어</param>
    /// <returns>유효하면 true</returns>
    private bool IsCharacterValid(PlayerInfo player)
    {
        // 캐릭터 유효성 검사 로직 (확장 가능)
        // 예: 캐릭터 선택 여부, 색상 중복 방지 등
        return true;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 연결된 모든 유효한 플레이어 정보 가져오기
    /// </summary>
    /// <returns>유효한 플레이어 정보 배열</returns>
    private PlayerInfo[] GetAllValidPlayers()
    {
        return NetworkServer.connections.Values
            .Where(conn => conn != null && conn.identity != null)
            .Select(conn => conn.identity.GetComponent<PlayerInfo>())
            .Where(p => p != null)
            .ToArray();
    }

    /// <summary>
    /// 플레이어 목록 업데이트 메시지 생성
    /// </summary>
    /// <param name="players">플레이어 정보 배열</param>
    /// <returns>생성된 플레이어 목록 메시지</returns>
    private PlayerListUpdateMessage CreatePlayerListMessage(PlayerInfo[] players)
    {
        var message = new PlayerListUpdateMessage
        {
            playerNetIds = new List<uint>(),
            playerNames = new List<string>(),
            playerReadyStates = new List<bool>(),
            playerColorIndices = new List<int>(),
            playerIsHost = new List<bool>()
        };

        foreach (var player in players)
        {
            message.playerNetIds.Add(player.netId);
            message.playerNames.Add(player.playerName);
            message.playerReadyStates.Add(player.isReady);
            message.playerColorIndices.Add((int)player.playerPalette);
            message.playerIsHost.Add(player.isHost);

            if (verboseLogging)
                Debug.Log($"  - {player.playerName}: Ready={player.isReady}, Host={player.isHost}");
        }

        return message;
    }

    /// <summary>
    /// GameNetworkManager에게 씬 전환 요청
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름</param>
    private void RequestSceneTransition(string sceneName)
    {
        var gameNetworkManager = FindObjectOfType<GameNetworkManager>();
        if (gameNetworkManager != null)
        {
            gameNetworkManager.StartSceneTransition(sceneName);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] GameNetworkManager를 찾을 수 없습니다!");
        }
    }

    #endregion
}