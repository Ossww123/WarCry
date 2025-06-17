using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 멀티플레이어 게임에서 플레이어의 핵심 정보와 상태를 관리하는 NetworkBehaviour
/// 플레이어 이름, 색상, 준비상태, 역할, 팀 정보의 네트워크 동기화를 담당하며
/// 씬별 UI 초기화, 캐릭터 렌더링, 게임플레이 액션 처리 등의 기능을 통합 관리
/// </summary>
public class PlayerInfo : NetworkBehaviour
{
    #region Network Synchronized Variables

    [Header("Player State")]
    [SyncVar(hook = nameof(OnReadyStatusChanged))]
    public bool isReady = false;

    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName = "Player";

    [SyncVar(hook = nameof(OnPlayerPaletteChanged))]
    public Palettes playerPalette;

    [SyncVar(hook = nameof(OnPlayerRoleChanged))]
    public bool isHost = false;

    [SyncVar(hook = nameof(OnTeamChanged))]
    public TeamIndex teamId = TeamIndex.Unknown;

    [SyncVar(hook = nameof(OnBattleReadyChanged))]
    public bool isBattleReady = false;

    #endregion

    #region Inspector Fields

    [Header("Character Appearance")]
    [SerializeField] private Renderer characterRenderer;
    [SerializeField] private Material[] availableMaterials;

    #endregion

    #region Static Variables

    /// <summary>
    /// 승리자 선언 여부를 추적하는 정적 변수 (중복 승리 방지)
    /// </summary>
    private static bool isWinnerDeclared = false;

    #endregion

    #region Private Fields

    /// <summary>
    /// 마지막으로 초기화한 씬 이름 (중복 초기화 방지)
    /// </summary>
    private string lastInitializedScene = "";

    #endregion

    #region Unity Lifecycle & Network Events

    /// <summary>
    /// 씬 로드 시 정적 변수 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticVariables()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 정적 변수 초기화 - isWinnerDeclared = false");
        isWinnerDeclared = false;
    }

    /// <summary>
    /// 클라이언트 시작 시 호출 - DontDestroyOnLoad 설정
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 서버 시작 시 호출 - DontDestroyOnLoad 설정 및 색상 할당
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        DontDestroyOnLoad(gameObject);

        // 색상 할당 지연 (모든 초기화가 완료된 후)
        StartCoroutine(AssignColorAfterInitialization());
    }

    /// <summary>
    /// 로컬 플레이어 권한 시작 시 호출 - 닉네임 설정 및 역할 초기화
    /// </summary>
    public override void OnStartAuthority()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] OnStartAuthority 호출됨 → 나는 로컬 플레이어입니다. netId: {netId}");

        base.OnStartAuthority();

        InitializeLocalPlayer();
        RegisterSceneEvents();

        // 현재 씬이 이미 로드되어 있을 수 있으므로 직접 초기화 호출
        Scene currentScene = SceneManager.GetActiveScene();
        OnSceneLoaded(currentScene, LoadSceneMode.Single);
    }

    private void Start()
    {
        // playerPalette 기반 캐릭터 머티리얼 적용
        ApplyMaterial(playerPalette);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region Local Player Initialization

    /// <summary>
    /// 로컬 플레이어 초기 설정
    /// </summary>
    private void InitializeLocalPlayer()
    {
        // AuthManager의 닉네임 가져와서 서버에 전송
        string nickname = AuthManager.Instance?.Nickname ?? "익명";
        CmdSetPlayerName(nickname);
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 닉네임 설정 요청: {nickname}");

        // PlayerPrefs에서 역할 정보 가져와 설정
        string role = PlayerPrefs.GetString("CurrentUserRole", "GUEST");
        bool shouldBeHost = (role == "HOST");

        // 서버에 역할 정보 전송
        CmdSetAsHost(shouldBeHost);
    }

    /// <summary>
    /// 씬 이벤트 등록
    /// </summary>
    private void RegisterSceneEvents()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    #endregion

    #region Color Assignment System

    /// <summary>
    /// 초기화 완료 후 색상 자동 할당
    /// </summary>
    private IEnumerator AssignColorAfterInitialization()
    {
        // 잠시 대기하여 모든 초기화가 완료되도록 함
        yield return new WaitForEndOfFrame();

        // 자동 색상 할당
        Palettes assignedColor = FindAvailableColorIndex();
        playerPalette = assignedColor;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] {playerName}(netId: {netId})에게 자동 색상 {assignedColor} 할당");
    }

    /// <summary>
    /// 사용 가능한 색상 인덱스 찾기
    /// </summary>
    /// <returns>사용 가능한 첫 번째 색상</returns>
    private Palettes FindAvailableColorIndex()
    {
        var usedIndices = GetUsedColorIndices();

        // 디버그 로깅
        string usedColorStr = string.Join(", ", usedIndices);
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 사용 중인 색상 인덱스: [{usedColorStr}]");

        // 가장 낮은 사용 가능한 인덱스 반환
        for (int i = 0; i < availableMaterials.Length; i++)
        {
            if (!usedIndices.Contains(i))
            {
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 사용 가능한 첫 번째 색상 인덱스: {i}");
                return (Palettes)i;
            }
        }

        // 모든 색상이 사용 중이면 0
        Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 모든 색상이 사용 중입니다. 기본값(0) 반환");
        return (Palettes)0;
    }

    /// <summary>
    /// 현재 사용 중인 색상 인덱스 목록 가져오기
    /// </summary>
    /// <returns>사용 중인 색상 인덱스 집합</returns>
    private HashSet<int> GetUsedColorIndices()
    {
        var usedIndices = new HashSet<int>();

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null && conn.identity.netId != netId)
            {
                var other = conn.identity.GetComponent<PlayerInfo>();
                if (other != null && other != this)
                {
                    Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 플레이어 {other.playerName}(netId: {other.netId})가 색상 {other.playerPalette} 사용 중");
                    usedIndices.Add((int)other.playerPalette);
                }
            }
        }

        return usedIndices;
    }

    /// <summary>
    /// 색상이 사용 가능한지 확인
    /// </summary>
    /// <param name="colorIndex">확인할 색상 인덱스</param>
    /// <returns>사용 가능하면 true</returns>
    private bool IsColorAvailable(int colorIndex)
    {
        // 자신이 현재 사용 중인 색상이면 가능
        if ((int)playerPalette == colorIndex)
            return true;

        // 다른 플레이어가 사용 중인지 확인
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null && conn.identity.netId != netId)
            {
                var other = conn.identity.GetComponent<PlayerInfo>();
                if (other != null && (int)other.playerPalette == colorIndex)
                {
                    return false; // 다른 플레이어가 사용 중
                }
            }
        }

        return true; // 사용 가능
    }

    #endregion

    #region Scene Management & UI Initialization

    /// <summary>
    /// 씬 로드 후 UI 초기화 처리 (중복 호출 방지)
    /// </summary>
    /// <param name="scene">로드된 씬</param>
    /// <param name="mode">씬 로드 모드</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 로컬 플레이어가 아니거나 컴포넌트 비활성 상태일 경우 무시
        if (!isLocalPlayer || !isActiveAndEnabled)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] OnSceneLoaded 중단: 로컬 플레이어가 아니거나 비활성 상태 (scene: {scene.name})");
            return;
        }

        // 같은 씬에서 중복 초기화 방지
        if (scene.name == lastInitializedScene)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 씬 {scene.name} 이미 초기화됨, 중복 처리 방지");
            return;
        }

        lastInitializedScene = scene.name;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 씬 로드됨: {scene.name} → UI 매니저 초기화 시도");

        InitializeSceneSpecificUI(scene.name);
    }

    /// <summary>
    /// 씬별 UI 초기화 처리
    /// </summary>
    /// <param name="sceneName">씬 이름</param>
    private void InitializeSceneSpecificUI(string sceneName)
    {
        switch (sceneName)
        {
            case "WaitingRoomScene":
                InitializeWaitingRoomUI();
                break;

            case "BattleScene":
                InitializeBattleSceneUI();
                break;

            default:
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 특별한 초기화가 필요한 씬이 아님: {sceneName}");
                break;
        }
    }

    /// <summary>
    /// WaitingRoom 씬 UI 초기화
    /// </summary>
    private void InitializeWaitingRoomUI()
    {
        // WaitingRoomInitializer를 통해 초기화
        WaitingRoomInitializer initializer = FindObjectOfType<WaitingRoomInitializer>();
        if (initializer != null)
        {
            initializer.InitializePlayerUI(this);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] WaitingRoomInitializer에 UI 초기화 요청");
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] WaitingRoomInitializer를 찾을 수 없습니다!");

            // 페일세이프: 기존 방식으로 시도
            InitializeWaitingRoomUIFallback();
        }
    }

    /// <summary>
    /// WaitingRoom UI 초기화 폴백 처리
    /// </summary>
    private void InitializeWaitingRoomUIFallback()
    {
        WaitingRoomUIManager uiManager = FindObjectOfType<WaitingRoomUIManager>();
        if (uiManager != null)
        {
            StartCoroutine(DelayedInitializeUI(uiManager));
        }
    }

    /// <summary>
    /// Battle 씬 UI 초기화
    /// </summary>
    private void InitializeBattleSceneUI()
    {
        BattleUIManager battleUI = FindFirstObjectByType<BattleUIManager>();
        if (battleUI != null)
        {
            battleUI.InitializeForPlayer(this);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] BattleUIManager 초기화 완료");
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] BattleUIManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 지연된 UI 초기화 처리
    /// </summary>
    /// <param name="uiManager">초기화할 UI 매니저</param>
    private IEnumerator DelayedInitializeUI(WaitingRoomUIManager uiManager)
    {
        // UI 매니저 초기화를 위한 안전 지연
        yield return new WaitForSeconds(0.5f);

        if (uiManager != null)
        {
            uiManager.InitializeForPlayer(this);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 지연된 UI 초기화 완료");
        }
    }

    #endregion

    #region Character Appearance Management

    /// <summary>
    /// 플레이어 색상에 따른 머티리얼 적용
    /// </summary>
    /// <param name="palettes">적용할 색상 팔레트</param>
    private void ApplyMaterial(Palettes palettes)
    {
        if (!ValidateMaterialConfiguration())
            return;

        int materialIndex = GetValidMaterialIndex(palettes);
        ApplyMaterialToRenderers(materialIndex);
    }

    /// <summary>
    /// 머티리얼 설정 유효성 검증
    /// </summary>
    /// <returns>유효하면 true</returns>
    private bool ValidateMaterialConfiguration()
    {
        if (availableMaterials == null || availableMaterials.Length == 0)
        {
            Debug.LogError($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 사용 가능한 머티리얼이 없습니다!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 유효한 머티리얼 인덱스 가져오기
    /// </summary>
    /// <param name="palettes">색상 팔레트</param>
    /// <returns>유효한 머티리얼 인덱스</returns>
    private int GetValidMaterialIndex(Palettes palettes)
    {
        int materialIndex = (int)palettes;

        if (materialIndex < 0 || materialIndex >= availableMaterials.Length)
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 머티리얼 인덱스 {materialIndex}가 범위를 벗어났습니다. 기본값(0)으로 설정합니다.");
            materialIndex = 0;
        }

        return materialIndex;
    }

    /// <summary>
    /// 렌더러들에 머티리얼 적용
    /// </summary>
    /// <param name="materialIndex">적용할 머티리얼 인덱스</param>
    private void ApplyMaterialToRenderers(int materialIndex)
    {
        if (characterRenderer != null)
        {
            // 지정된 렌더러에 적용
            characterRenderer.material = availableMaterials[materialIndex];
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 캐릭터 '{playerName}'에 머티리얼 {materialIndex} 적용됨");
        }
        else
        {
            // 자식 렌더러에 모두 적용
            ApplyMaterialToChildRenderers(materialIndex);
        }
    }

    /// <summary>
    /// 자식 렌더러들에 머티리얼 적용
    /// </summary>
    /// <param name="materialIndex">적용할 머티리얼 인덱스</param>
    private void ApplyMaterialToChildRenderers(int materialIndex)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.material = availableMaterials[materialIndex];
            }
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 캐릭터 '{playerName}'의 자식 렌더러({renderers.Length}개)에 머티리얼 {materialIndex} 적용됨");
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 캐릭터 '{playerName}'에 렌더러를 찾을 수 없습니다!");
        }
    }

    #endregion

    #region Network Commands - Player State

    /// <summary>
    /// 플레이어 준비 상태 설정
    /// </summary>
    /// <param name="readyStatus">준비 상태</param>
    [Command]
    public void CmdSetReady(bool readyStatus)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [COMMAND] {playerName} → isReady 변경 요청: 현재={isReady}, 변경할 값={readyStatus}");

        bool changed = isReady != readyStatus;
        isReady = readyStatus;

        if (changed && isServer)
        {
            OnReadyStatusChanged(!readyStatus, readyStatus); // 서버에서 직접 호출
        }
    }

    /// <summary>
    /// 플레이어 이름 설정
    /// </summary>
    /// <param name="newName">새로운 이름</param>
    [Command]
    public void CmdSetPlayerName(string newName)
    {
        playerName = newName;
    }

    /// <summary>
    /// 호스트 역할 설정
    /// </summary>
    /// <param name="isHostRole">호스트 여부</param>
    [Command]
    public void CmdSetAsHost(bool isHostRole)
    {
        isHost = isHostRole;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] 플레이어 {playerName}의 호스트 역할이 {isHostRole}로 설정됨");
    }

    /// <summary>
    /// 팀 ID 설정
    /// </summary>
    /// <param name="newTeamId">새로운 팀 ID</param>
    [Command]
    public void CmdSetTeamId(TeamIndex newTeamId)
    {
        teamId = newTeamId;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] 플레이어 {playerName}의 팀 ID가 {teamId}로 설정됨");
    }

    /// <summary>
    /// 플레이어 색상 변경
    /// </summary>
    /// <param name="palette">새로운 색상 팔레트</param>
    [Command]
    public void CmdSetPlayerColor(Palettes palette)
    {
        int colorIndex = (int)palette;

        if (!ValidateColorChange(colorIndex))
            return;

        if (!IsColorAvailable(colorIndex))
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 플레이어 {playerName}의 색상 변경 요청 거부: 이미 사용 중인 색상 {palette}");
            RpcNotifyColorRejected();
            return;
        }

        bool changed = playerPalette != palette;
        playerPalette = palette;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 플레이어 {playerName}의 색상이 {palette}로 설정됨");

        // hook이 호출 안될 수 있으므로 명시적으로 실행
        if (changed && isServer)
        {
            OnPlayerPaletteChanged(palette, palette);
        }
    }

    /// <summary>
    /// 색상 변경 유효성 검증
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <returns>유효하면 true</returns>
    private bool ValidateColorChange(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= availableMaterials.Length)
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 잘못된 색상 인덱스: {colorIndex}");
            return false;
        }

        // 클라이언트가 자기 자신의 색상만 변경 가능
        //if (!isLocalPlayer)
        //{
        //    Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 플레이어 {playerName}의 색상 변경 요청 거부: 권한 없음");
        //    return false;
        //}

        return true;
    }

    #endregion

    #region Network Commands - Battle System

    /// <summary>
    /// 배틀 준비 상태 설정
    /// </summary>
    /// <param name="readyState">준비 상태</param>
    [Command]
    public void CmdSetBattleReady(bool readyState)
    {
        isBattleReady = readyState;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] {playerName}의 배틀 준비 상태 = {readyState}");

        // 배틀 준비 상태가 변경되면 GameNetworkManager에서 체크
        var netManager = NetworkManager.singleton as GameNetworkManager;
        netManager?.CheckAllBattleReady();
    }

    /// <summary>
    /// 유닛 배치 요청
    /// </summary>
    /// <param name="unitType">유닛 타입</param>
    /// <param name="position">배치 위치</param>
    [Command]
    public void CmdRequestUnitPlacement(UnitType unitType, Vector3 position)
    {
        // PlacementManager를 찾아서 서버에서 실행
        var placementManager = FindFirstObjectByType<PlacementManager>();
        if (placementManager != null)
        {
            placementManager.ServerPlaceUnit(this, unitType, position);
        }
    }

    /// <summary>
    /// 승리 선언 처리
    /// </summary>
    [Command]
    public void CmdDeclareVictory()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] {playerName}의 승리 선언 처리 중...");

        // BattleSceneManager를 통해 승리 처리
        BattleSceneManager battleManager = FindObjectOfType<BattleSceneManager>();
        if (battleManager != null)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] BattleSceneManager에 승리 정보 전달");
            battleManager.DeclareWinner(playerName);
        }
        else
        {
            HandleVictoryFallback();
        }
    }

    /// <summary>
    /// 승리 처리 폴백 (BattleSceneManager가 없는 경우)
    /// </summary>
    private void HandleVictoryFallback()
    {
        Debug.LogError($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] BattleSceneManager를 찾을 수 없습니다!");

        // 추가 디버그 정보
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 현재 씬: {SceneManager.GetActiveScene().name}");
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 활성화된 GameObject 수: {FindObjectsOfType<GameObject>().Length}");

        // 폴백: 기존 방식으로 처리
        if (isWinnerDeclared)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] 이미 승자가 선언되었습니다.");
            return;
        }

        ProcessVictoryFallback();
    }

    /// <summary>
    /// 폴백 승리 처리 실행
    /// </summary>
    private void ProcessVictoryFallback()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [서버] 승리 상태 설정 및 모든 클라이언트에 알림...");
        isWinnerDeclared = true;

        // 모든 클라이언트에 승리자 알림
        RpcShowWinner(playerName);

        // GameNetworkManager에게 씬 전환 위임
        GameNetworkManager networkManager = NetworkManager.singleton as GameNetworkManager;
        if (networkManager != null)
        {
            networkManager.ScheduleReturnToMainMenu(3f);
        }
    }

    #endregion

    #region Network RPC Methods

    /// <summary>
    /// 색상 변경 거부 알림
    /// </summary>
    [ClientRpc]
    private void RpcNotifyColorRejected()
    {
        // 로컬 플레이어에게만 처리
        if (!isLocalPlayer) return;

        Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 색상 변경이 거부되었습니다: 다른 플레이어가 이미 사용 중");

        // UI 매니저 찾아서 서버 색상으로 복원
        WaitingRoomUIManager uiManager = FindObjectOfType<WaitingRoomUIManager>();
        if (uiManager != null)
        {
            uiManager.UpdateColorSelection((int)playerPalette);
        }
    }

    /// <summary>
    /// 승리자 표시
    /// </summary>
    /// <param name="winner">승리자 이름</param>
    [ClientRpc]
    private void RpcShowWinner(string winner)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [클라이언트] {winner}의 승리 메시지 수신!");

        // 승리 UI 업데이트는 BattleUIManager에게 위임
        BattleUIManager battleUI = FindFirstObjectByType<BattleUIManager>();
        if (battleUI != null)
        {
            battleUI.ShowWinner(winner);
        }
        else
        {
            Debug.LogError($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [클라이언트] BattleUIManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 전투 시작 신호
    /// </summary>
    [ClientRpc]
    public void RpcStartBattle()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [클라이언트] {playerName} 전투 시작 신호 수신");

        BattleUIManager ui = FindFirstObjectByType<BattleUIManager>();
        ui?.SetGameplayPhase();

        // TODO: 유닛 AI 활성화 등 전투 로직 시작
    }

    /// <summary>
    /// 매치 결과 설정
    /// </summary>
    /// <param name="matchResult">매치 결과</param>
    [ClientRpc]
    public void RpcSetMatchResult(string matchResult)
    {
        if (!isLocalPlayer) return;

        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 매치 결과 수신: {matchResult}");

        // 플레이어 역할 (이미 SyncVar로 관리되고 있음)
        string role = isHost ? "HOST" : "GUEST";

        // PlayerPrefs에 결과 저장 (ResultScene에서 사용)
        PlayerPrefs.SetString("CurrentUserRole", role);
        PlayerPrefs.SetString("MatchResult", matchResult);

        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 매치 결과 PlayerPrefs에 저장됨");
    }

    #endregion

    #region SyncVar Hook Methods

    /// <summary>
    /// 준비 상태 변경 시 호출되는 훅
    /// </summary>
    /// <param name="oldValue">이전 값</param>
    /// <param name="newValue">새 값</param>
    void OnReadyStatusChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] [HOOK] {playerName}의 isReady 변경됨: {oldValue} → {newValue} | isServer={isServer}");

        if (isServer)
        {
            var netMan = NetworkManager.singleton as GameNetworkManager;
            PlayerManager.Instance?.SendPlayerListToAll();
            netMan?.CheckAllPlayersReadyAndStartGame();
        }
    }

    /// <summary>
    /// 플레이어 이름 변경 시 호출되는 훅
    /// </summary>
    /// <param name="oldValue">이전 이름</param>
    /// <param name="newValue">새 이름</param>
    void OnPlayerNameChanged(string oldValue, string newValue)
    {
        if (isServer)
        {
            PlayerManager.Instance?.SendPlayerListToAll();
        }
    }

    /// <summary>
    /// 플레이어 색상 변경 시 호출되는 훅
    /// </summary>
    /// <param name="oldPalette">이전 색상</param>
    /// <param name="newPalette">새 색상</param>
    void OnPlayerPaletteChanged(Palettes oldPalette, Palettes newPalette)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 플레이어 {playerName}의 색상이 {oldPalette}에서 {newPalette}로 변경됨");
        ApplyMaterial(newPalette);

        if (isServer)
        {
            var netMan = NetworkManager.singleton as GameNetworkManager;
            PlayerManager.Instance?.SendPlayerListToAll();
        }
    }

    /// <summary>
    /// 플레이어 역할 변경 시 호출되는 훅
    /// </summary>
    /// <param name="oldValue">이전 역할</param>
    /// <param name="newValue">새 역할</param>
    void OnPlayerRoleChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 플레이어 {playerName}의 역할 변경: HOST={newValue}");
        // 추가 처리 코드가 필요하면 여기에 구현
    }

    /// <summary>
    /// 팀 변경 시 호출되는 훅
    /// </summary>
    /// <param name="oldTeamId">이전 팀 ID</param>
    /// <param name="newTeamId">새 팀 ID</param>
    void OnTeamChanged(TeamIndex oldTeamId, TeamIndex newTeamId)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] {playerName}의 팀이 {oldTeamId}에서 {newTeamId}로 변경됨");
        // 머터리얼 적용 같은 후처리 여기서 가능
    }

    /// <summary>
    /// 배틀 준비 상태 변경 시 호출되는 훅
    /// </summary>
    /// <param name="oldValue">이전 준비 상태</param>
    /// <param name="newValue">새 준비 상태</param>
    void OnBattleReadyChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] {playerName}의 배틀 준비 상태가 {oldValue}에서 {newValue}로 변경됨");

        // UI 업데이트 등 추가 작업 가능
    }

    #endregion
}