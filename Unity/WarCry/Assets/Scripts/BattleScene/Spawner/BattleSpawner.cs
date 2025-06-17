using UnityEngine;
using Mirror;
using System;

/// <summary>
/// Handles the spawning and initialization of castles in a networked battle scene.
/// </summary>
public class BattleSpawner : NetworkBehaviour
{
    /// <summary>
    /// Prefab used for creating castle game objects in the networked battle scene.
    /// </summary>
    /// <remarks>
    /// This variable holds a reference to the castle prefab that will be instantiated and used
    /// to represent the castles for both players in the game. It is essential to assign a valid
    /// prefab in the Unity Inspector to ensure proper functionality.
    /// </remarks>
    [Header("Prefabs")]
    [SerializeField] private GameObject castlePrefab;

    /// <summary>
    /// Specifies the spawn position for Player 1's castle in the networked battle scene.
    /// </summary>
    /// <remarks>
    /// This variable determines the exact location where Player 1's castle will be instantiated
    /// during the initialization phase of the game. It is represented by a 3D vector
    /// (x, y, z) that defines the position in world space. Ensure this value aligns with the
    /// game design to maintain balanced gameplay and proper scene setup.
    /// </remarks>
    [Header("Spawn Positions")]
    [SerializeField] private Vector3 player1CastlePosition = new Vector3(-5, 0.5f, 0);

    /// <summary>
    /// Position in the game world where Player 2's castle will be spawned.
    /// </summary>
    /// <remarks>
    /// This variable defines the exact location for Player 2's castle within the networked battle scene.
    /// It is used during the initialization process to instantiate and position the castle for Player 2.
    /// Ensure this position is appropriately configured to maintain balance and gameplay alignment within the scene.
    /// </remarks>
    [SerializeField]
    private Vector3 player2CastlePosition = new Vector3(5, 0.5f, 0);

    /// <summary>
    /// Indicates whether the BattleSpawner operates in debug mode, enabling detailed logging for debugging purposes.
    /// </summary>
    /// <remarks>
    /// When set to true, this variable allows debug messages to be logged to the Unity console during the
    /// initialization and operation of the BattleSpawner. This can be useful for identifying issues
    /// or verifying behavior during development or testing. It should typically be disabled (false) for production builds
    /// to avoid unnecessary performance costs and console clutter.
    /// </remarks>
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true;

    // 성채 참조
    private GameObject player1Castle;
    private GameObject player2Castle;

    // 초기화 - 성채 생성
    public void Initialize()
    {
        DebugLog("초기화 시작");

        // 수정: 서버만 확인하도록 변경 (NetworkClient.active 체크 제거)
        if (isServer)
        {
            DebugLog("서버에서 성채 생성 시작");
            SpawnCastles();
        }
        else if (isClient)
        {
            DebugLog("클라이언트에서는 성채 생성을 기다립니다");
        }

        DebugLog("초기화 완료");
    }

    // 서버에서 성채 생성
    [Server]
    private void SpawnCastles()
    {
        if (castlePrefab == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 성채 프리팹이 지정되지 않았습니다!");
            return;
        }

        DebugLog("성채 생성 시작");
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 성채 프리팹 경로: {castlePrefab.name}, 활성화 상태: {castlePrefab.activeSelf}");

        // 플레이어 1 성채 생성
        player1Castle = Instantiate(castlePrefab, player1CastlePosition, Quaternion.Euler(0, 90, 0));
        player1Castle.name = "Castle_Player1";
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Castle_Player1 생성됨: {player1Castle != null}, 위치: {player1CastlePosition}");

        // 중요: 팀 인덱스 설정
        SetupCastleTeam(player1Castle, TeamIndex.Left);

        try
        {
            NetworkServer.Spawn(player1Castle);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Castle_Player1 네트워크 스폰 성공");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Castle_Player1 네트워크 스폰 실패: {e.Message}");
        }

        NetworkServer.Spawn(player1Castle);

        // 플레이어 2 성채 생성
        player2Castle = Instantiate(castlePrefab, player2CastlePosition, Quaternion.Euler(0, -90, 0));
        player2Castle.name = "Castle_Player2";

        // 중요: 팀 인덱스 설정
        SetupCastleTeam(player2Castle, TeamIndex.Right);

        NetworkServer.Spawn(player2Castle);

        DebugLog("성채 생성 완료");

        // 모든 클라이언트에게 성채 설정 요청
        RpcSetupCastleTags();
    }

    // 성채에 팀 인덱스 설정 (서버 전용)
    [Server]
    private void SetupCastleTeam(GameObject castle, TeamIndex teamIndex)
    {
        Unit unitComp = castle.GetComponent<Unit>();
        if (unitComp != null)
        {
            unitComp.teamIndex = teamIndex;
            DebugLog($"성채 '{castle.name}'에 팀 인덱스 {teamIndex} 설정");
            
            // Since I am now using TheOneAndOnlyStats class, and all stats are set up in prefab,
            // so no additional setups are necessary.
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 성채 '{castle.name}'에 Unit 컴포넌트가 없습니다!");
        }
    }

    // 모든 클라이언트에게 성채 태그 설정 요청
    [ClientRpc]
    private void RpcSetupCastleTags()
    {
        DebugLog("모든 클라이언트에서 성채 태그 설정 시작");

        // 로컬 플레이어 정보 가져오기
        PlayerInfo localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerInfo>();
        if (localPlayer == null)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어 정보를 찾을 수 없습니다.");
            return;
        }

        TeamIndex localPlayerTeamIndex = localPlayer.teamId;
        DebugLog($"로컬 플레이어 팀: {localPlayerTeamIndex}");

        // 모든 성채 찾기
        GameObject[] castles = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (var obj in castles)
        {
            if (obj.name.Contains("Castle"))
            {
                Unit unit = obj.GetComponent<Unit>();
                if (unit != null && unit.teamIndex != localPlayerTeamIndex)
                {
                    // 적 성채에 Enemy 태그 설정
                    obj.tag = "Enemy";
                    DebugLog($"적 성채 '{obj.name}'에 Enemy 태그 설정 완료");
                }
            }
        }
    }

    // 디버그 로그
    private void DebugLog(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {message}");
        }
    }

    // 테스트용 - Unity 에디터에서 버튼으로 작동
    [ContextMenu("Manually Setup Castle Tags")]
    public void ManuallySetupCastleTags()
    {
        if (isClient)
        {
            DebugLog("수동으로 성채 태그 설정 시작");

            // 모든 성채 찾기
            var castles = FindObjectsOfType<GameObject>();
            foreach (var obj in castles)
            {
                if (obj.name.Contains("Castle"))
                {
                    // 적 성채 인지 확인
                    Unit unit = obj.GetComponent<Unit>();
                    if (unit != null)
                    {
                        PlayerInfo localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerInfo>();
                        if (localPlayer != null && unit.teamIndex != localPlayer.teamId)
                        {
                            obj.tag = "Enemy";
                            DebugLog($"성채 '{obj.name}'에 Enemy 태그 설정됨 (수동)");
                        }
                    }
                }
            }
        }
    }
}