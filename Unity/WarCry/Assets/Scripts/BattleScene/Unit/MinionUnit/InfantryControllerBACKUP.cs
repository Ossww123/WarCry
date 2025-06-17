//using UnityEngine;
//using Mirror;
//using UnityEngine.AI;
//using System.Collections;

//// 유닛 상태 정의
//public enum UnitState
//{
//    Idle,       // 정지
//    Moving,     // 명령 이동
//    Retreating, // 후퇴
//    Attacking   // 적 추적 및 공격
//}

//[RequireComponent(typeof(Unit))]
//[RequireComponent(typeof(NavMeshAgent))]
//[RequireComponent(typeof(Animator))]
//public class InfantryController : NetworkBehaviour
//{
//    [Header("Component References")]
//    private NavMeshAgent agent;
//    private Animator anim;
//    private Unit unitInfo;
//    private TheOneAndOnlyStats unitStats;

//    [Header("AI Settings")]
//    [SerializeField] private float updateInterval = 0.5f; // AI 업데이트 간격
//    [SerializeField] private float disengageRange = 0f;   // 적 추적 해제 범위

//    [Header("Animation")]
//    [SerializeField] private float motionSmoothTime = 0.1f;

//    [Header("Target")]
//    [SyncVar(hook = nameof(OnTargetChanged))]
//    private uint targetEnemyNetId;

//    private bool performMeleeAttack = true;
//    private float attackTimer = 0f;

//    [SyncVar(hook = nameof(OnAIStateChanged))]
//    private bool aiEnabled = false;
//    private Coroutine aiRoutine;

//    // 명령어 처리 관련 필드
//    [SyncVar] private string currentDirection = "";
//    [SyncVar] private string currentTarget = "";
//    [SyncVar] private Vector3 moveDirection = Vector3.zero;

//    // 후퇴 모드 관련 필드
//    [SyncVar] private bool isRetreating = false;
//    [SyncVar] private bool disableAutoTargeting = false;

//    // 상태 기반 자동 재탐색 관련
//    private UnitState currentState = UnitState.Idle;
//    private string previousDirection = "";
//    private Vector3 previousMoveDirection = Vector3.zero;

//    // 애니메이션 상태 동기화
//    [SyncVar(hook = nameof(OnMovingStateChanged))] private bool isMoving = false;
//    [SyncVar(hook = nameof(OnAttackingStateChanged))] private bool isAttacking = false;

//    // 위치/회전 동기화
//    [SyncVar] private Vector3 serverPosition;
//    [SyncVar] private Quaternion serverRotation;

//    // 이동 목적지 동기화
//    [SyncVar(hook = nameof(OnDestinationChanged))] private Vector3 targetDestination;

//    private void Awake()
//    {
//        agent = GetComponent<NavMeshAgent>();
//        anim = GetComponent<Animator>();
//        unitInfo = GetComponent<Unit>();
//        unitStats = GetComponent<TheOneAndOnlyStats>();

//        // disengageRange 기본값 설정
//        if (disengageRange <= 0f)
//            disengageRange = unitStats.detectionRange;

//        if (isServer && agent != null)
//        {
//            agent.speed = unitStats.moveSpeed;
//            agent.stoppingDistance = unitStats.attackRange;
//            agent.enabled = true;
//        }
//    }

//    public override void OnStartClient()
//    {
//        base.OnStartClient();
//        BattleController.OnBattleStart += OnBattleStartEvent;
//        UpdateAnimationState(isMoving, isAttacking);

//        if (!isServer && agent != null)
//            agent.enabled = false;
//    }

//    public override void OnStartServer()
//    {
//        base.OnStartServer();
//        if (agent != null && !agent.isOnNavMesh)
//            StartCoroutine(ReinitializeNavMeshAgent());
//    }

//    private IEnumerator ReinitializeNavMeshAgent()
//    {
//        if (!isServer) yield break;
//        agent.enabled = false;
//        yield return null;
//        agent.enabled = true;
//        if (!agent.isOnNavMesh)
//            Debug.LogError($"[{gameObject.name}] NavMeshAgent 재초기화 실패!");
//    }

//    private void OnDestroy()
//    {
//        BattleController.OnBattleStart -= OnBattleStartEvent;
//        if (aiRoutine != null) StopCoroutine(aiRoutine);
//    }

//    private void OnBattleStartEvent()
//    {
//        if (isClient && !isServer) CmdEnableAI(true);
//        else if (isServer) EnableAI(true);
//    }

//    [Command(requiresAuthority = false)]
//    public void CmdEnableAI(bool enable) => EnableAI(enable);

//    [Server]
//    public void EnableAI(bool enable)
//    {
//        if (aiEnabled == enable) return;
//        aiEnabled = enable;
//        if (aiRoutine != null) StopCoroutine(aiRoutine);
//        if (enable) aiRoutine = StartCoroutine(AIUpdateRoutine());
//        else aiRoutine = null;
//    }

//    private void OnAIStateChanged(bool _, bool newValue)
//    {
//        if (!newValue && anim != null)
//            UpdateAnimationState(false, false);
//    }

//    private void OnMovingStateChanged(bool _, bool newValue)
//        => UpdateAnimationState(newValue, isAttacking);

//    private void OnAttackingStateChanged(bool _, bool newValue)
//    {
//        UpdateAnimationState(isMoving, newValue);
//        if (newValue) StartCoroutine(ResetAttackAnimation());
//    }

//    private void OnDestinationChanged(Vector3 _, Vector3 newValue)
//    {
//        if (!isServer)
//        {
//            if (newValue != Vector3.zero)
//            {
//                isMoving = true;
//                UpdateAnimationState(true, isAttacking);
//            }
//        }
//    }

//    private void OnTargetChanged(uint _, uint newValue)
//    {
//        Debug.Log($"[{gameObject.name}] 타겟 변경됨: {newValue}");
//    }

//    [Server]
//    public void ApplyCommand(string direction, string target)
//    {
//        // 후퇴
//        if (direction == "뒤")
//        {
//            currentState = UnitState.Retreating;
//            isRetreating = true;
//            disableAutoTargeting = true;
//            targetEnemyNetId = 0;
//            moveDirection = ParseDirectionToVector(direction);
//            isMoving = true;
//            isAttacking = false;
//            UpdateAnimationState(true, false);
//            return;
//        }

//        // 후퇴 해제
//        if (isRetreating)
//        {
//            isRetreating = false;
//            disableAutoTargeting = false;
//        }

//        currentDirection = direction;
//        currentTarget = target;
//        moveDirection = ParseDirectionToVector(direction);

//        // 우선 이전 명령 저장 (재탐색 시 복귀용)
//        previousDirection = direction;
//        previousMoveDirection = moveDirection;

//        // 타겟 지정
//        if (!string.IsNullOrEmpty(target))
//        {
//            currentState = UnitState.Attacking;
//            disableAutoTargeting = false;
//            targetEnemyNetId = 0;
//            FindTargetByName(target);
//        }
//        // 방향 이동
//        else if (!string.IsNullOrEmpty(direction) && direction != "정지")
//        {
//            currentState = UnitState.Moving;
//            MoveInDirection(moveDirection);
//        }
//        // 정지
//        else
//        {
//            currentState = UnitState.Idle;
//            if (agent != null && agent.isOnNavMesh)
//                agent.isStopped = true;
//            isMoving = false;
//        }
//    }

//    [Command(requiresAuthority = false)]
//    public void CmdApplyCommand(string direction, string target)
//        => ApplyCommand(direction, target);

//    private Vector3 ParseDirectionToVector(string dir) => dir switch
//    {
//        "앞" => transform.right,
//        "뒤" => -transform.right,
//        "위" => transform.forward,
//        "아래" => -transform.forward,
//        _ => Vector3.zero
//    };

//    [Server]
//    private void MoveInDirection(Vector3 direction)
//    {
//        if (direction == Vector3.zero || agent == null || !agent.isOnNavMesh) return;
//        Vector3 dest = transform.position + direction * 5f;
//        if (NavMesh.SamplePosition(dest, out var hit, 5f, NavMesh.AllAreas))
//        {
//            agent.isStopped = false;
//            agent.SetDestination(hit.position);
//            targetDestination = hit.position;
//            isMoving = true;
//        }
//    }

//    private IEnumerator AIUpdateRoutine()
//    {
//        while (aiEnabled)
//        {
//            if (!isServer) yield break;

//            switch (currentState)
//            {
//                case UnitState.Retreating:
//                    MoveInDirection(moveDirection);
//                    break;

//                case UnitState.Idle:
//                case UnitState.Moving:
//                    if (!disableAutoTargeting && targetEnemyNetId == 0)
//                        FindNearestEnemy();

//                    var tgt = GetTargetEnemy();
//                    if (tgt != null)
//                    {
//                        float dist = Vector3.Distance(transform.position, tgt.transform.position);
//                        if (dist <= unitStats.attackRange)
//                        {
//                            currentState = UnitState.Attacking;
//                            Attack();
//                            agent.isStopped = true;
//                        }
//                        else if (dist <= unitStats.detectionRange)
//                        {
//                            currentState = UnitState.Attacking;
//                            MoveTowards(tgt.transform.position);
//                        }
//                    }
//                    else if (currentState == UnitState.Idle)
//                    {
//                        // 완전 가만히
//                        if (agent != null && agent.isOnNavMesh)
//                            agent.isStopped = true;
//                        isMoving = false;
//                    }
//                    break;

//                case UnitState.Attacking:
//                    var targetObj = GetTargetEnemy();
//                    if (targetObj != null)
//                    {
//                        float distance = Vector3.Distance(transform.position, targetObj.transform.position);
//                        if (distance <= unitStats.attackRange)
//                        {
//                            Attack();
//                            agent.isStopped = true;
//                        }
//                        else if (distance <= unitStats.detectionRange)
//                        {
//                            MoveTowards(targetObj.transform.position);
//                        }
//                        else if (distance > disengageRange)
//                        {
//                            // 타겟 이탈
//                            targetEnemyNetId = 0;
//                            // 이전 명령 복귀
//                            if (!string.IsNullOrEmpty(previousDirection))
//                            {
//                                currentState = UnitState.Moving;
//                                MoveInDirection(previousMoveDirection);
//                            }
//                            else
//                            {
//                                currentState = UnitState.Idle;
//                            }
//                        }
//                    }
//                    else
//                    {
//                        // 타겟 사라짐
//                        targetEnemyNetId = 0;
//                        if (!string.IsNullOrEmpty(previousDirection))
//                        {
//                            currentState = UnitState.Moving;
//                            MoveInDirection(previousMoveDirection);
//                        }
//                        else
//                        {
//                            currentState = UnitState.Idle;
//                        }
//                    }
//                    break;
//            }

//            // 공격 타이머
//            if (attackTimer > 0) attackTimer -= updateInterval;

//            // 위치/회전 동기화
//            if (isMoving)
//            {
//                serverPosition = transform.position;
//                serverRotation = transform.rotation;
//            }

//            yield return new WaitForSeconds(updateInterval);
//        }
//    }

//    private GameObject GetTargetEnemy()
//    {
//        if (targetEnemyNetId == 0) return null;
//        if (NetworkClient.spawned.TryGetValue(targetEnemyNetId, out var id) && id.gameObject != null)
//            return id.gameObject;
//        return null;
//    }

//    [Server]
//    private void FindNearestEnemy()
//    {
//        if (disableAutoTargeting) return;
//        var all = FindObjectsOfType<Unit>();
//        float closest = unitStats.detectionRange;
//        Unit best = null;
//        foreach (var u in all)
//        {
//            if (u.teamIndex != unitInfo.teamIndex)
//            {
//                float d = Vector3.Distance(transform.position, u.transform.position);
//                if (d < closest)
//                {
//                    closest = d; best = u;
//                }
//            }
//        }
//        if (best != null)
//        {
//            targetEnemyNetId = best.GetComponent<NetworkIdentity>().netId;
//        }
//    }

//    [Server]
//    private void FindTargetByName(string nameFilter)
//    {
//        var all = FindObjectsOfType<Unit>();
//        float bestDist = Mathf.Infinity;
//        Unit bestMatch = null;
//        foreach (var u in all)
//        {
//            if (u.teamIndex != unitInfo.teamIndex && u.name.Contains(nameFilter))
//            {
//                float d = Vector3.Distance(transform.position, u.transform.position);
//                if (d < bestDist)
//                {
//                    bestDist = d; bestMatch = u;
//                }
//            }
//        }
//        if (bestMatch != null)
//            targetEnemyNetId = bestMatch.GetComponent<NetworkIdentity>().netId;
//        else
//            FindNearestEnemy();
//    }

//    [Server]
//    private void MoveTowards(Vector3 pos)
//    {
//        if (agent == null || !agent.isOnNavMesh) return;
//        agent.isStopped = false;
//        agent.SetDestination(pos);
//        targetDestination = pos;
//        isMoving = true;
//    }

//    [Server]
//    private void Attack()
//    {
//        var tgt = GetTargetEnemy();
//        if (attackTimer > 0 || tgt == null) return;
//        Vector3 dir = tgt.transform.position - transform.position;
//        dir.y = 0;
//        transform.rotation = Quaternion.LookRotation(dir);
//        serverRotation = transform.rotation;

//        var stats = tgt.GetComponent<TheOneAndOnlyStats>();
//        if (stats != null)
//        {
//            stats.TakeDamage(unitStats.attackDamage);
//            if (stats.IsDead() || stats.currentHealth <= 0)
//                targetEnemyNetId = 0;
//        }

//        attackTimer = unitStats.GetAttackCooldown();
//        performMeleeAttack = false;
//        isAttacking = true;
//        StartCoroutine(AttackCooldown());
//    }

//    private IEnumerator AttackCooldown()
//    {
//        yield return new WaitForSeconds(unitStats.GetAttackCooldown());
//        performMeleeAttack = true;
//    }

//    private void UpdateAnimationState(bool move, bool attack)
//    {
//        if (anim == null) return;
//        anim.SetFloat("Blend", move ? 1f : 0f, motionSmoothTime, Time.deltaTime);
//        anim.SetBool("isAttacking", attack);
//    }

//    private IEnumerator ResetAttackAnimation()
//    {
//        yield return new WaitForSeconds(1f);
//        isAttacking = false;
//    }

//    [ClientCallback]
//    private void Update()
//    {
//        if (isServer) return;
//        if (isMoving && targetDestination != Vector3.zero)
//        {
//            Vector3 dir = targetDestination - transform.position;
//            float dist = dir.magnitude;
//            if (dist > 0.1f)
//            {
//                dir.Normalize(); dir.y = 0;
//                transform.rotation = Quaternion.Slerp(transform.rotation,
//                    Quaternion.LookRotation(dir), Time.deltaTime * 5f);
//                transform.position += dir * unitStats.moveSpeed * Time.deltaTime;
//            }
//        }
//        UpdateAnimationState(isMoving, isAttacking);
//    }

//    private void OnDrawGizmosSelected()
//    {
//        if (unitStats == null) return;
//        Gizmos.color = Color.yellow;
//        Gizmos.DrawWireSphere(transform.position, unitStats.detectionRange);
//        Gizmos.color = Color.red;
//        Gizmos.DrawWireSphere(transform.position, unitStats.attackRange);
//        Gizmos.color = Color.blue;
//        Gizmos.DrawWireSphere(transform.position, disengageRange);
//    }

//    // 애니메이션 이벤트 바인딩용 (필요시)
//    public void MeleeAttack() { }
//}
