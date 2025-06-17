using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Mirror;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class ResultSceneInitializer : MonoBehaviour
{
    public TMP_Text resultText;
    public TMP_Text pointText;
    public TMP_Text tierText;
    public TMP_Text nicknameText;

    public GameObject victoryEffects;
    public GameObject defeatEffects;

    // AudioClip 대신 인덱스 사용
    [Header("Sound Effects")]
    [SerializeField] private int victorySoundIndex = 0; // 승리 효과음 인덱스
    [SerializeField] private int defeatSoundIndex = 1;  // 패배 효과음 인덱스

    private string jwt;
    private int matchId;
    private string role;
    private string result;

    private const string apiUrlBase = "https://k12d104.p.ssafy.io";

    void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log("[ResultSceneInitializer] Headless 모드에서는 비활성화됨");
            gameObject.SetActive(false);
            return;
        }
    }

    void Start()
    {
        jwt = AuthManager.Instance?.Token;
        nicknameText.text = AuthManager.Instance?.Nickname ?? "알 수 없음";

        matchId = PlayerPrefs.GetInt("CurrentMatchId");
        role = PlayerPrefs.GetString("CurrentUserRole", "GUEST");
        result = PlayerPrefs.GetString("MatchResult", "LOSE");

        if (result == "WIN")
        {
            resultText.text = "🏆 승리!";
            if (victoryEffects != null) victoryEffects.SetActive(true);
            if (defeatEffects != null) defeatEffects.SetActive(false);
            SoundManager.Instance?.PlaySFX(victorySoundIndex); // 승리 효과음
        }
        else
        {
            resultText.text = "😢 패배";
            if (victoryEffects != null) victoryEffects.SetActive(false);
            if (defeatEffects != null) defeatEffects.SetActive(true);
            SoundManager.Instance?.PlaySFX(defeatSoundIndex); // 패배 효과음
        }

        DisconnectFromMirror();

        // HOST만 결과를 서버에 전송
        if (role == "HOST")
        {
            Debug.Log("HOST가 매치 결과 전송");
            StartCoroutine(SendMatchResultToServer());
        }
        else
        {
            Debug.Log("GUEST는 결과 전송 생략, UI 표시만 진행");
            // HOST가 결과를 보낸 것으로 가정하고 점수 정보 진행
            // 필요하다면 API를 통해 결과 정보만 조회하는 코드 추가
        }
    }

    void DisconnectFromMirror()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost(); // StopClient도 포함됨
            Debug.Log("Mirror 연결 종료됨");
        }
    }

    IEnumerator SendMatchResultToServer()
    {
        string url = $"{apiUrlBase}/api/match/{matchId}/result";

        var otherRole = role == "HOST" ? "GUEST" : "HOST";
        var otherResult = result == "WIN" ? "LOSE" : "WIN";

        var requestData = new MatchResultRequest
        {
            results = new List<ResultData>
            {
                new ResultData { role = role, result = result },
                new ResultData { role = otherRole, result = otherResult }
            }
        };

        string json = JsonUtility.ToJson(requestData);
        var req = new UnityWebRequest(url, "POST");
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {jwt}");
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"결과 전송 실패: {req.error} | {req.downloadHandler.text}");
            pointText.text = "포인트 전송 실패";
            tierText.text = "";
            yield break;
        }

        MatchResultResponse response = JsonUtility.FromJson<MatchResultResponse>(req.downloadHandler.text);
        if (response.success)
        {
            var change = response.ratingChanges.Find(r => r.role == role);
            if (change != null)
            {
                StartCoroutine(AnimatePointChange(change.pointBefore, change.pointAfter));
                tierText.text = $"티어: {change.tierBefore} → {change.tierAfter}";
            }
        }
    }

    public void OnClickReturnToLobby()
    {
        SceneManager.LoadScene("RoomListScene");
    }

    // 포인트 변화를 애니메이션으로 표시하는 코루틴
    private IEnumerator AnimatePointChange(int pointBefore, int pointAfter, float duration = 1.5f)
    {
        float startTime = Time.time;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime = Time.time - startTime;
            float t = elapsedTime / duration;
            int currentPoints = Mathf.RoundToInt(Mathf.Lerp(pointBefore, pointAfter, t));
            pointText.text = $"포인트: {currentPoints}";
            yield return null;
        }

        pointText.text = $"포인트: {pointAfter}";
    }

    // API 요청 구조
    [System.Serializable]
    public class ResultData
    {
        public string role;
        public string result;
    }

    [System.Serializable]
    public class MatchResultRequest
    {
        public List<ResultData> results;
    }

    [System.Serializable]
    public class MatchResultResponse
    {
        public bool success;
        public int matchId;
        public string message;
        public List<RatingChange> ratingChanges;
    }

    [System.Serializable]
    public class RatingChange
    {
        public string role;
        public int pointBefore;
        public int pointAfter;
        public int pointChange;
        public int tierBefore;
        public int tierAfter;
    }
}
