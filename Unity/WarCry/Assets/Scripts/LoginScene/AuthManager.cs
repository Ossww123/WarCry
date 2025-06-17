using UnityEngine;
using System;

/// <summary>
/// 사용자 인증 상태 및 정보를 전역적으로 관리하는 싱글톤 매니저
/// 로그인 토큰, 사용자 정보를 씬 간 유지하며, 인증 상태 변화 이벤트를 제공
/// 헤드리스 모드 지원과 토큰 유효성 검증 기능을 포함
/// </summary>
public class AuthManager : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// AuthManager 싱글톤 인스턴스
    /// </summary>
    public static AuthManager Instance { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// 로그인 성공 시 발생하는 이벤트
    /// </summary>
    public static event Action<string, string, string> OnLoginSuccess;

    /// <summary>
    /// 로그아웃 시 발생하는 이벤트
    /// </summary>
    public static event Action OnLogout;

    /// <summary>
    /// 토큰 만료 시 발생하는 이벤트
    /// </summary>
    public static event Action OnTokenExpired;

    #endregion

    #region Public Properties

    /// <summary>
    /// 현재 액세스 토큰
    /// </summary>
    public string Token { get; private set; }

    /// <summary>
    /// 현재 사용자 ID
    /// </summary>
    public string UserId { get; private set; }

    /// <summary>
    /// 현재 사용자 닉네임
    /// </summary>
    public string Nickname { get; private set; }

    /// <summary>
    /// 토큰 만료 시간 (Unix 타임스탬프)
    /// </summary>
    public long TokenExpiresAt { get; private set; }

    /// <summary>
    /// 현재 로그인 상태 여부
    /// </summary>
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token) && !IsTokenExpired();

    #endregion

    #region Inspector Fields

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    [Header("Token Settings")]
    [SerializeField] private bool autoRefreshToken = true;
    [SerializeField] private float tokenExpiryCheckInterval = 60f; // 1분마다 토큰 만료 확인

    #endregion

    #region Constants

    private const string TokenKey = "AuthToken";
    private const string UserIdKey = "AuthUserId";
    private const string NicknameKey = "AuthNickname";
    private const string TokenExpiryKey = "AuthTokenExpiry";
    private const long TokenExpiryBuffer = 300; // 5분 여유시간

    #endregion

    #region Private Fields

    private float tokenCheckTimer = 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeSingleton();
    }

    private void Start()
    {
        LoadPersistedAuthData();
    }

    private void Update()
    {
        if (autoRefreshToken && IsLoggedIn)
        {
            CheckTokenExpiry();
        }
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 AuthManager 비활성화됨");

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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] AuthManager 인스턴스 생성됨");
        }
        else
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] AuthManager 인스턴스가 이미 존재합니다. 중복 인스턴스를 제거합니다.");

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 저장된 인증 데이터 로드
    /// </summary>
    private void LoadPersistedAuthData()
    {
        if (HasPersistedAuthData())
        {
            string token = PlayerPrefs.GetString(TokenKey);
            string userId = PlayerPrefs.GetString(UserIdKey);
            string nickname = PlayerPrefs.GetString(NicknameKey);
            long expiresAt = Convert.ToInt64(PlayerPrefs.GetString(TokenExpiryKey, "0"));

            SetAuthDataInternal(token, userId, nickname, expiresAt, false);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 저장된 인증 데이터 로드됨 - UserId: {userId}");
        }
    }

    #endregion

    #region Public API - Authentication Data Management

    /// <summary>
    /// 로그인 후 인증 데이터 설정
    /// </summary>
    /// <param name="token">액세스 토큰</param>
    /// <param name="userId">사용자 ID</param>
    /// <param name="nickname">사용자 닉네임</param>
    /// <param name="expiresInSeconds">토큰 만료 시간(초)</param>
    public void SetAuthData(string token, string userId, string nickname, long expiresInSeconds = 0)
    {
        if (!ValidateAuthData(token, userId, nickname))
            return;

        long expiresAt = CalculateTokenExpiry(expiresInSeconds);
        SetAuthDataInternal(token, userId, nickname, expiresAt, true);

        PersistAuthData();
        TriggerLoginSuccessEvent();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 인증 데이터 설정 완료 - UserId: {userId}, 만료시간: {GetTokenExpiryString()}");
    }

    /// <summary>
    /// 인증 데이터 설정 (만료 시간 없음 - 기존 호환성)
    /// </summary>
    /// <param name="token">액세스 토큰</param>
    /// <param name="userId">사용자 ID</param>
    /// <param name="nickname">사용자 닉네임</param>
    public void SetAuthData(string token, string userId, string nickname)
    {
        SetAuthData(token, userId, nickname, 0);
    }

    /// <summary>
    /// 로그아웃 처리 및 인증 데이터 초기화
    /// </summary>
    public void ClearAuthData()
    {
        ClearAuthDataInternal();
        ClearPersistedAuthData();
        TriggerLogoutEvent();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 인증 데이터 초기화 완료");
    }

    /// <summary>
    /// 토큰 갱신
    /// </summary>
    /// <param name="newToken">새로운 액세스 토큰</param>
    /// <param name="expiresInSeconds">토큰 만료 시간(초)</param>
    public void RefreshToken(string newToken, long expiresInSeconds = 0)
    {
        if (string.IsNullOrEmpty(newToken))
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 유효하지 않은 토큰으로 갱신 시도됨");
            return;
        }

        Token = newToken;
        TokenExpiresAt = CalculateTokenExpiry(expiresInSeconds);

        PersistAuthData();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 토큰 갱신 완료 - 만료시간: {GetTokenExpiryString()}");
    }

    #endregion

    #region Public API - Token Validation

    /// <summary>
    /// 토큰 만료 여부 확인
    /// </summary>
    /// <returns>토큰이 만료되었으면 true</returns>
    public bool IsTokenExpired()
    {
        if (TokenExpiresAt == 0) return false; // 만료 시간이 설정되지 않은 경우

        long currentTime = GetCurrentUnixTimestamp();
        return currentTime >= (TokenExpiresAt - TokenExpiryBuffer);
    }

    /// <summary>
    /// 토큰 유효성 검증 (null, 빈 문자열, 만료 확인)
    /// </summary>
    /// <returns>토큰이 유효하면 true</returns>
    public bool IsTokenValid()
    {
        return !string.IsNullOrEmpty(Token) && !IsTokenExpired();
    }

    /// <summary>
    /// 인증 헤더 문자열 생성
    /// </summary>
    /// <returns>Bearer 토큰 형식 문자열, 토큰이 없으면 null</returns>
    public string GetAuthorizationHeader()
    {
        return IsTokenValid() ? $"Bearer {Token}" : null;
    }

    #endregion

    #region Token Expiry Management

    /// <summary>
    /// 토큰 만료 시간 주기적 확인
    /// </summary>
    private void CheckTokenExpiry()
    {
        tokenCheckTimer += Time.deltaTime;

        if (tokenCheckTimer >= tokenExpiryCheckInterval)
        {
            tokenCheckTimer = 0f;

            if (IsTokenExpired())
            {
                HandleTokenExpiry();
            }
        }
    }

    /// <summary>
    /// 토큰 만료 처리
    /// </summary>
    private void HandleTokenExpiry()
    {
        if (verboseLogging)
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 토큰이 만료되었습니다");

        TriggerTokenExpiredEvent();
        ClearAuthData();
    }

    /// <summary>
    /// 토큰 만료 시간 계산
    /// </summary>
    /// <param name="expiresInSeconds">만료까지 남은 시간(초)</param>
    /// <returns>만료 시간 Unix 타임스탬프</returns>
    private long CalculateTokenExpiry(long expiresInSeconds)
    {
        if (expiresInSeconds <= 0) return 0;

        return GetCurrentUnixTimestamp() + expiresInSeconds;
    }

    /// <summary>
    /// 현재 Unix 타임스탬프 가져오기
    /// </summary>
    /// <returns>현재 Unix 타임스탬프</returns>
    private long GetCurrentUnixTimestamp()
    {
        return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
    }

    /// <summary>
    /// 토큰 만료 시간 문자열 변환 (디버깅용)
    /// </summary>
    /// <returns>만료 시간 문자열</returns>
    private string GetTokenExpiryString()
    {
        if (TokenExpiresAt == 0) return "만료시간 없음";

        DateTime expiryDateTime = DateTimeOffset.FromUnixTimeSeconds(TokenExpiresAt).DateTime;
        return expiryDateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }

    #endregion

    #region Data Persistence

    /// <summary>
    /// 인증 데이터 영구 저장
    /// </summary>
    private void PersistAuthData()
    {
        PlayerPrefs.SetString(TokenKey, Token ?? "");
        PlayerPrefs.SetString(UserIdKey, UserId ?? "");
        PlayerPrefs.SetString(NicknameKey, Nickname ?? "");
        PlayerPrefs.SetString(TokenExpiryKey, TokenExpiresAt.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 저장된 인증 데이터 삭제
    /// </summary>
    private void ClearPersistedAuthData()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(UserIdKey);
        PlayerPrefs.DeleteKey(NicknameKey);
        PlayerPrefs.DeleteKey(TokenExpiryKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 저장된 인증 데이터 존재 여부 확인
    /// </summary>
    /// <returns>저장된 데이터가 있으면 true</returns>
    private bool HasPersistedAuthData()
    {
        return PlayerPrefs.HasKey(TokenKey) &&
               PlayerPrefs.HasKey(UserIdKey) &&
               !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey));
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// 인증 데이터 내부 설정
    /// </summary>
    /// <param name="token">액세스 토큰</param>
    /// <param name="userId">사용자 ID</param>
    /// <param name="nickname">사용자 닉네임</param>
    /// <param name="expiresAt">토큰 만료 시간</param>
    /// <param name="resetTimer">토큰 확인 타이머 리셋 여부</param>
    private void SetAuthDataInternal(string token, string userId, string nickname, long expiresAt, bool resetTimer)
    {
        Token = token;
        UserId = userId;
        Nickname = nickname;
        TokenExpiresAt = expiresAt;

        if (resetTimer)
        {
            tokenCheckTimer = 0f;
        }
    }

    /// <summary>
    /// 인증 데이터 내부 초기화
    /// </summary>
    private void ClearAuthDataInternal()
    {
        Token = null;
        UserId = null;
        Nickname = null;
        TokenExpiresAt = 0;
        tokenCheckTimer = 0f;
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 로그인 성공 이벤트 발생
    /// </summary>
    private void TriggerLoginSuccessEvent()
    {
        OnLoginSuccess?.Invoke(Token, UserId, Nickname);
    }

    /// <summary>
    /// 로그아웃 이벤트 발생
    /// </summary>
    private void TriggerLogoutEvent()
    {
        OnLogout?.Invoke();
    }

    /// <summary>
    /// 토큰 만료 이벤트 발생
    /// </summary>
    private void TriggerTokenExpiredEvent()
    {
        OnTokenExpired?.Invoke();
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 인증 데이터 유효성 검증
    /// </summary>
    /// <param name="token">액세스 토큰</param>
    /// <param name="userId">사용자 ID</param>
    /// <param name="nickname">사용자 닉네임</param>
    /// <returns>유효한 데이터면 true</returns>
    private bool ValidateAuthData(string token, string userId, string nickname)
    {
        if (string.IsNullOrEmpty(token))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 토큰이 비어있습니다");
            return false;
        }

        if (string.IsNullOrEmpty(userId))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 사용자 ID가 비어있습니다");
            return false;
        }

        if (string.IsNullOrEmpty(nickname))
        {
            if (verboseLogging)
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 닉네임이 비어있습니다");
            return false;
        }

        return true;
    }

    #endregion
}