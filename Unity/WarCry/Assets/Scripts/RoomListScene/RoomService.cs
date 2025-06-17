using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using RoomListData;

/// <summary>
/// 방 관련 API 통신을 전담하는 서비스 클래스
/// 방 목록 조회, 방 생성, 방 입장 등의 서버 통신을 처리하며
/// AuthManager와 연동하여 인증된 요청을 수행
/// </summary>
public class RoomService : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// RoomService 싱글톤 인스턴스
    /// </summary>
    public static RoomService Instance { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// 방 목록 조회 완료 시 발생하는 이벤트 (성공 여부, 방 목록, 메시지)
    /// </summary>
    public static event Action<bool, MatchListApiResponse, string> OnRoomListReceived;

    /// <summary>
    /// 방 생성 완료 시 발생하는 이벤트 (성공 여부, 응답 데이터, 메시지)
    /// </summary>
    public static event Action<bool, CreateMatchApiResponse, string> OnRoomCreated;

    /// <summary>
    /// 방 입장 완료 시 발생하는 이벤트 (성공 여부, 응답 데이터, 메시지)
    /// </summary>
    public static event Action<bool, JoinMatchApiResponse, string> OnRoomJoined;

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
    private const string MatchListEndpoint = "/api/match";
    private const string CreateMatchEndpoint = "/api/match";
    private const string JoinMatchEndpoint = "/api/match/{0}/join";

    // HTTP 헤더
    private const string ContentTypeHeader = "Content-Type";
    private const string AuthorizationHeader = "Authorization";
    private const string JsonContentType = "application/json";

    // 에러 메시지
    private const string NetworkErrorMessage = "네트워크 연결을 확인해주세요.";
    private const string ServerErrorMessage = "서버 오류가 발생했습니다.";
    private const string ParseErrorMessage = "응답 데이터 처리 중 오류가 발생했습니다.";
    private const string AuthErrorMessage = "인증이 필요합니다. 다시 로그인해주세요.";

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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 RoomService 비활성화됨");

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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RoomService 인스턴스 생성됨");
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] RoomService 인스턴스가 이미 존재합니다. 중복 인스턴스를 제거합니다.");

            Destroy(gameObject);
        }
    }

    #endregion

    #region Public API - Room Management

    /// <summary>
    /// 방 목록 조회 요청
    /// </summary>
    public void GetRoomListAsync()
    {
        if (!ValidateAuthentication())
        {
            TriggerRoomListReceived(false, null, AuthErrorMessage);
            return;
        }

        StartCoroutine(GetRoomListCoroutine());
    }

    /// <summary>
    /// 방 생성 요청
    /// </summary>
    /// <param name="title">방 제목</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <param name="password">방 비밀번호 (비공개 방인 경우)</param>
    public void CreateRoomAsync(string title, bool isPrivate, string password = null)
    {
        if (!ValidateAuthentication())
        {
            TriggerRoomCreated(false, null, AuthErrorMessage);
            return;
        }

        if (!ValidateCreateRoomInput(title, isPrivate, password))
            return;

        StartCoroutine(CreateRoomCoroutine(title, isPrivate, password));
    }

    /// <summary>
    /// 방 입장 요청
    /// </summary>
    /// <param name="matchId">입장할 방 ID</param>
    /// <param name="password">방 비밀번호 (비공개 방인 경우)</param>
    public void JoinRoomAsync(int matchId, string password = null)
    {
        if (!ValidateAuthentication())
        {
            TriggerRoomJoined(false, null, AuthErrorMessage);
            return;
        }

        if (!ValidateJoinRoomInput(matchId))
            return;

        StartCoroutine(JoinRoomCoroutine(matchId, password));
    }

    #endregion

    #region Coroutines - Room Management

    /// <summary>
    /// 방 목록 조회 API 호출 코루틴
    /// </summary>
    private IEnumerator GetRoomListCoroutine()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 조회 요청 시작");

        string url = BuildApiUrl(MatchListEndpoint);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            SetAuthorizationHeader(request);
            request.timeout = (int)requestTimeout;

            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessRoomListSuccess(request);
            }
            else
            {
                ProcessRoomListFailure(request);
            }
        }
    }

    /// <summary>
    /// 방 생성 API 호출 코루틴
    /// </summary>
    /// <param name="title">방 제목</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <param name="password">방 비밀번호</param>
    private IEnumerator CreateRoomCoroutine(string title, bool isPrivate, string password)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 요청 시작 - 제목: {title}, 비공개: {isPrivate}");

        CreateMatchApiRequest requestData = new CreateMatchApiRequest
        {
            title = title,
            isPrivate = isPrivate,
            password = isPrivate ? password : null
        };

        string json = JsonUtility.ToJson(requestData);
        string url = BuildApiUrl(CreateMatchEndpoint);

        using (UnityWebRequest request = CreatePostRequest(url, json))
        {
            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessCreateRoomSuccess(request);
            }
            else
            {
                ProcessCreateRoomFailure(request);
            }
        }
    }

    /// <summary>
    /// 방 입장 API 호출 코루틴
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <param name="password">방 비밀번호</param>
    private IEnumerator JoinRoomCoroutine(int matchId, string password)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 요청 시작 - 방 ID: {matchId}");

        JoinMatchApiRequest requestData = new JoinMatchApiRequest
        {
            password = password
        };

        string json = JsonUtility.ToJson(requestData);
        string url = BuildApiUrl(string.Format(JoinMatchEndpoint, matchId));

        using (UnityWebRequest request = CreatePostRequest(url, json))
        {
            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessJoinRoomSuccess(request);
            }
            else
            {
                ProcessJoinRoomFailure(request);
            }
        }
    }

    #endregion

    #region Response Processing - Room List

    /// <summary>
    /// 방 목록 조회 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessRoomListSuccess(UnityWebRequest request)
    {
        try
        {
            MatchListApiResponse response = JsonUtility.FromJson<MatchListApiResponse>(request.downloadHandler.text);

            if (response.success)
            {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 조회 성공 - {response.matches.Count}개 방 로드됨");

                TriggerRoomListReceived(true, response, "방 목록을 성공적으로 불러왔습니다.");
            }
            else
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 조회 API 응답 실패");

                TriggerRoomListReceived(false, null, "방 목록을 불러오는데 실패했습니다.");
            }
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 응답 파싱 실패: {e.Message}");

            TriggerRoomListReceived(false, null, ParseErrorMessage);
        }
    }

    /// <summary>
    /// 방 목록 조회 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessRoomListFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "방 목록을 불러오는데 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 목록 조회 실패: {errorMessage}");

        TriggerRoomListReceived(false, null, errorMessage);
    }

    #endregion

    #region Response Processing - Create Room

    /// <summary>
    /// 방 생성 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessCreateRoomSuccess(UnityWebRequest request)
    {
        try
        {
            CreateMatchApiResponse response = JsonUtility.FromJson<CreateMatchApiResponse>(request.downloadHandler.text);

            if (response.success)
            {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 성공 - 방 ID: {response.matchId}, IP: {response.serverIp}, Port: {response.serverPort}");

                TriggerRoomCreated(true, response, response.message ?? "방이 성공적으로 생성되었습니다.");
            }
            else
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 API 응답 실패: {response.message}");

                TriggerRoomCreated(false, null, response.message ?? "방 생성에 실패했습니다.");
            }
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 응답 파싱 실패: {e.Message}");

            TriggerRoomCreated(false, null, ParseErrorMessage);
        }
    }

    /// <summary>
    /// 방 생성 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessCreateRoomFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "방 생성에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 생성 실패: {errorMessage}");

        TriggerRoomCreated(false, null, errorMessage);
    }

    #endregion

    #region Response Processing - Join Room

    /// <summary>
    /// 방 입장 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessJoinRoomSuccess(UnityWebRequest request)
    {
        try
        {
            JoinMatchApiResponse response = JsonUtility.FromJson<JoinMatchApiResponse>(request.downloadHandler.text);

            if (response.success)
            {
                if (verboseLogging)
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 성공 - 방 ID: {response.matchId}, 역할: {response.role}, IP: {response.serverIp}, Port: {response.serverPort}");

                TriggerRoomJoined(true, response, response.message ?? "방에 성공적으로 입장했습니다.");
            }
            else
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 API 응답 실패: {response.message}");

                TriggerRoomJoined(false, null, response.message ?? "방 입장에 실패했습니다.");
            }
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 응답 파싱 실패: {e.Message}");

            TriggerRoomJoined(false, null, ParseErrorMessage);
        }
    }

    /// <summary>
    /// 방 입장 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessJoinRoomFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "방 입장에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 입장 실패: {errorMessage}");

        TriggerRoomJoined(false, null, errorMessage);
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

        SetAuthorizationHeader(request);

        return request;
    }

    /// <summary>
    /// 인증 헤더 설정
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void SetAuthorizationHeader(UnityWebRequest request)
    {
        if (AuthManager.Instance != null && !string.IsNullOrEmpty(AuthManager.Instance.Token))
        {
            request.SetRequestHeader(AuthorizationHeader, $"Bearer {AuthManager.Instance.Token}");
        }
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
                ErrorApiResponse errorResponse = JsonUtility.FromJson<ErrorApiResponse>(request.downloadHandler.text);
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

    #endregion

    #region Event Triggers

    /// <summary>
    /// 방 목록 조회 완료 이벤트 발생
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="response">응답 데이터</param>
    /// <param name="message">결과 메시지</param>
    private void TriggerRoomListReceived(bool success, MatchListApiResponse response, string message)
    {
        OnRoomListReceived?.Invoke(success, response, message);
    }

    /// <summary>
    /// 방 생성 완료 이벤트 발생
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="response">응답 데이터</param>
    /// <param name="message">결과 메시지</param>
    private void TriggerRoomCreated(bool success, CreateMatchApiResponse response, string message)
    {
        OnRoomCreated?.Invoke(success, response, message);
    }

    /// <summary>
    /// 방 입장 완료 이벤트 발생
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="response">응답 데이터</param>
    /// <param name="message">결과 메시지</param>
    private void TriggerRoomJoined(bool success, JoinMatchApiResponse response, string message)
    {
        OnRoomJoined?.Invoke(success, response, message);
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 인증 상태 검증
    /// </summary>
    /// <returns>인증이 유효하면 true</returns>
    private bool ValidateAuthentication()
    {
        if (AuthManager.Instance == null || string.IsNullOrEmpty(AuthManager.Instance.Token))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 인증 토큰이 없습니다");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 방 생성 입력값 검증
    /// </summary>
    /// <param name="title">방 제목</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <param name="password">방 비밀번호</param>
    /// <returns>유효한 입력값이면 true</returns>
    private bool ValidateCreateRoomInput(string title, bool isPrivate, string password)
    {
        if (string.IsNullOrEmpty(title))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 방 제목이 비어있습니다");

            TriggerRoomCreated(false, null, "방 제목을 입력해주세요.");
            return false;
        }

        if (isPrivate && string.IsNullOrEmpty(password))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 비공개 방은 비밀번호가 필요합니다");

            TriggerRoomCreated(false, null, "비공개 방은 비밀번호를 설정해야 합니다.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 방 입장 입력값 검증
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <returns>유효한 입력값이면 true</returns>
    private bool ValidateJoinRoomInput(int matchId)
    {
        if (matchId <= 0)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 방 ID: {matchId}");

            TriggerRoomJoined(false, null, "잘못된 방 정보입니다.");
            return false;
        }

        return true;
    }

    #endregion
}