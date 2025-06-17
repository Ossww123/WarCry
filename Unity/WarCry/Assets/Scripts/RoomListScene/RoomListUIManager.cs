using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using RoomListData;

/// <summary>
/// 방 목록 씬의 UI 요소들을 관리하는 매니저
/// 방 목록 표시, 방 생성 패널, 비밀번호 입력 패널 등의 UI 상호작용을 처리하며
/// RoomService와 연동하여 사용자 인터페이스를 제공
/// </summary>
public class RoomListUIManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Room List UI")]
    [SerializeField] private GameObject roomItemPrefab;
    [SerializeField] private Transform contentTransform;
    [SerializeField] private Button refreshButton;
    [SerializeField] private RectTransform refreshIconTransform;

    [Header("Create Room Panel")]
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Toggle privateToggle;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button confirmCreateButton;
    [SerializeField] private Button cancelCreateButton;

    [Header("Password Prompt Panel")]
    [SerializeField] private GameObject passwordPromptPanel;
    [SerializeField] private TMP_InputField passwordPromptInput;
    [SerializeField] private Button confirmPasswordButton;
    [SerializeField] private Button cancelPasswordButton;

    [Header("Navigation")]
    [SerializeField] private Button backToMainMenuButton;

    [Header("Feedback UI")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text statusText;

    [Header("Animation Settings")]
    [SerializeField] private float refreshIconRotationDuration = 0.6f;
    [SerializeField] private float statusMessageDuration = 3f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string DefaultStatusMessage = "방 목록을 불러오는 중...";
    private const string RefreshCompleteMessage = "방 목록이 갱신되었습니다.";
    private const string EmptyRoomListMessage = "현재 생성된 방이 없습니다.";

    #endregion

    #region Private Fields

    // UI 상태 관리
    private int pendingMatchId = -1;
    private bool isRefreshIconRotating = false;
    private Coroutine statusMessageCoroutine = null;

    // UI 컴포넌트 캐시
    private List<GameObject> currentRoomItems = new List<GameObject>();

    #endregion

    #region Events

    /// <summary>
    /// 방 생성 요청 시 발생하는 이벤트 (제목, 비공개 여부, 비밀번호)
    /// </summary>
    public System.Action<string, bool, string> OnCreateRoomRequested;

    /// <summary>
    /// 방 입장 요청 시 발생하는 이벤트 (방 ID, 비밀번호)
    /// </summary>
    public System.Action<int, string> OnJoinRoomRequested;

    /// <summary>
    /// 방 목록 새로고침 요청 시 발생하는 이벤트
    /// </summary>
    public System.Action OnRefreshRequested;

    /// <summary>
    /// 메인 메뉴로 돌아가기 요청 시 발생하는 이벤트
    /// </summary>
    public System.Action OnBackToMainMenuRequested;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeUIComponents();
    }

    private void Start()
    {
        SetupInitialUIState();
        RegisterButtonEvents();
    }

    private void OnDestroy()
    {
        UnregisterButtonEvents();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// UI 컴포넌트 초기화 및 검증
    /// </summary>
    private void InitializeUIComponents()
    {
        ValidateRequiredComponents();
        InitializeInputFields();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RoomListUIManager 컴포넌트 초기화 완료");
    }

    /// <summary>
    /// 필수 컴포넌트 존재 여부 검증
    /// </summary>
    private void ValidateRequiredComponents()
    {
        if (roomItemPrefab == null)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] roomItemPrefab이 설정되지 않았습니다!");

        if (contentTransform == null)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] contentTransform이 설정되지 않았습니다!");

        if (createRoomPanel == null)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] createRoomPanel이 설정되지 않았습니다!");

        if (passwordPromptPanel == null)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] passwordPromptPanel이 설정되지 않았습니다!");
    }

    /// <summary>
    /// 입력 필드 초기 설정
    /// </summary>
    private void InitializeInputFields()
    {
        if (passwordInput != null)
        {
            passwordInput.interactable = false;
            passwordInput.text = "";
        }

        if (roomNameInput != null)
        {
            roomNameInput.text = "";
        }

        if (passwordPromptInput != null)
        {
            passwordPromptInput.text = "";
        }
    }

    /// <summary>
    /// UI 초기 상태 설정
    /// </summary>
    private void SetupInitialUIState()
    {
        SetCreateRoomPanelVisible(false);
        SetPasswordPromptVisible(false);
        SetLoadingIndicatorVisible(false);

        UpdateStatusMessage(DefaultStatusMessage);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] UI 초기 상태 설정 완료");
    }

    /// <summary>
    /// 버튼 이벤트 등록
    /// </summary>
    private void RegisterButtonEvents()
    {
        // 방 목록 관련
        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshButtonClicked);

        // 방 생성 관련
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);

        if (confirmCreateButton != null)
            confirmCreateButton.onClick.AddListener(OnConfirmCreateRoomClicked);

        if (cancelCreateButton != null)
            cancelCreateButton.onClick.AddListener(OnCancelCreateRoomClicked);

        if (privateToggle != null)
            privateToggle.onValueChanged.AddListener(OnPrivateToggleChanged);

        // 비밀번호 입력 관련
        if (confirmPasswordButton != null)
            confirmPasswordButton.onClick.AddListener(OnConfirmPasswordClicked);

        if (cancelPasswordButton != null)
            cancelPasswordButton.onClick.AddListener(OnCancelPasswordClicked);

        // 네비게이션
        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.AddListener(OnBackToMainMenuClicked);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 버튼 이벤트 등록 완료");
    }

    /// <summary>
    /// 버튼 이벤트 등록 해제
    /// </summary>
    private void UnregisterButtonEvents()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveAllListeners();

        if (createRoomButton != null)
            createRoomButton.onClick.RemoveAllListeners();

        if (confirmCreateButton != null)
            confirmCreateButton.onClick.RemoveAllListeners();

        if (cancelCreateButton != null)
            cancelCreateButton.onClick.RemoveAllListeners();

        if (privateToggle != null)
            privateToggle.onValueChanged.RemoveAllListeners();

        if (confirmPasswordButton != null)
            confirmPasswordButton.onClick.RemoveAllListeners();

        if (cancelPasswordButton != null)
            cancelPasswordButton.onClick.RemoveAllListeners();

        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.RemoveAllListeners();
    }

    #endregion

    #region Public API - Room List Management

    /// <summary>
    /// 방 목록 UI 업데이트
    /// </summary>
    /// <param name="matchList">방 목록 데이터</param>
    public void UpdateRoomList(MatchListApiResponse matchList)
    {
        if (matchList == null || matchList.matches == null)
        {
            ShowEmptyRoomList();
            return;
        }

        ClearCurrentRoomList();

        if (matchList.matches.Count == 0)
        {
            ShowEmptyRoomList();
            return;
        }

        CreateRoomListItems(matchList.matches);
        UpdateStatusMessage($"{matchList.matches.Count}개의 방을 찾았습니다.");

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 UI 업데이트 완료 - {matchList.matches.Count}개 방");
    }

    /// <summary>
    /// 방 목록 새로고침 시작
    /// </summary>
    public void StartRefreshAnimation()
    {
        SetLoadingIndicatorVisible(true);
        UpdateStatusMessage("방 목록을 새로고침하는 중...");

        if (!isRefreshIconRotating)
        {
            StartCoroutine(RotateRefreshIcon());
        }
    }

    /// <summary>
    /// 방 목록 새로고침 완료
    /// </summary>
    public void CompleteRefreshAnimation()
    {
        SetLoadingIndicatorVisible(false);
        ShowTemporaryStatusMessage(RefreshCompleteMessage);
    }

    #endregion

    #region Public API - Panel Management

    /// <summary>
    /// 방 생성 패널 표시/숨김
    /// </summary>
    /// <param name="visible">표시 여부</param>
    public void SetCreateRoomPanelVisible(bool visible)
    {
        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(visible);

            if (visible)
            {
                ResetCreateRoomPanel();
            }
        }
    }

    /// <summary>
    /// 비밀번호 입력 패널 표시/숨김
    /// </summary>
    /// <param name="visible">표시 여부</param>
    public void SetPasswordPromptVisible(bool visible)
    {
        if (passwordPromptPanel != null)
        {
            passwordPromptPanel.SetActive(visible);

            if (visible && passwordPromptInput != null)
            {
                passwordPromptInput.text = "";
                passwordPromptInput.Select();
            }
        }
    }

    /// <summary>
    /// 비밀번호 입력 프롬프트 표시
    /// </summary>
    /// <param name="matchId">입장할 방 ID</param>
    public void ShowPasswordPrompt(int matchId)
    {
        pendingMatchId = matchId;
        SetPasswordPromptVisible(true);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 비밀번호 입력 프롬프트 표시 - 방 ID: {matchId}");
    }

    #endregion

    #region Public API - Feedback

    /// <summary>
    /// 로딩 인디케이터 표시/숨김
    /// </summary>
    /// <param name="visible">표시 여부</param>
    public void SetLoadingIndicatorVisible(bool visible)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(visible);
        }
    }

    /// <summary>
    /// 상태 메시지 업데이트
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    public void UpdateStatusMessage(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 상태 메시지 업데이트: {message}");
    }

    /// <summary>
    /// 임시 상태 메시지 표시 (자동으로 사라짐)
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    public void ShowTemporaryStatusMessage(string message)
    {
        UpdateStatusMessage(message);

        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
        }

        statusMessageCoroutine = StartCoroutine(ClearStatusMessageAfterDelay());
    }

    /// <summary>
    /// 에러 메시지 표시
    /// </summary>
    /// <param name="errorMessage">에러 메시지</param>
    public void ShowErrorMessage(string errorMessage)
    {
        UpdateStatusMessage($"오류: {errorMessage}");
        SetLoadingIndicatorVisible(false);

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 에러 메시지 표시: {errorMessage}");
    }

    #endregion

    #region Button Event Handlers

    /// <summary>
    /// 새로고침 버튼 클릭 처리
    /// </summary>
    private void OnRefreshButtonClicked()
    {
        OnRefreshRequested?.Invoke();
    }

    /// <summary>
    /// 방 생성 버튼 클릭 처리
    /// </summary>
    private void OnCreateRoomButtonClicked()
    {
        SetCreateRoomPanelVisible(true);
    }

    /// <summary>
    /// 방 생성 확인 버튼 클릭 처리
    /// </summary>
    private void OnConfirmCreateRoomClicked()
    {
        if (!ValidateCreateRoomInput())
            return;

        string title = roomNameInput.text.Trim();
        bool isPrivate = privateToggle.isOn;
        string password = isPrivate ? passwordInput.text.Trim() : null;

        OnCreateRoomRequested?.Invoke(title, isPrivate, password);
        SetCreateRoomPanelVisible(false);
    }

    /// <summary>
    /// 방 생성 취소 버튼 클릭 처리
    /// </summary>
    private void OnCancelCreateRoomClicked()
    {
        SetCreateRoomPanelVisible(false);
    }

    /// <summary>
    /// 비공개 방 토글 변경 처리
    /// </summary>
    /// <param name="isPrivate">비공개 방 여부</param>
    private void OnPrivateToggleChanged(bool isPrivate)
    {
        if (passwordInput != null)
        {
            passwordInput.interactable = isPrivate;

            if (!isPrivate)
            {
                passwordInput.text = "";
            }
        }
    }

    /// <summary>
    /// 비밀번호 확인 버튼 클릭 처리
    /// </summary>
    private void OnConfirmPasswordClicked()
    {
        if (pendingMatchId <= 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 유효하지 않은 방 ID입니다");
            return;
        }

        string password = passwordPromptInput.text.Trim();
        OnJoinRoomRequested?.Invoke(pendingMatchId, password);
        SetPasswordPromptVisible(false);
        pendingMatchId = -1;
    }

    /// <summary>
    /// 비밀번호 취소 버튼 클릭 처리
    /// </summary>
    private void OnCancelPasswordClicked()
    {
        SetPasswordPromptVisible(false);
        pendingMatchId = -1;
    }

    /// <summary>
    /// 메인 메뉴로 돌아가기 버튼 클릭 처리
    /// </summary>
    private void OnBackToMainMenuClicked()
    {
        OnBackToMainMenuRequested?.Invoke();
    }

    #endregion

    #region Room List UI Management

    /// <summary>
    /// 현재 방 목록 UI 제거
    /// </summary>
    private void ClearCurrentRoomList()
    {
        foreach (GameObject item in currentRoomItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }

        currentRoomItems.Clear();

        // Transform의 모든 자식도 제거 (안전장치)
        if (contentTransform != null)
        {
            foreach (Transform child in contentTransform)
            {
                Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// 방 목록 아이템 생성
    /// </summary>
    /// <param name="matches">방 목록 데이터</param>
    private void CreateRoomListItems(List<MatchApiData> matches)
    {
        foreach (MatchApiData match in matches)
        {
            // 종료된 방은 목록에서 제외
            if (match.status == MatchStatus.Ended)
                continue;

            GameObject roomItem = CreateSingleRoomItem(match);
            currentRoomItems.Add(roomItem);
        }
    }

    /// <summary>
    /// 개별 방 아이템 생성
    /// </summary>
    /// <param name="match">방 데이터</param>
    /// <returns>생성된 방 아이템 GameObject</returns>
    private GameObject CreateSingleRoomItem(MatchApiData match)
    {
        GameObject item = Instantiate(roomItemPrefab, contentTransform);

        SetupRoomItemDisplay(item, match);
        SetupRoomItemInteraction(item, match);

        return item;
    }

    /// <summary>
    /// 방 아이템 표시 정보 설정
    /// </summary>
    /// <param name="item">방 아이템 GameObject</param>
    /// <param name="match">방 데이터</param>
    private void SetupRoomItemDisplay(GameObject item, MatchApiData match)
    {
        // 방 제목과 호스트 정보
        Transform roomNameText = item.transform.Find("RoomNameText");
        if (roomNameText != null)
        {
            TMP_Text textComponent = roomNameText.GetComponent<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = $"{match.title} (호스트: {match.hostNickname})";
            }
        }

        // 방 상태 정보
        Transform playerCountText = item.transform.Find("PlayerCountText");
        if (playerCountText != null)
        {
            TMP_Text textComponent = playerCountText.GetComponent<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = GetStatusDisplayText(match.status);
            }
        }

        // 비밀번호 아이콘
        Transform lockIcon = item.transform.Find("LockIcon");
        if (lockIcon != null)
        {
            lockIcon.gameObject.SetActive(match.isPrivate);
        }
    }

    /// <summary>
    /// 방 아이템 상호작용 설정
    /// </summary>
    /// <param name="item">방 아이템 GameObject</param>
    /// <param name="match">방 데이터</param>
    private void SetupRoomItemInteraction(GameObject item, MatchApiData match)
    {
        Transform joinButtonTransform = item.transform.Find("JoinButton");
        if (joinButtonTransform != null)
        {
            Button joinButton = joinButtonTransform.GetComponent<Button>();
            if (joinButton != null)
            {
                // 입장 가능한 방만 버튼 활성화
                bool canJoin = CanJoinRoom(match.status);
                joinButton.interactable = canJoin;

                if (canJoin)
                {
                    SetupJoinButtonClick(joinButton, match);
                }
            }
        }
    }

    /// <summary>
    /// 입장 버튼 클릭 이벤트 설정
    /// </summary>
    /// <param name="joinButton">입장 버튼</param>
    /// <param name="match">방 데이터</param>
    private void SetupJoinButtonClick(Button joinButton, MatchApiData match)
    {
        joinButton.onClick.RemoveAllListeners();

        if (match.isPrivate)
        {
            // 비공개 방: 비밀번호 입력 프롬프트 표시
            joinButton.onClick.AddListener(() => ShowPasswordPrompt(match.matchId));
        }
        else
        {
            // 공개 방: 바로 입장
            joinButton.onClick.AddListener(() => OnJoinRoomRequested?.Invoke(match.matchId, null));
        }
    }

    /// <summary>
    /// 빈 방 목록 표시
    /// </summary>
    private void ShowEmptyRoomList()
    {
        ClearCurrentRoomList();
        UpdateStatusMessage(EmptyRoomListMessage);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 빈 방 목록 표시");
    }

    #endregion

    #region Panel Management

    /// <summary>
    /// 방 생성 패널 초기화
    /// </summary>
    private void ResetCreateRoomPanel()
    {
        if (roomNameInput != null)
        {
            roomNameInput.text = "";
            roomNameInput.Select();
        }

        if (privateToggle != null)
        {
            privateToggle.isOn = false;
        }

        if (passwordInput != null)
        {
            passwordInput.text = "";
            passwordInput.interactable = false;
        }
    }

    #endregion

    #region Animation and Effects

    /// <summary>
    /// 새로고침 아이콘 회전 애니메이션
    /// </summary>
    private IEnumerator RotateRefreshIcon()
    {
        if (refreshIconTransform == null)
            yield break;

        isRefreshIconRotating = true;

        float elapsed = 0f;
        float startRotation = refreshIconTransform.eulerAngles.z;
        float endRotation = startRotation - 360f;

        while (elapsed < refreshIconRotationDuration)
        {
            float t = elapsed / refreshIconRotationDuration;
            float currentRotation = Mathf.Lerp(startRotation, endRotation, t);
            refreshIconTransform.rotation = Quaternion.Euler(0f, 0f, currentRotation);

            elapsed += Time.deltaTime;
            yield return null;
        }

        refreshIconTransform.rotation = Quaternion.Euler(0f, 0f, endRotation % 360f);
        isRefreshIconRotating = false;
    }

    /// <summary>
    /// 상태 메시지 자동 지우기
    /// </summary>
    private IEnumerator ClearStatusMessageAfterDelay()
    {
        yield return new WaitForSeconds(statusMessageDuration);
        UpdateStatusMessage("");
        statusMessageCoroutine = null;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 방 생성 입력값 검증
    /// </summary>
    /// <returns>유효한 입력값이면 true</returns>
    private bool ValidateCreateRoomInput()
    {
        if (roomNameInput == null || string.IsNullOrEmpty(roomNameInput.text.Trim()))
        {
            ShowErrorMessage("방 제목을 입력해주세요.");
            return false;
        }

        if (privateToggle != null && privateToggle.isOn)
        {
            if (passwordInput == null || string.IsNullOrEmpty(passwordInput.text.Trim()))
            {
                ShowErrorMessage("비공개 방은 비밀번호를 설정해야 합니다.");
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 방 상태를 표시용 텍스트로 변환
    /// </summary>
    /// <param name="status">방 상태</param>
    /// <returns>표시용 텍스트</returns>
    private string GetStatusDisplayText(string status)
    {
        return RoomListData.RoomUtils.GetStatusDisplayText(status);
    }

    /// <summary>
    /// 방 입장 가능 여부 확인
    /// </summary>
    /// <param name="status">방 상태</param>
    /// <returns>입장 가능하면 true</returns>
    private bool CanJoinRoom(string status)
    {
        return RoomListData.RoomUtils.CanJoinRoom(status);
    }

    #endregion
}