using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Abilities : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool debugEnabled = false;

    [Header("Ability 1")]
    public KeyCode ability1Key;

    public float ability1Cooldown = 10;
    public bool isAbility1Cooldown = false;
    public float currentAbility1Cooldown;

    public Canvas ability1Canvas;
    public Image ability1Skillshot;

    [Header("Ability 2")]
    public KeyCode ability2Key;

    public float ability2Cooldown = 10;
    public bool isAbility2Cooldown = false;
    public float currentAbility2Cooldown;

    public Canvas ability2Canvas;
    public Image ability2RangeIndicator;
    public float maxAbility2Distance = 7;

    [Header("Ability 3")]
    public KeyCode ability3Key;

    public float ability3Cooldown = 15;
    public bool isAbility3Cooldown = false;
    public float currentAbility3Cooldown;

    public Canvas ability3Canvas;
    public Image ability3Cone;

    [Header("Ability 4")]
    public KeyCode ability4Key;

    public float ability4Cooldown = 30;
    public bool isAbility4Cooldown = false;
    public float currentAbility4Cooldown;
    
    // Ability4 UI 요소 추가
    public Canvas ability4Canvas;
    public Image ability4RangeIndicator;
    public float maxAbility4Distance = 5; // 광역 공격 범위
    
    private Vector3 position;
    private RaycastHit hit;
    private Ray ray;
    
    // KingController 참조 추가
    private KingController kingController;
    
    // R키 상태 추적용
    private bool ability4KeyPressed = false;

    void Start()
    {
        ability1Skillshot.enabled = false;
        ability2RangeIndicator.enabled = false;
        ability3Cone.enabled = false;
        
        // Ability4 범위 표시기 비활성화 초기화
        if (ability4RangeIndicator != null)
            ability4RangeIndicator.enabled = false;

        ability1Canvas.enabled = false;
        ability2Canvas.enabled = false;
        ability3Canvas.enabled = false;
        
        // Ability4 캔버스 비활성화 초기화
        if (ability4Canvas != null)
            ability4Canvas.enabled = false;
        
        // KingController 컴포넌트 참조 가져오기
        kingController = GetComponent<KingController>();
        
        // 디버그 로그로 UI 요소 확인
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Ability4Canvas: {(ability4Canvas != null ? "있음" : "없음")}, Ability4RangeIndicator: {(ability4RangeIndicator != null ? "있음" : "없음")}");
    }

    void Update()
    {
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Ability1Input();
        Ability2Input();
        Ability3Input();
        Ability4Input();

        AbilityCooldown(ref currentAbility1Cooldown, ref isAbility1Cooldown);
        AbilityCooldownHandler.TriggerAbility1CooldownChanged(currentAbility1Cooldown);

        AbilityCooldown(ref currentAbility2Cooldown, ref isAbility2Cooldown);
        AbilityCooldownHandler.TriggerAbility2CooldownChanged(currentAbility2Cooldown);

        AbilityCooldown(ref currentAbility3Cooldown, ref isAbility3Cooldown);
        AbilityCooldownHandler.TriggerAbility3CooldownChanged(currentAbility3Cooldown);

        AbilityCooldown(ref currentAbility4Cooldown, ref isAbility4Cooldown);
        AbilityCooldownHandler.TriggerAbility4CooldownChanged(currentAbility4Cooldown);

        Ability1Canvas();
        Ability2Canvas();
        Ability3Canvas();
        Ability4Canvas(); // Ability4 캔버스 업데이트 호출 추가
    }

    private void AbilityCooldown(ref float currentCooldown, ref bool isCooldown)
    {
        if (isCooldown)
        {
            currentCooldown -= Time.deltaTime;

            if (currentCooldown <= 0f)
            {
                isCooldown = false;
                currentCooldown = 0f;
            }
        }
    }

    private void Ability1Canvas()
    {
        if (ability1Skillshot.enabled)
        {
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                position = new Vector3(hit.point.x, hit.point.y, hit.point.z);
            }

            Quaternion ab1Canvas = Quaternion.LookRotation(position - transform.position);
            ab1Canvas.eulerAngles =
                new Vector3(0, ab1Canvas.eulerAngles.y, ab1Canvas.eulerAngles.z);

            ability1Canvas.transform.rotation =
                Quaternion.Lerp(ab1Canvas, ability1Canvas.transform.rotation, 0);
        }
    }

    private void Ability2Canvas()
    {
        int layerMask = ~LayerMask.GetMask("Player", "Enemy");
        if (!ability2Canvas.enabled)
        {
            return;
        }

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
        {
            Debug.Log($"[{DebugUtils.GetCallerInfo()}] Hit Detected: {hit.collider.gameObject}");
            if (hit.collider.gameObject != this.gameObject)
            {
                Debug.Log($"[{DebugUtils.GetCallerInfo()}] Hit point: {hit.point}");
                position = hit.point;
            }
        }

        var hitPosDir = (hit.point - transform.position).normalized;
        float distance = Vector3.Distance(hit.point, transform.position);
        distance = Mathf.Min(distance, maxAbility2Distance);

        var newHitPos = transform.position + hitPosDir * distance;
        ability2Canvas.transform.position = newHitPos;
    }

    private void Ability3Canvas()
    {
        if (ability3Cone.enabled)
        {
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                position = new Vector3(hit.point.x, hit.point.y, hit.point.z);
            }

            Quaternion ab3Canvas = Quaternion.LookRotation(position - transform.position);
            ab3Canvas.eulerAngles =
                new Vector3(0, ab3Canvas.eulerAngles.y, ab3Canvas.eulerAngles.z);

            ability3Canvas.transform.rotation =
                Quaternion.Lerp(ab3Canvas, ability3Canvas.transform.rotation, 0);
        }
    }
    
    // Ability4 캔버스 처리 메서드 추가
    private void Ability4Canvas()
    {
        // Canvas가 null이거나 비활성화 상태면 리턴
        if (ability4Canvas == null || !ability4Canvas.enabled)
        {
            return;
        }
        
        // 플레이어 위치에 범위 표시
        ability4Canvas.transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);
        
        // 캔버스 회전 (메인 카메라에 맞춰)
        if (Camera.main != null)
        {
            // XZ 평면에서만 카메라 방향으로 회전 (Y축만 사용)
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0;
            
            if (camForward != Vector3.zero) // 0벡터 예외 처리
            {
                ability4Canvas.transform.rotation = Quaternion.LookRotation(camForward);
            }
        }
        
        // 디버그: 범위 표시기 상태 확인
        if (ability4RangeIndicator != null && debugEnabled)
        {
            Debug.Log($"[{DebugUtils.GetCallerInfo()}] Ability4 Range Indicator 활성화 상태: {ability4RangeIndicator.enabled}, 크기: {ability4RangeIndicator.rectTransform.sizeDelta}");
        }
    }

    private void Ability1Input()
    {
        if (Input.GetKeyDown(ability1Key) && !isAbility1Cooldown)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 스킬 사용 (Q)");
            
            // 쿨다운 시작
            isAbility1Cooldown = true;
            currentAbility1Cooldown = ability1Cooldown;
            
            // UI 비활성화 (혹시 다른 스킬의 UI가 켜져있을 경우)
            ability1Canvas.enabled = false;
            ability1Skillshot.enabled = false;
            ability2Canvas.enabled = false;
            ability2RangeIndicator.enabled = false;
            ability3Canvas.enabled = false;
            ability3Cone.enabled = false;
            if (ability4Canvas != null)
                ability4Canvas.enabled = false;
            if (ability4RangeIndicator != null)
                ability4RangeIndicator.enabled = false;
            
            // 즉시 스킬 사용
            if (kingController != null)
            {
                kingController.CmdUseAbility(1);
            }
        }
    }

    private void Ability2Input()
    {
        if (Input.GetKeyDown(ability2Key) && !isAbility2Cooldown)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 스킬 사용 (W)");
            
            // 쿨다운 시작
            isAbility2Cooldown = true;
            currentAbility2Cooldown = ability2Cooldown;
            
            // UI 비활성화 (혹시 다른 스킬의 UI가 켜져있을 경우)
            ability1Canvas.enabled = false;
            ability1Skillshot.enabled = false;
            ability2Canvas.enabled = false;
            ability2RangeIndicator.enabled = false;
            ability3Canvas.enabled = false;
            ability3Cone.enabled = false;
            if (ability4Canvas != null)
                ability4Canvas.enabled = false;
            if (ability4RangeIndicator != null)
                ability4RangeIndicator.enabled = false;
            
            // 즉시 스킬 사용
            if (kingController != null)
            {
                kingController.CmdUseAbility(2);
            }
        }
    }

    private void Ability3Input()
    {
        if (Input.GetKeyDown(ability3Key) && !isAbility3Cooldown)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 체력 회복 스킬 사용 (E)");
            
            // 쿨다운 시작
            isAbility3Cooldown = true;
            currentAbility3Cooldown = ability3Cooldown;
            
            // UI 비활성화 (혹시 다른 스킬의 UI가 켜져있을 경우)
            ability1Canvas.enabled = false;
            ability1Skillshot.enabled = false;
            ability2Canvas.enabled = false;
            ability2RangeIndicator.enabled = false;
            ability3Canvas.enabled = false;
            ability3Cone.enabled = false;
            if (ability4Canvas != null)
                ability4Canvas.enabled = false;
            if (ability4RangeIndicator != null)
                ability4RangeIndicator.enabled = false;
            
            // 즉시 스킬 사용
            if (kingController != null)
            {
                kingController.CmdUseAbility(3);
            }
        }
    }

    private void Ability4Input()
    {
        // R키 눌림 감지
        if (Input.GetKeyDown(ability4Key) && !isAbility4Cooldown)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 UI 표시 (R)");
            
            // 다른 스킬 UI 비활성화
            ability1Canvas.enabled = false;
            ability1Skillshot.enabled = false;
            ability2Canvas.enabled = false;
            ability2RangeIndicator.enabled = false;
            ability3Canvas.enabled = false;
            ability3Cone.enabled = false;
            
            // 궁극기 범위 표시 활성화
            if (ability4Canvas != null)
            {
                ability4Canvas.enabled = true;
                
                if (ability4RangeIndicator != null)
                {
                    ability4RangeIndicator.enabled = true;
                    
                    // BuffSystem에서 범위 값 가져오기
                    KingController kingController = GetComponent<KingController>();
                    if (kingController != null)
                    {
                        BuffSystem buffSystem = GetComponent<BuffSystem>();
                        if (buffSystem != null)
                        {
                            // 범위 크기 조정
                            float scale = buffSystem.GetUltimateRadius() * 2; // 직경으로 변환
                            ability4RangeIndicator.rectTransform.sizeDelta = new Vector2(scale, scale);
                            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 범위 표시 크기 조정: {scale}");
                        }
                    }
                }
            }
            
            // R키 눌림 상태 기록
            ability4KeyPressed = true;
        }
        
        // R키 떼는 순간 감지 - 즉시 스킬 사용
        if (Input.GetKeyUp(ability4Key) && ability4KeyPressed && !isAbility4Cooldown)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 사용 (R)");
            
            // UI 비활성화
            if (ability4Canvas != null)
                ability4Canvas.enabled = false;
            if (ability4RangeIndicator != null)
                ability4RangeIndicator.enabled = false;
            
            // 쿨다운 시작
            isAbility4Cooldown = true;
            currentAbility4Cooldown = ability4Cooldown;
            
            // 키 눌림 상태 초기화
            ability4KeyPressed = false;
            
            // 스킬 사용
            if (kingController != null)
            {
                kingController.CmdUseAbility(4);
            }
        }
    }
}