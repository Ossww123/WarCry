using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;
using System.Collections;

/// <summary>
/// 대기실 UI의 총괄 관리를 담당하는 매니저
/// 하위 UI 컨트롤러들(ColorPalette, PlayerList, CharacterPreview)을 조정하고
/// 버튼 이벤트 처리, 플레이어 정보 동기화, UI 상태 관리 등의 기능을 제공
/// </summary>
public class WaitingRoomUIManager : MonoBehaviour
{
    #region Events

    /// <summary>
    /// 준비 상태 변경 요청 시 발생하는 이벤트 (준비 상태)
    /// </summary>
    public static event Action<bool> OnReadyStateChangeRequested;

    /// <summary>
    /// 방 나가기 요청 시 발생하는 이벤트
    /// </summary>
    public static event Action OnLeaveRoomRequested;

    #endregion

    #region Inspector Fields

    [Header("UI References")]
    [SerializeField] private TMP_Text roomTitleText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button leaveButton;

    [Header("Sub Controllers")]
    [SerializeField] private ColorPaletteController colorPaletteController;
    [SerializeField] private PlayerListUIController playerListController;
    [SerializeField] private CharacterPreviewController characterPreviewController;

    [Header("Button Text Configuration")]
    [SerializeField] private string readyButtonText = "준비 완료";
    [SerializeField] private string notReadyButtonText = "준비 취소";
    [SerializeField] private string hostReadyText = "게임 시작 가능";
    [SerializeField] private string hostNotReadyText = "준비 완료";

    [Header("Room Info")]
    [SerializeField] private string roomTitleFormat = "대기실 - 방 #{0}";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string CurrentMatchIdKey = "CurrentMatchId";
    private const int MinPlayersForGame = 2;

    #endregion

    #region Private Fields

    // 상태 관리
    private PlayerInfo localPlayer;
    private bool isInitialized = false;
    private bool currentReadyState = false;

    // UI 상태 추적
    private Dictionary<uint, bool> playerReadyStates = new Dictionary<uint, bool>();
    private HashSet<int> usedColorIndices = new HashSet<int>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        ValidateReferences();
    }

    private void OnDestroy()
    {
        CleanupUIManager();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 WaitingRoomUIManager 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 필수 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        ValidateUIReferences();
        ValidateSubControllers();
    }

    /// <summary>
    /// UI 참조 검증
    /// </summary>
    private void ValidateUIReferences()
    {
        if (roomTitleText == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] RoomTitleText가 설정되지 않았습니다!");
        }

        if (readyButton == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] ReadyButton이 설정되지 않았습니다!");
        }

        if (leaveButton == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] LeaveButton이 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// 하위 컨트롤러 검증
    /// </summary>
    private void ValidateSubControllers()
    {
        if (colorPaletteController == null)
        {
            colorPaletteController = FindObjectOfType<ColorPaletteController>();
            if (colorPaletteController == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] ColorPaletteController를 찾을 수 없습니다!");
            }
        }

        if (playerListController == null)
        {
            playerListController = FindObjectOfType<PlayerListUIController>();
            if (playerListController == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerListUIController를 찾을 수 없습니다!");
            }
        }

        if (characterPreviewController == null)
        {
            characterPreviewController = FindObjectOfType<CharacterPreviewController>();
            if (characterPreviewController == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] CharacterPreviewController를 찾을 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// 플레이어별 UI 초기화
    /// </summary>
    /// <param name="player">로컬 플레이어 정보</param>
    public void InitializeForPlayer(PlayerInfo player)
    {
        if (isInitialized)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] UI 매니저가 이미 초기화되었습니다");
            return;
        }

        localPlayer = player;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {player.playerName}({player.netId})에 대한 UI 초기화 시작");

        InitializeRoomInfo();
        InitializeButtons();
        InitializeSubControllers();
        RegisterEventHandlers();
        FinalizeInitialization();
    }

    /// <summary>
    /// 방 정보 초기화
    /// </summary>
    private void InitializeRoomInfo()
    {
        int roomId = PlayerPrefs.GetInt(CurrentMatchIdKey, -1);
        if (roomId != -1 && roomTitleText != null)
        {
            roomTitleText.text = string.Format(roomTitleFormat, roomId);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 정보 설정: 방 #{roomId}");
        }
    }

    /// <summary>
    /// 버튼 초기화
    /// </summary>
    private void InitializeButtons()
    {
        SetupReadyButton();
        SetupLeaveButton();
    }

    /// <summary>
    /// 준비 버튼 설정
    /// </summary>
    private void SetupReadyButton()
    {
        if (readyButton == null) return;

        readyButton.onClick.RemoveAllListeners();
        readyButton.onClick.AddListener(OnReadyButtonClicked);

        UpdateReadyButtonText();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 준비 버튼 설정 완료");
    }

    /// <summary>
    /// 나가기 버튼 설정
    /// </summary>
    private void SetupLeaveButton()
    {
        if (leaveButton == null) return;

        leaveButton.onClick.RemoveAllListeners();
        leaveButton.onClick.AddListener(OnLeaveButtonClicked);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 나가기 버튼 설정 완료");
    }

    /// <summary>
    /// 하위 컨트롤러 초기화
    /// </summary>
    private void InitializeSubControllers()
    {
        InitializeColorPalette();
        InitializePlayerList();
        InitializeCharacterPreview();
    }

    /// <summary>
    /// 색상 팔레트 초기화
    /// </summary>
    private void InitializeColorPalette()
    {
        if (colorPaletteController != null)
        {
            if (!colorPaletteController.IsInitialized())
            {
                colorPaletteController.InitializeColorPalette();
            }

            // 서버에서 할당된 현재 색상 적용
            if (localPlayer != null)
            {
                colorPaletteController.UpdateColorSelection((int)localPlayer.playerPalette);
            }

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 팔레트 초기화 완료");
        }
    }

    /// <summary>
    /// 플레이어 목록 초기화
    /// </summary>
    private void InitializePlayerList()
    {
        if (playerListController != null)
        {
            if (!playerListController.IsInitialized())
            {
                playerListController.InitializePlayerList();
            }

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 초기화 완료");
        }
    }

    /// <summary>
    /// 캐릭터 프리뷰 초기화
    /// </summary>
    private void InitializeCharacterPreview()
    {
        if (characterPreviewController != null)
        {
            if (!characterPreviewController.IsInitialized())
            {
                characterPreviewController.InitializeCharacterPreview();
            }

            // 현재 색상으로 프리뷰 업데이트
            if (localPlayer != null)
            {
                characterPreviewController.UpdateCharacterMaterial((int)localPlayer.playerPalette);
            }

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰 초기화 완료");
        }
    }

    /// <summary>
    /// 이벤트 핸들러 등록
    /// </summary>
    private void RegisterEventHandlers()
    {
        // 하위 컨트롤러 이벤트 구독
        ColorPaletteController.OnColorSelected += HandleColorSelected;
        PlayerListUIController.OnPlayerListUpdated += HandlePlayerListUpdated;
        CharacterPreviewController.OnPreviewUpdated += HandlePreviewUpdated;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이벤트 핸들러 등록 완료");
    }

    /// <summary>
    /// 초기화 완료 처리
    /// </summary>
    private void FinalizeInitialization()
    {
        isInitialized = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomUIManager 초기화 완료");
    }

    /// <summary>
    /// UI 매니저 정리
    /// </summary>
    private void CleanupUIManager()
    {
        UnregisterEventHandlers();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomUIManager 정리 완료");
    }

    /// <summary>
    /// 이벤트 핸들러 등록 해제
    /// </summary>
    private void UnregisterEventHandlers()
    {
        ColorPaletteController.OnColorSelected -= HandleColorSelected;
        PlayerListUIController.OnPlayerListUpdated -= HandlePlayerListUpdated;
        CharacterPreviewController.OnPreviewUpdated -= HandlePreviewUpdated;
    }

    #endregion

    #region Public API - Player List Update

    /// <summary>
    /// 플레이어 목록 업데이트 (외부에서 호출)
    /// </summary>
    /// <param name="netIds">네트워크 ID 목록</param>
    /// <param name="names">플레이어 이름 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    /// <param name="isHostList">호스트 여부 목록</param>
    public void UpdatePlayerList(List<uint> netIds, List<string> names, List<bool> readyStates, List<int> colorIndices, List<bool> isHostList)
    {
        if (!isInitialized)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] UI 매니저가 초기화되지 않았습니다");
            return;
        }

        try
        {
            // 플레이어 목록 업데이트 (PlayerListUIController에 위임)
            if (playerListController != null)
            {
                playerListController.UpdatePlayerList(netIds, names, readyStates, colorIndices, isHostList);
            }

            // 색상 사용 상태 업데이트
            UpdateColorUsageStatus(colorIndices);

            // 로컬 플레이어 상태 동기화
            SyncLocalPlayerState(netIds, readyStates, colorIndices);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 완료: {netIds?.Count ?? 0}명");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 색상 선택 UI 업데이트 (서버에서 거부된 경우)
    /// </summary>
    /// <param name="colorIndex">업데이트할 색상 인덱스</param>
    public void UpdateColorSelection(int colorIndex)
    {
        if (colorPaletteController != null)
        {
            colorPaletteController.UpdateColorSelection(colorIndex);
        }

        if (characterPreviewController != null)
        {
            characterPreviewController.UpdateCharacterMaterial(colorIndex);
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 선택 UI 업데이트: {colorIndex}");
    }

    #endregion

    #region Event Handlers - Sub Controllers

    /// <summary>
    /// 색상 선택 이벤트 처리
    /// </summary>
    /// <param name="colorIndex">선택된 색상 인덱스</param>
    private void HandleColorSelected(int colorIndex)
    {
        // 캐릭터 프리뷰 업데이트 (파티클 효과 포함)
        if (characterPreviewController != null)
        {
            characterPreviewController.UpdateCharacterWithEffect(colorIndex);
        }

        // 서버에 색상 변경 요청
        if (localPlayer != null)
        {
            localPlayer.CmdSetPlayerColor((Palettes)colorIndex);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 변경 요청 전송: {colorIndex}");
        }
    }

    /// <summary>
    /// 플레이어 목록 업데이트 완료 이벤트 처리
    /// </summary>
    /// <param name="playerCount">플레이어 수</param>
    private void HandlePlayerListUpdated(int playerCount)
    {
        // 게임 시작 가능 여부 업데이트
        UpdateReadyButtonState(playerCount);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 완료: {playerCount}명");
    }

    /// <summary>
    /// 캐릭터 프리뷰 업데이트 완료 이벤트 처리
    /// </summary>
    /// <param name="colorIndex">업데이트된 색상 인덱스</param>
    private void HandlePreviewUpdated(int colorIndex)
    {
        // 프리뷰 업데이트 완료 후 추가 처리
        // 예: UI 피드백, 사운드 재생 등

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐릭터 프리뷰 업데이트 완료: 색상 {colorIndex}");
    }

    #endregion

    #region Event Handlers - Button Events

    /// <summary>
    /// 준비 버튼 클릭 이벤트 처리
    /// </summary>
    private void OnReadyButtonClicked()
    {
        if (localPlayer == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어 참조가 없습니다!");
            return;
        }

        // 준비 상태 토글
        bool newReadyState = !currentReadyState;

        // 이벤트 발생
        TriggerReadyStateChangeRequested(newReadyState);

        // 서버에 준비 상태 변경 알림
        localPlayer.CmdSetReady(newReadyState);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 준비 상태 변경 요청: {newReadyState}");
    }

    /// <summary>
    /// 나가기 버튼 클릭 이벤트 처리
    /// </summary>
    private void OnLeaveButtonClicked()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 나가기 버튼 클릭됨");

        TriggerLeaveRoomRequested();
    }

    #endregion

    #region Private Methods - State Management

    /// <summary>
    /// 색상 사용 상태 업데이트
    /// </summary>
    /// <param name="colorIndices">사용 중인 색상 인덱스 목록</param>
    private void UpdateColorUsageStatus(List<int> colorIndices)
    {
        // 사용 중인 색상 수집
        usedColorIndices.Clear();
        if (colorIndices != null)
        {
            foreach (int index in colorIndices)
            {
                usedColorIndices.Add(index);
            }
        }

        // 색상 팔레트 업데이트
        if (colorPaletteController != null && localPlayer != null)
        {
            colorPaletteController.UpdateUsedColors(usedColorIndices, (int)localPlayer.playerPalette);
        }
    }

    /// <summary>
    /// 로컬 플레이어 상태 동기화
    /// </summary>
    /// <param name="netIds">네트워크 ID 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    private void SyncLocalPlayerState(List<uint> netIds, List<bool> readyStates, List<int> colorIndices)
    {
        if (localPlayer == null || netIds == null) return;

        // 로컬 플레이어 인덱스 찾기
        int localPlayerIndex = netIds.IndexOf(localPlayer.netId);
        if (localPlayerIndex == -1) return;

        // 준비 상태 동기화
        if (readyStates != null && localPlayerIndex < readyStates.Count)
        {
            bool newReadyState = readyStates[localPlayerIndex];
            if (currentReadyState != newReadyState)
            {
                currentReadyState = newReadyState;
                UpdateReadyButtonText();

                // 색상 변경 허용 상태 업데이트
                if (colorPaletteController != null)
                {
                    colorPaletteController.SetColorChangeAllowed(!newReadyState);
                }
            }
        }

        // 색상 동기화
        if (colorIndices != null && localPlayerIndex < colorIndices.Count)
        {
            int serverColorIndex = colorIndices[localPlayerIndex];
            if (colorPaletteController != null)
            {
                // 서버 색상과 UI가 다르면 동기화
                int currentUIColor = colorPaletteController.GetSelectedColorIndex();
                if (currentUIColor != serverColorIndex)
                {
                    UpdateColorSelection(serverColorIndex);
                }
            }
        }
    }

    #endregion

    #region Private Methods - UI Updates

    /// <summary>
    /// 준비 버튼 텍스트 업데이트
    /// </summary>
    private void UpdateReadyButtonText()
    {
        if (readyButton == null) return;

        TMP_Text buttonText = readyButton.GetComponentInChildren<TMP_Text>();
        if (buttonText == null) return;

        if (localPlayer != null && localPlayer.isHost)
        {
            buttonText.text = currentReadyState ? hostReadyText : hostNotReadyText;
        }
        else
        {
            buttonText.text = currentReadyState ? notReadyButtonText : readyButtonText;
        }
    }

    /// <summary>
    /// 준비 버튼 상태 업데이트
    /// </summary>
    /// <param name="playerCount">현재 플레이어 수</param>
    private void UpdateReadyButtonState(int playerCount)
    {
        // 최소 플레이어 수 미달 시 버튼 비활성화 등의 로직
        // 현재는 단순히 로그만 출력
        bool canStartGame = playerCount >= MinPlayersForGame;

        if (verboseLogging && localPlayer != null && localPlayer.isHost)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 게임 시작 가능: {canStartGame} (플레이어 수: {playerCount})");
        }
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 준비 상태 변경 요청 이벤트 발생
    /// </summary>
    /// <param name="readyState">요청된 준비 상태</param>
    private void TriggerReadyStateChangeRequested(bool readyState)
    {
        OnReadyStateChangeRequested?.Invoke(readyState);
    }

    /// <summary>
    /// 방 나가기 요청 이벤트 발생
    /// </summary>
    private void TriggerLeaveRoomRequested()
    {
        OnLeaveRoomRequested?.Invoke();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 초기화 완료 여부 확인
    /// </summary>
    /// <returns>초기화가 완료되었으면 true</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 로컬 플레이어 정보 반환
    /// </summary>
    /// <returns>로컬 플레이어 정보</returns>
    public PlayerInfo GetLocalPlayer()
    {
        return localPlayer;
    }

    /// <summary>
    /// 현재 준비 상태 반환
    /// </summary>
    /// <returns>현재 준비 상태</returns>
    public bool GetCurrentReadyState()
    {
        return currentReadyState;
    }

    /// <summary>
    /// 하위 컨트롤러 가져오기
    /// </summary>
    /// <returns>색상 팔레트 컨트롤러</returns>
    public ColorPaletteController GetColorPaletteController()
    {
        return colorPaletteController;
    }

    /// <summary>
    /// 플레이어 목록 컨트롤러 가져오기
    /// </summary>
    /// <returns>플레이어 목록 컨트롤러</returns>
    public PlayerListUIController GetPlayerListController()
    {
        return playerListController;
    }

    /// <summary>
    /// 캐릭터 프리뷰 컨트롤러 가져오기
    /// </summary>
    /// <returns>캐릭터 프리뷰 컨트롤러</returns>
    public CharacterPreviewController GetCharacterPreviewController()
    {
        return characterPreviewController;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// UI 매니저 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log UI Manager Status")]
    public void LogUIManagerStatus()
    {
        Debug.Log($"=== WaitingRoomUIManager 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"로컬 플레이어: {(localPlayer != null ? localPlayer.playerName : "없음")}");
        Debug.Log($"현재 준비 상태: {currentReadyState}");
        Debug.Log($"사용 중인 색상: [{string.Join(", ", usedColorIndices)}]");
        Debug.Log($"ColorPaletteController: {(colorPaletteController != null ? "연결됨" : "없음")}");
        Debug.Log($"PlayerListController: {(playerListController != null ? "연결됨" : "없음")}");
        Debug.Log($"CharacterPreviewController: {(characterPreviewController != null ? "연결됨" : "없음")}");
    }

    #endregion
}