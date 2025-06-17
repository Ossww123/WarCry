using System;
using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using Random = UnityEngine.Random;
using TMPro;

/// <summary>
/// Manages the placement of units in the game, including selecting unit types,
/// performing unit placement, and transitioning to a battle-ready state.
/// </summary>
public class PlacementManager : NetworkBehaviour
{
    [Header("Unit Prefabs")]
    [SerializeField] private GameObject kingPrefab;
    [SerializeField] private GameObject infantryPrefab;
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject cavalryPrefab;
    [SerializeField] private GameObject wizardPrefab;

    [Header("Required Unit Counts")]
    [SerializeField] private int requiredKingCount = 1;
    [SerializeField] private int requiredInfantryCount = 5;
    [SerializeField] private int requiredArcherCount = 5;
    [SerializeField] private int requiredCavalryCount = 5;
    [SerializeField] private int requiredWizardCount = 5;

    [Header("Placement Settings")]
    [SerializeField] private float placementYOffset = 0.001f; // 지형 위 높이
    [SerializeField] private LayerMask groundLayer;        // 지형 레이어
    [SerializeField] private float maxPlacementDistance = 150f; // 최대 레이캐스트 거리

    [Header("Player Zone Settings")]
    [SerializeField] private float mapWidth = 300f; // 전체 맵의 x축 폭
    [SerializeField] private float areaWidth = 25f; // 각 영역의 x축 길이
    [SerializeField] private float edgeOffset = 60f; // 가장자리에서 얼마나 떨어뜨릴지
    [SerializeField] private GameObject leftZoneOverlay;  // Inspector에서 할당
    [SerializeField] private GameObject rightZoneOverlay; // Inspector에서 할당

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI kingRemainedText;
    [SerializeField] private TextMeshProUGUI infantryRemainedText;
    [SerializeField] private TextMeshProUGUI archerRemainedText;
    [SerializeField] private TextMeshProUGUI cavalryRemainedText;
    [SerializeField] private TextMeshProUGUI wizardRemainedText;

    [Header("References")]
    [SerializeField] private BattlePhaseController battlePhaseController;
    [SerializeField] private BattleSceneManager battleSceneManager;

    // 현재 선택된 유닛 타입
    private UnitType selectedUnitType = UnitType.None;

    // 유닛 배치 추적을 위한 딕셔너리
    private Dictionary<UnitType, List<GameObject>> placedUnits = new Dictionary<UnitType, List<GameObject>>();
    private Dictionary<UnitType, int> requiredUnitCounts = new Dictionary<UnitType, int>();

    // 로컬 플레이어 참조
    private PlayerInfo localPlayer;
    private bool isLeftSidePlayer; // true = 왼쪽(x < 0), false = 오른쪽(x > 0)

    // 유닛이 배치 가능한 상태인지
    private bool canPlaceUnits = true;

    // 텍스트 깜빡임 코루틴을 위한 딕셔너리
    private Dictionary<UnitType, Coroutine> flashCoroutines = new Dictionary<UnitType, Coroutine>();

    private IEnumerator Start()
    {
        // 필수 유닛 수량 초기화
        InitializeRequiredUnitCounts();

        // 유닛 리스트 초기화
        placedUnits[UnitType.King] = new List<GameObject>();
        placedUnits[UnitType.Infantry] = new List<GameObject>();
        placedUnits[UnitType.Archer] = new List<GameObject>();
        placedUnits[UnitType.Cavalry] = new List<GameObject>();
        placedUnits[UnitType.Wizard] = new List<GameObject>();

        // 서버면 여기서 중단
        if (isServer && !isClient)
        {
            yield break;
        }

        yield return new WaitUntil(() => NetworkClient.localPlayer != null);
        yield return new WaitForSeconds(0.1f); // 약간의 안정성 보강 지연

        localPlayer = NetworkClient.localPlayer.GetComponent<PlayerInfo>();

        if (localPlayer != null)
        {
            isLeftSidePlayer = localPlayer.teamId == TeamIndex.Left;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어({localPlayer.playerName})는 {(isLeftSidePlayer ? "왼쪽" : "오른쪽")} 진영입니다.");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어 정보를 찾을 수 없습니다!");
        }

        // BattleSceneManager 찾기
        if (battleSceneManager == null)
        {
            battleSceneManager = FindObjectOfType<BattleSceneManager>();
        }

        // UI 초기 업데이트
        UpdateAllRemainedTexts();

        // 배치 영역 오버레이 설정
        SetupZoneOverlays();

        // 이벤트 구독
        if (battleSceneManager != null)
        {
            BattleSceneManager.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (battleSceneManager != null)
        {
            BattleSceneManager.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(BattleSceneManager.BattlePhase phase)
    {
        // 배치 단계일 때만 오버레이 표시
        bool showOverlays = (phase == BattleSceneManager.BattlePhase.UnitPlacement);

        if (leftZoneOverlay != null)
        {
            leftZoneOverlay.SetActive(showOverlays);
        }

        if (rightZoneOverlay != null)
        {
            rightZoneOverlay.SetActive(showOverlays);
        }
    }

    private void SetupZoneOverlays()
    {
        if (leftZoneOverlay == null || rightZoneOverlay == null) return;

        // 왼쪽 플레이어 영역 설정
        float leftBoundary = -mapWidth / 2 + edgeOffset;
        leftZoneOverlay.transform.position = new Vector3(leftBoundary + areaWidth / 2, 0.05f, 0);

        // Plane의 기본 크기(10x10)를 고려한 스케일 계산
        float xScale = areaWidth / 10.0f;  // 25 / 10 = 2.5
        float zScale = 200 / 10.0f;        // 200 / 10 = 20

        leftZoneOverlay.transform.localScale = new Vector3(xScale, 1f, zScale);

        // 오른쪽 플레이어 영역 설정
        float rightBoundary = mapWidth / 2 - edgeOffset;
        rightZoneOverlay.transform.position = new Vector3(rightBoundary - areaWidth / 2, 0.05f, 0);
        rightZoneOverlay.transform.localScale = new Vector3(xScale, 1f, zScale);

        // 초기에는 표시 (Unit Placement 단계에서 시작)
        leftZoneOverlay.SetActive(true);
        rightZoneOverlay.SetActive(true);
    }

    private void InitializeRequiredUnitCounts()
    {
        requiredUnitCounts[UnitType.King] = requiredKingCount;
        requiredUnitCounts[UnitType.Infantry] = requiredInfantryCount;
        requiredUnitCounts[UnitType.Archer] = requiredArcherCount;
        requiredUnitCounts[UnitType.Cavalry] = requiredCavalryCount;
        requiredUnitCounts[UnitType.Wizard] = requiredWizardCount;
    }

    private void UpdateAllRemainedTexts()
    {
        if (!isClient) return; // 서버에서는 UI 업데이트 불필요

        UpdateRemainedText(UnitType.King, kingRemainedText);
        UpdateRemainedText(UnitType.Infantry, infantryRemainedText);
        UpdateRemainedText(UnitType.Archer, archerRemainedText);
        UpdateRemainedText(UnitType.Cavalry, cavalryRemainedText);
        UpdateRemainedText(UnitType.Wizard, wizardRemainedText);
    }

    private void UpdateRemainedText(UnitType unitType, TextMeshProUGUI textElement)
    {
        if (textElement == null) return;

        int required = GetRequiredUnitCount(unitType);
        int placed = GetPlacedUnitCount(unitType);
        int remaining = Math.Max(0, required - placed);

        if (required > 0)
        {
            textElement.text = $"x{remaining}";
            // 선택적: 다 배치했으면 색상 변경
            textElement.color = (remaining == 0) ? Color.green : Color.white;
        }
        else
        {
            textElement.text = "";
        }
    }

    void Update()
    {
        // 헤드리스 서버에서는 Update 로직 스킵
        if (isServer && !isClient) return;
        if (!canPlaceUnits || !isClient) return;

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[PlacementManager] 클릭 감지됨");

            if (selectedUnitType == UnitType.None)
            {
                Debug.LogWarning($"[PlacementManager] 선택된 유닛 타입이 없습니다!");
                return;
            }

            // 마우스 레이캐스트로 배치 위치 찾기
            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Debug.DrawRay(ray.origin, ray.direction * maxPlacementDistance, Color.red, 10f);

                if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, groundLayer))
                {
                    Vector3 placementPosition = hit.point + Vector3.up * placementYOffset;

                    // 플레이어 영역 체크 및 유닛 배치 로직 호출
                    ClientTryPlaceUnit(placementPosition);
                }
                else
                {
                    Debug.Log($"[PlacementManager] 레이캐스트 실패: 지형을 찾을 수 없음");
                }
            }
            else
            {
                Debug.LogError($"[PlacementManager] Camera.main이 null입니다!");
            }
        }

        // 숫자키로 유닛 선택 및 배치
        CheckKeyboardInput();
    }

    // 클라이언트에서만 호출되는 메서드
    private void ClientTryPlaceUnit(Vector3 placementPosition)
    {
        // 플레이어의 진영 영역 내에 있는지 확인
        if (!IsWithinPlayerZone(placementPosition))
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 진영 밖에는 배치할 수 없습니다!");
            return;
        }

        // 해당 유닛 타입이 최대 수량을 배치했는지 확인
        int placed = GetPlacedUnitCount(selectedUnitType);
        int required = GetRequiredUnitCount(selectedUnitType);

        if (placed < required)
        {
            // 서버에 배치 요청
            if (localPlayer != null)
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {selectedUnitType} 유닛 배치 요청 전송: 위치 {placementPosition}");
                localPlayer.CmdRequestUnitPlacement(selectedUnitType, placementPosition);
            }
            else
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] localPlayer가 null입니다!");
            }
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] {selectedUnitType} 유닛은 이미 모두 배치되었습니다.");
        }
    }

    /// <summary>
    /// 에디터에서만 보이는 유닛 배치 영역
    /// </summary>
    void OnDrawGizmos()
    {
        // 배치 영역 시각화 (에디터에서만 표시됨)
        Gizmos.color = Color.green;

        // 왼쪽 플레이어 영역 - 왼쪽 가장자리
        float leftBoundary = -mapWidth / 2 + edgeOffset;
        Gizmos.DrawCube(
            new Vector3(leftBoundary + areaWidth / 2, 1, 0),
            new Vector3(areaWidth, 2, 200)
        );

        // 오른쪽 플레이어 영역 - 오른쪽 가장자리
        float rightBoundary = mapWidth / 2 - edgeOffset;
        Gizmos.DrawCube(
            new Vector3(rightBoundary - areaWidth / 2, 1, 0),
            new Vector3(areaWidth, 2, 200)
        );
    }

    // 숫자키 입력 확인 및 처리
    private void CheckKeyboardInput()
    {
        // 1: 왕, 2: 보병, 3: 궁수, 4: 기병, 5: 마법사
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectAndPlaceUnit(UnitType.King);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectAndPlaceUnit(UnitType.Infantry);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SelectAndPlaceUnit(UnitType.Archer);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            SelectAndPlaceUnit(UnitType.Cavalry);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
        {
            SelectAndPlaceUnit(UnitType.Wizard);
        }
    }

    // 유닛 선택 및 배치를 한 번에 처리
    private void SelectAndPlaceUnit(UnitType unitType)
    {
        // 배치 가능한 유닛 수량 확인
        int placed = GetPlacedUnitCount(unitType);
        int required = GetRequiredUnitCount(unitType);

        if (placed >= required)
        {
            // 이미 최대 수량이 배치되었으면 안내 메시지 표시
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {unitType} 유닛은 이미 모두 배치되었습니다.");

            // 선택적: 화면에 피드백 표시 (예: UI 텍스트 깜빡임)
            FlashUnitCountText(unitType);
            return;
        }

        // 유닛 타입 선택
        selectedUnitType = unitType;

        // 선택적: UI에서 현재 선택된 버튼 시각적으로 표시 (하이라이트 등)
        HighlightSelectedButton(unitType);

        // 바로 배치 시도는 사용자가 클릭할 때만
    }

    // 유닛 수량 텍스트 깜빡임 효과 (선택적 기능)
    private void FlashUnitCountText(UnitType unitType)
    {
        TextMeshProUGUI textElement = GetTextElementForUnitType(unitType);
        if (textElement == null) return;

        // 코루틴이 이미 실행 중이면 중단
        if (flashCoroutines.ContainsKey(unitType) && flashCoroutines[unitType] != null)
        {
            StopCoroutine(flashCoroutines[unitType]);
        }

        // 깜빡임 코루틴 시작
        flashCoroutines[unitType] = StartCoroutine(FlashTextCoroutine(textElement));
    }

    // 텍스트 깜빡임 코루틴
    private IEnumerator FlashTextCoroutine(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        Color originalColor = text.color;

        // 빨간색으로 변경
        text.color = Color.red;

        // 잠시 대기
        yield return new WaitForSeconds(0.2f);

        // 원래 색상으로 복원
        text.color = originalColor;
    }

    // 선택된 버튼 하이라이트 (선택적 기능)
    private void HighlightSelectedButton(UnitType unitType)
    {
        // BattlePhaseController에 메서드 호출 전달
        if (battlePhaseController != null)
        {
            battlePhaseController.HighlightButton(unitType);
        }
    }

    // 외부에서 호출: 유닛 타입 선택
    public void SelectUnitType(UnitType unitType)
    {
        if (!canPlaceUnits || !isClient) return;

        // 이미 최대 수량이 배치되었는지 확인
        int placed = GetPlacedUnitCount(unitType);
        int required = GetRequiredUnitCount(unitType);

        if (placed >= required)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {unitType} 유닛은 이미 모두 배치되었습니다.");
            return;
        }

        selectedUnitType = unitType;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {unitType} 유닛 타입 선택됨");
    }

    // 플레이어 진영 영역 내에 있는지 확인
    private bool IsWithinPlayerZone(Vector3 position)
    {
        if (isLeftSidePlayer)
        {
            // 왼쪽 플레이어는 맵 왼쪽 가장자리 영역에만 배치 가능
            float leftBoundary = -mapWidth / 2 + edgeOffset;
            float rightBoundary = leftBoundary + areaWidth;
            return position.x >= leftBoundary && position.x <= rightBoundary;
        }
        else
        {
            // 오른쪽 플레이어는 맵 오른쪽 가장자리 영역에만 배치 가능
            float rightBoundary = mapWidth / 2 - edgeOffset;
            float leftBoundary = rightBoundary - areaWidth;
            return position.x >= leftBoundary && position.x <= rightBoundary;
        }
    }

    // 서버 측에서 특정 팀의 배치 영역인지 확인
    [Server]
    private bool IsWithinTeamZone(Vector3 position, TeamIndex teamIndex)
    {
        // 여기에 더 자세한 로깅 추가
        float leftBoundary, rightBoundary;

        if (teamIndex == TeamIndex.Left)
        {
            // 왼쪽 영역 검사
            leftBoundary = -mapWidth / 2 + edgeOffset;
            rightBoundary = leftBoundary + areaWidth;

            bool isValid = position.x >= leftBoundary && position.x <= rightBoundary;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 왼쪽 팀 영역 검사: x={position.x}, 범위=[{leftBoundary}~{rightBoundary}], 유효={isValid}");
            return isValid;
        }
        else if (teamIndex == TeamIndex.Right)
        {
            // 오른쪽 영역 검사
            rightBoundary = mapWidth / 2 - edgeOffset;
            leftBoundary = rightBoundary - areaWidth;

            bool isValid = position.x >= leftBoundary && position.x <= rightBoundary;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 오른쪽 팀 영역 검사: x={position.x}, 범위=[{leftBoundary}~{rightBoundary}], 유효={isValid}");
            return isValid;
        }

        Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 알 수 없는 팀 인덱스: {teamIndex}");
        return false; // 알 수 없는 팀
    }

    // 유닛 배치 로직 (서버 측)
    [Server]
    public void ServerPlaceUnit(PlayerInfo playerInfo, UnitType unitType, Vector3 position)
    {
        if (playerInfo == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 서버 - PlayerInfo가 null입니다.");
            return;
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - {playerInfo.playerName}(netId: {playerInfo.netId})의 팀ID = {playerInfo.teamId}, 위치 요청: {position}");

        // 유효한 팀 영역 내의 위치인지 확인
        if (!IsWithinTeamZone(position, playerInfo.teamId))
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 잘못된 위치 요청: 유닛이 플레이어 진영 밖에 배치될 수 없습니다.");
            return;
        }

        // 서버에서 유닛 제한 확인
        int currentCount = GetServerUnitCount(playerInfo.netId, unitType);
        int requiredCount = GetRequiredUnitCount(unitType);

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 유닛 카운트 확인: {unitType}({currentCount}/{requiredCount})");

        if (currentCount >= requiredCount)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 유닛 제한 초과: {unitType}({currentCount}/{requiredCount})");
            return;
        }

        // 프리팹 선택
        GameObject prefab = GetPrefabForUnitType(unitType);
        if (prefab == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 유닛 프리팹이 없습니다! ({unitType})");
            return;
        }

        // 배치 위치 무결성 검사 (필요하면 조정)
        Vector3 validatedPosition = position;
        validatedPosition.y = position.y + placementYOffset; // 필요시 높이 조정

        // 유닛 생성
        GameObject unit = Instantiate(prefab, validatedPosition, Quaternion.identity);
        unit.name = $"{playerInfo.teamId}_{unitType}{currentCount}_{UnityEngine.Random.Range(0, 999_999):D6}";

        // 유닛 컴포넌트 설정
        Unit unitComp = unit.GetComponent<Unit>();
        if (unitComp != null)
        {
            unitComp.ownerNetId = playerInfo.netId;
            unitComp.teamIndex = playerInfo.teamId;
            unitComp.palette = playerInfo.playerPalette;

            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 유닛 생성 성공: {unit.name}, 팀: {playerInfo.teamId}, 팔레트: {playerInfo.playerPalette}");
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 유닛 컴포넌트가 없습니다! ({unit.name})");
        }

        // 네트워크 스폰
        NetworkServer.Spawn(unit, playerInfo.connectionToClient);
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 네트워크 스폰 완료: {unit.name}");

        // 클라이언트에게 배치 완료 알림
        TargetOnUnitPlaced(playerInfo.connectionToClient, unitType, unit);
    }

    [Server]
    private int GetServerUnitCount(uint playerNetId, UnitType unitType)
    {
        // 서버 카운트 대신 직접 씬에서 유닛 찾기
        int count = 0;
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in allUnits)
        {
            if (unit.ownerNetId == playerNetId)
            {
                // 유닛 타입 확인 - 이름으로 구분
                if (unit.name.Contains(unitType.ToString()))
                {
                    count++;
                }
            }
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 - 플레이어 {playerNetId}의 {unitType} 유닛 직접 카운트: {count}/{GetRequiredUnitCount(unitType)}");
        return count;
    }

    private GameObject GetPrefabForUnitType(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.King: return kingPrefab;
            case UnitType.Infantry: return infantryPrefab;
            case UnitType.Archer: return archerPrefab;
            case UnitType.Cavalry: return cavalryPrefab;
            case UnitType.Wizard: return wizardPrefab;
            default: return null;
        }
    }

    // 클라이언트에게 유닛 배치 성공 알림
    [TargetRpc]
    private void TargetOnUnitPlaced(NetworkConnection target, UnitType unitType, GameObject unitObj)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 - 유닛 배치 완료 알림 수신: {unitType}");

        // 리스트에 유닛 추가
        if (!placedUnits.ContainsKey(unitType))
        {
            placedUnits[unitType] = new List<GameObject>();
        }

        placedUnits[unitType].Add(unitObj);

        // 유닛 타입별 로그
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {unitType} 유닛 배치 완료 (총 {placedUnits[unitType].Count}/{GetRequiredUnitCount(unitType)}개)");

        // UI 업데이트 (남은 유닛 텍스트)
        UpdateRemainedText(unitType, GetTextElementForUnitType(unitType));

        // BattlePhaseController UI 업데이트
        if (battlePhaseController != null)
        {
            battlePhaseController.UpdatePlacementUI(unitType);
        }

        // 모든 필수 유닛이 배치되었는지 확인하고 준비 버튼 상태 업데이트
        if (battlePhaseController != null)
        {
            battlePhaseController.UpdateReadyButtonState(HasPlacedAllRequiredUnits());
        }

        // 배치 후 선택 초기화
        selectedUnitType = UnitType.None;
    }

    private TextMeshProUGUI GetTextElementForUnitType(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.King: return kingRemainedText;
            case UnitType.Infantry: return infantryRemainedText;
            case UnitType.Archer: return archerRemainedText;
            case UnitType.Cavalry: return cavalryRemainedText;
            case UnitType.Wizard: return wizardRemainedText;
            default: return null;
        }
    }

    // 외부에서 호출: 배치 단계 종료 및 준비 상태 설정
    public void SetBattleReady(bool isReady)
    {
        if (!isClient) return;
        if (localPlayer == null) return;

        // 배치 상태 업데이트
        if (battleSceneManager != null)
        {
            // BattleSceneManager를 통해 준비 상태 설정
            battleSceneManager.CmdSetBattleReady(isReady, null);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] BattleSceneManager를 통해 배틀 준비 상태 설정 = {isReady}");
        }
        else
        {
            // 기존 방식 - 직접 PlayerInfo 호출
            localPlayer.CmdSetBattleReady(isReady);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 기존 방식으로 배틀 준비 상태 설정 = {isReady}");
        }

        // 유닛 배치 활성화/비활성화
        canPlaceUnits = !isReady;
    }

    // 모든 필수 유닛을 배치했는지 확인
    public bool HasPlacedAllRequiredUnits()
    {
        foreach (var unitType in requiredUnitCounts.Keys)
        {
            int required = GetRequiredUnitCount(unitType);
            int placed = GetPlacedUnitCount(unitType);

            // 필수 유닛 수량이 있고, 그만큼 배치되지 않았으면 false
            if (required > 0 && placed < required)
            {
                return false;
            }
        }

        // 모든 필수 유닛이 배치되었으면 true
        return true;
    }

    // 배치된 유닛 수 반환
    public int GetPlacedUnitCount(UnitType unitType)
    {
        if (placedUnits.ContainsKey(unitType))
        {
            return placedUnits[unitType].Count;
        }
        return 0;
    }

    // 필수 유닛 수량 반환
    public int GetRequiredUnitCount(UnitType unitType)
    {
        if (requiredUnitCounts.ContainsKey(unitType))
        {
            return requiredUnitCounts[unitType];
        }
        return 0;
    }
}