using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

/// <summary>
/// 인증 관련 API 통신을 전담하는 서비스 클래스
/// 로그인, 회원가입, 사용자 정보 조회, 중복 확인 등의 서버 통신을 처리하며
/// AuthManager와 연동하여 인증 상태를 관리
/// </summary>
public class AuthService : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// AuthService 싱글톤 인스턴스
    /// </summary>
    public static AuthService Instance { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// 로그인 완료 시 발생하는 이벤트 (성공 여부, 메시지)
    /// </summary>
    public static event Action<bool, string> OnLoginCompleted;

    /// <summary>
    /// 회원가입 완료 시 발생하는 이벤트 (성공 여부, 메시지)
    /// </summary>
    public static event Action<bool, string> OnSignupCompleted;

    /// <summary>
    /// 중복 확인 완료 시 발생하는 이벤트 (타입, 사용가능 여부, 메시지)
    /// </summary>
    public static event Action<string, bool, string> OnDuplicateCheckCompleted;

    #endregion

    #region Inspector Fields

    [Header("Server Configuration")]
    [SerializeField] private string serverUrl = "https://k12d104.p.ssafy.io";
    [SerializeField] private float requestTimeout = 10f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    // API 엔드포인트
    private const string LoginEndpoint = "/api/auth/login";
    private const string SignupEndpoint = "/api/auth/signup";
    private const string UserInfoEndpoint = "/api/auth/me";
    private const string CheckUsernameEndpoint = "/api/auth/check-username";
    private const string CheckNicknameEndpoint = "/api/auth/check-nickname";

    // HTTP 헤더
    private const string ContentTypeHeader = "Content-Type";
    private const string AuthorizationHeader = "Authorization";
    private const string JsonContentType = "application/json";

    // 에러 메시지
    private const string NetworkErrorMessage = "네트워크 연결을 확인해주세요.";
    private const string ServerErrorMessage = "서버 오류가 발생했습니다.";
    private const string ParseErrorMessage = "응답 데이터 처리 중 오류가 발생했습니다.";

    #endregion

    #region Data Classes

    /// <summary>
    /// 로그인 요청 데이터
    /// </summary>
    [System.Serializable]
    public class LoginRequest
    {
        public string username;
        public string password;
    }

    /// <summary>
    /// 회원가입 요청 데이터
    /// </summary>
    [System.Serializable]
    public class SignupRequest
    {
        public string username;
        public string password;
        public string nickname;
    }

    /// <summary>
    /// 로그인 응답 데이터
    /// </summary>
    [System.Serializable]
    public class LoginResponse
    {
        public string accessToken;
        public string tokenType;
        public long expiresIn;
    }

    /// <summary>
    /// 사용자 정보 응답 데이터
    /// </summary>
    [System.Serializable]
    public class UserInfoResponse
    {
        public string username;
        public string nickname;
    }

    /// <summary>
    /// 중복 확인 응답 데이터
    /// </summary>
    [System.Serializable]
    public class AvailabilityResponse
    {
        public bool available;
    }

    /// <summary>
    /// 에러 응답 데이터
    /// </summary>
    [System.Serializable]
    public class ErrorResponse
    {
        public bool success;
        public string errorCode;
        public string message;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeSingleton();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 AuthService 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글톤 인스턴스 초기화
    /// </summary>
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] AuthService 인스턴스 생성됨");
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] AuthService 인스턴스가 이미 존재합니다. 중복 인스턴스를 제거합니다.");

            Destroy(gameObject);
        }
    }

    #endregion

    #region Public API - Authentication

    /// <summary>
    /// 사용자 로그인 처리
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    public void LoginAsync(string username, string password)
    {
        if (!ValidateLoginInput(username, password))
        {
            TriggerLoginCompleted(false, "아이디와 비밀번호를 모두 입력해주세요.");
            return;
        }

        StartCoroutine(LoginCoroutine(username, password));
    }

    /// <summary>
    /// 사용자 회원가입 처리
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="nickname">닉네임</param>
    public void SignupAsync(string username, string password, string nickname)
    {
        if (!ValidateSignupInput(username, password, nickname))
        {
            TriggerSignupCompleted(false, "모든 필드를 입력해주세요.");
            return;
        }

        StartCoroutine(SignupCoroutine(username, password, nickname));
    }

    /// <summary>
    /// 중복 확인 처리
    /// </summary>
    /// <param name="type">확인 타입 ("username" 또는 "nickname")</param>
    /// <param name="value">확인할 값</param>
    public void CheckDuplicateAsync(string type, string value)
    {
        if (!ValidateDuplicateCheckInput(type, value))
        {
            TriggerDuplicateCheckCompleted(type, false, $"{type}을(를) 입력해주세요.");
            return;
        }

        StartCoroutine(CheckDuplicateCoroutine(type, value));
    }

    #endregion

    #region Coroutines - Authentication

    /// <summary>
    /// 로그인 API 호출 코루틴
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    private IEnumerator LoginCoroutine(string username, string password)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 시도 - 아이디: {username}");

        LoginRequest loginData = new LoginRequest
        {
            username = username,
            password = password
        };

        string jsonData = JsonUtility.ToJson(loginData);
        string url = BuildApiUrl(LoginEndpoint);

        using (UnityWebRequest request = CreatePostRequest(url, jsonData))
        {
            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                yield return ProcessLoginSuccess(request);
            }
            else
            {
                ProcessLoginFailure(request);
            }
        }
    }

    /// <summary>
    /// 로그인 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private IEnumerator ProcessLoginSuccess(UnityWebRequest request)
    {
        LoginResponse loginResponse;

        try
        {
            loginResponse = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);

            if (verboseLogging)
                Debug.Log($"토큰 수신 - 타입: {loginResponse.tokenType}");
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"로그인 응답 파싱 실패: {e.Message}");

            TriggerLoginCompleted(false, ParseErrorMessage);
            yield break;
        }

        yield return FetchUserInfoCoroutine(loginResponse.accessToken, loginResponse.expiresIn);
    }

    /// <summary>
    /// 로그인 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessLoginFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "로그인에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 로그인 실패: {errorMessage}");

        TriggerLoginCompleted(false, errorMessage);
    }

    /// <summary>
    /// 사용자 정보 조회 코루틴
    /// </summary>
    /// <param name="accessToken">액세스 토큰</param>
    /// <param name="expiresIn">토큰 만료 시간</param>
    private IEnumerator FetchUserInfoCoroutine(string accessToken, long expiresIn)
    {
        string url = BuildApiUrl(UserInfoEndpoint);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader(AuthorizationHeader, $"Bearer {accessToken}");
            request.timeout = (int)requestTimeout;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 사용자 정보 요청 전송");

            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessUserInfoSuccess(request, accessToken, expiresIn);
            }
            else
            {
                ProcessUserInfoFailure(request);
            }
        }
    }

    /// <summary>
    /// 사용자 정보 조회 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    /// <param name="accessToken">액세스 토큰</param>
    /// <param name="expiresIn">토큰 만료 시간</param>
    private void ProcessUserInfoSuccess(UnityWebRequest request, string accessToken, long expiresIn)
    {
        try
        {
            UserInfoResponse userInfo = JsonUtility.FromJson<UserInfoResponse>(request.downloadHandler.text);

            // AuthManager에 인증 데이터 저장
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.SetAuthData(accessToken, userInfo.username, userInfo.nickname, expiresIn);
            }

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로그인 완료 - 사용자: {userInfo.username}, 닉네임: {userInfo.nickname}");

            TriggerLoginCompleted(true, "로그인 성공!");
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 사용자 정보 파싱 실패: {e.Message}");

            TriggerLoginCompleted(false, ParseErrorMessage);
        }
    }

    /// <summary>
    /// 사용자 정보 조회 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessUserInfoFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "사용자 정보를 가져오는데 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 사용자 정보 조회 실패: {errorMessage}");

        TriggerLoginCompleted(false, errorMessage);
    }

    #endregion

    #region Coroutines - Signup

    /// <summary>
    /// 회원가입 API 호출 코루틴
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="nickname">닉네임</param>
    private IEnumerator SignupCoroutine(string username, string password, string nickname)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 시도 - 아이디: {username}, 닉네임: {nickname}");

        SignupRequest signupData = new SignupRequest
        {
            username = username,
            password = password,
            nickname = nickname
        };

        string jsonData = JsonUtility.ToJson(signupData);
        string url = BuildApiUrl(SignupEndpoint);

        using (UnityWebRequest request = CreatePostRequest(url, jsonData))
        {
            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessSignupSuccess();
            }
            else
            {
                ProcessSignupFailure(request);
            }
        }
    }

    /// <summary>
    /// 회원가입 성공 처리
    /// </summary>
    private void ProcessSignupSuccess()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 성공");

        TriggerSignupCompleted(true, "회원가입 성공! 로그인 화면으로 이동합니다.");
    }

    /// <summary>
    /// 회원가입 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessSignupFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "회원가입에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 회원가입 실패: {errorMessage}");

        TriggerSignupCompleted(false, errorMessage);
    }

    #endregion

    #region Coroutines - Duplicate Check

    /// <summary>
    /// 중복 확인 API 호출 코루틴
    /// </summary>
    /// <param name="type">확인 타입</param>
    /// <param name="value">확인할 값</param>
    private IEnumerator CheckDuplicateCoroutine(string type, string value)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 중복 확인 요청 - 타입: {type}, 값: {value}");

        string url = BuildDuplicateCheckUrl(type, value);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = (int)requestTimeout;

            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessDuplicateCheckSuccess(request, type);
            }
            else
            {
                ProcessDuplicateCheckFailure(request, type);
            }
        }
    }

    /// <summary>
    /// 중복 확인 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    /// <param name="type">확인 타입</param>
    private void ProcessDuplicateCheckSuccess(UnityWebRequest request, string type)
    {
        try
        {
            AvailabilityResponse response = JsonUtility.FromJson<AvailabilityResponse>(request.downloadHandler.text);

            string message = response.available ? $"{type} 사용 가능!" : $"{type} 이미 사용 중입니다.";

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 중복 확인 결과 - {type}: {(response.available ? "사용가능" : "사용중")}");

            TriggerDuplicateCheckCompleted(type, response.available, message);
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 중복 확인 응답 파싱 실패: {e.Message}");

            TriggerDuplicateCheckCompleted(type, false, ParseErrorMessage);
        }
    }

    /// <summary>
    /// 중복 확인 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    /// <param name="type">확인 타입</param>
    private void ProcessDuplicateCheckFailure(UnityWebRequest request, string type)
    {
        string errorMessage = ExtractErrorMessage(request, $"{type} 중복 확인에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 중복 확인 실패: {errorMessage}");

        TriggerDuplicateCheckCompleted(type, false, errorMessage);
    }

    #endregion

    #region HTTP Utilities

    /// <summary>
    /// POST 요청 객체 생성
    /// </summary>
    /// <param name="url">요청 URL</param>
    /// <param name="jsonData">JSON 데이터</param>
    /// <returns>설정된 UnityWebRequest 객체</returns>
    private UnityWebRequest CreatePostRequest(string url, string jsonData)
    {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader(ContentTypeHeader, JsonContentType);
        request.timeout = (int)requestTimeout;

        return request;
    }

    /// <summary>
    /// 요청 전송 및 로깅
    /// </summary>
    /// <param name="request">전송할 요청</param>
    private IEnumerator SendRequest(UnityWebRequest request)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] API 요청 전송: {request.method} {request.url}");

        yield return request.SendWebRequest();

        if (verboseLogging)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] API 응답: {request.responseCode} - {request.result}");

            if (!string.IsNullOrEmpty(request.downloadHandler.text))
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 응답 데이터 길이: {request.downloadHandler.text.Length}");
            }
        }
    }

    /// <summary>
    /// 요청 성공 여부 확인
    /// </summary>
    /// <param name="request">확인할 요청</param>
    /// <returns>성공하면 true</returns>
    private bool IsRequestSuccessful(UnityWebRequest request)
    {
        return request.result == UnityWebRequest.Result.Success;
    }

    /// <summary>
    /// 에러 메시지 추출
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    /// <param name="defaultMessage">기본 메시지</param>
    /// <returns>추출된 에러 메시지</returns>
    private string ExtractErrorMessage(UnityWebRequest request, string defaultMessage)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.DataProcessingError)
        {
            return NetworkErrorMessage;
        }

        try
        {
            if (!string.IsNullOrEmpty(request.downloadHandler.text))
            {
                ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                return !string.IsNullOrEmpty(errorResponse.message) ? errorResponse.message : defaultMessage;
            }
        }
        catch
        {
            // 파싱 실패 시 기본 메시지 사용
        }

        return defaultMessage;
    }

    #endregion

    #region URL Building

    /// <summary>
    /// API URL 생성
    /// </summary>
    /// <param name="endpoint">API 엔드포인트</param>
    /// <returns>완전한 API URL</returns>
    private string BuildApiUrl(string endpoint)
    {
        return serverUrl + endpoint;
    }

    /// <summary>
    /// 중복 확인 URL 생성
    /// </summary>
    /// <param name="type">확인 타입</param>
    /// <param name="value">확인할 값</param>
    /// <returns>중복 확인 URL</returns>
    private string BuildDuplicateCheckUrl(string type, string value)
    {
        string endpoint = type == "username" ? CheckUsernameEndpoint : CheckNicknameEndpoint;
        string paramName = type == "username" ? "username" : "nickname";

        return $"{serverUrl}{endpoint}?{paramName}={UnityWebRequest.EscapeURL(value)}";
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 로그인 완료 이벤트 발생
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="message">결과 메시지</param>
    private void TriggerLoginCompleted(bool success, string message)
    {
        OnLoginCompleted?.Invoke(success, message);
    }

    /// <summary>
    /// 회원가입 완료 이벤트 발생
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="message">결과 메시지</param>
    private void TriggerSignupCompleted(bool success, string message)
    {
        OnSignupCompleted?.Invoke(success, message);
    }

    /// <summary>
    /// 중복 확인 완료 이벤트 발생
    /// </summary>
    /// <param name="type">확인 타입</param>
    /// <param name="available">사용 가능 여부</param>
    /// <param name="message">결과 메시지</param>
    private void TriggerDuplicateCheckCompleted(string type, bool available, string message)
    {
        OnDuplicateCheckCompleted?.Invoke(type, available, message);
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 로그인 입력값 검증
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <returns>유효한 입력값이면 true</returns>
    private bool ValidateLoginInput(string username, string password)
    {
        return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
    }

    /// <summary>
    /// 회원가입 입력값 검증
    /// </summary>
    /// <param name="username">사용자 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="nickname">닉네임</param>
    /// <returns>유효한 입력값이면 true</returns>
    private bool ValidateSignupInput(string username, string password, string nickname)
    {
        return !string.IsNullOrEmpty(username) &&
               !string.IsNullOrEmpty(password) &&
               !string.IsNullOrEmpty(nickname);
    }

    /// <summary>
    /// 중복 확인 입력값 검증
    /// </summary>
    /// <param name="type">확인 타입</param>
    /// <param name="value">확인할 값</param>
    /// <returns>유효한 입력값이면 true</returns>
    private bool ValidateDuplicateCheckInput(string type, string value)
    {
        return !string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(value);
    }

    #endregion
}