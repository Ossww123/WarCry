using Mirror;
using UnityEngine;
using NetworkMessages;

/// <summary>
/// 플레이어 스폰 관리를 담당하는 매니저
/// 플레이어 생성 요청 처리, PlayerPrefab 검증, 네트워크 플레이어 객체 생성을 담당
/// </summary>
public class PlayerSpawnManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Spawn Settings")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Private Fields

    private GameNetworkManager networkManager;

    #endregion

    #region Initialization

    /// <summary>
    /// PlayerSpawnManager 초기화
    /// </summary>
    /// <param name="manager">참조할 GameNetworkManager 인스턴스</param>
    public void Initialize(GameNetworkManager manager)
    {
        networkManager = manager;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] PlayerSpawnManager 초기화 완료");
    }

    #endregion

    #region Public API

    /// <summary>
    /// 플레이어 스폰 요청 처리
    /// </summary>
    /// <param name="conn">요청한 네트워크 연결</param>
    /// <param name="msg">플레이어 스폰 요청 메시지</param>
    public void HandlePlayerSpawnRequest(NetworkConnection conn, PlayerSpawnRequest msg)
    {
        var connToClient = ValidateConnection(conn);
        if (connToClient == null)
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 스폰 요청 수신 (연결 ID: {connToClient.connectionId})");

        OnServerAddPlayer(connToClient);
    }

    /// <summary>
    /// 서버에서 플레이어 추가 처리
    /// </summary>
    /// <param name="conn">플레이어를 추가할 클라이언트 연결</param>
    public void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 추가 요청 (연결 ID: {conn.connectionId})");

        if (!ValidatePlayerAddition(conn))
            return;

        CreatePlayerForConnection(conn);
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 네트워크 연결 유효성 검증
    /// </summary>
    /// <param name="conn">검증할 네트워크 연결</param>
    /// <returns>유효한 NetworkConnectionToClient 또는 null</returns>
    private NetworkConnectionToClient ValidateConnection(NetworkConnection conn)
    {
        var connToClient = conn as NetworkConnectionToClient;
        if (connToClient == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 연결 타입입니다. NetworkConnectionToClient가 필요합니다.");
            return null;
        }

        return connToClient;
    }

    /// <summary>
    /// 플레이어 추가 가능 여부 검증
    /// </summary>
    /// <param name="conn">검증할 클라이언트 연결</param>
    /// <returns>추가 가능하면 true</returns>
    private bool ValidatePlayerAddition(NetworkConnectionToClient conn)
    {
        // 이미 플레이어가 존재하는지 확인
        if (conn.identity != null)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 연결 ID {conn.connectionId}에 이미 플레이어가 존재합니다");
            return false;
        }

        // PlayerPrefab 설정 확인
        if (networkManager == null || networkManager.playerPrefab == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerPrefab이 설정되지 않았습니다!");
            return false;
        }

        return true;
    }

    #endregion

    #region Player Creation

    /// <summary>
    /// 연결에 대한 플레이어 객체 생성
    /// </summary>
    /// <param name="conn">플레이어를 생성할 클라이언트 연결</param>
    private void CreatePlayerForConnection(NetworkConnectionToClient conn)
    {
        GameObject playerPrefab = networkManager.playerPrefab;

        // PlayerInfo 컴포넌트 존재 여부에 따른 처리
        if (HasPlayerInfoComponent(playerPrefab))
        {
            CreateNormalPlayer(conn, playerPrefab);
        }
        else
        {
            CreatePlayerWithRuntimeComponent(conn, playerPrefab);
        }

        NotifyPlayerSpawned();
    }

    /// <summary>
    /// 정상적인 플레이어 객체 생성 (PlayerInfo 컴포넌트 포함)
    /// </summary>
    /// <param name="conn">클라이언트 연결</param>
    /// <param name="prefab">플레이어 프리팹</param>
    private void CreateNormalPlayer(NetworkConnectionToClient conn, GameObject prefab)
    {
        GameObject player = Instantiate(prefab);
        NetworkServer.AddPlayerForConnection(conn, player);

        // 디버깅을 위한 이름 설정
        var networkIdentity = player.GetComponent<NetworkIdentity>();
        player.name = $"Player_{conn.connectionId}_NetId_{networkIdentity.netId}";

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 생성 완료: {player.name}");
    }

    /// <summary>
    /// 런타임에 PlayerInfo 컴포넌트를 추가하여 플레이어 생성 (개발용)
    /// </summary>
    /// <param name="conn">클라이언트 연결</param>
    /// <param name="prefab">플레이어 프리팹</param>
    private void CreatePlayerWithRuntimeComponent(NetworkConnectionToClient conn, GameObject prefab)
    {
        Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] PlayerInfo 컴포넌트가 없어 런타임에 추가합니다 (개발 전용)");

        GameObject tempPrefab = Instantiate(prefab);
        tempPrefab.AddComponent<PlayerInfo>();
        NetworkServer.AddPlayerForConnection(conn, tempPrefab);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] PlayerInfo 컴포넌트가 추가된 플레이어 생성 완료");
    }

    /// <summary>
    /// PlayerPrefab에 PlayerInfo 컴포넌트가 있는지 확인
    /// </summary>
    /// <param name="prefab">확인할 프리팹</param>
    /// <returns>PlayerInfo 컴포넌트가 있으면 true</returns>
    private bool HasPlayerInfoComponent(GameObject prefab)
    {
        return prefab.GetComponent<PlayerInfo>() != null;
    }

    /// <summary>
    /// 플레이어 스폰 완료 알림
    /// </summary>
    private void NotifyPlayerSpawned()
    {
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.UpdatePlayerListAfterSpawn();
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] PlayerManager 인스턴스를 찾을 수 없습니다");
        }
    }

    #endregion
}