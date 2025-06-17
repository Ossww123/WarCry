using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class HighlightManager : MonoBehaviour
{
    private Transform highlightedObj;
    private Transform selectedObj;
    public string targetTag = "Enemy";

    private Outline highlightOutline;
    private RaycastHit hit;
    
    // 색상 및 두께 설정 추가
    public Color hoverColor = Color.white;  // 마우스 오버 시 흰색으로 변경
    public Color selectedColor = Color.red;
    public float outlineWidth = 5f;
    
    // 디버그 모드
    public bool debugMode = true;

    void Start()
    {
        if(debugMode)
            Debug.Log($"[HighlightManager] 초기화됨. 대상 태그: {targetTag}");
    }

    void Update()
    {
        // 마우스 클릭 감지
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            // UI 요소 위에 마우스가 있는지 확인
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!pointerOverUI)
            {
                // 레이캐스트로 클릭한 객체 확인
                Ray clickRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(clickRay, out RaycastHit clickHit, 100f))
                {
                    // 클릭한 객체가 대상 태그를 가지고 있는지 확인
                    if (clickHit.transform != null && clickHit.transform.CompareTag(targetTag))
                    {
                        // 유닛 클릭 - 선택 처리
                        if(debugMode)
                            Debug.Log($"[HighlightManager] 유닛 클릭됨: {clickHit.transform.name}");
                        SelectObject(clickHit.transform);
                    }
                    else if (clickHit.transform != null && clickHit.transform.CompareTag("Ground"))
                    {
                        // 땅 클릭 - 선택 해제
                        if(debugMode)
                            Debug.Log($"[HighlightManager] 땅 클릭됨: {clickHit.transform.name}, 태그: {clickHit.transform.tag}");
                        DeselectCurrentObject();
                    }
                    else
                    {
                        // 다른 객체 클릭 - 선택 해제
                        if(debugMode)
                            Debug.Log($"[HighlightManager] 다른 객체 클릭됨: {clickHit.transform.name}, 태그: {clickHit.transform.tag}");
                        DeselectCurrentObject();
                    }
                }
                else
                {
                    // 아무것도 클릭하지 않음 - 선택 해제
                    if(debugMode)
                        Debug.Log("[HighlightManager] 아무것도 감지되지 않음");
                    DeselectCurrentObject();
                }
            }
            else if(debugMode)
            {
                Debug.Log("[HighlightManager] UI 위에서 클릭됨");
            }
        }
        
        // 마우스 호버 효과 업데이트
        UpdateHoverHighlight();
    }
    
    // 마우스 호버 효과 업데이트
    private void UpdateHoverHighlight()
    {
        // 이전 하이라이트 객체 처리
        if (highlightedObj != null)
        {
            // 선택된 객체가 아닌 경우에만 아웃라인 제거
            if (selectedObj == null || highlightedObj != selectedObj)
            {
                Outline outline = highlightedObj.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                    if(debugMode)
                        Debug.Log($"[HighlightManager] 이전 하이라이트 비활성화: {highlightedObj.name}");
                }
            }
        }
        
        // 현재 호버 객체 초기화
        highlightedObj = null;
        highlightOutline = null;

        // 메인 카메라 확인
        if (Camera.main == null)
            return;
            
        // UI 요소 위에 마우스가 있는지 확인
        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (pointerOverUI)
            return;
            
        // 레이캐스트로 마우스 아래 객체 확인
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform != null && hit.transform.CompareTag(targetTag))
            {
                // 유닛 위에 마우스가 있음
                highlightedObj = hit.transform;
                
                // 이미 선택된 객체인지 확인
                bool isSelected = (selectedObj != null && highlightedObj == selectedObj);
                
                // 아웃라인 컴포넌트 가져오기
                highlightOutline = highlightedObj.GetComponent<Outline>();
                if (highlightOutline == null)
                {
                    // 없으면 추가
                    highlightOutline = highlightedObj.gameObject.AddComponent<Outline>();
                }
                
                // 이미 선택된 객체라면 빨간색 유지, 아니면 호버 색상(흰색) 설정
                if (!isSelected)
                {
                    highlightOutline.OutlineColor = hoverColor;
                    highlightOutline.OutlineWidth = outlineWidth;
                    highlightOutline.enabled = true;
                    
                    if (debugMode)
                        Debug.Log($"[HighlightManager] 호버 하이라이트: {highlightedObj.name}, 색상=흰색");
                }
                else if (debugMode)
                {
                    Debug.Log($"[HighlightManager] 선택된 객체 위에 호버: {highlightedObj.name}, 색상=빨간색 유지");
                }
            }
        }
    }
    
    // 객체 선택 처리
    private void SelectObject(Transform obj)
    {
        if (obj == null)
            return;
            
        if (debugMode)
            Debug.Log($"[HighlightManager] 객체 선택: {obj.name}");
            
        // 기존 선택 객체 처리
        DeselectCurrentObject();
        
        // 새 객체 선택
        selectedObj = obj;
        
        // 아웃라인 컴포넌트 가져오기
        Outline outline = selectedObj.GetComponent<Outline>();
        if (outline == null)
        {
            // 없으면 추가
            outline = selectedObj.gameObject.AddComponent<Outline>();
        }
        
        // 선택 색상(빨간색) 설정
        outline.OutlineColor = selectedColor;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = true;
        
        if (debugMode)
            Debug.Log($"[HighlightManager] 선택 완료: {selectedObj.name}, 색상=빨간색");
    }
    
    // 현재 선택된 객체 선택 해제
    private void DeselectCurrentObject()
    {
        if (selectedObj != null)
        {
            if (debugMode)
                Debug.Log($"[HighlightManager] 선택 해제 시도: {selectedObj.name}");
                
            // 아웃라인 비활성화
            Outline outline = selectedObj.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
                if(debugMode)
                    Debug.Log($"[HighlightManager] 아웃라인 비활성화 완료: {selectedObj.name}");
            }
            else if(debugMode)
            {
                Debug.Log($"[HighlightManager] 아웃라인 컴포넌트를 찾을 수 없음: {selectedObj.name}");
            }
            
            // 선택 객체 초기화
            selectedObj = null;
            if(debugMode)
                Debug.Log("[HighlightManager] 선택 객체 변수 초기화 완료");
        }
        else if(debugMode)
        {
            Debug.Log("[HighlightManager] 선택 해제 시도했으나 선택된 객체가 없음");
        }
    }
    
    // 외부에서 호출할 수 있는 대상 선택 메소드
    public void SelectTarget(GameObject target)
    {
        if (target == null)
            return;
            
        if(debugMode)
            Debug.Log($"[HighlightManager] 외부에서 타겟 설정 요청: {target.name}");
        
        // 이미 같은 타겟이 선택되어 있다면 중복 처리 방지
        if(selectedObj != null && selectedObj == target.transform)
        {
            if(debugMode)
                Debug.Log($"[HighlightManager] 이미 선택된 타겟임: {target.name}");
            return;
        }
        
        // 새 타겟 선택
        SelectObject(target.transform);
    }
    
    // 외부에서 호출할 수 있는 선택 해제 메소드
    public void DeselectHighlight()
    {
        if(debugMode)
            Debug.Log("[HighlightManager] 외부에서 선택 해제 요청됨");
        DeselectCurrentObject();
    }
    
    // KingController와의 호환성을 위한 메서드
    public void SelectedHighlight()
    {
        // 현재 마우스 아래 있는 객체 선택
        if (highlightedObj != null && highlightedObj.CompareTag(targetTag))
        {
            if(debugMode)
                Debug.Log($"[HighlightManager] SelectedHighlight 메서드에서 객체 선택: {highlightedObj.name}");
            SelectObject(highlightedObj);
        }
    }

    // 지면 레이어와 유닛 감지를 개선하기 위한 메서드
    private bool IsGroundOrNonTargetObject(Transform obj)
    {
        // null 체크
        if (obj == null) return false;
        
        // 대상 태그를 가진 객체가 아닌지 확인
        return !obj.CompareTag(targetTag);
    }
}
