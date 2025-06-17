using System.Diagnostics;
using UnityEngine;

public class VoiceCommandLauncher : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private string pythonExePath = "voice_command.exe";
    [SerializeField] private string apiKeyPrefsKey = "OpenAI_API_Key";

    [Header("API 키 (테스트용)")]
    [SerializeField] private string tempApiKey = ""; // 여기에 임시로 입력

    private Process voiceCommandProcess = null;
    private string apiKey = "";

    private void Awake()
    {
        // 싱글톤 패턴 적용 (선택사항)
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 저장된 API 키 로드
        apiKey = PlayerPrefs.GetString(apiKeyPrefsKey, "");

        // 임시 API 키가 입력되어 있으면 PlayerPrefs에 저장
        if (!string.IsNullOrEmpty(tempApiKey))
        {
            apiKey = tempApiKey;
            PlayerPrefs.SetString(apiKeyPrefsKey, apiKey);
            PlayerPrefs.Save();
            UnityEngine.Debug.Log($"API 키가 PlayerPrefs에 저장되었습니다. 길이: {apiKey.Length}");
        }
    }

    public void LaunchVoiceCommand()
    {
        UnityEngine.Debug.Log("LaunchVoiceCommand 메서드 시작");

        // API 키 확인 (PlayerPrefs에서)
        apiKey = PlayerPrefs.GetString(apiKeyPrefsKey, "");
        if (string.IsNullOrEmpty(apiKey))
        {
            UnityEngine.Debug.LogError($"API 키가 설정되지 않았습니다. Inspector의 Temp Api Key 필드에 API 키를 입력하세요.");
            return;
        }

        UnityEngine.Debug.Log($"API 키 확인됨. 길이: {apiKey.Length}");

        // 경로 확인
        if (string.IsNullOrEmpty(pythonExePath))
        {
            UnityEngine.Debug.LogError("Python EXE 경로가 설정되지 않았습니다.");
            return;
        }

        UnityEngine.Debug.Log($"사용할 Python EXE 경로: {pythonExePath}");

        // 기존 프로세스가 실행 중이면 종료
        if (voiceCommandProcess != null && !voiceCommandProcess.HasExited)
        {
            UnityEngine.Debug.Log("기존 프로세스 종료 중...");
            voiceCommandProcess.Kill();
            voiceCommandProcess = null;
        }

        // 현재 방 ID 가져오기
        int matchId = GetCurrentMatchId();

        // 현재 팀 ID 가져오기
        int teamId = GetCurrentTeamId();

        UnityEngine.Debug.Log($"음성 명령 실행: matchId={matchId}, teamId={teamId}, API 키 길이={apiKey.Length}");

        try
        {
            // ProcessStartInfo 설정
            ProcessStartInfo startInfo = new ProcessStartInfo();

            // Windows에서 실행 (cmd.exe 사용)
            startInfo.FileName = "cmd.exe";
            string arguments = $"/c set MATCH_ID={matchId} && set TEAM_ID={teamId} && set OPENAI_API_KEY={apiKey} && start {pythonExePath}";
            startInfo.Arguments = arguments;
            startInfo.CreateNoWindow = false; // 창 보이기 (디버깅용)

            UnityEngine.Debug.Log($"실행할 명령: cmd.exe {arguments}");

            // 실행
            voiceCommandProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("프로세스 시작됨");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"프로세스 시작 오류: {e.Message}\n{e.StackTrace}");
        }
    }

    // 현재 방 ID 가져오기
    private int GetCurrentMatchId()
    {
        // 방법 1: PlayerPrefs에서 가져오기
        int savedMatchId = PlayerPrefs.GetInt("CurrentRoomId", -1);
        if (savedMatchId != -1)
        {
            UnityEngine.Debug.Log($"PlayerPrefs에서 방 ID 가져옴: {savedMatchId}");
            return savedMatchId;
        }

        // 방법 2: WaitingRoomManager에서 가져오기
        WaitingRoomManager roomManager = FindObjectOfType<WaitingRoomManager>();
        if (roomManager != null)
        {
            UnityEngine.Debug.Log($"WaitingRoomManager에서 방 ID 가져옴: {roomManager.RoomId}");
            return roomManager.RoomId;
        }

        // 기본값
        UnityEngine.Debug.Log("기본 방 ID 사용: 999");
        return 999;
    }

    // 현재 팀 ID 가져오기
    private int GetCurrentTeamId()
    {
        // 로컬 플레이어의 PlayerInfo 찾기
        PlayerInfo[] players = FindObjectsOfType<PlayerInfo>();
        foreach (var player in players)
        {
            if (player.isLocalPlayer)
            {
                UnityEngine.Debug.Log($"로컬 PlayerInfo에서 팀 ID 가져옴: {(int)player.teamId}");
                return (int)player.teamId;
            }
        }

        // DontDestroyOnLoad에서 찾기
        GameObject playerObject = GameObject.Find("DontDestroyOnLoad/PlayerPrefab(Clone)");
        if (playerObject != null)
        {
            PlayerInfo playerInfo = playerObject.GetComponent<PlayerInfo>();
            if (playerInfo != null)
            {
                UnityEngine.Debug.Log($"DontDestroyOnLoad에서 팀 ID 가져옴: {(int)playerInfo.teamId}");
                return (int)playerInfo.teamId;
            }
        }

        // 기본값
        UnityEngine.Debug.Log("기본 팀 ID 사용: 0");
        return 0;
    }

    // 게임 종료 시 프로세스 정리
    private void OnApplicationQuit()
    {
        if (voiceCommandProcess != null && !voiceCommandProcess.HasExited)
        {
            try
            {
                voiceCommandProcess.Kill();
                voiceCommandProcess = null;
                UnityEngine.Debug.Log("음성 명령 프로세스 종료됨");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"프로세스 종료 중 오류: {e.Message}");
            }
        }
    }
}