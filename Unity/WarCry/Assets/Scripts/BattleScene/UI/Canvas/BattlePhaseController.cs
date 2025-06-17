using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles UI phase transitions during battle (placement, gameplay, results)
/// </summary>
public class BattlePhaseController : MonoBehaviour
{
    [Header("UI 패널")]
    [SerializeField]
    private GameObject placementPanel;

    [SerializeField]
    private GameObject gameplayPanel;

    [SerializeField]
    private GameObject skillPanel;

    [SerializeField]
    private GameObject winnerPanel;

    [Header("배치 UI")]
    [SerializeField]
    private Button kingButton;

    [SerializeField]
    private Button infantryButton;

    [SerializeField]
    private Button archerButton;

    [SerializeField]
    private Button cavalryButton;

    [SerializeField]
    private Button wizardButton;

    [SerializeField]
    private Button readyButton;

    [SerializeField]
    private PlacementManager placementManager;

    [Header("카운트다운 UI")]
    [SerializeField]
    private TextMeshProUGUI countdownText;

    [Header("게임플레이 UI")]
    [SerializeField]
    private TextMeshProUGUI infoText;

    [Header("승리 UI")]
    [SerializeField]
    private TextMeshProUGUI winnerText;

    [Header("Manager References")]
    [SerializeField]
    private BattleSceneManager battleSceneManager;

    private Coroutine countdownRoutine;
    private PlayerInfo localPlayer;

    private Color originalButtonColor = new Color(1f, 1f, 1f, 1f); // 기본 버튼 색상
    private Color highlightColor = new Color(0.8f, 0.8f, 1f, 1f);  // 하이라이트 색상

    // 마지막으로 하이라이트된 버튼
    private Button lastHighlightedButton = null;

    private void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Headless 서버 모드에서 비활성화됨");
            gameObject.SetActive(false);
            return;
        }
    }

    public void Initialize(PlayerInfo player)
    {
        localPlayer = player;
        Debug.Log(
            $"[{DebugUtils.ResolveCallerMethod()}] 플레이어 {player.playerName}에 대한 UI 초기화");

        // UI 초기화
        SetPlacementPhase();

        // 버튼 이벤트 등록
        if (kingButton != null)
        {
            kingButton.onClick.AddListener(OnKingButtonClicked);
        }

        if (infantryButton != null)
        {
            infantryButton.onClick.AddListener(OnInfantryButtonClicked);
        }

        if (archerButton != null)
        {
            archerButton.onClick.AddListener(OnArcherButtonClicked);
        }

        if (cavalryButton != null)
        {
            cavalryButton.onClick.AddListener(OnCavalryButtonClicked);
        }

        if (wizardButton != null)
        {
            wizardButton.onClick.AddListener(OnWizardButtonClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        // BattleSceneManager 찾기
        if (battleSceneManager == null)
        {
            battleSceneManager = FindObjectOfType<BattleSceneManager>();
        }

        // BattleSceneManager 이벤트 구독
        if (battleSceneManager != null)
        {
            BattleSceneManager.OnPhaseChanged += OnBattlePhaseChanged;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (battleSceneManager != null)
        {
            BattleSceneManager.OnPhaseChanged -= OnBattlePhaseChanged;
        }
    }

    // BattleSceneManager에서 발생시키는 단계 변경 이벤트 처리
    private void OnBattlePhaseChanged(BattleSceneManager.BattlePhase phase)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 단계 변경 알림 수신: {phase}");

        switch (phase)
        {
            case BattleSceneManager.BattlePhase.UnitPlacement:
                SetPlacementPhase();
                break;

            case BattleSceneManager.BattlePhase.BattleInProgress:
                SetGameplayPhase();
                break;
        }
    }

    // 배치 단계 UI 활성화
    public void SetPlacementPhase()
    {
        if (placementPanel != null)
        {
            placementPanel.SetActive(true);
        }

        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(false);
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(false);
        }

        if (infoText != null)
        {
            infoText.text = "유닛을 배치하세요";
        }
    }

    // 전투 단계 UI 활성화
    public void SetGameplayPhase()
    {
        if (placementPanel != null)
        {
            placementPanel.SetActive(false);
        }

        if (skillPanel != null)
        {
            skillPanel.SetActive(true);
        }

        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(true);
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(false);
        }

        if (infoText != null)
        {
            infoText.text = "전투를 시작합니다!";
        }
    }

    // 승자 표시
    public void ShowWinner(string winnerName)
    {
        if (placementPanel != null)
        {
            placementPanel.SetActive(false);
        }

        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(false);
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(true);
        }

        if (winnerText != null)
        {
            winnerText.text = $"{winnerName}의 승리!";
        }
    }

    // 버튼 이벤트 핸들러
    private void OnKingButtonClicked()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 왕 유닛 선택됨");
        placementManager?.SelectUnitType(UnitType.King);
    }

    private void OnInfantryButtonClicked()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 보병 유닛 선택됨");
        placementManager?.SelectUnitType(UnitType.Infantry);
    }

    private void OnArcherButtonClicked()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁수 유닛 선택됨");
        placementManager?.SelectUnitType(UnitType.Archer);
    }

    private void OnCavalryButtonClicked()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 기병 유닛 선택됨");
        placementManager?.SelectUnitType(UnitType.Cavalry);
    }

    private void OnWizardButtonClicked()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 마법사 유닛 선택됨");
        placementManager?.SelectUnitType(UnitType.Wizard);
    }

    private void OnReadyButtonClicked()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 준비 완료 버튼 클릭됨");

        // 개선: BattleSceneManager 통해 준비 요청
        if (battleSceneManager != null)
        {
            // PlacementManager에 준비 상태 설정
            placementManager?.SetBattleReady(true);

            // 음성 명령 시스템 실행
            VoiceCommandLauncher voiceCommandLauncher = FindObjectOfType<VoiceCommandLauncher>();
            if (voiceCommandLauncher != null)
            {
                voiceCommandLauncher.LaunchVoiceCommand();
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 음성 명령 시스템 실행 요청됨");
            }
            else
            {
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] VoiceCommandLauncher를 찾을 수 없습니다.");
            }

            // 이제 이전 코드처럼 직접 PlayerInfo에 접근하지 않고 BattleSceneManager에 준비 요청
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] BattleSceneManager에 준비 요청");
        }
        else if (localPlayer != null)
        {
            // 폴백: 기존 방식
            localPlayer.CmdSetBattleReady(true);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 기존 방식으로 준비 요청");
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어가 null입니다!");
        }
    }

    // 버튼 하이라이트
    public void HighlightButton(UnitType unitType)
    {
        // 모든 버튼 원래 색상으로 리셋
        ResetAllButtonColors();

        // 선택된 유닛 타입에 따라 버튼 하이라이트
        Button buttonToHighlight = null;

        switch (unitType)
        {
            case UnitType.King:
                buttonToHighlight = kingButton;
                break;
            case UnitType.Infantry:
                buttonToHighlight = infantryButton;
                break;
            case UnitType.Archer:
                buttonToHighlight = archerButton;
                break;
            case UnitType.Cavalry:
                buttonToHighlight = cavalryButton;
                break;
            case UnitType.Wizard:
                buttonToHighlight = wizardButton;
                break;
        }

        // 버튼이 존재하고 활성화되어 있으면 하이라이트
        if (buttonToHighlight != null && buttonToHighlight.interactable)
        {
            ColorBlock colors = buttonToHighlight.colors;
            colors.normalColor = highlightColor;
            buttonToHighlight.colors = colors;
            lastHighlightedButton = buttonToHighlight;
        }
    }

    // 모든 버튼 색상 리셋
    private void ResetAllButtonColors()
    {
        // 마지막으로 하이라이트된 버튼만 리셋 (최적화)
        if (lastHighlightedButton != null)
        {
            ColorBlock colors = lastHighlightedButton.colors;
            colors.normalColor = originalButtonColor;
            lastHighlightedButton.colors = colors;
        }
    }

    /// <summary>
    /// Updates the placement phase UI based on the specified unit type.
    /// This can include enabling or disabling buttons, updating text, or
    /// other visual indicators relevant to the specific unit type.
    /// </summary>
    /// <param name="unitType">The type of unit for which the placement UI should be updated.</param>

    // 새로운 메서드: 준비 버튼 상태 직접 업데이트
    public void UpdateReadyButtonState(bool canBeReady)
    {
        if (readyButton != null)
        {
            readyButton.interactable = canBeReady;

            // 선택적: 준비 불가능할 때 Tool Tip 표시
            if (!canBeReady)
            {
                if (infoText != null)
                {
                    infoText.text = "모든 유닛을 배치해야 준비할 수 있습니다.";
                }
            }
            else
            {
                if (infoText != null && infoText.text.Contains("모든 유닛을 배치"))
                {
                    infoText.text = "유닛을 배치하세요";
                }
            }
        }
    }

    public void UpdatePlacementUI(UnitType unitType)
    {
        // 버튼 비활성화, 텍스트 갱신 등
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {unitType} 배치 UI 업데이트");

        if (placementManager == null) return;

        // 배치된 유닛 수와 필수 유닛 수 가져오기
        int placed = placementManager.GetPlacedUnitCount(unitType);
        int required = placementManager.GetRequiredUnitCount(unitType);

        // 버튼 상태 업데이트 - 필요한 수량을 모두 배치했으면 비활성화
        switch (unitType)
        {
            case UnitType.King:
                if (kingButton != null)
                {
                    kingButton.interactable = placed < required;
                }
                break;

            case UnitType.Infantry:
                if (infantryButton != null)
                {
                    infantryButton.interactable = placed < required;
                }
                break;

            case UnitType.Archer:
                if (archerButton != null)
                {
                    archerButton.interactable = placed < required;
                }
                break;

            case UnitType.Cavalry:
                if (cavalryButton != null)
                {
                    cavalryButton.interactable = placed < required;
                }
                break;

            case UnitType.Wizard:
                if (wizardButton != null)
                {
                    wizardButton.interactable = placed < required;
                }
                break;
        }
    }

    public void ShowCountdown(float seconds)
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
        }

        countdownRoutine = StartCoroutine(CountdownRoutine(seconds));
    }

    private IEnumerator CountdownRoutine(float seconds)
    {
        float remaining = seconds;

        while (remaining > 0)
        {
            if (countdownText != null)
            {
                countdownText.text = Mathf.CeilToInt(remaining)
                    .ToString();
                countdownText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (countdownText != null)
        {
            countdownText.text = "전투 시작!";
            yield return new WaitForSeconds(1f);
            countdownText.gameObject.SetActive(false);
            countdownText.text = string.Empty;
        }
    }
}