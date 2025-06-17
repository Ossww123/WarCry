using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 색상 팔레트 UI 관리를 담당하는 컨트롤러
/// 플레이어 색상 선택 UI를 생성하고 관리하며, 색상 선택 이벤트를 처리
/// 다른 플레이어가 사용 중인 색상 비활성화, 준비 상태에 따른 색상 변경 제한 등의 기능을 제공
/// </summary>
public class ColorPaletteController : MonoBehaviour
{
    #region Events

    /// <summary>
    /// 색상 선택 시 발생하는 이벤트 (색상 인덱스)
    /// </summary>
    public static event Action<int> OnColorSelected;

    #endregion

    #region Inspector Fields

    [Header("UI References")]
    [SerializeField] private Transform colorPaletteContainer;
    [SerializeField] private GameObject colorButtonPrefab;

    [Header("Color Configuration")]
    [SerializeField] private Color[] buttonColors = PalettesManager.colors;
    [SerializeField] private int maxColorCount = 12;

    [Header("Button Layout")]
    [SerializeField] private Vector2 cellSize = new Vector2(80, 80);
    [SerializeField] private Vector2 spacing = new Vector2(10, 10);
    [SerializeField] private int paddingLeft = 20;
    [SerializeField] private int paddingRight = 20;
    [SerializeField] private int paddingTop = 20;
    [SerializeField] private int paddingBottom = 20;
    [SerializeField] private int constraintCount = 6;

    [Header("Visual Effects")]
    [SerializeField] private Vector3 selectedScale = new Vector3(1.1f, 1.1f, 1.1f);
    [SerializeField] private Color highlightColor = new Color(1, 1, 1, 0.5f);

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Private Fields

    // UI 컴포넌트
    private Button[] colorButtons;
    private GridLayoutGroup gridLayoutGroup;

    // 상태 관리
    private int selectedColorIndex = 0;
    private bool isInitialized = false;
    private bool isColorChangeAllowed = true;

    // 색상 사용 상태 추적
    private HashSet<int> usedColorIndices = new HashSet<int>();

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
        InitializeColorPalette();
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
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 ColorPaletteController 비활성화됨");

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
        if (colorPaletteContainer == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] ColorPaletteContainer가 설정되지 않았습니다!");
            return;
        }

        if (colorButtonPrefab == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] ColorButtonPrefab이 설정되지 않았습니다!");
            return;
        }

        if (buttonColors == null || buttonColors.Length == 0)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] ButtonColors가 설정되지 않았습니다!");
            return;
        }
    }

    /// <summary>
    /// 색상 팔레트 초기화
    /// </summary>
    public void InitializeColorPalette()
    {
        if (isInitialized)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 색상 팔레트가 이미 초기화되었습니다");
            return;
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 팔레트 초기화 시작");

        SetupGridLayout();
        ClearExistingButtons();
        CreateColorButtons();
        FinalizeInitialization();
    }

    /// <summary>
    /// Grid Layout Group 설정
    /// </summary>
    private void SetupGridLayout()
    {
        gridLayoutGroup = colorPaletteContainer.GetComponent<GridLayoutGroup>();

        if (gridLayoutGroup == null)
        {
            gridLayoutGroup = colorPaletteContainer.gameObject.AddComponent<GridLayoutGroup>();
        }

        ConfigureGridLayout();
    }

    /// <summary>
    /// Grid Layout 설정 적용
    /// </summary>
    private void ConfigureGridLayout()
    {
        gridLayoutGroup.cellSize = cellSize;
        gridLayoutGroup.spacing = spacing;
        gridLayoutGroup.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = constraintCount;
        gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Grid Layout 설정 완료 - 셀 크기: {cellSize}, 제약: {constraintCount}열");
    }

    /// <summary>
    /// 기존 색상 버튼 제거
    /// </summary>
    private void ClearExistingButtons()
    {
        foreach (Transform child in colorPaletteContainer)
        {
            Destroy(child.gameObject);
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 기존 색상 버튼 제거 완료");
    }

    /// <summary>
    /// 색상 버튼 생성
    /// </summary>
    private void CreateColorButtons()
    {
        int buttonCount = Mathf.Min(maxColorCount, buttonColors.Length);
        colorButtons = new Button[buttonCount];

        for (int i = 0; i < buttonCount; i++)
        {
            CreateSingleColorButton(i);
        }

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 버튼 생성 완료: {buttonCount}개");
    }

    /// <summary>
    /// 개별 색상 버튼 생성
    /// </summary>
    /// <param name="index">색상 인덱스</param>
    private void CreateSingleColorButton(int index)
    {
        GameObject buttonObj = Instantiate(colorButtonPrefab, colorPaletteContainer);
        Button button = buttonObj.GetComponent<Button>();
        Image buttonImage = buttonObj.GetComponent<Image>();

        ConfigureColorButton(buttonObj, button, buttonImage, index);
        CreateHighlightObject(buttonObj);
        RegisterButtonEvents(button, index);

        colorButtons[index] = button;
    }

    /// <summary>
    /// 색상 버튼 기본 설정
    /// </summary>
    /// <param name="buttonObj">버튼 게임오브젝트</param>
    /// <param name="button">버튼 컴포넌트</param>
    /// <param name="buttonImage">버튼 이미지</param>
    /// <param name="index">색상 인덱스</param>
    private void ConfigureColorButton(GameObject buttonObj, Button button, Image buttonImage, int index)
    {
        // 이름 설정
        buttonObj.name = $"ColorButton_{index}";

        // 색상 설정
        if (index < buttonColors.Length)
        {
            buttonImage.color = buttonColors[index];
        }

        // 초기 스케일 설정
        buttonObj.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 하이라이트 오브젝트 생성
    /// </summary>
    /// <param name="buttonObj">부모 버튼 오브젝트</param>
    private void CreateHighlightObject(GameObject buttonObj)
    {
        Transform existingHighlight = buttonObj.transform.Find("Highlight");
        if (existingHighlight != null)
        {
            existingHighlight.gameObject.SetActive(false);
            return;
        }

        GameObject highlightObj = CreateNewHighlightObject(buttonObj);
        ConfigureHighlightObject(highlightObj, buttonObj);
    }

    /// <summary>
    /// 새로운 하이라이트 오브젝트 생성
    /// </summary>
    /// <param name="buttonObj">부모 버튼 오브젝트</param>
    /// <returns>생성된 하이라이트 오브젝트</returns>
    private GameObject CreateNewHighlightObject(GameObject buttonObj)
    {
        GameObject highlightObj = new GameObject("Highlight");
        highlightObj.transform.SetParent(buttonObj.transform, false);

        return highlightObj;
    }

    /// <summary>
    /// 하이라이트 오브젝트 설정
    /// </summary>
    /// <param name="highlightObj">하이라이트 오브젝트</param>
    /// <param name="buttonObj">부모 버튼 오브젝트</param>
    private void ConfigureHighlightObject(GameObject highlightObj, GameObject buttonObj)
    {
        // 이미지 컴포넌트 추가
        Image highlightImage = highlightObj.AddComponent<Image>();

        // 버튼 이미지와 같은 스프라이트 사용
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null)
        {
            highlightImage.sprite = buttonImage.sprite;
        }

        // 위치 및 크기 설정
        RectTransform rectTransform = highlightObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = new Vector2(10, 10); // 테두리 크기

        // 색상 설정
        highlightImage.color = highlightColor;

        // 초기 상태에서는 비활성화
        highlightObj.SetActive(false);
    }

    /// <summary>
    /// 버튼 이벤트 등록
    /// </summary>
    /// <param name="button">등록할 버튼</param>
    /// <param name="index">색상 인덱스</param>
    private void RegisterButtonEvents(Button button, int index)
    {
        button.onClick.AddListener(() => OnColorButtonClicked(index));
    }

    /// <summary>
    /// 초기화 완료 처리
    /// </summary>
    private void FinalizeInitialization()
    {
        isInitialized = true;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 팔레트 초기화 완료");
    }

    #endregion

    #region Public API - Color Selection

    /// <summary>
    /// 선택된 색상 업데이트 (UI만)
    /// </summary>
    /// <param name="colorIndex">선택할 색상 인덱스</param>
    public void UpdateColorSelection(int colorIndex)
    {
        if (!ValidateColorIndex(colorIndex))
            return;

        ClearAllSelections();
        ApplyColorSelection(colorIndex);
        UpdateSelectedIndex(colorIndex);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 선택 UI 업데이트: {colorIndex}");
    }

    /// <summary>
    /// 사용 중인 색상 목록 업데이트
    /// </summary>
    /// <param name="usedColors">사용 중인 색상 인덱스 목록</param>
    /// <param name="localPlayerColorIndex">로컬 플레이어 색상 인덱스</param>
    public void UpdateUsedColors(HashSet<int> usedColors, int localPlayerColorIndex)
    {
        usedColorIndices = usedColors ?? new HashSet<int>();
        RefreshButtonStates(localPlayerColorIndex);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 사용 중인 색상 업데이트: [{string.Join(", ", usedColors)}]");
    }

    /// <summary>
    /// 색상 변경 허용 상태 설정
    /// </summary>
    /// <param name="allowed">색상 변경 허용 여부</param>
    public void SetColorChangeAllowed(bool allowed)
    {
        isColorChangeAllowed = allowed;
        RefreshAllButtonInteractability();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 변경 허용 상태: {allowed}");
    }

    #endregion

    #region Private Methods - Color Selection

    /// <summary>
    /// 색상 버튼 클릭 처리
    /// </summary>
    /// <param name="colorIndex">클릭된 색상 인덱스</param>
    private void OnColorButtonClicked(int colorIndex)
    {
        if (!ValidateColorSelection(colorIndex))
            return;

        // UI 업데이트
        UpdateColorSelection(colorIndex);

        // 이벤트 발생
        TriggerColorSelected(colorIndex);

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 색상 버튼 클릭: {colorIndex}");
    }

    /// <summary>
    /// 모든 색상 선택 해제
    /// </summary>
    private void ClearAllSelections()
    {
        for (int i = 0; i < colorButtons.Length; i++)
        {
            if (colorButtons[i] == null) continue;

            ClearButtonSelection(colorButtons[i]);
        }
    }

    /// <summary>
    /// 개별 버튼 선택 해제
    /// </summary>
    /// <param name="button">해제할 버튼</param>
    private void ClearButtonSelection(Button button)
    {
        // 하이라이트 비활성화
        Transform highlight = button.transform.Find("Highlight");
        if (highlight != null)
        {
            highlight.gameObject.SetActive(false);
        }

        // 크기 초기화
        button.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 색상 선택 적용
    /// </summary>
    /// <param name="colorIndex">선택할 색상 인덱스</param>
    private void ApplyColorSelection(int colorIndex)
    {
        Button selectedButton = colorButtons[colorIndex];
        if (selectedButton == null) return;

        ApplyButtonSelection(selectedButton);
    }

    /// <summary>
    /// 개별 버튼 선택 적용
    /// </summary>
    /// <param name="button">선택할 버튼</param>
    private void ApplyButtonSelection(Button button)
    {
        // 하이라이트 활성화
        Transform highlight = button.transform.Find("Highlight");
        if (highlight != null)
        {
            highlight.gameObject.SetActive(true);
        }

        // 크기 확대
        button.transform.localScale = selectedScale;
    }

    /// <summary>
    /// 선택된 색상 인덱스 업데이트
    /// </summary>
    /// <param name="colorIndex">선택된 색상 인덱스</param>
    private void UpdateSelectedIndex(int colorIndex)
    {
        selectedColorIndex = colorIndex;
    }

    #endregion

    #region Private Methods - Button State Management

    /// <summary>
    /// 버튼 상태 새로고침
    /// </summary>
    /// <param name="localPlayerColorIndex">로컬 플레이어 색상 인덱스</param>
    private void RefreshButtonStates(int localPlayerColorIndex)
    {
        for (int i = 0; i < colorButtons.Length; i++)
        {
            if (colorButtons[i] == null) continue;

            UpdateButtonInteractability(i, localPlayerColorIndex);
        }
    }

    /// <summary>
    /// 개별 버튼 상호작용 가능 상태 업데이트
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <param name="localPlayerColorIndex">로컬 플레이어 색상 인덱스</param>
    private void UpdateButtonInteractability(int colorIndex, int localPlayerColorIndex)
    {
        Button button = colorButtons[colorIndex];
        if (button == null) return;

        bool isInteractable = CalculateButtonInteractability(colorIndex, localPlayerColorIndex);
        button.interactable = isInteractable;
    }

    /// <summary>
    /// 버튼 상호작용 가능 여부 계산
    /// </summary>
    /// <param name="colorIndex">색상 인덱스</param>
    /// <param name="localPlayerColorIndex">로컬 플레이어 색상 인덱스</param>
    /// <returns>상호작용 가능하면 true</returns>
    private bool CalculateButtonInteractability(int colorIndex, int localPlayerColorIndex)
    {
        // 색상 변경이 허용되지 않으면 비활성화
        if (!isColorChangeAllowed)
            return false;

        // 다른 플레이어가 사용 중인 색상인지 확인
        bool isUsedByOther = usedColorIndices.Contains(colorIndex) && colorIndex != localPlayerColorIndex;

        return !isUsedByOther;
    }

    /// <summary>
    /// 모든 버튼 상호작용 가능 상태 새로고침
    /// </summary>
    private void RefreshAllButtonInteractability()
    {
        for (int i = 0; i < colorButtons.Length; i++)
        {
            if (colorButtons[i] == null) continue;

            colorButtons[i].interactable = isColorChangeAllowed;
        }
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// 색상 인덱스 유효성 검증
    /// </summary>
    /// <param name="colorIndex">검증할 색상 인덱스</param>
    /// <returns>유효한 인덱스면 true</returns>
    private bool ValidateColorIndex(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= colorButtons.Length)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 유효하지 않은 색상 인덱스: {colorIndex}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 색상 선택 유효성 검증
    /// </summary>
    /// <param name="colorIndex">선택할 색상 인덱스</param>
    /// <returns>선택 가능하면 true</returns>
    private bool ValidateColorSelection(int colorIndex)
    {
        if (!ValidateColorIndex(colorIndex))
            return false;

        if (!isColorChangeAllowed)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 색상 변경이 허용되지 않습니다");
            return false;
        }

        Button button = colorButtons[colorIndex];
        if (button == null || !button.interactable)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 색상 {colorIndex}은(는) 선택할 수 없습니다");
            return false;
        }

        return true;
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// 색상 선택 이벤트 발생
    /// </summary>
    /// <param name="colorIndex">선택된 색상 인덱스</param>
    private void TriggerColorSelected(int colorIndex)
    {
        OnColorSelected?.Invoke(colorIndex);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 현재 선택된 색상 인덱스 반환
    /// </summary>
    /// <returns>선택된 색상 인덱스</returns>
    public int GetSelectedColorIndex()
    {
        return selectedColorIndex;
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
    /// 사용 가능한 색상 수 반환
    /// </summary>
    /// <returns>사용 가능한 색상 수</returns>
    public int GetAvailableColorCount()
    {
        return colorButtons?.Length ?? 0;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 색상 팔레트 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log Color Palette Status")]
    public void LogColorPaletteStatus()
    {
        Debug.Log($"=== ColorPaletteController 상태 정보 ===");
        Debug.Log($"초기화 완료: {isInitialized}");
        Debug.Log($"선택된 색상: {selectedColorIndex}");
        Debug.Log($"색상 변경 허용: {isColorChangeAllowed}");
        Debug.Log($"생성된 버튼 수: {colorButtons?.Length ?? 0}");
        Debug.Log($"사용 중인 색상: [{string.Join(", ", usedColorIndices)}]");
    }

    #endregion
}