using Mirror;
using UnityEngine;
using NetworkMessages;

/// <summary>
/// 네트워크 메시지 송수신을 관리하는 핸들러
/// 클라이언트 메시지 핸들러 등록, 플레이어 스폰 요청, 플레이어 목록 업데이트 처리를 담당
/// </summary>
public class NetworkMessageHandler : MonoBehaviour
{
    #region Inspector Fields

    [Header("Message Settings")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Static Fields

    /// <summary>
    /// 클라이언트 핸들러 등록 상태 추적
    /// </summary>
    private static bool handlersRegistered = false;

    #endregion

    #region Initialization

    /// <summary>
    /// NetworkMessageHandler 초기화
    /// </summary>
    public void Initialize()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkMessageHandler 초기화 완료");
    }

    #endregion

    #region Client Message Handling

    /// <summary>
    /// 클라이언트 메시지 핸들러 등록
    /// 중복 등록을 방지하고 NetworkClient가 활성화된 상태에서만 등록
    /// </summary>
    public void RegisterClientHandlers()
    {
        if (handlersRegistered)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 핸들러가 이미 등록되어 있습니다");
            return;
        }

        if (!NetworkClient.active)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] NetworkClient가 비활성화 상태에서 핸들러 등록 시도됨");
            return;
        }

        NetworkClient.RegisterHandler<PlayerListUpdateMessage>(OnPlayerListReceived);
        handlersRegistered = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 메시지 핸들러 등록 완료");
    }

    /// <summary>
    /// 서버에 플레이어 스폰 요청 전송
    /// </summary>
    public void SendPlayerSpawnRequest()
    {
        // 핸들러가 등록되지 않은 경우 재등록 시도
        if (!handlersRegistered)
        {
            RegisterClientHandlers();
        }

        if (!ValidateNetworkClientState())
            return;

        var spawnRequest = new PlayerSpawnRequest();
        NetworkClient.Send(spawnRequest);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 스폰 요청 전송됨");
    }

    /// <summary>
    /// NetworkClient 상태 검증
    /// </summary>
    /// <returns>NetworkClient가 활성화되어 있으면 true, 그렇지 않으면 false</returns>
    private bool ValidateNetworkClientState()
    {
        if (!NetworkClient.active)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetworkClient가 비활성화 상태입니다!");
            return false;
        }

        return true;
    }

    #endregion

    #region Message Receivers

    /// <summary>
    /// 서버로부터 플레이어 목록 업데이트 메시지 수신 처리
    /// </summary>
    /// <param name="message">수신된 플레이어 목록 업데이트 메시지</param>
    private void OnPlayerListReceived(PlayerListUpdateMessage message)
    {
        if (message.playerNames == null)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 플레이어 목록 메시지 수신됨");
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 수신: {message.playerNames.Count}명");

        // GameNetworkManager로 이벤트 전달
        GameNetworkManager.TriggerPlayerListUpdate(message);
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 컴포넌트 파괴 시 정리 작업
    /// </summary>
    private void OnDestroy()
    {
        // 정적 변수 초기화 (다른 인스턴스에서 재사용 가능하도록)
        handlersRegistered = false;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkMessageHandler 정리 완료");
    }

    #endregion
}