using UnityEngine;
using Mirror;
using NetworkMessages;

/// <summary>
/// 플레이어 스폰 처리를 담당하는 컴포넌트
/// Mirror 네트워크 연결 상태에 따라 플레이어 스폰 요청을 자동으로 전송하며
/// 호스트 모드와 클라이언트 모드를 구분하여 적절한 타이밍에 스폰 요청을 처리
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    #region Inspector Fields

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 컴포넌트 초기화 시 네트워크 상태에 따른 스폰 처리 설정
    /// </summary>
    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializePlayerSpawning();
    }

    /// <summary>
    /// 컴포넌트 파괴 시 이벤트 정리
    /// </summary>
    private void OnDestroy()
    {
        CleanupEventSubscriptions();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 PlayerSpawner 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 네트워크 상태에 따른 플레이어 스폰 초기화
    /// </summary>
    private void InitializePlayerSpawning()
    {
        if (IsHostMode())
        {
            HandleHostModeSpawning();
        }
        else
        {
            HandleClientModeSpawning();
        }
    }

    /// <summary>
    /// 호스트 모드 여부 확인
    /// </summary>
    /// <returns>호스트 모드면 true</returns>
    private bool IsHostMode()
    {
        return NetworkServer.active && NetworkClient.isConnected;
    }

    #endregion

    #region Host Mode Handling

    /// <summary>
    /// 호스트 모드 스폰 처리
    /// 서버와 클라이언트가 동시에 활성화된 상태에서 즉시 스폰 요청 전송
    /// </summary>
    private void HandleHostModeSpawning()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 호스트 모드 감지 → 수동으로 Ready 및 SpawnRequest 실행");

        ExecuteSpawnSequence();
    }

    #endregion

    #region Client Mode Handling

    /// <summary>
    /// 클라이언트 모드 스폰 처리
    /// 연결 완료 이벤트를 구독하여 연결 후 스폰 요청 전송
    /// </summary>
    private void HandleClientModeSpawning()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 모드 → 연결 이벤트 구독");

        NetworkClient.OnConnectedEvent += OnClientConnected;
    }

    /// <summary>
    /// 클라이언트 연결 완료 이벤트 처리
    /// </summary>
    private void OnClientConnected()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 연결 완료 → Ready 및 SpawnRequest 전송");

        ExecuteSpawnSequence();
    }

    #endregion

    #region Spawn Sequence

    /// <summary>
    /// 플레이어 스폰 시퀀스 실행
    /// NetworkClient Ready 상태 설정 및 스폰 요청 메시지 전송
    /// </summary>
    private void ExecuteSpawnSequence()
    {
        EnsureClientReady();
        SendPlayerSpawnRequest();
    }

    /// <summary>
    /// NetworkClient Ready 상태 확인 및 설정
    /// </summary>
    private void EnsureClientReady()
    {
        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkClient Ready 상태로 설정됨");
        }
    }

    /// <summary>
    /// 플레이어 스폰 요청 메시지 전송
    /// </summary>
    private void SendPlayerSpawnRequest()
    {
        var spawnRequest = new PlayerSpawnRequest();
        NetworkClient.Send(spawnRequest);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] PlayerSpawnRequest 메시지 전송 완료");
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void CleanupEventSubscriptions()
    {
        NetworkClient.OnConnectedEvent -= OnClientConnected;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] PlayerSpawner 이벤트 구독 해제 완료");
    }

    #endregion
}