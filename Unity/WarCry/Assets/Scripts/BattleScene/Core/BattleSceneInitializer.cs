using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Handles initialization logic for the battle scene in a multiplayer game context.
/// </summary>
/// <remarks>
/// This class extends the NetworkBehaviour from the Mirror networking library, providing
/// functionality to initialize key components like the camera, UI, and spawner for
/// the battle scene when the client connects or the scene is loaded.
/// </remarks>
public class BattleSceneInitializer : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField]
    private Vector3 player1CameraPosition = new Vector3(-10, 5, 0);

    [SerializeField]
    private Vector3 player1CameraRotation = Vector3.zero;

    [SerializeField]
    private Vector3 player2CameraPosition = new Vector3(10, 5, 0);

    [SerializeField]
    private Vector3 player2CameraRotation = Vector3.zero;

    [Header("References")]
    [SerializeField]
    private BattleSpawner battleSpawner;

    [SerializeField]
    private BattleUIManager uiManager;

    [SerializeField]
    private SceneTransitionManager transitionManager;

    /// <summary>
    /// Called when the client starts and successfully connects to the server in a multiplayer game
    /// using the Mirror networking framework.
    /// </summary>
    /// <remarks>
    /// This method is overridden to perform additional initialization specific to the battle scene.
    /// It ensures that key components such as the camera and UI are properly set up and ready for the
    /// client. Additionally, it starts a coroutine to wait for the local player to be fully initialized
    /// before proceeding with further logic.
    /// </remarks>
    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(WaitForLocalPlayer());
    }

    /// <summary>
    /// Waits for the local player to be fully initialized and the client to be ready
    /// in a multiplayer game before proceeding with further initialization logic.
    /// </summary>
    /// <returns>
    /// A coroutine enumerator that yields until the network client is ready and the local player
    /// is assigned, ensuring proper synchronization in the battle scene setup.
    /// </returns>
    private IEnumerator WaitForLocalPlayer()
    {
        // 페이드 아웃이 진행되는 동안 NetworkClient 준비를 기다림
        yield return new WaitUntil(() => NetworkClient.ready && NetworkClient.localPlayer != null);

        // 카메라 위치를 즉시 설정합니다 (페이드 이펙트가 가려줄 것이기 때문에 부드러운 전환 필요 없음)
        PlayerInfo localPlayer = NetworkClient.localPlayer.GetComponent<PlayerInfo>();
        if (localPlayer != null)
        {
            SetupCamera(localPlayer.teamId == TeamIndex.Left);
        }

        Initialize();
    }

    /// <summary>
    /// Initializes components and settings necessary for the battle scene, ensuring proper setup of
    /// the player-specific camera, battle spawner, and UI manager.
    /// </summary>
    /// <remarks>
    /// This method retrieves the local player's information, sets up the camera based on the player's role,
    /// and initializes the battle spawner and UI manager. Logs errors if critical components like the
    /// local player, UI manager, or spawner are missing.
    /// </remarks>
    public void Initialize()
    {
        Debug.Log($"[{DebugUtils.GetCallerInfo()}]: 초기화 시작");

        // 서버 로직 - 항상 먼저 실행
        if (isServer && battleSpawner != null)
        {
            Debug.Log($"[{DebugUtils.GetCallerInfo()}]: 서버에서 BattleSpawner 초기화 호출");
            battleSpawner.Initialize();
        }

        // 클라이언트 로직
        if (isClient)
        {
            PlayerInfo localPlayer = null;

            // NetworkClient.localPlayer가 null이 아닐 때만 로컬 플레이어 찾기 시도
            if (NetworkClient.localPlayer != null)
            {
                localPlayer = NetworkClient.localPlayer.GetComponent<PlayerInfo>();

                if (localPlayer == null)
                {
                    Debug.LogWarning($"[{DebugUtils.GetCallerInfo()}]: NetworkClient.localPlayer에서 PlayerInfo 컴포넌트를 찾을 수 없습니다!");
                }
                else
                {
                    // 카메라 설정 등 클라이언트 로직
                    SetupCamera(localPlayer.teamId == TeamIndex.Left);

                    // UI 초기화는 localPlayer가 있을 때만
                    if (uiManager != null)
                    {
                        Debug.Log($"[{DebugUtils.GetCallerInfo()}]: UI 매니저 초기화 (플레이어: {localPlayer.playerName})");
                        uiManager.InitializeForPlayer(localPlayer);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[{DebugUtils.GetCallerInfo()}]: NetworkClient.localPlayer가 null입니다. UI 초기화 건너뜁니다.");
            }
        }

        Debug.Log($"[{DebugUtils.GetCallerInfo()}]: 초기화 완료");
    }

    private void SetupCamera(bool isLeftPlayer)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            if (isLeftPlayer)
            {
                mainCamera.transform.position = player1CameraPosition;
                mainCamera.transform.LookAt(player1CameraRotation);
            }
            else
            {
                mainCamera.transform.position = player2CameraPosition;
                mainCamera.transform.LookAt(player2CameraRotation);
            }

            Debug.Log(
                $"[{DebugUtils.GetCallerInfo()}]: 카메라 설정 완료 ({(isLeftPlayer ? "Left" : "Right")} Player)");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.GetCallerInfo()}]: 메인 카메라를 찾을 수 없습니다!");
        }
    }

    // 수동 호출용으로 남겨둬도 무방
    public void OnSceneLoaded()
    {
        Initialize();
    }
}