using System.Collections;
using UnityEngine;
using Mirror;
using System;

/// <summary>
/// Central manager for the battle scene that coordinates all major subsystems.
/// Acts as the primary orchestrator for the entire battle flow.
/// </summary>
public class BattleSceneManager : NetworkBehaviour
{
    // Scene phase states
    public enum BattlePhase
    {
        Initialization,    // 초기화 단계
        UnitPlacement,     // 유닛 배치 단계
        BattleStarting,    // 전투 시작 카운트다운
        BattleInProgress,  // 전투 진행 중
        BattleEnded        // 전투 종료
    }

    [Header("Battle Flow")]
    [SerializeField] private float initialDelayBeforePlacement = 1.0f;
    [SerializeField] private float countdownDuration = 3.0f;
    [SerializeField] private bool autoTransitionToPlacement = true;

    [Header("Dependencies")]
    [SerializeField] private BattleSceneInitializer sceneInitializer;
    [SerializeField] private BattleSpawner battleSpawner;
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private BattleController battleController;
    [SerializeField] private BattleUIManager uiManager;

    [SyncVar(hook = nameof(OnBattlePhaseChanged))]
    private BattlePhase currentPhase = BattlePhase.Initialization;

    // Events for phase transitions
    public static event Action<BattlePhase> OnPhaseChanged;

    private void Start()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] BattleSceneManager 시작");

        // Subscribe to key battle events
        BattleController.OnBattleStart += HandleBattleStart;

        // Begin the initialization process
        StartCoroutine(InitializationSequence());
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        BattleController.OnBattleStart -= HandleBattleStart;
    }

    private IEnumerator InitializationSequence()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 초기화 시퀀스 시작");

        // Step 1: Wait for NetworkClient to be ready (if we're a client)
        if (NetworkClient.active && !isServer)
        {
            yield return new WaitUntil(() => NetworkClient.ready && NetworkClient.localPlayer != null);
        }

        // Step 2: Initialize the battle scene components
        if (sceneInitializer != null)
        {
            sceneInitializer.Initialize();
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] BattleSceneInitializer가 null입니다!");
        }

        // Step 3: Spawn castles on the server
        if (isServer && battleSpawner != null)
        {
            battleSpawner.Initialize();
        }

        // Step 4: Give components time to initialize
        yield return new WaitForSeconds(initialDelayBeforePlacement);

        // Step 5: Transition to placement phase
        if (autoTransitionToPlacement)
        {
            TransitionToPlacementPhase();
        }
    }

    // Server-only method to change the battle phase
    [Server]
    public void SetBattlePhase(BattlePhase newPhase)
    {
        if (currentPhase == newPhase) return;

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 단계 변경: {currentPhase} -> {newPhase}");
        currentPhase = newPhase;

        // Handle specific phase transitions on the server
        switch (newPhase)
        {
            case BattlePhase.BattleStarting:
                // Server triggers battle start countdown
                StartCoroutine(StartBattleCountdown());
                break;

            case BattlePhase.BattleEnded:
                // Schedule return to main menu
                GameNetworkManager networkManager = NetworkManager.singleton as GameNetworkManager;
                if (networkManager != null)
                {
                    networkManager.ScheduleReturnToMainMenu(5f);
                }
                break;
        }
    }

    // Client-side hook for battle phase changes
    private void OnBattlePhaseChanged(BattlePhase oldPhase, BattlePhase newPhase)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 단계 변경 감지: {oldPhase} -> {newPhase}");

        // Update UI based on the new phase
        UpdateUIForPhase(newPhase);

        // Notify other systems about the phase change
        OnPhaseChanged?.Invoke(newPhase);
    }

    // Update UI elements based on battle phase
    private void UpdateUIForPhase(BattlePhase phase)
    {
        if (uiManager == null) return;

        switch (phase)
        {
            case BattlePhase.UnitPlacement:
                uiManager.SetPlacementPhase();
                break;

            case BattlePhase.BattleStarting:
                // Countdown UI will be handled separately
                break;

            case BattlePhase.BattleInProgress:
                uiManager.SetGameplayPhase();
                break;

            case BattlePhase.BattleEnded:
                // Winner UI is handled separately
                break;
        }
    }

    // Command to request phase transition to placement
    [Command(requiresAuthority = false)]
    public void CmdRequestPlacementPhase()
    {
        TransitionToPlacementPhase();
    }

    // Server-side method to transition to placement phase
    [Server]
    private void TransitionToPlacementPhase()
    {
        SetBattlePhase(BattlePhase.UnitPlacement);
    }

    // Command to request battle ready state
    [Command(requiresAuthority = false)]
    public void CmdSetBattleReady(bool isReady, NetworkConnectionToClient sender = null)
    {
        // Find the player's PlayerInfo
        PlayerInfo playerInfo = null;
        if (sender != null && sender.identity != null)
        {
            playerInfo = sender.identity.GetComponent<PlayerInfo>();
        }

        if (playerInfo != null)
        {
            // Set player's battle ready state
            playerInfo.isBattleReady = isReady;

            // Check if all players are ready
            CheckAllPlayersReady();
        }
    }

    // Server method to check if all players are ready to start
    [Server]
    private void CheckAllPlayersReady()
    {
        // Get the GameNetworkManager to check player ready states
        GameNetworkManager networkManager = NetworkManager.singleton as GameNetworkManager;
        networkManager?.CheckAllBattleReady();
    }

    // Server coroutine for battle countdown
    [Server]
    private IEnumerator StartBattleCountdown()
    {
        // Show countdown on all clients
        RpcShowCountdown(countdownDuration);

        // Wait for countdown duration
        yield return new WaitForSeconds(countdownDuration);

        // Start the battle
        SetBattlePhase(BattlePhase.BattleInProgress);
        battleController?.StartBattle();
    }

    // ClientRpc to show countdown on all clients
    [ClientRpc]
    private void RpcShowCountdown(float duration)
    {
        uiManager?.ShowCountdown(duration);
    }

    // Event handler for battle start
    private void HandleBattleStart()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 시작 이벤트 처리");
        // Add any additional logic needed when battle starts
    }

    // Public method to declare a winner (called by PlayerInfo or other components)
    [Server]
    public void DeclareWinner(string winnerName)
    {
        if (currentPhase == BattlePhase.BattleEnded)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이미 전투가 종료됨, 중복 승리 선언 무시: {winnerName}");
            return;
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 승자 선언: {winnerName}, 현재 단계: {currentPhase}");

        // 추가 디버그: 네트워크 연결 상태 확인
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 활성 상태: {NetworkServer.active}, 클라이언트 수: {NetworkServer.connections.Count}");

        // 셰이스 변경 설정
        SetBattlePhase(BattlePhase.BattleEnded);

        // 모든 클라이언트에 승자 표시
        RpcShowWinner(winnerName);

        // 모든 플레이어에게 결과 전송
        SetResultForAllPlayers(winnerName);

        // 결과 씬으로 전환
        StartCoroutine(TransitionToResultScene(3f));
    }

    [Server]
    private void SetResultForAllPlayers(string winnerName)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn?.identity == null) continue;

            PlayerInfo playerInfo = conn.identity.GetComponent<PlayerInfo>();
            if (playerInfo == null) continue;

            bool isWinner = playerInfo.playerName == winnerName;
            string result = isWinner ? "WIN" : "LOSE";

            // 클라이언트에 결과 전송
            playerInfo.RpcSetMatchResult(result);

            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 {playerInfo.playerName}에게 결과 전송: {result}");
        }
    }

    // ResultScene으로 전환하는 코루틴
    [Server]
    private IEnumerator TransitionToResultScene(float delay)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {delay}초 후 ResultScene으로 전환 예정");
        yield return new WaitForSeconds(delay);

        // 서버에서 씬 전환
        NetworkManager.singleton.ServerChangeScene("ResultScene");
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] ResultScene으로 씬 전환 시작");
    }

    // ClientRpc to show winner on all clients
    [ClientRpc]
    private void RpcShowWinner(string winnerName)
    {
        uiManager?.ShowWinner(winnerName);
    }
}