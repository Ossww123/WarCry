using UnityEngine;

/// <summary>
/// Main coordinator for battle UI components.
/// Acts as a facade to coordinate all the UI subsystems.
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField]
    private BattlePhaseController phaseController;

    [SerializeField]
    private AbilityCooldownHandler cooldownHandler;

    [SerializeField]
    private PlayerStatsDisplay statsDisplay;

    [Header("UI Panels")]
    [SerializeField]
    private GameObject skillPanel;

    private PlayerInfo localPlayer;
    private GameObject playerKingUnit;
    private string playerName;

    private bool foundPlayerKing = false;
    private bool foundPlayerName = false;
    private bool componentsInitialized = false;
    private bool isBattlePhase = false; // 전투 단계 여부를 추적하는 변수 추가

    private void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Headless 서버 모드에서 비활성화됨");
            gameObject.SetActive(false);
            return;
        }
    }

    public void InitializeForPlayer(PlayerInfo player)
    {
        localPlayer = player;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 {player.playerName}에 대한 UI 초기화");

        // 스킬 패널은 항상 처음에는 비활성화
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        // Hide UI panels until king is found
        if (statsDisplay != null)
        {
            statsDisplay.gameObject.SetActive(false);
        }
        
        // Initialize phase controller immediately
        if (phaseController != null)
        {
            phaseController.Initialize(player);
        }

        // 배틀 매니저의 이벤트 구독
        BattleSceneManager battleManager = FindObjectOfType<BattleSceneManager>();
        if (battleManager != null)
        {
            BattleSceneManager.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    // 단계 변경 이벤트 핸들러 추가
    private void HandlePhaseChanged(BattleSceneManager.BattlePhase phase)
    {
        Debug.Log($"[BattleUIManager] 전투 단계 변경: {phase}");

        // 전투 중 단계일 때만 스킬 패널 활성화
        isBattlePhase = (phase == BattleSceneManager.BattlePhase.BattleInProgress);

        // UI 상태 업데이트
        UpdateUIBasedOnPhase();
    }

    private void UpdateUIBasedOnPhase()
    {
        // 스킬 패널은 전투 중에만 표시
        if (skillPanel != null)
        {
            skillPanel.SetActive(isBattlePhase && foundPlayerKing);
        }
    }

    private void Update()
    {
        // Step 1: Find player king if not found yet
        if (!foundPlayerKing)
        {
            foundPlayerKing = LocalPlayerLocator.TryFindPlayerKing(out playerKingUnit);

            if (!foundPlayerKing)
            {
                return; // Wait until we find the player king
            }

            Debug.Log(
                $"[{DebugUtils.ResolveCallerMethod()}] Player king found: {SceneNavigator.RetrievePath(playerKingUnit)}");
        }

        // Step 2: Find player name if not found yet
        if (!foundPlayerName)
        {
            foundPlayerName =
                LocalPlayerLocator.TryFindForUsername(playerKingUnit, out playerName);

            if (!foundPlayerName)
            {
                return; // Wait until we find the player name
            }
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Player name found: {playerName}");
        }

        // Step 3: Initialize components if not done yet
        if (!componentsInitialized)
        {
            InitializeComponents();
            componentsInitialized = true;
            return;
        }

        // Step 4: Check if player was destroyed and reset if needed
        if (componentsInitialized && playerKingUnit == null)
        {
            ResetPlayerComponents();
            foundPlayerKing = false;
            foundPlayerName = false;
            componentsInitialized = false;
        }
    }

    private void InitializeComponents()
    {
        // Initialize UI components when player is found
        if (statsDisplay != null)
        {
            statsDisplay.gameObject.SetActive(true);
            statsDisplay.Initialize(playerKingUnit);
        }

        // 수정: 스킬 패널은 전투 단계에만 활성화
        if (cooldownHandler != null && skillPanel != null)
        {
            // 전투 중일 때만 활성화
            skillPanel.SetActive(isBattlePhase);

            Abilities playerAbilities = playerKingUnit.GetComponent<Abilities>();
            if (playerAbilities != null)
            {
                cooldownHandler.Initialize(playerAbilities);
            }
        }

        Debug.Log(
            $"[{DebugUtils.ResolveCallerMethod()}] Components initialized for player {playerName}");

        // UI 업데이트
        UpdateUIBasedOnPhase();
    }

    private void ResetPlayerComponents()
    {
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Player lost, components reset");
    }

    // Phase transition methods - delegate to phase controller
    public void SetPlacementPhase()
    {
        isBattlePhase = false;
        phaseController?.SetPlacementPhase();
        UpdateUIBasedOnPhase(); // UI 상태 업데이트
    }

    public void SetGameplayPhase()
    {
        isBattlePhase = true;
        phaseController?.SetGameplayPhase();
        UpdateUIBasedOnPhase(); // UI 상태 업데이트
    }

    public void ShowWinner(string winnerName)
    {
        phaseController?.ShowWinner(winnerName);
    }

    public void ShowCountdown(float seconds)
    {
        phaseController?.ShowCountdown(seconds);
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        BattleSceneManager battleManager = FindObjectOfType<BattleSceneManager>();
        if (battleManager != null)
        {
            BattleSceneManager.OnPhaseChanged -= HandlePhaseChanged;
        }
    }
}