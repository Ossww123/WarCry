using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

[Serializable]
public class VoiceCommand
{
    public int matchId;
    public int teamId;
    public string infantry;
    public string direction;
    public string target;
}

public class CommandProcessor : MonoBehaviour
{
    [Header("Match & Team Settings")]
    public int matchId;   // WaitingRoomManager 등에서 동적으로 할당
    public int teamId;    // TeamIndex enum 기반 (0 = Left, 1 = Right)

    [Header("Debug")]
    public bool showDebugLogs = true;

    [Header("Unit Name Mapping")]
    [SerializeField]
    private Dictionary<string, string> unitNameMapping = new Dictionary<string, string>()
    {
        {"infantry", "Infantry"},  // 보병 → Infantry
        {"bowman", "Archer"},      // 궁병 → Archer
        {"cavalry", "Cavalry"},    // 기병 → Cavalry  
        {"mage", "Wizard"}         // 마법사 → Wizard
    };

    private InfantryController[] infantryControllers;

    // ID 값 변경 모니터링을 위한 이전 값 저장
    private int _prevMatchId;
    private int _prevTeamId;

    // 명령 처리 결과 피드백
    private float lastCommandTime;
    private const float COMMAND_COOLDOWN = 0.5f; // 명령 간 최소 간격

    void Awake()
    {
        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] Awake: {gameObject.name}");

        // Dictionary 초기화 (Inspector에서 설정이 안 될 수 있으므로)
        InitializeUnitNameMapping();
    }

    private void InitializeUnitNameMapping()
    {
        if (unitNameMapping == null || unitNameMapping.Count == 0)
        {
            unitNameMapping = new Dictionary<string, string>()
            {
                {"infantry", "Infantry"},  // 보병 → Infantry
                {"bowman", "Archer"},      // 궁병 → Archer
                {"cavalry", "Cavalry"},    // 기병 → Cavalry  
                {"mage", "Wizard"}         // 마법사 → Wizard
            };
        }

        if (showDebugLogs)
        {
            Debug.Log("[CommandProcessor] 유닛 이름 매핑 초기화:");
            foreach (var mapping in unitNameMapping)
            {
                Debug.Log($"  {mapping.Key} → {mapping.Value}");
            }
        }
    }

    // 유닛 이름 매핑 메서드 (대소문자 구별 없이)
    private string MapUnitName(string originalName)
    {
        if (string.IsNullOrEmpty(originalName))
            return originalName;

        string lowerName = originalName.ToLower();
        if (unitNameMapping.ContainsKey(lowerName))
        {
            string mappedName = unitNameMapping[lowerName];
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 유닛 이름 매핑: {originalName} → {mappedName}");
            return mappedName;
        }

        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] 매핑되지 않은 유닛 이름: {originalName} (그대로 사용)");
        return originalName;
    }

    void Start()
    {
        // 초기화
        RefreshInfantryControllers();

        // 방 번호 초기화
        InitializeMatchId();

        // 팀 정보 초기화
        InitializeTeamId();

        // 현재 값을 이전 값으로 저장 (변경 감지용)
        _prevMatchId = matchId;
        _prevTeamId = teamId;

        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] 초기화 완료 - match:{matchId}, team:{teamId}, Infantries:{infantryControllers?.Length ?? 0}");
    }

    void OnEnable()
    {
        // 씬 전환 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // 씬 전환 이벤트 구독 해제
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] 씬 전환 감지: {scene.name}");
        // 씬 전환 후 지연 시간을 두고 초기화
        StartCoroutine(DelayedInitialization());
    }

    private IEnumerator DelayedInitialization()
    {
        // 다른 객체들이 초기화될 시간을 줌
        yield return new WaitForSeconds(1.0f);

        if (showDebugLogs)
            Debug.Log("[CommandProcessor] 씬 전환 후 지연 초기화 시작");

        // 방 번호 초기화
        InitializeMatchId();

        // 팀 정보 초기화
        InitializeTeamId();

        // InfantryController 다시 찾기
        RefreshInfantryControllers();

        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] 씬 전환 후 재초기화 완료 - match:{matchId}, team:{teamId}, Infantries:{infantryControllers?.Length ?? 0}");
    }

    void Update()
    {
        // ID 값이 변경되었는지 확인하고 로그 출력
        if ((matchId != _prevMatchId || teamId != _prevTeamId) && showDebugLogs)
        {
            Debug.Log($"[CommandProcessor] ID 값 변경 감지: match:{_prevMatchId}→{matchId}, team:{_prevTeamId}→{teamId}");
            _prevMatchId = matchId;
            _prevTeamId = teamId;
        }

        // 필요 시 InfantryController 배열 갱신 - 주기적으로 체크
        if (infantryControllers == null || infantryControllers.Length == 0)
        {
            RefreshInfantryControllers();
        }
    }

    private void InitializeMatchId()
    {
        // 방법 1: PlayerPrefs에서 직접 불러오기
        int savedMatchId = PlayerPrefs.GetInt("CurrentRoomId", -1);
        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] PlayerPrefs의 CurrentRoomId 값: {savedMatchId}");

        if (savedMatchId != -1)
        {
            matchId = savedMatchId;
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] PlayerPrefs에서 방 ID 초기화: {matchId}");
            return;
        }

        // 방법 2: WaitingRoomManager에서 가져오기
        WaitingRoomManager roomManager = FindObjectOfType<WaitingRoomManager>();
        if (roomManager != null)
        {
            matchId = roomManager.RoomId;
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] WaitingRoomManager에서 방 ID 초기화: {matchId}");
            return;
        }

        // 기본값 999 사용 (Python 코드의 기본값과 일치)
        if (matchId <= 0)
        {
            matchId = 999;
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 방 ID를 기본값({matchId})으로 설정");
        }
    }

    private void InitializeTeamId()
    {
        // 모든 PlayerInfo 객체 로깅
        PlayerInfo[] allPlayerInfos = FindObjectsOfType<PlayerInfo>();
        if (showDebugLogs)
        {
            Debug.Log($"[CommandProcessor] 발견된 PlayerInfo 객체 수: {allPlayerInfos.Length}");
            foreach (var playerInfo in allPlayerInfos)
            {
                Debug.Log($"[CommandProcessor] PlayerInfo: name={playerInfo.name}, isLocalPlayer={playerInfo.isLocalPlayer}, teamId={playerInfo.teamId}");
            }
        }

        // 방법 1: 로컬 플레이어의 PlayerInfo 객체 찾기
        PlayerInfo localPlayerInfo = null;

        // 현재 플레이어의 PlayerInfo 찾기
        foreach (var player in allPlayerInfos)
        {
            if (player.isLocalPlayer)
            {
                localPlayerInfo = player;
                break;
            }
        }

        if (localPlayerInfo != null)
        {
            teamId = (int)localPlayerInfo.teamId;
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 로컬 PlayerInfo에서 팀 ID 초기화: {localPlayerInfo.teamId} → {teamId}");
            return;
        }

        // 방법 2: DontDestroyOnLoad 씬에서 찾기
        GameObject playerObject = GameObject.Find("DontDestroyOnLoad/PlayerPrefab(Clone)");
        if (playerObject != null)
        {
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] DontDestroyOnLoad/PlayerPrefab(Clone) 발견됨");

            PlayerInfo playerInfo = playerObject.GetComponent<PlayerInfo>();
            if (playerInfo != null)
            {
                teamId = (int)playerInfo.teamId;
                if (showDebugLogs)
                    Debug.Log($"[CommandProcessor] DontDestroyOnLoad에서 팀 ID 초기화: {playerInfo.teamId} → {teamId}");
                return;
            }
        }

        // 기본값 0 사용 (Python 코드의 기본값과 일치)
        if (showDebugLogs)
            Debug.Log("[CommandProcessor] 플레이어 정보를 찾을 수 없어 팀 ID를 기본값(0)으로 설정");
        teamId = 0;
    }

    // TeamIndex 값이 변경될 때 호출될 수 있는 이벤트 구독 메서드
    public void OnTeamChanged(TeamIndex newTeamIndex)
    {
        int newTeamId = (int)newTeamIndex;
        if (teamId != newTeamId)
        {
            teamId = newTeamId;
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 팀 ID 업데이트됨: {teamId}");
        }
    }

    // 수동으로 ID를 설정하는 메서드 (외부 스크립트에서 호출 가능)
    public void SetMatchAndTeamId(int newMatchId, int newTeamId)
    {
        if (matchId != newMatchId || teamId != newTeamId)
        {
            matchId = newMatchId;
            teamId = newTeamId;
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] ID 수동 설정: match:{matchId}, team:{teamId}");
        }
    }

    // ProcessCommand 메서드 수정 - 명령 처리 개선
    public void ProcessCommand(string jsonCommand)
    {
        // 명령 쿨다운 체크
        if (Time.time - lastCommandTime < COMMAND_COOLDOWN)
        {
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 명령 쿨다운 중 - 무시됨");
            return;
        }

        VoiceCommand cmd;
        try
        {
            cmd = JsonUtility.FromJson<VoiceCommand>(jsonCommand);
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 명령 파싱 성공: matchId={cmd.matchId}, teamId={cmd.teamId}, infantry={cmd.infantry}, direction={cmd.direction}, target={cmd.target}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CommandProcessor] JSON 파싱 오류: {e.Message} → 원본: {jsonCommand}");
            return;
        }

        // 방·팀 필터링
        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] 명령 필터링 체크: 명령({cmd.matchId}, {cmd.teamId}) vs 현재({matchId}, {teamId})");

        if (cmd.matchId != matchId || cmd.teamId != teamId)
        {
            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 필터링됨: 매치/팀 불일치");
            return;
        }

        // 유닛 이름 매핑 적용
        string mappedInfantry = MapUnitName(cmd.infantry);
        string mappedTarget = MapUnitName(cmd.target);

        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] 명령 실행 시작 → infantry:'{mappedInfantry}' (원본: '{cmd.infantry}'), direction:'{cmd.direction}', target:'{mappedTarget}' (원본: '{cmd.target}')");

        // InfantryController 유효성 체크
        if (infantryControllers == null || infantryControllers.Length == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[CommandProcessor] InfantryController가 없거나 초기화되지 않음 - 재검색 시도");
            RefreshInfantryControllers();
        }

        if (infantryControllers == null || infantryControllers.Length == 0)
        {
            Debug.LogError($"[CommandProcessor] InfantryController를 찾을 수 없음 - 명령 실행 불가");
            return;
        }

        // 명령 적용
        bool commandApplied = false;
        int validControllers = 0;
        int teamMatchControllers = 0;
        int nameMatchControllers = 0;

        foreach (var inf in infantryControllers)
        {
            if (inf == null)
                continue;

            validControllers++;

            // 유닛의 팀 ID 확인 (Unit 컴포넌트 참조)
            Unit unitInfo = inf.GetComponent<Unit>();
            if (unitInfo != null)
            {
                if (showDebugLogs)
                    Debug.Log($"[CommandProcessor] 유닛 검사: {inf.gameObject.name}, unitTeam={unitInfo.teamIndex}, 명령팀={cmd.teamId}");

                // 다른 팀 유닛이면 건너뛰기
                if ((int)unitInfo.teamIndex != cmd.teamId)
                {
                    continue;
                }
                teamMatchControllers++;
            }

            // 유닛 이름 매칭 (대소문자 구별 없이, Contains 사용)
            bool nameMatches = string.IsNullOrEmpty(mappedInfantry) ||
                              inf.gameObject.name.ToLower().Contains(mappedInfantry.ToLower());

            if (showDebugLogs)
                Debug.Log($"[CommandProcessor] 이름 매칭 체크: {inf.gameObject.name} vs {mappedInfantry} → {nameMatches}");

            if (nameMatches)
            {
                nameMatchControllers++;

                // 네트워크 명령 전송
                try
                {
                    inf.CmdApplyCommand(cmd.direction, mappedTarget);
                    commandApplied = true;
                    if (showDebugLogs)
                        Debug.Log($"[CommandProcessor] ✓ 명령 전송 성공: {inf.gameObject.name}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CommandProcessor] 명령 전송 실패: {inf.gameObject.name} → {e.Message}");
                }
            }
        }

        // 명령 처리 결과 로그
        if (showDebugLogs)
        {
            Debug.Log($"[CommandProcessor] 명령 처리 결과:");
            Debug.Log($"  - 전체 컨트롤러: {infantryControllers.Length}");
            Debug.Log($"  - 유효한 컨트롤러: {validControllers}");
            Debug.Log($"  - 팀 일치 컨트롤러: {teamMatchControllers}");
            Debug.Log($"  - 이름 일치 컨트롤러: {nameMatchControllers}");
            Debug.Log($"  - 명령 적용 여부: {commandApplied}");
        }

        if (!commandApplied)
        {
            Debug.LogWarning($"[CommandProcessor] ⚠️ 어떤 InfantryController에도 명령이 적용되지 않음");
            Debug.LogWarning($"  원본 infantry: '{cmd.infantry}' → 매핑: '{mappedInfantry}'");
            Debug.LogWarning($"  사용 가능한 유닛들:");
            foreach (var inf in infantryControllers)
            {
                if (inf != null)
                {
                    var unit = inf.GetComponent<Unit>();
                    Debug.LogWarning($"    - {inf.gameObject.name} (팀: {(unit ? unit.teamIndex.ToString() : "Unknown")})");
                }
            }
        }
        else
        {
            lastCommandTime = Time.time; // 성공한 명령만 쿨다운 적용
        }
    }

    // InfantryController 배열 갱신 메서드
    public void RefreshInfantryControllers()
    {
        infantryControllers = FindObjectsOfType<InfantryController>();
        if (showDebugLogs)
            Debug.Log($"[CommandProcessor] InfantryController 배열 갱신: {infantryControllers?.Length ?? 0}개 발견");

        // 발견된 컨트롤러들 정보 출력
        if (showDebugLogs && infantryControllers != null)
        {
            foreach (var controller in infantryControllers)
            {
                if (controller != null)
                {
                    var unit = controller.GetComponent<Unit>();
                    Debug.Log($"  - {controller.gameObject.name} (팀: {(unit ? unit.teamIndex.ToString() : "Unknown")})");
                }
            }
        }
    }

    // 유닛 매핑 정보 확인용 메서드 (디버그용)
    [ContextMenu("Show Unit Mapping")]
    public void ShowUnitMapping()
    {
        Debug.Log("[CommandProcessor] 현재 유닛 매핑:");
        foreach (var mapping in unitNameMapping)
        {
            Debug.Log($"  {mapping.Key} → {mapping.Value}");
        }
    }

    // 현재 상태 확인용 메서드 (디버그용)
    [ContextMenu("Show Current Status")]
    public void ShowCurrentStatus()
    {
        Debug.Log($"[CommandProcessor] 현재 상태:");
        Debug.Log($"  - Match ID: {matchId}");
        Debug.Log($"  - Team ID: {teamId}");
        Debug.Log($"  - InfantryController 수: {infantryControllers?.Length ?? 0}");
        Debug.Log($"  - 마지막 명령 시간: {lastCommandTime}");

        if (infantryControllers != null)
        {
            Debug.Log($"  - 유닛 목록:");
            foreach (var controller in infantryControllers)
            {
                if (controller != null)
                {
                    var unit = controller.GetComponent<Unit>();
                    Debug.Log($"    * {controller.gameObject.name} (팀: {(unit ? unit.teamIndex.ToString() : "Unknown")})");
                }
            }
        }
    }

    // 테스트용 명령 실행 메서드
    [ContextMenu("Test Command")]
    public void TestCommand()
    {
        var testCmd = new VoiceCommand
        {
            matchId = this.matchId,
            teamId = this.teamId,
            infantry = "infantry",
            direction = "앞",
            target = ""
        };

        string json = JsonUtility.ToJson(testCmd);
        Debug.Log($"[CommandProcessor] 테스트 명령 실행: {json}");
        ProcessCommand(json);
    }
}