using System.Collections.Generic;

/// <summary>
/// 방 목록 관련 API 요청/응답 데이터 클래스들을 정의
/// 서버와의 통신에 사용되는 모든 데이터 구조체를 포함하며
/// JSON 직렬화/역직렬화를 지원
/// </summary>
namespace RoomListData
{
    #region API Response Classes

    /// <summary>
    /// 방 목록 조회 API 응답 데이터
    /// </summary>
    [System.Serializable]
    public class MatchListApiResponse
    {
        /// <summary>
        /// API 호출 성공 여부
        /// </summary>
        public bool success;

        /// <summary>
        /// 방 목록 데이터
        /// </summary>
        public List<MatchApiData> matches;
    }

    /// <summary>
    /// 개별 방 정보 데이터
    /// </summary>
    [System.Serializable]
    public class MatchApiData
    {
        /// <summary>
        /// 방 고유 ID
        /// </summary>
        public int matchId;

        /// <summary>
        /// 방 제목
        /// </summary>
        public string title;

        /// <summary>
        /// 방장 닉네임
        /// </summary>
        public string hostNickname;

        /// <summary>
        /// 비공개 방 여부
        /// </summary>
        public bool isPrivate;

        /// <summary>
        /// 방 상태 ("WAITING", "PLAYING", "ENDED")
        /// </summary>
        public string status;
    }

    /// <summary>
    /// 방 생성 API 응답 데이터
    /// </summary>
    [System.Serializable]
    public class CreateMatchApiResponse
    {
        /// <summary>
        /// API 호출 성공 여부
        /// </summary>
        public bool success;

        /// <summary>
        /// 생성된 방 ID
        /// </summary>
        public int matchId;

        /// <summary>
        /// Mirror 서버 IP 주소
        /// </summary>
        public string serverIp;

        /// <summary>
        /// Mirror 서버 포트 번호
        /// </summary>
        public int serverPort;

        /// <summary>
        /// 방 상태
        /// </summary>
        public string status;

        /// <summary>
        /// 서버 응답 메시지
        /// </summary>
        public string message;
    }

    /// <summary>
    /// 방 입장 API 응답 데이터
    /// </summary>
    [System.Serializable]
    public class JoinMatchApiResponse
    {
        /// <summary>
        /// API 호출 성공 여부
        /// </summary>
        public bool success;

        /// <summary>
        /// 입장한 방 ID
        /// </summary>
        public int matchId;

        /// <summary>
        /// Mirror 서버 IP 주소
        /// </summary>
        public string serverIp;

        /// <summary>
        /// Mirror 서버 포트 번호
        /// </summary>
        public int serverPort;

        /// <summary>
        /// 사용자 역할 ("HOST" 또는 "GUEST")
        /// </summary>
        public string role;

        /// <summary>
        /// 방 상태
        /// </summary>
        public string status;

        /// <summary>
        /// 서버 응답 메시지
        /// </summary>
        public string message;
    }

    /// <summary>
    /// 공통 에러 응답 데이터
    /// </summary>
    [System.Serializable]
    public class ErrorApiResponse
    {
        /// <summary>
        /// API 호출 성공 여부 (에러 시 false)
        /// </summary>
        public bool success;

        /// <summary>
        /// 에러 코드
        /// </summary>
        public string errorCode;

        /// <summary>
        /// 에러 메시지
        /// </summary>
        public string message;
    }

    #endregion

    #region API Request Classes

    /// <summary>
    /// 방 생성 API 요청 데이터
    /// </summary>
    [System.Serializable]
    public class CreateMatchApiRequest
    {
        /// <summary>
        /// 방 제목
        /// </summary>
        public string title;

        /// <summary>
        /// 비공개 방 여부
        /// </summary>
        public bool isPrivate;

        /// <summary>
        /// 방 비밀번호 (비공개 방인 경우에만 설정)
        /// </summary>
        public string password;
    }

    /// <summary>
    /// 방 입장 API 요청 데이터
    /// </summary>
    [System.Serializable]
    public class JoinMatchApiRequest
    {
        /// <summary>
        /// 방 비밀번호 (비공개 방 입장 시 필요)
        /// </summary>
        public string password;
    }

    #endregion

    #region Constants

    /// <summary>
    /// 방 상태 상수 정의
    /// </summary>
    public static class MatchStatus
    {
        /// <summary>
        /// 대기 중 상태
        /// </summary>
        public const string Waiting = "WAITING";

        /// <summary>
        /// 게임 진행 중 상태
        /// </summary>
        public const string Playing = "PLAYING";

        /// <summary>
        /// 게임 종료 상태
        /// </summary>
        public const string Ended = "ENDED";
    }

    /// <summary>
    /// 사용자 역할 상수 정의
    /// </summary>
    public static class UserRole
    {
        /// <summary>
        /// 방장 역할
        /// </summary>
        public const string Host = "HOST";

        /// <summary>
        /// 게스트 역할
        /// </summary>
        public const string Guest = "GUEST";
    }

    /// <summary>
    /// 에러 코드 상수 정의
    /// </summary>
    public static class ErrorCode
    {
        /// <summary>
        /// 잘못된 비밀번호 에러
        /// </summary>
        public const string InvalidPassword = "INVALID_PASSWORD";

        /// <summary>
        /// 방이 가득 참 에러
        /// </summary>
        public const string RoomFull = "ROOM_FULL";

        /// <summary>
        /// 방을 찾을 수 없음 에러
        /// </summary>
        public const string RoomNotFound = "ROOM_NOT_FOUND";
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 유틸리티 메서드 클래스
    /// </summary>
    public static class RoomUtils
    {
        /// <summary>
        /// 방 상태를 한국어로 변환
        /// </summary>
        /// <param name="status">영문 방 상태</param>
        /// <returns>한국어 방 상태</returns>
        public static string GetStatusDisplayText(string status)
        {
            switch (status)
            {
                case MatchStatus.Waiting:
                    return "대기 중";
                case MatchStatus.Playing:
                    return "게임 중";
                case MatchStatus.Ended:
                    return "종료됨";
                default:
                    return "알 수 없음";
            }
        }

        /// <summary>
        /// 방 입장 가능 여부 확인
        /// </summary>
        /// <param name="status">방 상태</param>
        /// <returns>입장 가능하면 true</returns>
        public static bool CanJoinRoom(string status)
        {
            return status == MatchStatus.Waiting;
        }
    }

    #endregion
}