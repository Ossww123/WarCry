using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 로그인 씬의 전체 흐름과 컴포넌트 간 조정을 담당하는 메인 컨트롤러
/// AuthService, InputValidator, LoginUIManager 간의 상호작용을 관리하며
/// 씬 초기화, 헤드리스 모드 처리, 씬 전환 등의 핵심 기능을 제공
/// </summary>
public class LoginSceneController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Scene References")]
    [SerializeField] private LoginUIManager loginUIManager;
    [SerializeField] private GameObject authServicePrefab;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "MainMenuScene";
    [SerializeField] private float sceneTransitionDelay = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string AuthServiceObjectName = "AuthService";
    private const string AuthManagerObjectName = "AuthManager";

    #endregion

    #region Private Fields

    // 컴포넌트 참조
    private AuthService authService;
    private bool isSceneTransitioning = false;

    // 씬 상태
    private bool isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeLoginScene();
    }

    private void OnDestroy()
    {
        CleanupLoginScene();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 LoginScene 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 로그인 씬 초기화
    /// </summary>
    private void InitializeLoginScene()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] LoginScene 초기화 시작");

        InitializeRequiredServices();
        ValidateSceneComponents();
        RegisterEventHandlers();
        FinalizeInitialization();
    }

    /// <summary>
    /// 필수 서비스 초기화
    /// </summary>
    private void InitializeRequiredServices()
    {
        EnsureAuthManagerExists();
        EnsureAuthServiceExists();
    }

    /// <summary>
    /// AuthManager 존재 확인 및 생성
    /// </summary>
    private void EnsureAuthManagerExists()
    {
        if (AuthManager.Instance == null)
        {
            GameObject authManagerObj = GameObject.Find(AuthManagerObjectName);
            if (authManagerObj == null)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] AuthManager가 씬에 없습니다. 자동 생성을 시도합니다.");

                CreateAuthManager();
            }
        }
    }

    /// <summary>
    /// AuthService 존재 확인 및 생성
    /// </summary>
    private void EnsureAuthServiceExists()
    {
        authService = AuthService.Instance;

        if (authService == null)
        {
            GameObject authServiceObj = GameObject.Find(AuthServiceObjectName);
            if (authServiceObj == null && authServicePrefab != null)
            {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] AuthService 프리팹에서 생성");

                Instantiate(authServicePrefab);
                authService = AuthService.Instance;
            }
        }

        if (authService == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] AuthService를 찾거나 생성할 수 없습니다!");
        }
    }

    /// <summary>
    /// AuthManager 오브젝트 생성
    /// </summary>
    private void CreateAuthManager()
    {
        GameObject authManagerObj = new GameObject(AuthManagerObjectName);
        authManagerObj.AddComponent<AuthManager>();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] AuthManager 오브젝트가 생성되었습니다");
    }

    /// <summary>
    /// 씬 컴포넌트 유효성 검증
    /// </summary>
    private void ValidateSceneComponents()
    {
        if (loginUIManager == null)
        {
            loginUIManager = FindObjectOfType<LoginUIManager>();

            if (loginUIManager == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] LoginUIManager를 찾을 수 없습니다!");
                return;
            }
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 컴포넌트 검증 완료");
    }

    /// <summary>
    /// 이벤트 핸들러 등록
    /// </summary>
    private void RegisterEventHandlers()
    {
        // AuthService 이벤트 구독
        AuthService.OnLoginCompleted += HandleLoginCompleted;
        AuthService.OnSignupCompleted += HandleSignupCompleted;

        // AuthManager 이벤트 구독 (옵션)
        AuthManager.OnLoginSuccess += HandleAuthManagerLoginSuccess;

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
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] LoginScene 초기화 완료");
    }

    /// <summary>
    /// 씬 정리 작업
    /// </summary>
    private void CleanupLoginScene()
    {
        UnregisterEventHandlers();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] LoginScene 정리 완료");
    }

    /// <summary>
    /// 이벤트 핸들러 등록 해제
    /// </summary>
    private void UnregisterEventHandlers()
    {
        AuthService.OnLoginCompleted -= HandleLoginCompleted;
        AuthService.OnSignupCompleted -= HandleSignupCompleted;
        AuthManager.OnLoginSuccess -= HandleAuthManagerLoginSuccess;
    }

    #endregion

    #region Event Handlers - AuthService

    /// <summary>
    /// AuthService 로그인 완료 이벤트 처리
    /// </summary>
    /// <param name="success">로그인 성공 여부</param>
    /// <param name="message">결과 메시지</param>
    private void HandleLoginCompleted(bool success, string message)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 완료 - 성공: {success}, 메시지: {message}");

        if (loginUIManager != null)
        {
            loginUIManager.OnLoginResult(success, message);
        }

        if (success)
        {
            StartSceneTransition();
        }
    }

    /// <summary>
    /// AuthService 회원가입 완료 이벤트 처리
    /// </summary>
    /// <param name="success">회원가입 성공 여부</param>
    /// <param name="message">결과 메시지</param>
    private void HandleSignupCompleted(bool success, string message)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 완료 - 성공: {success}, 메시지: {message}");

        if (loginUIManager != null)
        {
            loginUIManager.OnSignupResult(success, message);
        }
    }

    #endregion

    #region Event Handlers - AuthManager

    /// <summary>
    /// AuthManager 로그인 성공 이벤트 처리
    /// </summary>
    /// <param name="token">액세스 토큰</param>
    /// <param name="userId">사용자 ID</param>
    /// <param name="nickname">사용자 닉네임</param>
    private void HandleAuthManagerLoginSuccess(string token, string userId, string nickname)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] AuthManager 인증 완료 - 사용자: {userId}");

        // 추가적인 로그인 성공 처리가 필요한 경우 여기에 구현
    }

    #endregion

    #region Public API - Scene Management

    /// <summary>
    /// 씬 전환 시작
    /// </summary>
    public void StartSceneTransition()
    {
        if (isSceneTransitioning)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환이 이미 진행 중입니다");
            return;
        }

        isSceneTransitioning = true;
        StartCoroutine(SceneTransitionCoroutine());
    }

    /// <summary>
    /// 개발자 모드 씬 건너뛰기
    /// </summary>
    public void SkipLoginForDevelopment()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 개발자 모드: 로그인 건너뛰기");

        StartSceneTransition();
    }

    /// <summary>
    /// 애플리케이션 종료
    /// </summary>
    public void QuitApplication()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 애플리케이션 종료 요청");

        Application.Quit();

        // 에디터에서는 플레이 모드 종료
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    #endregion

    #region Public API - Service Access

    /// <summary>
    /// 로그인 요청 (UI에서 호출)
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    public void RequestLogin(string username, string password)
    {
        if (!ValidateLoginRequest(username, password))
            return;

        if (authService != null)
        {
            authService.LoginAsync(username, password);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] AuthService를 사용할 수 없습니다");

            if (loginUIManager != null)
            {
                loginUIManager.OnLoginResult(false, "인증 서비스에 연결할 수 없습니다.");
            }
        }
    }

    /// <summary>
    /// 회원가입 요청 (UI에서 호출)
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="confirmPassword">비밀번호 확인</param>
    /// <param name="nickname">닉네임</param>
    public void RequestSignup(string username, string password, string confirmPassword, string nickname)
    {
        if (!ValidateSignupRequest(username, password, confirmPassword, nickname))
            return;

        if (authService != null)
        {
            authService.SignupAsync(username, password, nickname);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] AuthService를 사용할 수 없습니다");

            if (loginUIManager != null)
            {
                loginUIManager.OnSignupResult(false, "인증 서비스에 연결할 수 없습니다.");
            }
        }
    }

    /// <summary>
    /// 중복 확인 요청 (UI에서 호출)
    /// </summary>
    /// <param name="type">확인 타입 ("username" 또는 "nickname")</param>
    /// <param name="value">확인할 값</param>
    public void RequestDuplicateCheck(string type, string value)
    {
        if (authService != null)
        {
            authService.CheckDuplicateAsync(type, value);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] AuthService를 사용할 수 없습니다");
        }
    }

    #endregion

    #region Scene Transition

    /// <summary>
    /// 씬 전환 코루틴
    /// </summary>
    private IEnumerator SceneTransitionCoroutine()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환 시작: {nextSceneName}");

        // 전환 지연 시간 대기
        yield return new WaitForSeconds(sceneTransitionDelay);

        // 씬 로드
        SceneManager.LoadScene(nextSceneName);
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 로그인 요청 유효성 검증
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <returns>유효한 요청이면 true</returns>
    private bool ValidateLoginRequest(string username, string password)
    {
        var validation = InputValidator.ValidateLoginForm(username, password);

        if (!validation.IsValid)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 로그인 입력 검증 실패: {validation.ErrorMessage}");

            if (loginUIManager != null)
            {
                loginUIManager.OnLoginResult(false, validation.ErrorMessage);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 회원가입 요청 유효성 검증
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="confirmPassword">비밀번호 확인</param>
    /// <param name="nickname">닉네임</param>
    /// <returns>유효한 요청이면 true</returns>
    private bool ValidateSignupRequest(string username, string password, string confirmPassword, string nickname)
    {
        var validation = InputValidator.ValidateSignupForm(username, password, confirmPassword, nickname);

        if (!validation.IsValid)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 입력 검증 실패: {validation.ErrorMessage}");

            if (loginUIManager != null)
            {
                loginUIManager.OnSignupResult(false, validation.ErrorMessage);
            }

            return false;
        }

        return true;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 씬 초기화 완료 여부 확인
    /// </summary>
    /// <returns>초기화가 완료되었으면 true</returns>
    public bool IsSceneInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 현재 씬 전환 상태 확인
    /// </summary>
    /// <returns>씬 전환 중이면 true</returns>
    public bool IsSceneTransitioning()
    {
        return isSceneTransitioning;
    }

    /// <summary>
    /// AuthService 사용 가능 여부 확인
    /// </summary>
    /// <returns>AuthService가 사용 가능하면 true</returns>
    public bool IsAuthServiceAvailable()
    {
        return authService != null && AuthService.Instance != null;
    }

    /// <summary>
    /// AuthManager 사용 가능 여부 확인
    /// </summary>
    /// <returns>AuthManager가 사용 가능하면 true</returns>
    public bool IsAuthManagerAvailable()
    {
        return AuthManager.Instance != null;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 씬 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Scene Status")]
    public void LogSceneStatus()
    {
        Debug.Log($"=== LoginScene 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"씬 전환 중: {isSceneTransitioning}");
        Debug.Log($"AuthService 사용 가능: {IsAuthServiceAvailable()}");
        Debug.Log($"AuthManager 사용 가능: {IsAuthManagerAvailable()}");
        Debug.Log($"LoginUIManager 연결: {loginUIManager != null}");
        Debug.Log($"다음 씬: {nextSceneName}");
    }

    #endregion
}