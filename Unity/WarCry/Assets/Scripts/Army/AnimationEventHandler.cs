using UnityEngine;

[RequireComponent(typeof(UnitController))]
public class AnimationEventHandler : MonoBehaviour
{
    private UnitController unitController;
    private Animator animator;
    private bool attackTriggered = false;
    
    [Tooltip("애니메이션 이벤트가 없을 때 사용할 수동 공격 타이밍(초)")]
    public float manualAttackTiming = 0.5f;
    
    [Tooltip("공격 애니메이션/상태 이름 (기본: 'Attack')")]
    public string attackStateName = "Attack";

    void Awake()
    {
        unitController = GetComponent<UnitController>();
        animator = GetComponent<Animator>();
        
        if (unitController == null)
            Debug.LogError("AnimationEventHandler requires a UnitController on the same GameObject.");
    }
    
    void Update()
    {
        // 애니메이션 상태 검사
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        
        // 공격 애니메이션이 재생 중인지 확인 (여러 가능한 이름 체크)
        bool isAttacking = stateInfo.IsName(attackStateName) || 
                          stateInfo.IsName("Base Layer." + attackStateName) ||
                          stateInfo.IsName("Attack") || 
                          stateInfo.IsName("Base Layer.Attack");
        
        if (isAttacking)
        {
            // 공격 모션의 중간 지점에서 자동으로 OnAttackHit 호출
            float normalizedTime = stateInfo.normalizedTime % 1.0f; // 애니메이션 루프 고려
            
            // 애니메이션의 manualAttackTiming 시점(기본 0.5 = 50%)에서 공격 발동
            if (normalizedTime >= manualAttackTiming && !attackTriggered)
            {
                attackTriggered = true;
                Debug.Log($"{gameObject.name}: 수동 애니메이션 이벤트 발생 (normalizedTime: {normalizedTime})");
                OnAttackHit();
            }
            // 애니메이션이 끝나면(또는 새 루프가 시작되면) 트리거 리셋
            else if (normalizedTime < manualAttackTiming)
            {
                attackTriggered = false;
            }
        }
        else
        {
            // 공격 애니메이션이 아닌 상태면 트리거 리셋
            attackTriggered = false;
        }
    }

    // 애니메이션 이벤트에서 호출: 공격 히트 시점
    public void OnAttackHit()
    {
        Debug.Log($"{gameObject.name}: AnimationEventHandler.OnAttackHit() 호출됨");
        unitController.OnAttackHit();
    }

    // 필요 시 추가 이벤트 핸들러
    public void OnAttackStart()
    {
        unitController.OnAttackStart();
    }

    public void OnAttackEnd()
    {
        unitController.OnAttackEnd();
    }
}
