using Mirror;
using UnityEngine;
using kcp2k;

/// <summary>
/// 네트워크 연결 관리를 담당하는 매니저
/// 클라이언트-서버 간 연결 설정, 호스트 모드 시작, 연결 상태 모니터링을 처리
/// </summary>
public class NetworkConnectionManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Connection Settings")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Private Fields

    private GameNetworkManager networkManager;
    private NetworkMessageHandler messageHandler;

    #endregion

    #region Initialization

    /// <summary>
    /// NetworkConnectionManager 초기화
    /// </summary>
    /// <param name="manager">참조할 GameNetworkManager 인스턴스</param>
    public void Initialize(GameNetworkManager manager)
    {
        networkManager = manager;
        InitializeMessageHandler();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkConnectionManager 초기화 완료");
    }

    /// <summary>
    /// 메시지 핸들러 초기화 또는 생성
    /// </summary>
    private void InitializeMessageHandler()
    {
        messageHandler = GetComponent<NetworkMessageHandler>();
        if (messageHandler == null)
        {
            messageHandler = gameObject.AddComponent<NetworkMessageHandler>();
        }

        messageHandler.Initialize();
    }

    #endregion

    #region Connection Setup

    /// <summary>
    /// 클라이언트 연결 정보 설정
    /// </summary>
    /// <param name="ip">서버 IP 주소</param>
    /// <param name="port">서버 포트 번호</param>
    public void SetupClient(string ip, int port)
    {
        if (networkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetworkManager가 초기화되지 않았습니다!");
            return;
        }

        networkManager.networkAddress = ip;

        var transport = GetComponent<KcpTransport>();
        if (transport != null)
        {
            transport.Port = (ushort)port;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 연결 설정 완료: {ip}:{port}");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] KcpTransport 컴포넌트를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 호스트 모드로 서버 시작
    /// </summary>
    public void StartHostMode()
    {
        if (NetworkServer.active)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버가 이미 실행 중입니다!");
            return;
        }

        if (networkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetworkManager가 초기화되지 않았습니다!");
            return;
        }

        networkManager.StartHost();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 호스트 모드 시작됨");
    }

    /// <summary>
    /// 클라이언트로 서버에 연결
    /// </summary>
    public void ConnectClient()
    {
        if (NetworkClient.active)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트가 이미 활성화되어 있습니다!");
            return;
        }

        if (networkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetworkManager가 초기화되지 않았습니다!");
            return;
        }

        var transport = GetComponent<KcpTransport>();
        var connectionInfo = $"{networkManager.networkAddress}:{transport?.Port}";

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 연결 시도: {connectionInfo}");

        networkManager.StartClient();
    }

    #endregion

    #region Network Events

    /// <summary>
    /// 클라이언트 시작 시 호출
    /// </summary>
    public void OnStartClient()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 시작됨");

        messageHandler?.RegisterClientHandlers();
    }

    /// <summary>
    /// 클라이언트가 서버에 연결되었을 때 호출
    /// </summary>
    public void OnClientConnect()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 연결 성공");

        messageHandler?.SendPlayerSpawnRequest();
    }

    /// <summary>
    /// 서버에 클라이언트가 연결되었을 때 호출
    /// </summary>
    /// <param name="conn">연결된 클라이언트 커넥션</param>
    public void OnServerConnect(NetworkConnectionToClient conn)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 연결됨 (ID: {conn.connectionId})");
    }

    /// <summary>
    /// 서버에서 클라이언트 연결이 해제되었을 때 호출
    /// </summary>
    /// <param name="conn">해제된 클라이언트 커넥션</param>
    public void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 연결 해제됨 (ID: {conn.connectionId})");

        // 플레이어 목록 업데이트 요청
        PlayerManager.Instance?.UpdatePlayerListAfterDisconnect();
    }

    #endregion
}
