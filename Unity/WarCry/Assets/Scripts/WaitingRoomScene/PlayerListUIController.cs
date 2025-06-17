using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

/// <summary>
/// 플레이어 목록 UI 관리를 담당하는 컨트롤러
/// 플레이어 목록 아이템의 생성, 업데이트, 제거를 처리하며
/// 플레이어 정보 변경 시 UI 동기화, 호스트 표시, 준비 상태 표시 등의 기능을 제공
/// </summary>
public class PlayerListUIController : MonoBehaviour
{
    #region Events

    /// <summary>
    /// 플레이어 목록 업데이트 완료 시 발생하는 이벤트 (플레이어 수)
    /// </summary>
    public static event Action<int> OnPlayerListUpdated;

    #endregion

    #region Inspector Fields

    [Header("UI References")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerListItemPrefab;

    [Header("Visual Configuration")]
    [SerializeField] private Color[] playerColors = PalettesManager.colors;
    [SerializeField] private Sprite hostCrownSprite;

    [Header("Host Icon Settings")]
    [SerializeField] private Vector2 hostIconSize = new Vector2(24, 24);
    [SerializeField] private float hostIconOffsetX = -30f;

    [Header("Color Indicator Settings")]
    [SerializeField] private Vector2 colorIndicatorSize = new Vector2(20, 20);
    [SerializeField] private float colorIndicatorOffsetX = 5f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string HostIconName = "HostIcon";
    private const string ColorIndicatorName = "ColorIndicator";
    private const string NameTextName = "NameText";
    private const string ReadyIconName = "ReadyIcon";

    #endregion

    #region Private Fields

    // UI 상태 관리
    private Dictionary<uint, GameObject> playerListItems = new Dictionary<uint, GameObject>();
    private bool isInitialized = false;

    // 데이터 캐시
    private Dictionary<uint, PlayerData> playerDataCache = new Dictionary<uint, PlayerData>();

    #endregion

    #region Data Classes

    /// <summary>
    /// 플레이어 데이터 구조체
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        public uint netId;
        public string playerName;
        public bool isReady;
        public int colorIndex;
        public bool isHost;

        public PlayerData(uint id, string name, bool ready, int color, bool host)
        {
            netId = id;
            playerName = name;
            isReady = ready;
            colorIndex = color;
            isHost = host;
        }

        public bool HasChanged(PlayerData other)
        {
            if (other == null) return true;

            return playerName != other.playerName ||
                   isReady != other.isReady ||
                   colorIndex != other.colorIndex ||
                   isHost != other.isHost;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        ValidateReferences();
    }

    private void Start()
    {
        InitializePlayerList();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 PlayerListUIController 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 필수 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (playerListContainer == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerListContainer가 설정되지 않았습니다!");
            return;
        }

        if (playerListItemPrefab == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerListItemPrefab이 설정되지 않았습니다!");
            return;
        }

        if (playerColors == null || playerColors.Length == 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] PlayerColors가 설정되지 않았습니다!");
            return;
        }
    }

    /// <summary>
    /// 플레이어 목록 초기화
    /// </summary>
    public void InitializePlayerList()
    {
        if (isInitialized)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록이 이미 초기화되었습니다");
            return;
        }

        ClearAllPlayerItems();
        isInitialized = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 초기화 완료");
    }

    #endregion

    #region Public API - Player List Management

    /// <summary>
    /// 플레이어 목록 업데이트
    /// </summary>
    /// <param name="netIds">네트워크 ID 목록</param>
    /// <param name="names">플레이어 이름 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    /// <param name="isHostList">호스트 여부 목록</param>
    public void UpdatePlayerList(List<uint> netIds, List<string> names, List<bool> readyStates, List<int> colorIndices, List<bool> isHostList)
    {
        if (!ValidatePlayerListData(netIds, names, readyStates, colorIndices, isHostList))
            return;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 시작: {netIds.Count}명");

        try
        {
            ProcessPlayerListUpdate(netIds, names, readyStates, colorIndices, isHostList);
            TriggerPlayerListUpdated(netIds.Count);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록 업데이트 중 오류: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 특정 플레이어 정보 업데이트
    /// </summary>
    /// <param name="netId">네트워크 ID</param>
    /// <param name="playerName">플레이어 이름</param>
    /// <param name="isReady">준비 상태</param>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <param name="isHost">호스트 여부</param>
    public void UpdateSinglePlayer(uint netId, string playerName, bool isReady, int colorIndex, bool isHost)
    {
        PlayerData newData = new PlayerData(netId, playerName, isReady, colorIndex, isHost);

        if (playerListItems.TryGetValue(netId, out GameObject existingItem))
        {
            UpdateExistingPlayerItem(existingItem, newData);
        }
        else
        {
            CreateNewPlayerItem(newData);
        }

        UpdatePlayerDataCache(netId, newData);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 단일 플레이어 업데이트: {playerName} (NetId: {netId})");
    }

    /// <summary>
    /// 특정 플레이어 제거
    /// </summary>
    /// <param name="netId">제거할 플레이어의 네트워크 ID</param>
    public void RemovePlayer(uint netId)
    {
        if (playerListItems.TryGetValue(netId, out GameObject playerItem))
        {
            Destroy(playerItem);
            playerListItems.Remove(netId);
            playerDataCache.Remove(netId);

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 제거: NetId {netId}");

            TriggerPlayerListUpdated(playerListItems.Count);
        }
    }

    /// <summary>
    /// 모든 플레이어 목록 제거
    /// </summary>
    public void ClearAllPlayers()
    {
        ClearAllPlayerItems();
        TriggerPlayerListUpdated(0);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 모든 플레이어 목록 제거 완료");
    }

    #endregion

    #region Private Methods - Player List Processing

    /// <summary>
    /// 플레이어 목록 업데이트 처리
    /// </summary>
    /// <param name="netIds">네트워크 ID 목록</param>
    /// <param name="names">플레이어 이름 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    /// <param name="isHostList">호스트 여부 목록</param>
    private void ProcessPlayerListUpdate(List<uint> netIds, List<string> names, List<bool> readyStates, List<int> colorIndices, List<bool> isHostList)
    {
        // 기존 플레이어 항목 임시 저장
        Dictionary<uint, GameObject> oldItems = new Dictionary<uint, GameObject>(playerListItems);
        playerListItems.Clear();

        // 새 플레이어 목록으로 UI 갱신
        for (int i = 0; i < netIds.Count; i++)
        {
            ProcessSinglePlayerUpdate(i, netIds, names, readyStates, colorIndices, isHostList, oldItems);
        }

        // 더 이상 사용되지 않는 항목 제거
        RemoveUnusedPlayerItems(oldItems);
    }

    /// <summary>
    /// 개별 플레이어 업데이트 처리
    /// </summary>
    /// <param name="index">플레이어 인덱스</param>
    /// <param name="netIds">네트워크 ID 목록</param>
    /// <param name="names">플레이어 이름 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    /// <param name="isHostList">호스트 여부 목록</param>
    /// <param name="oldItems">기존 플레이어 항목</param>
    private void ProcessSinglePlayerUpdate(int index, List<uint> netIds, List<string> names, List<bool> readyStates, List<int> colorIndices, List<bool> isHostList, Dictionary<uint, GameObject> oldItems)
    {
        uint netId = netIds[index];
        PlayerData playerData = ExtractPlayerData(index, netId, names, readyStates, colorIndices, isHostList);

        if (oldItems.TryGetValue(netId, out GameObject existingItem))
        {
            // 기존 항목 재사용
            playerListItems[netId] = existingItem;
            UpdateExistingPlayerItem(existingItem, playerData);
            oldItems.Remove(netId);
        }
        else
        {
            // 새 플레이어 UI 항목 추가
            CreateNewPlayerItem(playerData);
        }

        UpdatePlayerDataCache(netId, playerData);
    }

    /// <summary>
    /// 플레이어 데이터 추출
    /// </summary>
    /// <param name="index">플레이어 인덱스</param>
    /// <param name="netId">네트워크 ID</param>
    /// <param name="names">플레이어 이름 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    /// <param name="isHostList">호스트 여부 목록</param>
    /// <returns>추출된 플레이어 데이터</returns>
    private PlayerData ExtractPlayerData(int index, uint netId, List<string> names, List<bool> readyStates, List<int> colorIndices, List<bool> isHostList)
    {
        string playerName = GetSafeListValue(names, index, "Unknown");
        bool isReady = GetSafeListValue(readyStates, index, false);
        int colorIndex = GetSafeListValue(colorIndices, index, 0);
        bool isHost = GetSafeListValue(isHostList, index, false);

        return new PlayerData(netId, playerName, isReady, colorIndex, isHost);
    }

    /// <summary>
    /// 사용되지 않는 플레이어 항목 제거
    /// </summary>
    /// <param name="unusedItems">사용되지 않는 항목들</param>
    private void RemoveUnusedPlayerItems(Dictionary<uint, GameObject> unusedItems)
    {
        foreach (var item in unusedItems.Values)
        {
            Destroy(item);
        }

        // 캐시에서도 제거
        foreach (var netId in unusedItems.Keys)
        {
            playerDataCache.Remove(netId);
        }
    }

    #endregion

    #region Private Methods - Player Item Management

    /// <summary>
    /// 새로운 플레이어 UI 항목 생성
    /// </summary>
    /// <param name="playerData">플레이어 데이터</param>
    private void CreateNewPlayerItem(PlayerData playerData)
    {
        GameObject listItem = Instantiate(playerListItemPrefab, playerListContainer);
        listItem.name = $"PlayerItem_{playerData.netId}";

        ConfigurePlayerItem(listItem, playerData);
        playerListItems[playerData.netId] = listItem;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 새 플레이어 항목 생성: {playerData.playerName} (NetId: {playerData.netId})");
    }

    /// <summary>
    /// 기존 플레이어 항목 업데이트
    /// </summary>
    /// <param name="listItem">업데이트할 항목</param>
    /// <param name="playerData">새로운 플레이어 데이터</param>
    private void UpdateExistingPlayerItem(GameObject listItem, PlayerData playerData)
    {
        // 데이터 변경 확인
        if (playerDataCache.TryGetValue(playerData.netId, out PlayerData cachedData))
        {
            if (!playerData.HasChanged(cachedData))
            {
                return; // 변경사항 없음
            }
        }

        ConfigurePlayerItem(listItem, playerData);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 기존 플레이어 항목 업데이트: {playerData.playerName} (NetId: {playerData.netId})");
    }

    /// <summary>
    /// 플레이어 항목 설정
    /// </summary>
    /// <param name="listItem">설정할 항목</param>
    /// <param name="playerData">플레이어 데이터</param>
    private void ConfigurePlayerItem(GameObject listItem, PlayerData playerData)
    {
        UpdatePlayerName(listItem, playerData.playerName);
        UpdateReadyStatus(listItem, playerData.isReady);
        UpdateColorIndicator(listItem, playerData.colorIndex);
        UpdateHostIcon(listItem, playerData.isHost);
    }

    /// <summary>
    /// 플레이어 이름 업데이트
    /// </summary>
    /// <param name="listItem">플레이어 항목</param>
    /// <param name="playerName">플레이어 이름</param>
    private void UpdatePlayerName(GameObject listItem, string playerName)
    {
        Transform nameTextTransform = listItem.transform.Find(NameTextName);
        if (nameTextTransform != null)
        {
            TMP_Text nameText = nameTextTransform.GetComponent<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = playerName;
            }
        }
    }

    /// <summary>
    /// 준비 상태 업데이트
    /// </summary>
    /// <param name="listItem">플레이어 항목</param>
    /// <param name="isReady">준비 상태</param>
    private void UpdateReadyStatus(GameObject listItem, bool isReady)
    {
        Transform readyIconTransform = listItem.transform.Find(ReadyIconName);
        if (readyIconTransform != null)
        {
            readyIconTransform.gameObject.SetActive(isReady);
        }
    }

    /// <summary>
    /// 색상 인디케이터 업데이트
    /// </summary>
    /// <param name="listItem">플레이어 항목</param>
    /// <param name="colorIndex">색상 인덱스</param>
    private void UpdateColorIndicator(GameObject listItem, int colorIndex)
    {
        Transform colorIndicatorTransform = listItem.transform.Find(ColorIndicatorName);

        if (colorIndicatorTransform == null)
        {
            colorIndicatorTransform = CreateColorIndicator(listItem);
        }

        ApplyColorToIndicator(colorIndicatorTransform, colorIndex);
    }

    /// <summary>
    /// 색상 인디케이터 생성
    /// </summary>
    /// <param name="listItem">부모 항목</param>
    /// <returns>생성된 색상 인디케이터</returns>
    private Transform CreateColorIndicator(GameObject listItem)
    {
        GameObject colorObj = new GameObject(ColorIndicatorName);
        colorObj.transform.SetParent(listItem.transform, false);

        ConfigureColorIndicatorLayout(colorObj);
        AddColorIndicatorImage(colorObj);

        return colorObj.transform;
    }

    /// <summary>
    /// 색상 인디케이터 레이아웃 설정
    /// </summary>
    /// <param name="colorObj">색상 인디케이터 오브젝트</param>
    private void ConfigureColorIndicatorLayout(GameObject colorObj)
    {
        RectTransform rectTransform = colorObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0.5f);
        rectTransform.anchorMax = new Vector2(0, 0.5f);
        rectTransform.pivot = new Vector2(0, 0.5f);
        rectTransform.sizeDelta = colorIndicatorSize;
        rectTransform.anchoredPosition = new Vector2(colorIndicatorOffsetX, 0);
    }

    /// <summary>
    /// 색상 인디케이터 이미지 추가
    /// </summary>
    /// <param name="colorObj">색상 인디케이터 오브젝트</param>
    private void AddColorIndicatorImage(GameObject colorObj)
    {
        Image colorImage = colorObj.AddComponent<Image>();

        // 원형 스프라이트 로드 시도
        Sprite circleSprite = Resources.Load<Sprite>("UI/Circle");
        if (circleSprite != null)
        {
            colorImage.sprite = circleSprite;
        }
    }

    /// <summary>
    /// 색상 인디케이터에 색상 적용
    /// </summary>
    /// <param name="colorIndicatorTransform">색상 인디케이터 Transform</param>
    /// <param name="colorIndex">색상 인덱스</param>
    private void ApplyColorToIndicator(Transform colorIndicatorTransform, int colorIndex)
    {
        if (colorIndicatorTransform == null) return;

        Image colorImage = colorIndicatorTransform.GetComponent<Image>();
        if (colorImage != null && IsValidColorIndex(colorIndex))
        {
            colorImage.color = playerColors[colorIndex];
        }
    }

    /// <summary>
    /// 호스트 아이콘 업데이트
    /// </summary>
    /// <param name="listItem">플레이어 항목</param>
    /// <param name="isHost">호스트 여부</param>
    private void UpdateHostIcon(GameObject listItem, bool isHost)
    {
        Transform hostIconTransform = listItem.transform.Find(HostIconName);

        if (isHost && hostIconTransform == null)
        {
            hostIconTransform = CreateHostIcon(listItem);
        }

        if (hostIconTransform != null)
        {
            hostIconTransform.gameObject.SetActive(isHost);
        }
    }

    /// <summary>
    /// 호스트 아이콘 생성
    /// </summary>
    /// <param name="listItem">부모 항목</param>
    /// <returns>생성된 호스트 아이콘</returns>
    private Transform CreateHostIcon(GameObject listItem)
    {
        GameObject hostIconObj = new GameObject(HostIconName);
        hostIconObj.transform.SetParent(listItem.transform, false);

        ConfigureHostIconLayout(hostIconObj, listItem);
        AddHostIconImage(hostIconObj);

        return hostIconObj.transform;
    }

    /// <summary>
    /// 호스트 아이콘 레이아웃 설정
    /// </summary>
    /// <param name="hostIconObj">호스트 아이콘 오브젝트</param>
    /// <param name="listItem">부모 항목</param>
    private void ConfigureHostIconLayout(GameObject hostIconObj, GameObject listItem)
    {
        RectTransform rectTransform = hostIconObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0.5f);
        rectTransform.anchorMax = new Vector2(0, 0.5f);
        rectTransform.pivot = new Vector2(0, 0.5f);
        rectTransform.sizeDelta = hostIconSize;

        // 이름 텍스트 위치를 기준으로 호스트 아이콘 위치 계산
        float xPos = CalculateHostIconPosition(listItem);
        rectTransform.anchoredPosition = new Vector2(xPos, 0);
    }

    /// <summary>
    /// 호스트 아이콘 위치 계산
    /// </summary>
    /// <param name="listItem">부모 항목</param>
    /// <returns>계산된 X 위치</returns>
    private float CalculateHostIconPosition(GameObject listItem)
    {
        Transform nameTextTransform = listItem.transform.Find(NameTextName);
        if (nameTextTransform != null)
        {
            return nameTextTransform.GetComponent<RectTransform>().anchoredPosition.x + hostIconOffsetX;
        }

        return hostIconOffsetX;
    }

    /// <summary>
    /// 호스트 아이콘 이미지 추가
    /// </summary>
    /// <param name="hostIconObj">호스트 아이콘 오브젝트</param>
    private void AddHostIconImage(GameObject hostIconObj)
    {
        Image hostImage = hostIconObj.AddComponent<Image>();

        if (hostCrownSprite != null)
        {
            hostImage.sprite = hostCrownSprite;
        }
        else
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 호스트 크라운 스프라이트가 설정되지 않았습니다.");
        }
    }

    #endregion

    #region Private Methods - Utility

    /// <summary>
    /// 모든 플레이어 항목 제거
    /// </summary>
    private void ClearAllPlayerItems()
    {
        foreach (var item in playerListItems.Values)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }

        playerListItems.Clear();
        playerDataCache.Clear();
    }

    /// <summary>
    /// 플레이어 데이터 캐시 업데이트
    /// </summary>
    /// <param name="netId">네트워크 ID</param>
    /// <param name="playerData">플레이어 데이터</param>
    private void UpdatePlayerDataCache(uint netId, PlayerData playerData)
    {
        playerDataCache[netId] = playerData;
    }

    /// <summary>
    /// 리스트에서 안전하게 값 가져오기
    /// </summary>
    /// <typeparam name="T">값 타입</typeparam>
    /// <param name="list">대상 리스트</param>
    /// <param name="index">인덱스</param>
    /// <param name="defaultValue">기본값</param>
    /// <returns>리스트 값 또는 기본값</returns>
    private T GetSafeListValue<T>(List<T> list, int index, T defaultValue)
    {
        if (list != null && index >= 0 && index < list.Count)
        {
            return list[index];
        }

        return defaultValue;
    }

    /// <summary>
    /// 색상 인덱스 유효성 확인
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <returns>유효한 인덱스면 true</returns>
    private bool IsValidColorIndex(int colorIndex)
    {
        return colorIndex >= 0 && colorIndex < playerColors.Length;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 플레이어 목록 데이터 유효성 검증
    /// </summary>
    /// <param name="netIds">네트워크 ID 목록</param>
    /// <param name="names">플레이어 이름 목록</param>
    /// <param name="readyStates">준비 상태 목록</param>
    /// <param name="colorIndices">색상 인덱스 목록</param>
    /// <param name="isHostList">호스트 여부 목록</param>
    /// <returns>유효한 데이터면 true</returns>
    private bool ValidatePlayerListData(List<uint> netIds, List<string> names, List<bool> readyStates, List<int> colorIndices, List<bool> isHostList)
    {
        if (netIds == null || netIds.Count == 0)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 비어있는 플레이어 목록을 받았습니다.");
            return false;
        }

        if (!isInitialized)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 목록이 초기화되지 않았습니다.");
            return false;
        }

        return true;
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 플레이어 목록 업데이트 완료 이벤트 발생
    /// </summary>
    /// <param name="playerCount">플레이어 수</param>
    private void TriggerPlayerListUpdated(int playerCount)
    {
        OnPlayerListUpdated?.Invoke(playerCount);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 현재 플레이어 수 반환
    /// </summary>
    /// <returns>현재 플레이어 수</returns>
    public int GetPlayerCount()
    {
        return playerListItems.Count;
    }

    /// <summary>
    /// 특정 플레이어 존재 여부 확인
    /// </summary>
    /// <param name="netId">네트워크 ID</param>
    /// <returns>플레이어가 존재하면 true</returns>
    public bool HasPlayer(uint netId)
    {
        return playerListItems.ContainsKey(netId);
    }

    /// <summary>
    /// 초기화 완료 여부 확인
    /// </summary>
    /// <returns>초기화가 완료되었으면 true</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 플레이어 데이터 가져오기
    /// </summary>
    /// <param name="netId">네트워크 ID</param>
    /// <returns>플레이어 데이터 (없으면 null)</returns>
    public PlayerData GetPlayerData(uint netId)
    {
        playerDataCache.TryGetValue(netId, out PlayerData playerData);
        return playerData;
    }

    /// <summary>
    /// 모든 플레이어 데이터 가져오기
    /// </summary>
    /// <returns>모든 플레이어 데이터 목록</returns>
    public List<PlayerData> GetAllPlayerData()
    {
        return new List<PlayerData>(playerDataCache.Values);
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 플레이어 목록 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Player List Status")]
    public void LogPlayerListStatus()
    {
        Debug.Log($"=== PlayerListUIController 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"플레이어 수: {playerListItems.Count}");
        Debug.Log($"캐시된 데이터 수: {playerDataCache.Count}");

        Debug.Log("=== 플레이어 목록 ===");
        foreach (var kvp in playerDataCache)
        {
            var data = kvp.Value;
            Debug.Log($"  - {data.playerName} (NetId: {data.netId}) - Ready: {data.isReady}, Host: {data.isHost}, Color: {data.colorIndex}");
        }
    }

    #endregion
}