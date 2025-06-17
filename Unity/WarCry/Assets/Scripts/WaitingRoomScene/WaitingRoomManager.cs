using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

/// <summary>
/// 대기실 관련 API 통신을 전담하는 서비스 클래스
/// 방 나가기 API 통신을 처리하며, AuthManager와 연동하여 인증된 요청을 수행
/// 호스트와 게스트의 역할에 따라 다른 API 엔드포인트를 호출하고 결과를 이벤트로 전달
/// </summary>
public class WaitingRoomManager : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// WaitingRoomManager 싱글톤 인스턴스
    /// </summary>
    public static WaitingRoomManager Instance { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// 방 나가기 완료 시 발생하는 이벤트 (성공 여부, 결과 타입, 메시지, 새 호스트 정보)
    /// </summary>
    public static event Action<bool, LeaveRoomResult, string, HostTransferInfo> OnLeaveRoomCompleted;

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
    private const string GuestLeaveEndpoint = "/api/match/{0}/leave";
    private const string HostLeaveEndpoint = "/api/match/{0}/host-leave";

    // HTTP 헤더
    private const string ContentTypeHeader = "Content-Type";
    private const string AuthorizationHeader = "Authorization";
    private const string JsonContentType = "application/json";

    // 에러 메시지
    private const string NetworkErrorMessage = "네트워크 연결을 확인해주세요.";
    private const string ServerErrorMessage = "서버 오류가 발생했습니다.";
    private const string ParseErrorMessage = "응답 데이터 처리 중 오류가 발생했습니다.";
    private const string AuthErrorMessage = "인증이 필요합니다. 다시 로그인해주세요.";

    // PlayerPrefs 키
    private const string CurrentMatchIdKey = "CurrentMatchId";
    private const string CurrentUserRoleKey = "CurrentUserRole";

    #endregion

    #region Data Classes

    /// <summary>
    /// 방 나가기 결과 타입
    /// </summary>
    public enum LeaveRoomResult
    {
        Success,        // 성공적으로 나감
        Transferred,    // 호스트 권한 이전됨
        Disbanded,      // 방이 해산됨
        Failed          // 실패
    }

    /// <summary>
    /// 호스트 권한 이전 정보
    /// </summary>
    [System.Serializable]
    public class HostTransferInfo
    {
        public int newHostId;
        public string newHostNickname;
        public uint newHostNetId;

        public HostTransferInfo(int hostId, string hostNickname, uint netId = 0)
        {
            newHostId = hostId;
            newHostNickname = hostNickname;
            newHostNetId = netId;
        }
    }

    /// <summary>
    /// 호스트 나가기 API 응답 데이터
    /// </summary>
    [System.Serializable]
    private class HostLeaveApiResponse
    {
        public bool success;
        public int matchId;
        public string result; // "DISBANDED" 또는 "TRANSFERRED"
        public int newHostId;
        public string newHostNickname;
        public string message;
    }

    /// <summary>
    /// 에러 응답 데이터
    /// </summary>
    [System.Serializable]
    private class ErrorApiResponse
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 WaitingRoomManager 비활성화됨");

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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomManager 인스턴스 생성됨");
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] WaitingRoomManager 인스턴스가 이미 존재합니다. 중복 인스턴스를 제거합니다.");

            Destroy(gameObject);
        }
    }

    #endregion

    #region Public API - Room Management

    /// <summary>
    /// 현재 사용자 역할에 따른 방 나가기 처리
    /// </summary>
    public void LeaveRoom()
    {
        if (!ValidateAuthentication())
        {
            TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, AuthErrorMessage, null);
            return;
        }

        int matchId = GetCurrentMatchId();
        string userRole = GetCurrentUserRole();

        if (!ValidateMatchId(matchId))
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 역할 '{userRole}'로 방 #{matchId} 나가기 시도");

        if (userRole == "HOST")
        {
            LeaveAsHostAsync(matchId);
        }
        else
        {
            LeaveAsGuestAsync(matchId);
        }
    }

    /// <summary>
    /// 게스트로 방 나가기 요청
    /// </summary>
    /// <param name="matchId">방 ID</param>
    public void LeaveAsGuestAsync(int matchId)
    {
        if (!ValidateLeaveRequest(matchId))
            return;

        StartCoroutine(LeaveAsGuestCoroutine(matchId));
    }

    /// <summary>
    /// 호스트로 방 나가기 요청
    /// </summary>
    /// <param name="matchId">방 ID</param>
    public void LeaveAsHostAsync(int matchId)
    {
        if (!ValidateLeaveRequest(matchId))
            return;

        StartCoroutine(LeaveAsHostCoroutine(matchId));
    }

    #endregion

    #region Coroutines - Guest Leave

    /// <summary>
    /// 게스트 방 나가기 API 호출 코루틴
    /// </summary>
    /// <param name="matchId">방 ID</param>
    private IEnumerator LeaveAsGuestCoroutine(int matchId)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 게스트 나가기 요청 시작 - 방 ID: {matchId}");

        string url = BuildApiUrl(string.Format(GuestLeaveEndpoint, matchId));

        using (UnityWebRequest request = CreateLeaveRequest(url))
        {
            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessGuestLeaveSuccess(request);
            }
            else
            {
                ProcessGuestLeaveFailure(request);
            }
        }
    }

    /// <summary>
    /// 게스트 나가기 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessGuestLeaveSuccess(UnityWebRequest request)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 게스트 나가기 성공");

        TriggerLeaveRoomCompleted(true, LeaveRoomResult.Success, "방에서 나갔습니다.", null);
    }

    /// <summary>
    /// 게스트 나가기 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessGuestLeaveFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "방 나가기에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 게스트 나가기 실패: {errorMessage}");

        TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, errorMessage, null);
    }

    #endregion

    #region Coroutines - Host Leave

    /// <summary>
    /// 호스트 방 나가기 API 호출 코루틴
    /// </summary>
    /// <param name="matchId">방 ID</param>
    private IEnumerator LeaveAsHostCoroutine(int matchId)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 호스트 나가기 요청 시작 - 방 ID: {matchId}");

        string url = BuildApiUrl(string.Format(HostLeaveEndpoint, matchId));

        using (UnityWebRequest request = CreateLeaveRequest(url))
        {
            yield return SendRequest(request);

            if (IsRequestSuccessful(request))
            {
                ProcessHostLeaveSuccess(request);
            }
            else
            {
                ProcessHostLeaveFailure(request);
            }
        }
    }

    /// <summary>
    /// 호스트 나가기 성공 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessHostLeaveSuccess(UnityWebRequest request)
    {
        try
        {
            HostLeaveApiResponse response = JsonUtility.FromJson<HostLeaveApiResponse>(request.downloadHandler.text);

            if (response.success)
            {
                ProcessHostLeaveResult(response);
            }
            else
            {
                if (verboseLogging)
                    Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 호스트 나가기 API 응답 실패: {response.message}");

                TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, response.message ?? "호스트 나가기에 실패했습니다.", null);
            }
        }
        catch (Exception e)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 호스트 나가기 응답 파싱 실패: {e.Message}");

            TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, ParseErrorMessage, null);
        }
    }

    /// <summary>
    /// 호스트 나가기 결과 처리
    /// </summary>
    /// <param name="response">API 응답 데이터</param>
    private void ProcessHostLeaveResult(HostLeaveApiResponse response)
    {
        if (response.result == "TRANSFERRED")
        {
            ProcessHostTransfer(response);
        }
        else if (response.result == "DISBANDED")
        {
            ProcessRoomDisbanded(response);
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 알 수 없는 호스트 나가기 결과: {response.result}");

            TriggerLeaveRoomCompleted(true, LeaveRoomResult.Success, response.message ?? "방에서 나갔습니다.", null);
        }
    }

    /// <summary>
    /// 호스트 권한 이전 처리
    /// </summary>
    /// <param name="response">API 응답 데이터</param>
    private void ProcessHostTransfer(HostLeaveApiResponse response)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 호스트 권한이 {response.newHostNickname}에게 이전됨");

        HostTransferInfo transferInfo = new HostTransferInfo(
            response.newHostId,
            response.newHostNickname,
            FindNetIdByPlayerName(response.newHostNickname)
        );

        TriggerLeaveRoomCompleted(true, LeaveRoomResult.Transferred, response.message ?? "호스트 권한이 이전되었습니다.", transferInfo);
    }

    /// <summary>
    /// 방 해산 처리
    /// </summary>
    /// <param name="response">API 응답 데이터</param>
    private void ProcessRoomDisbanded(HostLeaveApiResponse response)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방이 해산되었습니다 (다른 참가자 없음)");

        TriggerLeaveRoomCompleted(true, LeaveRoomResult.Disbanded, response.message ?? "방이 해산되었습니다.", null);
    }

    /// <summary>
    /// 호스트 나가기 실패 처리
    /// </summary>
    /// <param name="request">HTTP 요청 객체</param>
    private void ProcessHostLeaveFailure(UnityWebRequest request)
    {
        string errorMessage = ExtractErrorMessage(request, "호스트 나가기에 실패했습니다.");

        if (verboseLogging)
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 호스트 나가기 실패: {errorMessage}");

        TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, errorMessage, null);
    }

    #endregion

    #region HTTP Utilities

    /// <summary>
    /// 방 나가기 POST 요청 객체 생성
    /// </summary>
    /// <param name="url">요청 URL</param>
    /// <returns>설정된 UnityWebRequest 객체</returns>
    private UnityWebRequest CreateLeaveRequest(string url)
    {
        // 빈 JSON 객체로 POST 요청 생성
        byte[] emptyJsonBytes = System.Text.Encoding.UTF8.GetBytes("{}");

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(emptyJsonBytes);
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 응답 데이터: {request.downloadHandler.text}");
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
    /// 방 나가기 완료 이벤트 발생
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="result">결과 타입</param>
    /// <param name="message">결과 메시지</param>
    /// <param name="transferInfo">호스트 이전 정보 (해당하는 경우)</param>
    private void TriggerLeaveRoomCompleted(bool success, LeaveRoomResult result, string message, HostTransferInfo transferInfo)
    {
        OnLeaveRoomCompleted?.Invoke(success, result, message, transferInfo);
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
    /// 방 나가기 요청 유효성 검증
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <returns>유효한 요청이면 true</returns>
    private bool ValidateLeaveRequest(int matchId)
    {
        if (!ValidateAuthentication())
        {
            TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, AuthErrorMessage, null);
            return false;
        }

        if (!ValidateMatchId(matchId))
            return false;

        return true;
    }

    /// <summary>
    /// 방 ID 유효성 검증
    /// </summary>
    /// <param name="matchId">방 ID</param>
    /// <returns>유효한 방 ID면 true</returns>
    private bool ValidateMatchId(int matchId)
    {
        if (matchId <= 0)
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 유효하지 않은 방 ID: {matchId}");

            TriggerLeaveRoomCompleted(false, LeaveRoomResult.Failed, "방 정보를 찾을 수 없습니다.", null);
            return false;
        }

        return true;
    }

    #endregion

    #region Public Properties - Backward Compatibility

    /// <summary>
    /// 현재 방 ID (기존 코드 호환성을 위한 프로퍼티)
    /// </summary>
    public int RoomId => GetCurrentMatchId();

    #endregion

    #region Helper Methods

    /// <summary>
    /// 현재 방 ID 가져오기
    /// </summary>
    /// <returns>현재 방 ID</returns>
    private int GetCurrentMatchId()
    {
        return PlayerPrefs.GetInt(CurrentMatchIdKey, -1);
    }

    /// <summary>
    /// 현재 사용자 역할 가져오기
    /// </summary>
    /// <returns>사용자 역할</returns>
    private string GetCurrentUserRole()
    {
        return PlayerPrefs.GetString(CurrentUserRoleKey, "GUEST");
    }

    /// <summary>
    /// 플레이어 이름으로 NetworkIdentity ID 찾기
    /// </summary>
    /// <param name="playerName">플레이어 이름</param>
    /// <returns>찾은 NetworkIdentity ID (없으면 0)</returns>
    private uint FindNetIdByPlayerName(string playerName)
    {
        if (Mirror.NetworkClient.spawned == null)
            return 0;

        foreach (var kvp in Mirror.NetworkClient.spawned)
        {
            PlayerInfo playerInfo = kvp.Value.GetComponent<PlayerInfo>();
            if (playerInfo != null && playerInfo.playerName == playerName)
            {
                return kvp.Key;
            }
        }

        if (verboseLogging)
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 '{playerName}'에 해당하는 netId를 찾을 수 없습니다.");

        return 0;
    }

    #endregion
}