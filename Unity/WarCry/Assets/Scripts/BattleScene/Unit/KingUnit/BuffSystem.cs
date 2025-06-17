using System.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어 버프 효과를 관리하는 클래스
/// </summary>
public class BuffSystem : NetworkBehaviour
{
    [Header("버프 상태")]
    [SyncVar(hook = nameof(OnAttackBuffChanged))]
    public bool attackBuffActive = false;
    
    [SyncVar(hook = nameof(OnDefenseBuffChanged))]
    public bool defenseBuffActive = false;
    
    [SyncVar(hook = nameof(OnMoveSpeedBuffChanged))]
    public bool moveSpeedBuffActive = false;
    
    [Header("버프 설정")]
    [SerializeField] private float attackBuffDuration = 5f;     // 공격력 버프 지속 시간
    [SerializeField] private float attackBuffAmount = 25f;      // 공격력 증가량 (절대값 +25)
    [SerializeField] private float speedBuffAmount = 2f;      // 이동속도 증가량
    
    [SerializeField] private float defenseBuffDuration = 5f;    // 방어력 버프 지속 시간
    [SerializeField] private float defenseBuffAmount = 10f;     // 방어력 증가량 (절대값 +10)
    
    [SerializeField] private float healingAmount = 30f;         // 고정 체력 회복량 (30)
    
    [Header("궁극기 설정")]
    [SerializeField] private float ultimateDamage = 50f;        // 궁극기 데미지
    [SerializeField] private float ultimateRadius = 5f;         // 궁극기 공격 범위
    [SerializeField] private string enemyTag = "Enemy";         // 적 태그 (기본값: "Enemy")
    
    [Header("참조")]
    private TheOneAndOnlyStats unitStats;
    private KingController kingController;
    private UnityEngine.AI.NavMeshAgent agent;
    
    // 원래 스탯 값 저장
    private float originalAttack;
    private float originalDefense;
    private float originalSpeed;
    
    // 버프 종료 타이머용 코루틴
    private Coroutine attackBuffCoroutine;
    private Coroutine defenseBuffCoroutine;
    private Coroutine moveSpeedBuffCoroutine;
    
    // 버프 이벤트 델리게이트
    public delegate void BuffChangeHandler(bool active);
    public event BuffChangeHandler OnAttackBuffEvent;
    public event BuffChangeHandler OnDefenseBuffEvent;
    public event BuffChangeHandler OnMoveSpeedBuffEvent;
    
    // 초기화
    private void Awake()
    {
        unitStats = GetComponent<TheOneAndOnlyStats>();
        kingController = GetComponent<KingController>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        
        // 초기값 저장
        if (unitStats != null)
        {
            originalAttack = unitStats.attackDamage;
            originalDefense = unitStats.armor;
            originalSpeed = unitStats.moveSpeed;
        }
    }
    
    // 공격력 버프 적용
    [Server]
    public void ApplyAttackBuff()
    {
        if (attackBuffCoroutine != null)
        {
            StopCoroutine(attackBuffCoroutine);
        }
        
        // 원래 값 저장
        originalAttack = unitStats.attackDamage;
        
        // 버프 적용
        unitStats.attackDamage = originalAttack + attackBuffAmount;
        attackBuffActive = true;
        
        // 이동속도 버프도 같이 적용
        ApplyMoveSpeedBuff();
        
        // 버프 종료 타이머 시작
        attackBuffCoroutine = StartCoroutine(EndAttackBuffAfterDelay());
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 적용: 공격력 +{attackBuffAmount} 증가 ({originalAttack} → {unitStats.attackDamage})");
    }
    
    // 공격력 버프 종료 타이머
    [Server]
    private IEnumerator EndAttackBuffAfterDelay()
    {
        yield return new WaitForSeconds(attackBuffDuration);
        
        // 버프 종료 - 원래 값으로 복원
        unitStats.attackDamage = originalAttack;
        attackBuffActive = false;
        attackBuffCoroutine = null;
        
        // 이벤트 직접 호출 (SyncVar hook이 호출되지 않는 경우를 대비)
        if (OnAttackBuffEvent != null)
            OnAttackBuffEvent.Invoke(false);
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 종료: 공격력 원래 값으로 복원");
        
        // 이동속도 버프도 종료
        EndMoveSpeedBuff();
    }
    
    // 방어력 버프 적용
    [Server]
    public void ApplyDefenseBuff()
    {
        if (defenseBuffCoroutine != null)
        {
            StopCoroutine(defenseBuffCoroutine);
        }
        
        // 원래 방어력 저장
        originalDefense = unitStats.armor;
        
        // 버프 적용 - 방어력 값 직접 증가
        unitStats.armor = originalDefense + defenseBuffAmount;
        defenseBuffActive = true;
        
        // 버프 종료 타이머 시작
        defenseBuffCoroutine = StartCoroutine(EndDefenseBuffAfterDelay());
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 적용: 방어력 +{defenseBuffAmount} 증가 ({originalDefense} → {unitStats.armor})");
    }
    
    // 방어력 버프 종료 타이머
    [Server]
    private IEnumerator EndDefenseBuffAfterDelay()
    {
        yield return new WaitForSeconds(defenseBuffDuration);
        
        // 버프 종료 - 방어력 원래 값으로 복원
        unitStats.armor = originalDefense;
        defenseBuffActive = false;
        defenseBuffCoroutine = null;
        
        // 이벤트 직접 호출 (SyncVar hook이 호출되지 않는 경우를 대비)
        if (OnDefenseBuffEvent != null)
            OnDefenseBuffEvent.Invoke(false);
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 종료: 방어력 원래 값으로 복원 ({unitStats.armor})");
    }
    
    // 이동속도 버프 적용
    [Server]
    public void ApplyMoveSpeedBuff()
    {
        if (moveSpeedBuffCoroutine != null)
        {
            StopCoroutine(moveSpeedBuffCoroutine);
        }
        
        // 원래 이동속도 저장
        originalSpeed = unitStats.moveSpeed;
        
        // 버프 적용
        unitStats.moveSpeed = originalSpeed + speedBuffAmount;
        moveSpeedBuffActive = true;
        
        // 버프 종료 타이머 시작
        moveSpeedBuffCoroutine = StartCoroutine(EndMoveSpeedBuffAfterDelay());
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이동속도 버프 적용: 이동속도 +{speedBuffAmount} 증가 ({originalSpeed} → {unitStats.moveSpeed})");
    }
    
    // 이동속도 버프 종료 타이머
    [Server]
    private IEnumerator EndMoveSpeedBuffAfterDelay()
    {
        yield return new WaitForSeconds(attackBuffDuration);
        
        EndMoveSpeedBuff();
    }
    
    // 이동속도 버프 종료
    [Server]
    private void EndMoveSpeedBuff()
    {
        if (moveSpeedBuffActive)
        {
            // 버프 종료 - 원래 값으로 복원
            unitStats.moveSpeed = originalSpeed;
            moveSpeedBuffActive = false;
            moveSpeedBuffCoroutine = null;
            
            // 이벤트 직접 호출 (SyncVar hook이 호출되지 않는 경우를 대비)
            if (OnMoveSpeedBuffEvent != null)
                OnMoveSpeedBuffEvent.Invoke(false);
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이동속도 버프 종료: 이동속도 원래 값으로 복원");
        }
    }
    
    // 체력 회복 효과 적용
    [Server]
    public void ApplyHealingEffect()
    {
        if (unitStats != null)
        {
            float healAmount = healingAmount;
            unitStats.currentHealth = Mathf.Min(unitStats.currentHealth + healAmount, unitStats.maxHealth);
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 체력 회복 적용: {healAmount} HP 회복됨");
        }
    }
    
    // 궁극기: 범위 내 모든 적에게 데미지를 주는 광역 공격
    [Server]
    public void ApplyUltimateEffect()
    {
        // 현재 유닛의 팀 인덱스 가져오기
        Unit myUnit = GetComponent<Unit>();
        if (myUnit == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Unit 컴포넌트를 찾을 수 없습니다!");
            return;
        }
        
        TeamIndex myTeam = myUnit.teamIndex;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 사용자 팀: {myTeam}");
        
        // 플레이어 위치에서 일정 범위 내 모든 콜라이더 찾기
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, ultimateRadius);
        int enemiesHit = 0;
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 사용: 범위 {ultimateRadius}m 내 객체 탐색 시작. 감지된 콜라이더: {hitColliders.Length}개");
        
        // 각 콜라이더를 확인하여 적 팀 객체에만 데미지 적용
        foreach (var hitCollider in hitColliders)
        {
            // Unit 컴포넌트가 있는지 확인
            Unit hitUnit = hitCollider.GetComponent<Unit>();
            if (hitUnit == null) continue;
            
            // 다른 팀인지 확인 (같은 팀이면 건너뛰기)
            if (hitUnit.teamIndex == myTeam)
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 같은 팀 유닛 '{hitCollider.name}' 무시됨 (팀: {hitUnit.teamIndex})");
                continue;
            }
            
            // TheOneAndOnlyStats 컴포넌트가 있는지 확인
            TheOneAndOnlyStats enemyStats = hitCollider.GetComponent<TheOneAndOnlyStats>();
            if (enemyStats != null && !enemyStats.isDead)
            {
                // 적 유닛에게 데미지 적용
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 적 '{hitCollider.name}'에게 {ultimateDamage} 데미지 적용 (팀: {hitUnit.teamIndex})");
                enemyStats.TakeDamage(ultimateDamage);
                enemiesHit++;
            }
        }
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 효과 종료: 총 {enemiesHit}명의 적에게 데미지 적용됨");
    }
    
    // 공격력 버프 상태 변경 시 호출되는 함수
    private void OnAttackBuffChanged(bool oldValue, bool newValue)
    {
        OnAttackBuffEvent?.Invoke(newValue);
    }
    
    // 방어력 버프 상태 변경 시 호출되는 함수
    private void OnDefenseBuffChanged(bool oldValue, bool newValue)
    {
        OnDefenseBuffEvent?.Invoke(newValue);
    }
    
    // 이동속도 버프 상태 변경 시 호출되는 함수
    private void OnMoveSpeedBuffChanged(bool oldValue, bool newValue)
    {
        // NavMeshAgent 속도 업데이트
        if (agent != null)
        {
            agent.speed = unitStats.moveSpeed;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이동속도 변경됨: NavMeshAgent 속도 = {agent.speed}");
        }
        
        OnMoveSpeedBuffEvent?.Invoke(newValue);
    }
    
    // 데미지 감소 계수 반환 (방어력 버프가 활성화된 경우)
    public float GetArmorBonus()
    {
        return defenseBuffActive ? defenseBuffAmount : 0f;
    }
    
    // 궁극기 범위 반환
    public float GetUltimateRadius()
    {
        return ultimateRadius;
    }
    
    // 디버깅을 위한 Gizmos - 궁극기 범위 표시
    private void OnDrawGizmosSelected()
    {
        // 궁극기 범위 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, ultimateRadius);
    }
}