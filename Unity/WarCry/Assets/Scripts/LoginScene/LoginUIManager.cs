using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 로그인 씬의 UI 관리를 전담하는 매니저
/// 패널 전환, 입력 필드 관리, 버튼 이벤트 처리, 메시지 표시 등 순수 UI 기능만 담당하며
/// 실제 인증 처리는 LoginSceneController에 위임하는 구조로 설계
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signupPanel;

    [Header("Login Inputs")]
    [SerializeField] private TMP_InputField loginUsernameInput;
    [SerializeField] private TMP_InputField loginPasswordInput;

    [Header("Signup Inputs")]
    [SerializeField] private TMP_InputField signupUsernameInput;
    [SerializeField] private TMP_InputField signupPasswordInput;
    [SerializeField] private TMP_InputField signupPasswordConfirmInput;
    [SerializeField] private TMP_InputField signupNicknameInput;

    [Header("UI Buttons")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button toSignupButton;
    [SerializeField] private Button signupButton;
    [SerializeField] private Button backToLoginButton;
    [SerializeField] private Button checkUsernameButton;
    [SerializeField] private Button checkNicknameButton;
    [SerializeField] private Button devSkipButton;
    [SerializeField] private Button exitButton;

    [Header("Message")]
    [SerializeField] private TMP_Text messageText;

    [Header("UI Settings")]
    [SerializeField] private float messageDisplayDuration = 3f;
    [SerializeField] private float signupSuccessDelay = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    // 메시지 색상
    private static readonly Color SuccessColor = Color.green;
    private static readonly Color ErrorColor = Color.red;
    private static readonly Color InfoColor = Color.white;

    // UI 상태
    private const string UsernameInputType = "username";
    private const string PasswordInputType = "password";
    private const string NicknameInputType = "nickname";

    #endregion

    #region Private Fields

    // 컨트롤러 참조
    private LoginSceneController sceneController;

    // UI 상태
    private bool isProcessingRequest = false;
    private Coroutine messageCoroutine;
    private Coroutine signupSuccessCoroutine;

    // 입력 검증 플래그
    private bool isValidatingInput = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeLoginUI();
    }

    private void Update()
    {
        HandleKeyboardInput();
    }

    private void OnDestroy()
    {
        CleanupLoginUI();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 LoginUIManager 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 로그인 UI 초기화
    /// </summary>
    private void InitializeLoginUI()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] LoginUIManager 초기화 시작");

        FindSceneController();
        ValidateUIComponents();
        SetupInitialPanelState();
        RegisterButtonEvents();
        RegisterInputEvents();
        SetupInputNavigation();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] LoginUIManager 초기화 완료");
    }

    /// <summary>
    /// 씬 컨트롤러 참조 찾기
    /// </summary>
    private void FindSceneController()
    {
        sceneController = FindObjectOfType<LoginSceneController>();

        if (sceneController == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] LoginSceneController를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// UI 컴포넌트 유효성 검증
    /// </summary>
    private void ValidateUIComponents()
    {
        if (loginPanel == null || signupPanel == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 필수 패널이 설정되지 않았습니다!");
            return;
        }

        if (messageText == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 메시지 텍스트가 설정되지 않았습니다!");
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] UI 컴포넌트 검증 완료");
    }

    /// <summary>
    /// 초기 패널 상태 설정
    /// </summary>
    private void SetupInitialPanelState()
    {
        ShowLoginPanel();
        ClearMessage();
    }

    /// <summary>
    /// 로그인 UI 정리
    /// </summary>
    private void CleanupLoginUI()
    {
        StopAllCoroutines();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] LoginUIManager 정리 완료");
    }

    #endregion

    #region Button Events Registration

    /// <summary>
    /// 버튼 이벤트 등록
    /// </summary>
    private void RegisterButtonEvents()
    {
        RegisterPanelSwitchEvents();
        RegisterAuthenticationEvents();
        RegisterDuplicateCheckEvents();
        RegisterUtilityEvents();
    }

    /// <summary>
    /// 패널 전환 버튼 이벤트 등록
    /// </summary>
    private void RegisterPanelSwitchEvents()
    {
        if (toSignupButton != null)
        {
            toSignupButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 화면으로 전환");

                ShowSignupPanel();
            });
        }

        if (backToLoginButton != null)
        {
            backToLoginButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 화면으로 돌아가기");

                ShowLoginPanel();
            });
        }
    }

    /// <summary>
    /// 인증 관련 버튼 이벤트 등록
    /// </summary>
    private void RegisterAuthenticationEvents()
    {
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 버튼 클릭");

                ProcessLogin();
            });
        }

        if (signupButton != null)
        {
            signupButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 버튼 클릭");

                ProcessSignup();
            });
        }
    }

    /// <summary>
    /// 중복 확인 버튼 이벤트 등록
    /// </summary>
    private void RegisterDuplicateCheckEvents()
    {
        if (checkUsernameButton != null)
        {
            checkUsernameButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 아이디 중복 확인 버튼 클릭");

                ProcessDuplicateCheck(UsernameInputType, signupUsernameInput?.text);
            });
        }

        if (checkNicknameButton != null)
        {
            checkNicknameButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 닉네임 중복 확인 버튼 클릭");

                ProcessDuplicateCheck(NicknameInputType, signupNicknameInput?.text);
            });
        }
    }

    /// <summary>
    /// 유틸리티 버튼 이벤트 등록
    /// </summary>
    private void RegisterUtilityEvents()
    {
        if (devSkipButton != null)
        {
            devSkipButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 개발자 모드: 로그인 건너뛰기");

                ProcessDevSkip();
            });
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(() => {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 나가기 버튼 클릭");

                ProcessExit();
            });
        }
    }

    #endregion

    #region Input Events Registration

    /// <summary>
    /// 입력 필드 이벤트 등록
    /// </summary>
    private void RegisterInputEvents()
    {
        RegisterInputValidationEvents();
    }

    /// <summary>
    /// 입력 검증 이벤트 등록
    /// </summary>
    private void RegisterInputValidationEvents()
    {
        // 로그인 필드
        if (loginUsernameInput != null)
            loginUsernameInput.onValueChanged.AddListener((value) => ValidateAndFilterInput(value, UsernameInputType, loginUsernameInput));

        // 회원가입 필드
        if (signupUsernameInput != null)
            signupUsernameInput.onValueChanged.AddListener((value) => ValidateAndFilterInput(value, UsernameInputType, signupUsernameInput));

        if (signupPasswordInput != null)
            signupPasswordInput.onValueChanged.AddListener((value) => ValidateAndFilterInput(value, PasswordInputType, signupPasswordInput));

        if (signupPasswordConfirmInput != null)
            signupPasswordConfirmInput.onValueChanged.AddListener((value) => ValidateAndFilterInput(value, PasswordInputType, signupPasswordConfirmInput));

        if (signupNicknameInput != null)
            signupNicknameInput.onValueChanged.AddListener((value) => ValidateAndFilterInput(value, NicknameInputType, signupNicknameInput));
    }

    #endregion

    #region Input Navigation Setup

    /// <summary>
    /// 탭 키 네비게이션 설정
    /// </summary>
    private void SetupInputNavigation()
    {
        SetupLoginPanelNavigation();
        SetupSignupPanelNavigation();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 입력 네비게이션 설정 완료");
    }

    /// <summary>
    /// 로그인 패널 네비게이션 설정
    /// </summary>
    private void SetupLoginPanelNavigation()
    {
        SetupInputFieldNavigation(loginUsernameInput, loginPasswordInput);
        SetupInputFieldNavigation(loginPasswordInput, loginUsernameInput);
    }

    /// <summary>
    /// 회원가입 패널 네비게이션 설정
    /// </summary>
    private void SetupSignupPanelNavigation()
    {
        SetupInputFieldNavigation(signupUsernameInput, signupPasswordInput);
        SetupInputFieldNavigation(signupPasswordInput, signupPasswordConfirmInput);
        SetupInputFieldNavigation(signupPasswordConfirmInput, signupNicknameInput);
        SetupInputFieldNavigation(signupNicknameInput, signupUsernameInput);
    }

    /// <summary>
    /// 개별 입력 필드 네비게이션 설정
    /// </summary>
    /// <param name="current">현재 필드</param>
    /// <param name="next">다음 필드</param>
    private void SetupInputFieldNavigation(TMP_InputField current, TMP_InputField next)
    {
        if (current == null || next == null) return;

        Navigation nav = current.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnDown = next;
        current.navigation = nav;
    }

    #endregion

    #region Panel Management

    /// <summary>
    /// 로그인 패널 표시
    /// </summary>
    private void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (signupPanel != null) signupPanel.SetActive(false);

        ClearMessage();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 패널 표시");
    }

    /// <summary>
    /// 회원가입 패널 표시
    /// </summary>
    private void ShowSignupPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (signupPanel != null) signupPanel.SetActive(true);

        ClearMessage();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 패널 표시");
    }

    #endregion

    #region Authentication Processing

    /// <summary>
    /// 로그인 처리
    /// </summary>
    private void ProcessLogin()
    {
        if (isProcessingRequest)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 이미 요청을 처리 중입니다");
            return;
        }

        string username = loginUsernameInput?.text ?? "";
        string password = loginPasswordInput?.text ?? "";

        if (sceneController != null)
        {
            SetProcessingState(true);
            sceneController.RequestLogin(username, password);
        }
        else
        {
            ShowMessage("시스템 오류가 발생했습니다.", ErrorColor);
        }
    }

    /// <summary>
    /// 회원가입 처리
    /// </summary>
    private void ProcessSignup()
    {
        if (isProcessingRequest)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 이미 요청을 처리 중입니다");
            return;
        }

        string username = signupUsernameInput?.text ?? "";
        string password = signupPasswordInput?.text ?? "";
        string confirmPassword = signupPasswordConfirmInput?.text ?? "";
        string nickname = signupNicknameInput?.text ?? "";

        if (sceneController != null)
        {
            SetProcessingState(true);
            sceneController.RequestSignup(username, password, confirmPassword, nickname);
        }
        else
        {
            ShowMessage("시스템 오류가 발생했습니다.", ErrorColor);
        }
    }

    /// <summary>
    /// 중복 확인 처리
    /// </summary>
    /// <param name="type">확인 타입</param>
    /// <param name="value">확인할 값</param>
    private void ProcessDuplicateCheck(string type, string value)
    {
        if (sceneController != null)
        {
            sceneController.RequestDuplicateCheck(type, value);
        }
        else
        {
            ShowMessage("시스템 오류가 발생했습니다.", ErrorColor);
        }
    }

    /// <summary>
    /// 개발자 모드 건너뛰기 처리
    /// </summary>
    private void ProcessDevSkip()
    {
        if (sceneController != null)
        {
            sceneController.SkipLoginForDevelopment();
        }
    }

    /// <summary>
    /// 애플리케이션 종료 처리
    /// </summary>
    private void ProcessExit()
    {
        if (sceneController != null)
        {
            sceneController.QuitApplication();
        }
    }

    #endregion

    #region Input Validation and Filtering

    /// <summary>
    /// 입력값 검증 및 필터링 처리
    /// </summary>
    /// <param name="input">입력값</param>
    /// <param name="inputType">입력 타입</param>
    /// <param name="inputField">입력 필드</param>
    private void ValidateAndFilterInput(string input, string inputType, TMP_InputField inputField)
    {
        if (isValidatingInput || inputField == null) return;

        isValidatingInput = true;

        var filterResult = InputValidator.FilterInputByType(input, inputType);

        if (filterResult.WasChanged)
        {
            inputField.text = filterResult.FilteredValue;

            // 커서를 끝으로 이동
            inputField.caretPosition = filterResult.FilteredValue.Length;
        }

        isValidatingInput = false;
    }

    #endregion

    #region Keyboard Input Handling

    /// <summary>
    /// 키보드 입력 처리
    /// </summary>
    private void HandleKeyboardInput()
    {
        HandleTabNavigation();
        HandleEnterKey();
    }

    /// <summary>
    /// 탭 키 네비게이션 처리
    /// </summary>
    private void HandleTabNavigation()
    {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;

        GameObject currentObject = EventSystem.current.currentSelectedGameObject;
        if (currentObject == null) return;

        if (loginPanel.activeInHierarchy)
        {
            HandleLoginPanelTabNavigation(currentObject);
        }
        else if (signupPanel.activeInHierarchy)
        {
            HandleSignupPanelTabNavigation(currentObject);
        }
    }

    /// <summary>
    /// 로그인 패널 탭 네비게이션 처리
    /// </summary>
    /// <param name="currentObject">현재 선택된 오브젝트</param>
    private void HandleLoginPanelTabNavigation(GameObject currentObject)
    {
        if (currentObject == loginUsernameInput?.gameObject)
        {
            SelectInputField(loginPasswordInput);
        }
        else if (currentObject == loginPasswordInput?.gameObject)
        {
            SelectInputField(loginUsernameInput);
        }
    }

    /// <summary>
    /// 회원가입 패널 탭 네비게이션 처리
    /// </summary>
    /// <param name="currentObject">현재 선택된 오브젝트</param>
    private void HandleSignupPanelTabNavigation(GameObject currentObject)
    {
        if (currentObject == signupUsernameInput?.gameObject)
        {
            SelectInputField(signupPasswordInput);
        }
        else if (currentObject == signupPasswordInput?.gameObject)
        {
            SelectInputField(signupPasswordConfirmInput);
        }
        else if (currentObject == signupPasswordConfirmInput?.gameObject)
        {
            SelectInputField(signupNicknameInput);
        }
        else if (currentObject == signupNicknameInput?.gameObject)
        {
            SelectInputField(signupUsernameInput);
        }
    }

    /// <summary>
    /// 입력 필드 선택 및 커서 위치 설정
    /// </summary>
    /// <param name="inputField">선택할 입력 필드</param>
    private void SelectInputField(TMP_InputField inputField)
    {
        if (inputField == null) return;

        inputField.Select();
        inputField.caretPosition = inputField.text.Length;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 입력 필드 선택: {inputField.name}");
    }

    /// <summary>
    /// 엔터 키 처리
    /// </summary>
    private void HandleEnterKey()
    {
        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;

        if (loginPanel.activeInHierarchy)
        {
            ProcessLogin();
        }
        else if (signupPanel.activeInHierarchy)
        {
            HandleSignupPanelEnterKey();
        }
    }

    /// <summary>
    /// 회원가입 패널에서 엔터 키 처리
    /// </summary>
    private void HandleSignupPanelEnterKey()
    {
        GameObject currentObject = EventSystem.current.currentSelectedGameObject;

        if (currentObject != null)
        {
            Button selectedButton = currentObject.GetComponent<Button>();
            if (selectedButton != null && selectedButton.interactable)
            {
                selectedButton.onClick.Invoke();
                return;
            }

            if (currentObject == signupNicknameInput?.gameObject)
            {
                ProcessSignup();
            }
        }
    }

    #endregion

    #region Public API - Result Handling

    /// <summary>
    /// 로그인 결과 처리 (LoginSceneController에서 호출)
    /// </summary>
    /// <param name="success">로그인 성공 여부</param>
    /// <param name="message">결과 메시지</param>
    public void OnLoginResult(bool success, string message)
    {
        SetProcessingState(false);

        Color messageColor = success ? SuccessColor : ErrorColor;
        ShowMessage(message, messageColor);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 결과 - 성공: {success}, 메시지: {message}");
    }

    /// <summary>
    /// 회원가입 결과 처리 (LoginSceneController에서 호출)
    /// </summary>
    /// <param name="success">회원가입 성공 여부</param>
    /// <param name="message">결과 메시지</param>
    public void OnSignupResult(bool success, string message)
    {
        SetProcessingState(false);

        Color messageColor = success ? SuccessColor : ErrorColor;
        ShowMessage(message, messageColor);

        if (success)
        {
            StartSignupSuccessTransition();
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 결과 - 성공: {success}, 메시지: {message}");
    }

    /// <summary>
    /// 중복 확인 결과 처리 (AuthService 이벤트에서 호출 가능)
    /// </summary>
    /// <param name="type">확인 타입</param>
    /// <param name="available">사용 가능 여부</param>
    /// <param name="message">결과 메시지</param>
    public void OnDuplicateCheckResult(string type, bool available, string message)
    {
        Color messageColor = available ? SuccessColor : ErrorColor;
        ShowMessage(message, messageColor);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 중복 확인 결과 - 타입: {type}, 사용가능: {available}");
    }

    #endregion

    #region Message Display

    /// <summary>
    /// 메시지 표시
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="color">메시지 색상</param>
    private void ShowMessage(string message, Color color)
    {
        if (messageText == null) return;

        // 기존 메시지 코루틴 중지
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }

        messageText.text = message;
        messageText.color = color;

        // 일정 시간 후 메시지 자동 제거
        messageCoroutine = StartCoroutine(ClearMessageAfterDelay());

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 메시지 표시: {message}");
    }

    /// <summary>
    /// 메시지 초기화
    /// </summary>
    private void ClearMessage()
    {
        if (messageText != null)
        {
            messageText.text = "";
            messageText.color = InfoColor;
        }

        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
            messageCoroutine = null;
        }
    }

    /// <summary>
    /// 지연 후 메시지 자동 제거 코루틴
    /// </summary>
    private IEnumerator ClearMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayDuration);
        ClearMessage();
    }

    #endregion

    #region UI State Management

    /// <summary>
    /// 요청 처리 상태 설정
    /// </summary>
    /// <param name="processing">처리 중 여부</param>
    private void SetProcessingState(bool processing)
    {
        isProcessingRequest = processing;

        // 버튼 상태 업데이트
        SetButtonInteractable(loginButton, !processing);
        SetButtonInteractable(signupButton, !processing);
    }

    /// <summary>
    /// 버튼 상호작용 가능 상태 설정
    /// </summary>
    /// <param name="button">대상 버튼</param>
    /// <param name="interactable">상호작용 가능 여부</param>
    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    /// <summary>
    /// 회원가입 성공 후 로그인 패널로 전환
    /// </summary>
    private void StartSignupSuccessTransition()
    {
        if (signupSuccessCoroutine != null)
        {
            StopCoroutine(signupSuccessCoroutine);
        }

        signupSuccessCoroutine = StartCoroutine(SignupSuccessTransitionCoroutine());
    }

    /// <summary>
    /// 회원가입 성공 전환 코루틴
    /// </summary>
    private IEnumerator SignupSuccessTransitionCoroutine()
    {
        yield return new WaitForSeconds(signupSuccessDelay);
        ShowLoginPanel();
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 현재 활성 패널 확인
    /// </summary>
    /// <returns>현재 활성 패널 이름</returns>
    public string GetActivePanelName()
    {
        if (loginPanel != null && loginPanel.activeInHierarchy)
            return "Login";
        else if (signupPanel != null && signupPanel.activeInHierarchy)
            return "Signup";
        else
            return "None";
    }

    /// <summary>
    /// 요청 처리 중 여부 확인
    /// </summary>
    /// <returns>처리 중이면 true</returns>
    public bool IsProcessingRequest()
    {
        return isProcessingRequest;
    }

    #endregion
}