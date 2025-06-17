using UnityEngine;
using Mirror;
using UnityEngine.AI;

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BuffSystem))]
public class KingController : NetworkBehaviour
{
    [Header("Component References")]
    private NavMeshAgent agent;
    private Animator anim;
    private Unit unitInfo;
    private HighlightManager hmScript;
    private TheOneAndOnlyStats unitStats;
    private BuffSystem buffSystem;

    [Header("Movement Settings")]
    [SerializeField] private float rotateSpeedMovement = 0.05f;
    private float rotateVelocity;
    [SerializeField] private float motionSmoothTime = 0.1f;

    [Header("Rotation Before Attack")]
    [SerializeField] private float rotateSpeedBeforeAttack = 0.1f;  // 추가: 공격 전 회전 속도
    [SerializeField] private float minAngleToAttack = 5f;           // 추가: 공격 가능한 최소 각도

    [Header("Target")]
    [SyncVar] private GameObject targetEnemy;
    private bool performMeleeAttack = true;
    private bool isRotatingToAttack = false;
    private float nextAttackTime = 0;
    [SyncVar] private bool isAttacking = false; // 공격 중 상태를 추적하는 변수 추가

    [Header("Debug Settings")]
    [SerializeField] private bool debugEnabled = true;
    [SerializeField] private LayerMask groundLayerMask; // "Ground" 레이어를 인스펙터에서 설정

    [Header("스킬 파티클 효과")]
    [SerializeField] private GameObject attackBuffParticle;    // 공격력 버프 파티클 프리팹
    [SerializeField] private GameObject defenseBuffParticle;   // 방어력 버프 파티클 프리팹
    [SerializeField] private GameObject healingParticle;       // 체력 회복 파티클 프리팹
    [SerializeField] private GameObject ultimateParticle;      // 궁극기 파티클 프리팹
    
    [Header("파티클 지속 시간 설정")]
    [SerializeField] private float ultimateParticleTime = 2.5f;   // 궁극기 파티클 지속 시간
    [SerializeField] private float healingParticleTime = 0.5f;    // 체력 회복 파티클 지속 시간
    [SerializeField] private float buffParticleTime = 1f;         // 일반 버프 파티클 지속 시간

    [Header("버프 지속 파티클 효과")]
    [SerializeField] private GameObject attackBuffActiveParticle;  // 공격력 버프 지속 파티클 프리팹
    [SerializeField] private GameObject defenseBuffActiveParticle; // 방어력 버프 지속 파티클 프리팹
    [SerializeField] private Vector3 buffParticleOffset = new Vector3(0, 0, 0); // 파티클 위치 오프셋

    // 플레이어 제어 가능 여부
    private bool canControl = false;

    // 활성화된 버프 파티클 참조 저장
    private GameObject activeAttackBuffParticle;
    private GameObject activeDefenseBuffParticle;

    private void Awake()
    {
        // 컴포넌트 가져오기
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        unitInfo = GetComponent<Unit>();
        hmScript = GetComponent<HighlightManager>();
        unitStats = GetComponent<TheOneAndOnlyStats>();
        buffSystem = GetComponent<BuffSystem>();

        // Ground 레이어 자동 설정 (레이어 8번이 Ground라고 가정)
        if (groundLayerMask.value == 0)
        {
            groundLayerMask = 1 << 8; // Ground 레이어를 8번으로 가정
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] Ground 레이어 자동 설정됨");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        BattleController.OnBattleStart += OnBattleStart;
        
        // OnUnitDeath 이벤트 구독 추가
        if (unitStats != null)
        {
            unitStats.OnUnitDeath += OnUnitDeath;
            unitStats.OnResurrection += OnResurrection;
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 이벤트 구독됨: OnUnitDeath, OnResurrection");
        }
        
        // BuffSystem 이벤트 구독
        if (buffSystem != null)
        {
            buffSystem.OnAttackBuffEvent += OnAttackBuffChanged;
            buffSystem.OnDefenseBuffEvent += OnDefenseBuffChanged;
            buffSystem.OnMoveSpeedBuffEvent += OnMoveSpeedBuffChanged;
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 이벤트 구독됨: OnAttackBuffEvent, OnDefenseBuffEvent, OnMoveSpeedBuffEvent");
        }

        // 테스트 목적으로 즉시 조작 활성화 (필요시 주석 처리)
        if (isLocalPlayer && debugEnabled)
        {
            EnableControl(true);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 테스트 모드: 즉시 조작 활성화됨");
        }
    }

    void OnDestroy()
    {
        BattleController.OnBattleStart -= OnBattleStart;
        
        // 이벤트 구독 해제
        if (unitStats != null)
        {
            unitStats.OnUnitDeath -= OnUnitDeath;
            unitStats.OnResurrection -= OnResurrection;
        }
        
        // BuffSystem 이벤트 구독 해제
        if (buffSystem != null)
        {
            buffSystem.OnAttackBuffEvent -= OnAttackBuffChanged;
            buffSystem.OnDefenseBuffEvent -= OnDefenseBuffChanged;
            buffSystem.OnMoveSpeedBuffEvent -= OnMoveSpeedBuffChanged;
        }
        
        // 남아있는 파티클 제거
        DestroyBuffParticles();
    }

    private void OnBattleStart()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] OnBattleStart 이벤트 수신");
        if (unitInfo != null && unitInfo.IsOwnedByLocalPlayer())
        {
            EnableControl(true);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 전투 시작: 유닛 조작 활성화");
        }
    }

    // 조작 활성화/비활성화
    public void EnableControl(bool enable)
    {
        canControl = enable;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 조작 {(enable ? "활성화" : "비활성화")} 됨");
    }

    void Update()
    {
        // 애니메이션 업데이트
        UpdateAnimations();

        // 권한 확인 - unitInfo.IsOwnedByLocalPlayer() 사용
        if (unitInfo == null || !unitInfo.IsOwnedByLocalPlayer())
        {
            return;
        }

        // 조작 불가능 상태면 리턴
        if (!canControl)
        {
            return;
        }

        // T키 테스트 이동
        if (Input.GetKeyDown(KeyCode.T) && debugEnabled)
        {
            Vector3 testPos = transform.position + transform.forward * 5f;
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] T키 테스트 이동: {testPos}");
            MoveToPositionLocal(testPos);
        }
        
        // R키로 공격 상태 초기화 (디버깅용)
        if (Input.GetKeyDown(KeyCode.R) && debugEnabled)
        {
            ResetAttackState();
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 상태 수동 초기화됨");
        }

        // 우클릭 이동 처리
        HandleMouseInput();

        // 공격 처리
        HandleAttack();
    }

    private void UpdateAnimations()
    {
        if (agent != null && anim != null)
        {
            float speed = agent.velocity.magnitude / agent.speed;
            anim.SetFloat("Blend", speed, motionSmoothTime, Time.deltaTime);
        }
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(1)) // 우클릭
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 우클릭 감지됨");

            // 카메라 확인
            if (Camera.main == null)
            {
                Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Main Camera가 없습니다!");
                return;
            }

            // 마우스 레이캐스트
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 디버그 레이 그리기
            if (debugEnabled)
            {
                Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 2f);
            }

            // 모든 충돌 객체 확인 (디버깅 목적)
            if (debugEnabled)
            {
                RaycastHit[] allHits = Physics.RaycastAll(ray, 100f);
                foreach (var h in allHits)
                {
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] RaycastAll 충돌: {h.collider.name}, 태그: {h.collider.tag}, 레이어: {LayerMask.LayerToName(h.collider.gameObject.layer)}");
                }
            }

            bool didHit = Physics.Raycast(ray, out hit, 100f);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 레이캐스트 결과: 충돌={didHit}, 거리={hit.distance}");

            if (didHit)
            {
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 충돌 객체: {hit.collider.name}, 태그: {hit.collider.tag}, 레이어: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

                // Ground 레이어 또는 Ground 태그 확인
                bool isGround = ((1 << hit.collider.gameObject.layer) & groundLayerMask) != 0 ||
                                 hit.collider.CompareTag("Ground");

                if (isGround)
                {
                    Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 지면 감지됨 - 이동 실행: {hit.point}");
                    MoveToPositionLocal(hit.point);
                }
                else
                {
                    // 유닛 타겟팅
                    Unit hitUnit = hit.collider.GetComponent<Unit>();
                    if (hitUnit != null)
                    {
                        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 유닛 감지됨: {hit.collider.name}, 팀: {hitUnit.teamIndex}");

                        // 적 유닛인지 확인
                        if (unitInfo != null && hitUnit.teamIndex != unitInfo.teamIndex)
                        {
                            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 적 유닛 타겟팅");
                            AttackEnemyLocal(hit.collider.gameObject);
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 레이캐스트 충돌 없음");
            }
        }
    }

    // 공격 상태 초기화 함수
    private void ResetAttackState()
    {
        isAttacking = false;
        isRotatingToAttack = false;
        performMeleeAttack = true;
        nextAttackTime = 0;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 상태 초기화: isAttacking={isAttacking}, isRotatingToAttack={isRotatingToAttack}, performMeleeAttack={performMeleeAttack}");
    }

    private void HandleAttack()
    {
        // 사망 상태 확인 추가
        if (unitStats != null && unitStats.isDead)
        {
            return;
        }
        
        if (targetEnemy != null && performMeleeAttack && Time.time > nextAttackTime && !isAttacking)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 조건 체크: targetEnemy={targetEnemy != null}, performMeleeAttack={performMeleeAttack}, 쿨다운={Time.time > nextAttackTime}, !isAttacking={!isAttacking}");
            
            float distance = Vector3.Distance(transform.position, targetEnemy.transform.position);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 타겟과의 거리: {distance}, 공격 범위: {unitStats.attackRange}");
            
            if (distance <= unitStats.attackRange)
            {
                if (!isRotatingToAttack)
                {
                    Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격을 위한 회전 시작");
                    StartCoroutine(RotateTowardsTarget());
                }
            }
        }
    }

    // 로컬 이동 메서드
    private void MoveToPositionLocal(Vector3 position)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 로컬 이동 요청: {position}");

        // NavMeshAgent 설정
        if (agent != null)
        {
            agent.stoppingDistance = 0f;
            agent.isStopped = false;
            agent.SetDestination(position);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] NavMeshAgent 설정 완료: hasPath={agent.hasPath}, pathStatus={agent.pathStatus}");

            // 타겟 초기화
            if (targetEnemy != null && hmScript != null)
            {
                hmScript.DeselectHighlight();
                targetEnemy = null;
            }
        }
        else
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 오류: NavMeshAgent가 null입니다!");
        }

        // 서버에 이동 요청
        CmdMoveToPosition(position);
    }

    // 로컬 공격 메서드
    private void AttackEnemyLocal(GameObject enemy)
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 로컬 적 타겟팅: {enemy.name}");
        
        // 새 타겟 설정 시 공격 상태 초기화
        ResetAttackState();

        if (agent != null)
        {
            agent.stoppingDistance = unitStats.attackRange;
            agent.isStopped = false;
            agent.SetDestination(enemy.transform.position);

            // hmScript가 null인지 다시 확인하고, null이면 찾기 시도
            if (hmScript == null)
            {
                hmScript = FindObjectOfType<HighlightManager>();
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 시 HighlightManager 다시 찾기: {(hmScript != null ? "성공" : "실패")}");
            }

            // 하이라이트 설정 - 새 메서드 사용
            if (hmScript != null)
            {
                hmScript.SelectTarget(enemy);
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 하이라이트 매니저로 타겟 선택: {enemy.name}");
            }
            else
            {
                Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 하이라이트 매니저가 null이어서 타겟 선택 불가: {enemy.name}");
            }

            targetEnemy = enemy;
        }

        // 서버에 타겟팅 요청
        CmdMoveTowardsEnemy(enemy);
    }

    [Command]
    public void CmdMoveToPosition(Vector3 position)
    {
        position.y = Mathf.Max(position.y, 0.1f); // 최소 높이 설정

        // 서버에서 이동 처리
        if (agent != null)
        {
            agent.stoppingDistance = 0f;
            agent.SetDestination(position);
            targetEnemy = null;
        }

        // 다른 클라이언트에게 알림
        RpcMoveToPosition(position);
    }

    [ClientRpc]
    private void RpcMoveToPosition(Vector3 position)
    {
        // 내 캐릭터는 이미 처리되었으므로 스킵
        if (isLocalPlayer) return;

        if (agent != null)
        {
            agent.stoppingDistance = 0f;
            agent.SetDestination(position);

            if (targetEnemy != null && hmScript != null)
            {
                hmScript.DeselectHighlight();
                targetEnemy = null;
            }
        }
    }

    [Command]
    public void CmdMoveTowardsEnemy(GameObject enemy)
    {
        // 유효성 검사
        if (enemy == null) return;

        Unit enemyUnit = enemy.GetComponent<Unit>();
        if (enemyUnit == null || enemyUnit.teamIndex == unitInfo.teamIndex)
        {
            return; // 같은 팀이면 타겟팅 안함
        }

        // 서버에서도 공격 상태 초기화 (중요)
        isAttacking = false;
        
        targetEnemy = enemy;

        if (agent != null)
        {
            agent.stoppingDistance = unitStats.attackRange;
            agent.SetDestination(enemy.transform.position);
        }

        // 다른 클라이언트에게 알림
        RpcMoveTowardsEnemy(enemy);
    }

    [ClientRpc]
    private void RpcMoveTowardsEnemy(GameObject enemy)
    {
        if (isLocalPlayer) return;

        targetEnemy = enemy;

        if (agent != null && enemy != null)
        {
            agent.stoppingDistance = unitStats.attackRange;
            agent.SetDestination(enemy.transform.position);

            // hmScript가 null인지 다시 확인하고, null이면 찾기 시도
            if (hmScript == null)
            {
                hmScript = FindObjectOfType<HighlightManager>();
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] RPC 실행 시 HighlightManager 다시 찾기: {(hmScript != null ? "성공" : "실패")}");
            }

            if (hmScript != null)
            {
                hmScript.SelectTarget(enemy);
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] RPC 하이라이트 매니저로 타겟 선택: {enemy.name}");
            }
            else
            {
                Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] RPC 하이라이트 매니저가 null이어서 타겟 선택 불가: {enemy.name}");
            }
        }
    }

    private System.Collections.IEnumerator RotateTowardsTarget()
    {
        if (targetEnemy == null || isRotatingToAttack) yield break; // 이미 회전 중이면 중단

        isRotatingToAttack = true;
        Vector3 targetDirection = targetEnemy.transform.position - transform.position;
        targetDirection.y = 0;

        float angleToTarget = Vector3.Angle(transform.forward, targetDirection);

        while (angleToTarget > minAngleToAttack)
        {
            if (targetEnemy == null)
            {
                isRotatingToAttack = false;
                yield break;
            }

            targetDirection = targetEnemy.transform.position - transform.position;
            targetDirection.y = 0;

            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime / rotateSpeedBeforeAttack
                );
            }

            angleToTarget = Vector3.Angle(transform.forward, targetDirection);
            yield return null;
        }

        isRotatingToAttack = false;
        
        // 공격 전 다시 한번 쿨다운과 공격 중 상태를 확인
        if (Time.time > nextAttackTime && !isAttacking)
        {
            CmdAttack();
            // 로컬에서도 쿨다운 설정
            nextAttackTime = Time.time + unitStats.GetAttackCooldown();
            performMeleeAttack = false;
        }
    }

    [Command]
    private void CmdAttack()
    {
        if (targetEnemy == null || isAttacking)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 취소: 타겟 없음 또는 이미 공격 중");
            return;
        }

        float distance = Vector3.Distance(transform.position, targetEnemy.transform.position);
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 시도: 타겟={targetEnemy.name}, 거리={distance}, 필요 거리={unitStats.attackRange}");

        if (distance > unitStats.attackRange)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 공격 취소: 타겟이 너무 멀리 있음");
            return;
        }

        // 공격 상태 true로 설정
        isAttacking = true;

        Unit enemyUnit = targetEnemy.GetComponent<Unit>();
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 적 유닛 정보: 이름={targetEnemy.name}, 팀={enemyUnit?.teamIndex}, 태그={targetEnemy.tag}");

        // Also, I changed this logic to use my stat system instead.
        TheOneAndOnlyStats targetStats = targetEnemy.GetComponent<TheOneAndOnlyStats>();
        if (targetStats != null)
        {
            targetStats.TakeDamage(unitStats.attackDamage);
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 데미지 적용: {unitStats.attackDamage}");
        }
        else
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 오류: 타겟에 Stats 컴포넌트 없음");
        }

        RpcPlayAttackAnimation();
        
        // 공격 후 서버 쿨다운 시작
        StartCoroutine(ServerAttackCooldown());
    }
    
    // 서버측 공격 쿨다운 처리
    private System.Collections.IEnumerator ServerAttackCooldown()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 서버 쿨다운 시작");
        yield return new WaitForSeconds(unitStats.GetAttackCooldown());
        isAttacking = false;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 서버 쿨다운 종료, isAttacking = false");
    }

    [ClientRpc]
    private void RpcPlayAttackAnimation()
    {
        StartCoroutine(MeleeAttackInterval());
    }

    private System.Collections.IEnumerator MeleeAttackInterval()
    {
        performMeleeAttack = false;

        if (anim != null)
        {
            anim.SetBool("isAttacking", true);
        }

        yield return new WaitForSeconds(unitStats.GetAttackCooldown());

        if (targetEnemy == null)
        {
            if (anim != null)
            {
                anim.SetBool("isAttacking", false);
            }
            performMeleeAttack = true;
        }
        else
        {
            Vector3 targetDirection = targetEnemy.transform.position - transform.position;
            targetDirection.y = 0;
            if (targetDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(targetDirection);
            }

            nextAttackTime = Time.time + unitStats.GetAttackCooldown();
            performMeleeAttack = true;

            if (anim != null)
            {
                anim.SetBool("isAttacking", false);
            }
        }
    }

    [Command]
    public void CmdUseAbility(int abilityIndex)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 스킬 {abilityIndex} 사용 요청");
        
        // buffSystem이 없으면 스킬 사용 불가
        if (buffSystem == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] BuffSystem이 없어 스킬을 사용할 수 없습니다!");
            return;
        }
        
        // 스킬 인덱스에 따라 적절한 버프 적용
        switch (abilityIndex)
        {
            case 1: // 공격력 버프
                buffSystem.ApplyAttackBuff();
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 스킬 사용");
                break;
                
            case 2: // 방어력 버프
                buffSystem.ApplyDefenseBuff();
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 스킬 사용");
                break;
                
            case 3: // 체력 회복
                buffSystem.ApplyHealingEffect();
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 체력 회복 스킬 사용");
                break;
                
            case 4: // 궁극기 (모든 버프 효과)
                buffSystem.ApplyUltimateEffect();
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 스킬 사용");
                break;
                
            default:
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 알 수 없는 스킬 인덱스: {abilityIndex}");
                break;
        }
        
        // 애니메이션 재생 (모든 클라이언트에서)
        RpcPlayAbilityAnimation(abilityIndex);
    }

    [ClientRpc]
    private void RpcPlayAbilityAnimation(int abilityIndex)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 스킬 {abilityIndex} 애니메이션 재생");
        
        // 스킬 애니메이션 재생
        if (anim != null)
        {
            // 애니메이션이 있다면 재생 (현재는 일반 공격 애니메이션으로 대체)
            anim.SetTrigger("Attack");
            
            // 추후 스킬별 애니메이션 트리거를 추가할 수 있음
            // anim.SetTrigger("Ability" + abilityIndex);
        }
        
        // 스킬 효과 표시 (파티클 프리팹 생성)
        GameObject particleObj = null;
        float destroyTime = buffParticleTime; // 기본값
        
        switch (abilityIndex)
        {
            case 1: // 공격력 버프
                if (attackBuffParticle != null)
                {
                    particleObj = Instantiate(attackBuffParticle, transform.position, Quaternion.identity);
                    destroyTime = buffParticleTime;
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 파티클 생성");
                }
                break;
                
            case 2: // 방어력 버프
                if (defenseBuffParticle != null)
                {
                    particleObj = Instantiate(defenseBuffParticle, transform.position, Quaternion.identity);
                    destroyTime = buffParticleTime;
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 파티클 생성");
                }
                break;
                
            case 3: // 체력 회복
                if (healingParticle != null)
                {
                    particleObj = Instantiate(healingParticle, transform.position, Quaternion.identity);
                    destroyTime = healingParticleTime; // 체력 회복은 짧게
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 체력 회복 파티클 생성");
                }
                break;
                
            case 4: // 궁극기
                if (ultimateParticle != null)
                {
                    // 광역 공격 파티클 이펙트 생성 - 플레이어 중심으로 범위 표시
                    particleObj = Instantiate(ultimateParticle, transform.position, Quaternion.identity);
                    destroyTime = ultimateParticleTime; // 궁극기는 더 길게
                    
                    // BuffSystem에서 설정한 범위 값과 일치시키기
                    BuffSystem buffSystem = GetComponent<BuffSystem>();
                    if (buffSystem != null)
                    {
                        // 사거리 표시를 위해 범위 값 가져오기
                        float ultimateRadius = 5f; // 기본값
                        
                        // BuffSystem에서 privateField 값을 반영하기 위한 메서드 호출
                        ultimateRadius = buffSystem.GetUltimateRadius();
                        
                        // 파티클 범위 확장을 위한 스케일 조정
                        float scale = ultimateRadius / 5.0f; // 파티클 기본 크기가 5 단위라고 가정
                        particleObj.transform.localScale = new Vector3(scale, scale, scale);
                        
                        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 범위: {ultimateRadius}, 적용된 스케일: {scale}");
                    }
                    
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 궁극기 광역 공격 파티클 생성");
                }
                break;
        }
        
        // 파티클 오브젝트를 캐릭터의 자식으로 설정하고 자동 제거 설정
        if (particleObj != null)
        {
            // 캐릭터를 부모로 설정 (이동에 따라 파티클도 함께 이동)
            particleObj.transform.SetParent(transform);
            
            // 오프셋 조정 (필요한 경우)
            particleObj.transform.localPosition = new Vector3(0, 1, 0); // 캐릭터 중심에서 약간 위로 오프셋
            
            // 일정 시간 후 파티클 오브젝트 제거
            Destroy(particleObj, destroyTime);
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 파티클 제거 시간 설정: {destroyTime}초");
        }
    }

    // 디버깅을 위한 Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, unitStats.attackRange);
    }

    // 사망 이벤트 처리 핸들러
    private void OnUnitDeath()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 사망 처리: 모든 행동 중지");
        
        // 타겟 초기화
        targetEnemy = null;
        
        // 공격 상태 초기화
        ResetAttackState();
        
        // 이동 중지
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        
        // 사망 시 애니메이터 파라미터 설정
        if (anim != null)
        {
            anim.SetBool("isAttacking", false);
        }
        
        // 컨트롤 비활성화
        EnableControl(false);
        
        // 모든 버프 파티클 제거
        DestroyBuffParticles();
        
        // 클라이언트도 서버에 상관없이 강제로 죽음 상태로 변경하도록 추가
        if (unitStats != null && !unitStats.isDead)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 클라이언트에서 사망 강제 처리");
            // 클라이언트에서는 클라이언트 로직만 실행
            EnableControl(false);
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }
    }

    private void OnResurrection()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 부활 처리: 제어권 부여 (isServer={isServer}, isLocalPlayer={isLocalPlayer}, isClientOnly={isClientOnly})");
        
        // NavMeshAgent 상태 확인 및 초기화
        if (agent != null)
        {
            agent.isStopped = false;
            if (!agent.enabled)
            {
                agent.enabled = true;
            }
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] NavMeshAgent 재설정: enabled={agent.enabled}, isStopped={agent.isStopped}");
        }
        
        // 제어권 부여
        EnableControl(true);
        
        // 보이지 않는 경우를 위한 추가 체크
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool allRenderersEnabled = true;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled)
            {
                renderer.enabled = true;
                allRenderersEnabled = false;
            }
        }
        
        if (!allRenderersEnabled)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 일부 렌더러가 비활성화되어 있어서 활성화했습니다.");
        }
        
        // 콜라이더 체크
        Collider[] colliders = GetComponentsInChildren<Collider>();
        bool allCollidersEnabled = true;
        foreach (var collider in colliders)
        {
            if (!collider.enabled)
            {
                collider.enabled = true;
                allCollidersEnabled = false;
            }
        }
        
        if (!allCollidersEnabled)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 일부 콜라이더가 비활성화되어 있어서 활성화했습니다.");
        }
        
        // 현재 버프 상태에 따라 파티클 재생성
        if (buffSystem != null)
        {
            // 공격력 버프 활성화 상태 확인
            if (buffSystem.attackBuffActive)
            {
                CreateAttackBuffParticle();
            }
            
            // 방어력 버프 활성화 상태 확인
            if (buffSystem.defenseBuffActive)
            {
                CreateDefenseBuffParticle();
            }
        }
        
        // 클라이언트에서만 서버에 EnableControl 요청
        if (!isServer && isLocalPlayer)
        {
            CmdEnableControlAfterResurrection();
        }
    }
    
    [Command]
    private void CmdEnableControlAfterResurrection()
    {
        // 서버에서 제어권 부여 (클라이언트 요청)
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 클라이언트 요청으로 서버에서 제어권 부여");
        
        // RPC를 통해 다시 모든 클라이언트에게 알림
        RpcEnableControlAfterResurrection();
    }
    
    [ClientRpc]
    private void RpcEnableControlAfterResurrection()
    {
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] RPC: 부활 후 제어권 부여");
        
        // 로컬 플레이어인 경우에만 처리
        if (isLocalPlayer)
        {
            EnableControl(true);
            
            // NavMeshAgent 재활성화
            if (agent != null)
            {
                agent.isStopped = false;
            }
        }
    }

    // 공격력 버프 상태 변경 시 호출되는 메서드
    private void OnAttackBuffChanged(bool active)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 상태 변경: {active} (스레드: {System.Threading.Thread.CurrentThread.ManagedThreadId})");
        
        // 메인 스레드에서 실행되도록 보장
        if (Mirror.NetworkServer.active)
        {
            // 서버에서는 바로 처리
            HandleAttackBuffChange(active);
        }
        else
        {
            // 클라이언트에서는 메인 스레드에서 실행
            StartCoroutine(DelayedBuffChange(() => HandleAttackBuffChange(active)));
        }
    }
    
    // 공격력 버프 상태 변경 실제 처리
    private void HandleAttackBuffChange(bool active)
    {
        if (active)
        {
            // 공격력 버프 활성화 파티클 생성
            CreateAttackBuffParticle();
        }
        else
        {
            // 공격력 버프 파티클 제거
            DestroyAttackBuffParticle();
        }
    }
    
    // 방어력 버프 상태 변경 시 호출되는 메서드
    private void OnDefenseBuffChanged(bool active)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 상태 변경: {active} (스레드: {System.Threading.Thread.CurrentThread.ManagedThreadId})");
        
        // 메인 스레드에서 실행되도록 보장
        if (Mirror.NetworkServer.active)
        {
            // 서버에서는 바로 처리
            HandleDefenseBuffChange(active);
        }
        else
        {
            // 클라이언트에서는 메인 스레드에서 실행
            StartCoroutine(DelayedBuffChange(() => HandleDefenseBuffChange(active)));
        }
    }
    
    // 방어력 버프 상태 변경 실제 처리
    private void HandleDefenseBuffChange(bool active)
    {
        if (active)
        {
            // 방어력 버프 활성화 파티클 생성
            CreateDefenseBuffParticle();
        }
        else
        {
            // 방어력 버프 파티클 제거
            DestroyDefenseBuffParticle();
        }
    }
    
    // 메인 스레드에서 실행되도록 지연 처리하는 코루틴
    private System.Collections.IEnumerator DelayedBuffChange(System.Action action)
    {
        yield return null; // 다음 프레임까지 대기
        action?.Invoke();  // 액션 실행
    }
    
    // 공격력 버프 활성화 파티클 생성
    private void CreateAttackBuffParticle()
    {
        // 이미 활성화된 파티클이 있으면 제거
        DestroyAttackBuffParticle();
        
        // 파티클 프리팹이 설정되어 있으면 생성
        if (attackBuffActiveParticle != null)
        {
            // 파티클 생성 및 위치 설정
            activeAttackBuffParticle = Instantiate(attackBuffActiveParticle, transform.position + buffParticleOffset, Quaternion.identity);
            activeAttackBuffParticle.transform.SetParent(transform);
            
            // 파티클 시스템 설정 확인 및 수정
            var particleSystems = activeAttackBuffParticle.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None; // 자동 삭제되지 않도록 설정
                main.loop = true; // 반복 재생 설정
            }
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 지속 파티클 생성됨");
        }
    }
    
    // 방어력 버프 활성화 파티클 생성
    private void CreateDefenseBuffParticle()
    {
        // 이미 활성화된 파티클이 있으면 제거
        DestroyDefenseBuffParticle();
        
        // 파티클 프리팹이 설정되어 있으면 생성
        if (defenseBuffActiveParticle != null)
        {
            // 파티클 생성 및 위치 설정
            activeDefenseBuffParticle = Instantiate(defenseBuffActiveParticle, transform.position + buffParticleOffset, Quaternion.identity);
            activeDefenseBuffParticle.transform.SetParent(transform);
            
            // 파티클 시스템 설정 확인 및 수정
            var particleSystems = activeDefenseBuffParticle.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None; // 자동 삭제되지 않도록 설정
                main.loop = true; // 반복 재생 설정
            }
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 지속 파티클 생성됨");
        }
    }
    
    // 공격력 버프 파티클 제거
    private void DestroyAttackBuffParticle()
    {
        if (activeAttackBuffParticle != null)
        {
            // 파티클 시스템 페이드아웃 (옵션)
            var particleSystems = activeAttackBuffParticle.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            
            // 오브젝트 제거
            Destroy(activeAttackBuffParticle, 1.0f); // 파티클이 자연스럽게 사라지도록 1초 지연
            activeAttackBuffParticle = null;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 공격력 버프 지속 파티클 제거 요청됨");
        }
    }
    
    // 방어력 버프 파티클 제거
    private void DestroyDefenseBuffParticle()
    {
        if (activeDefenseBuffParticle != null)
        {
            // 파티클 시스템 페이드아웃 (옵션)
            var particleSystems = activeDefenseBuffParticle.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            
            // 오브젝트 제거
            Destroy(activeDefenseBuffParticle, 1.0f); // 파티클이 자연스럽게 사라지도록 1초 지연
            activeDefenseBuffParticle = null;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 방어력 버프 지속 파티클 제거 요청됨");
        }
    }
    
    // 모든 버프 파티클 제거
    private void DestroyBuffParticles()
    {
        DestroyAttackBuffParticle();
        DestroyDefenseBuffParticle();
    }

    // 이동속도 버프 상태 변경 시 호출되는 메서드
    private void OnMoveSpeedBuffChanged(bool active)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이동속도 버프 상태 변경: {active}, 현재속도: {unitStats.moveSpeed}");
        
        // NavMeshAgent 속도 업데이트 (한번 더 확인)
        if (agent != null)
        {
            agent.speed = unitStats.moveSpeed;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 이동속도 변경 반영: NavMeshAgent 속도 = {agent.speed}");
        }
    }
}