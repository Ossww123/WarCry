using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// The BattleController class manages and coordinates the battle logic within a multiplayer networked game.
/// It is responsible for initiating battles on the server, handling unit activation, and propagating
/// battle start events to all connected clients.
/// </summary>
public class BattleController : NetworkBehaviour
{
    [Header("Battle Settings")]
    [SerializeField] private float battleStartDelay = 3f; // 전투 시작 전 카운트다운

    [SyncVar(hook = nameof(OnBattleStateChanged))]
    private bool battleStarted = false;

    [Header("References")]
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BattleSceneManager sceneManager;

    // 전투 시작 시 호출할 이벤트
    public delegate void BattleStartEventHandler();
    public static event BattleStartEventHandler OnBattleStart;

    private bool countdownStarted = false;

    private void Awake()
    {
        // 이벤트 자동 연결
        OnBattleStart += ActivatePlayerUnits;
    }

    private void Start()
    {
        // BattleSceneManager가 없으면 찾기
        if (sceneManager == null)
        {
            sceneManager = FindObjectOfType<BattleSceneManager>();
        }
    }

    private void OnDestroy()
    {
        // 이벤트 해제 필수
        OnBattleStart -= ActivatePlayerUnits;
    }

    // 서버에서 전투 시작
    [Server]
    public void StartBattle()
    {
        if (battleStarted) return;

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 시작 신호 전송");
        battleStarted = true;

        // 전투 시작 RPC 호출
        RpcStartBattle();
    }

    // 모든 유닛의 AI 활성화 (Command 버전 - 클라이언트에서 호출 가능)
    [Command(requiresAuthority = false)]
    public void CmdActivateAllUnits()
    {
        ActivateAllUnits();
    }

    // 모든 유닛의 AI 활성화 (서버 전용)
    [Server]
    private void ActivateAllUnits()
    {
        InfantryController[] allUnits = FindObjectsByType<InfantryController>(FindObjectsSortMode.None);
        foreach (var unit in allUnits)
        {
            unit.EnableAI(true);
            Debug.Log($"[BattleController] 서버에서 {unit.name}의 AI 활성화 (팀: {unit.GetComponent<Unit>()?.teamIndex})");
        }
    }

    // 클라이언트에 전투 시작 알림
    [ClientRpc]
    private void RpcStartBattle()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 시작 신호 수신");

        // 중복 실행 방지
        if (countdownStarted) return;
        countdownStarted = true;

        // UI 업데이트
        if (uiManager != null)
        {
            uiManager.SetGameplayPhase();
        }

        // 전투 시작 이벤트 발생
        OnBattleStart?.Invoke();

        // 서버라면 모든 유닛의 AI 활성화
        if (isServer)
        {
            ActivateAllUnits();
        }
        // 클라이언트라면 서버에 AI 활성화 요청
        else if (isClient && !isServer)
        {
            CmdActivateAllUnits();
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투가 시작되었습니다!");
    }

    // 상태 변경 시 호출되는 훅
    private void OnBattleStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 전투 상태 변경: {oldValue} -> {newValue}");
    }

    // 유닛 활성화 메서드 - 이벤트에 자동 연결됨
    public void ActivatePlayerUnits()
    {
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerInfo>();
        if (localPlayer == null)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어를 찾을 수 없습니다.");
            return;
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 내 PlayerInfo: netId={localPlayer.netId}, teamId={localPlayer.teamId}");

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in allUnits)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 검사 중 유닛 netId={unit.netId}, ownerNetId={unit.ownerNetId}");

            if (unit.ownerNetId == localPlayer.netId)
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] ✅ 로컬 플레이어 소유 유닛: {unit.name}");

                var king = unit.GetComponent<KingController>();
                if (king != null)
                {
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] ▶ 왕 유닛 EnableControl(true) 호출");
                    king.EnableControl(true);
                }
            }
            else
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] ⛔ 다른 플레이어 유닛: {unit.name}");
            }
        }
    }
}