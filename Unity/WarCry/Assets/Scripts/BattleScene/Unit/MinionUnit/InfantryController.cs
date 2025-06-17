using UnityEngine;
using Mirror;
using UnityEngine.AI;
using System.Collections;
using System;

public enum UnitState { Idle, Moving, Retreating, Attacking }

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class InfantryController : NetworkBehaviour
{
    [Header("Component References")]
    private NavMeshAgent agent;
    private Animator anim;
    private Unit unitInfo;
    private TheOneAndOnlyStats unitStats;

    [Header("AI Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private float disengageRange = 0f;

    [Header("Animation")]
    [SerializeField] private float motionSmoothTime = 0.1f;

    [Header("Target")]
    [SyncVar(hook = nameof(OnTargetChanged))] private uint targetEnemyNetId;

    private bool performMeleeAttack = true;
    private float attackTimer = 0f;

    [SyncVar(hook = nameof(OnAIStateChanged))] private bool aiEnabled = false;
    private Coroutine aiRoutine;

    // 명령 처리
    [SyncVar] private string currentDirection = "";
    [SyncVar] private string currentTarget = "";
    [SyncVar] private Vector3 moveDirection = Vector3.zero;

    // 서버에서 클라이언트로 명령 동기화를 위한 변수
    [SyncVar(hook = nameof(OnDirectionChanged))] private string syncedDirection = "";
    [SyncVar(hook = nameof(OnTargetStringChanged))] private string syncedTarget = "";

    [SyncVar] private bool isRetreating = false;
    [SyncVar] private bool disableAutoTargeting = false;

    private UnitState currentState = UnitState.Idle;
    private string previousDirection = "";
    private Vector3 previousMoveDirection = Vector3.zero;

    [SyncVar(hook = nameof(OnMovingStateChanged))] private bool isMoving = false;
    [SyncVar(hook = nameof(OnAttackingStateChanged))] private bool isAttacking = false;

    [SyncVar(hook = nameof(OnDestinationChanged))] private Vector3 targetDestination;

    // 클라이언트 전용 이동 관련 변수
    private Vector3 clientTargetPosition;
    private bool clientIsMoving = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        unitInfo = GetComponent<Unit>();
        unitStats = GetComponent<TheOneAndOnlyStats>();
        if (disengageRange <= 0f)
            disengageRange = unitStats.detectionRange;

        // 서버에서만 NavMeshAgent 활성화
        if (isServer && agent != null)
        {
            agent.speed = unitStats.moveSpeed;
            agent.stoppingDistance = unitStats.attackRange;
            agent.enabled = true;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        BattleController.OnBattleStart += OnBattleStartEvent;
        UpdateAnimationState(isMoving, isAttacking);

        // 클라이언트에서는 NavMeshAgent를 비활성화하되, 수동 이동 처리
        if (!isServer && agent != null)
        {
            agent.enabled = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (agent != null && !agent.isOnNavMesh)
            StartCoroutine(ReinitializeNavMeshAgent());
    }

    private IEnumerator ReinitializeNavMeshAgent()
    {
        if (!isServer) yield break;
        agent.enabled = false;
        yield return null;
        agent.enabled = true;
        if (!agent.isOnNavMesh)
            Debug.LogError($"[{gameObject.name}] NavMeshAgent 재초기화 실패!");
    }

    private void OnDestroy()
    {
        BattleController.OnBattleStart -= OnBattleStartEvent;
        if (aiRoutine != null) StopCoroutine(aiRoutine);
    }

    private void OnBattleStartEvent()
    {
        if (isClient && !isServer) CmdEnableAI(true);
        else if (isServer) EnableAI(true);
    }

    [Command]
    public void CmdEnableAI(bool enable) => EnableAI(enable);

    [Server]
    public void EnableAI(bool enable)
    {
        if (aiEnabled == enable) return;
        aiEnabled = enable;
        if (aiRoutine != null) StopCoroutine(aiRoutine);
        if (enable) aiRoutine = StartCoroutine(AIUpdateRoutine());
        else aiRoutine = null;
    }

    private void OnAIStateChanged(bool _, bool newValue)
    {
        if (!newValue && anim != null)
            UpdateAnimationState(false, false);
    }

    private void OnMovingStateChanged(bool _, bool newValue)
    {
        UpdateAnimationState(newValue, isAttacking);

        // 클라이언트에서 이동 상태 동기화
        if (!isServer)
        {
            clientIsMoving = newValue;
        }
    }

    private void OnAttackingStateChanged(bool _, bool newValue)
    {
        UpdateAnimationState(isMoving, newValue);
        if (newValue) StartCoroutine(ResetAttackAnimation());
    }

    private void OnDestinationChanged(Vector3 _, Vector3 newValue)
    {
        if (!isServer && newValue != Vector3.zero)
        {
            clientTargetPosition = newValue;
            clientIsMoving = true;
            UpdateAnimationState(true, isAttacking);
        }
    }

    private void OnDirectionChanged(string oldValue, string newValue)
    {
        if (!isServer && !string.IsNullOrEmpty(newValue))
        {
            currentDirection = newValue;
            moveDirection = ParseDirectionToVector(newValue);
            if (newValue == "뒤")
            {
                isRetreating = true;
                disableAutoTargeting = true;
                clientIsMoving = true;
                UpdateAnimationState(true, false);
            }
            else if (newValue == "정지")
            {
                clientIsMoving = false;
                UpdateAnimationState(false, isAttacking);
            }
            else
            {
                clientIsMoving = true;
                UpdateAnimationState(true, isAttacking);
            }
        }
    }

    private void OnTargetStringChanged(string oldValue, string newValue)
    {
        if (!isServer && !string.IsNullOrEmpty(newValue))
        {
            currentTarget = newValue;
            UpdateAnimationState(isMoving, true);
        }
    }

    private void OnTargetChanged(uint _, uint newValue)
    {
        // Target NetId 변경 로그
        Debug.Log($"[{gameObject.name}] Target changed to NetId: {newValue}");
    }

    [Server]
    public void ApplyCommand(string direction, string target)
    {
        Debug.Log($"[{gameObject.name}] 명령 적용: direction={direction}, target={target}");

        syncedDirection = direction;
        syncedTarget = target;

        // --- 뒤로 후퇴 ---
        if (direction == "뒤")
        {
            currentState = UnitState.Retreating;
            isRetreating = true;
            disableAutoTargeting = true;
            targetEnemyNetId = 0;
            moveDirection = ParseDirectionToVector(direction);
            MoveInDirection(ParseDirectionToVector(direction));
            RpcUpdateAnimationState(true, false);
            Debug.Log($"[{gameObject.name}] 후퇴 명령 실행");
            return;
        }

        // 후퇴 끝나면 자동 타겟팅 재활성화
        isRetreating = false;
        disableAutoTargeting = false;

        // --- 타겟 지정 공격 ---
        if (!string.IsNullOrEmpty(target))
        {
            currentState = UnitState.Attacking;
            FindTargetByName(target);
            RpcUpdateAnimationState(isMoving, true);
            Debug.Log($"[{gameObject.name}] 타겟 공격 명령 실행: {target}");
            return;
        }

        // --- 일반 이동 (앞/위/아래) ---
        if (!string.IsNullOrEmpty(direction) && direction != "정지")
        {
            currentState = UnitState.Moving;
            moveDirection = ParseDirectionToVector(direction);
            Vector3 dirVec = ParseDirectionToVector(direction);
            MoveInDirection(dirVec);

            // 이동 중 주변 적이 있으면 즉시 추적
            FindNearestEnemy();
            if (targetEnemyNetId != 0)
            {
                currentState = UnitState.Attacking;
                RpcUpdateAnimationState(false, true);
            }
            Debug.Log($"[{gameObject.name}] 이동 명령 실행: {direction}");
            return;
        }

        // --- 정지 ---
        if (direction == "정지")
        {
            currentState = UnitState.Idle;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                isMoving = false;
            }
            RpcUpdateAnimationState(false, isAttacking);
            Debug.Log($"[{gameObject.name}] 정지 명령 실행");
        }
    }

    // 권한 체크를 완화하여 CommandProcessor에서 호출 가능하도록 수정
    [Command(requiresAuthority = false)]
    public void CmdApplyCommand(string direction, string target)
    {
        Debug.Log($"[{gameObject.name}] 네트워크 명령 수신: direction={direction}, target={target}");
        ApplyCommand(direction, target);
    }

    private Vector3 ParseDirectionToVector(string dir)
    {
        // TeamIndex.Right 는 네 코드에서 1로 지정된 값이라고 가정
        bool reverse = unitInfo.teamIndex == TeamIndex.Right;

        return dir switch
        {
            "앞" => reverse ? Vector3.left : Vector3.right,
            "뒤" => reverse ? Vector3.right : Vector3.left,
            "위" => Vector3.forward,
            "아래" => Vector3.back,
            _ => Vector3.zero
        };
    }

    [Server]
    private void MoveInDirection(Vector3 direction)
    {
        if (direction == Vector3.zero || agent == null || !agent.isOnNavMesh) return;
        Vector3 dest = transform.position + direction * 100f;
        if (NavMesh.SamplePosition(dest, out var hit, 100f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            targetDestination = hit.position;
            isMoving = true;
            RpcUpdateAnimationState(true, isAttacking);
            Debug.Log($"[{gameObject.name}] 서버: 목적지 설정 - {hit.position}");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] NavMesh 목적지를 찾을 수 없음: {dest}");
        }
    }

    private IEnumerator AIUpdateRoutine()
    {
        while (aiEnabled)
        {
            if (!isServer) yield break;
            switch (currentState)
            {
                case UnitState.Retreating:
                    MoveInDirection(moveDirection);
                    break;
                case UnitState.Idle:
                case UnitState.Moving:
                    if (!disableAutoTargeting && targetEnemyNetId == 0) FindNearestEnemy();
                    var tgt = GetTargetEnemy();
                    if (tgt != null)
                    {
                        float dist = Vector3.Distance(transform.position, tgt.transform.position);
                        if (dist <= unitStats.attackRange)
                        {
                            currentState = UnitState.Attacking;
                            Attack();
                            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                        }
                        else if (dist <= unitStats.detectionRange)
                        {
                            currentState = UnitState.Attacking;
                            MoveTowards(tgt.transform.position);
                        }
                    }
                    else if (currentState == UnitState.Idle)
                    {
                        if (agent != null && agent.isOnNavMesh)
                        {
                            agent.isStopped = true;
                            isMoving = false;
                            RpcUpdateAnimationState(false, isAttacking);
                        }
                    }
                    break;
                case UnitState.Attacking:
                    var targetObj = GetTargetEnemy();
                    if (targetObj != null)
                    {
                        float distance = Vector3.Distance(transform.position, targetObj.transform.position);
                        if (distance <= unitStats.attackRange)
                        {
                            Attack();
                            if (agent != null && agent.isOnNavMesh)
                            {
                                agent.isStopped = true;
                                isMoving = false;
                                RpcUpdateAnimationState(false, true);
                            }
                        }
                        else if (distance <= unitStats.detectionRange)
                        {
                            MoveTowards(targetObj.transform.position);
                        }
                        else if (distance > disengageRange)
                        {
                            targetEnemyNetId = 0;
                            if (!string.IsNullOrEmpty(previousDirection))
                            {
                                currentState = UnitState.Moving;
                                MoveInDirection(previousMoveDirection);
                            }
                            else
                            {
                                currentState = UnitState.Idle;
                                isMoving = false;
                                RpcUpdateAnimationState(false, false);
                            }
                        }
                    }
                    else
                    {
                        targetEnemyNetId = 0;
                        if (!string.IsNullOrEmpty(previousDirection))
                        {
                            currentState = UnitState.Moving;
                            MoveInDirection(previousMoveDirection);
                        }
                        else
                        {
                            currentState = UnitState.Idle;
                            isMoving = false;
                            RpcUpdateAnimationState(false, false);
                        }
                    }
                    break;
            }
            if (attackTimer > 0) attackTimer -= updateInterval;
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private GameObject GetTargetEnemy()
    {
        if (targetEnemyNetId == 0) return null;
        if (NetworkClient.spawned.TryGetValue(targetEnemyNetId, out var id) && id.gameObject != null)
            return id.gameObject;
        targetEnemyNetId = 0;
        return null;
    }

    [Server]
    private void FindNearestEnemy()
    {
        if (disableAutoTargeting || unitInfo == null) return;
        var allUnits = FindObjectsOfType<Unit>();
        float closestDistance = unitStats.detectionRange;
        Unit closestEnemy = null;
        foreach (var other in allUnits)
        {
            if (other == unitInfo || other.teamIndex == unitInfo.teamIndex) continue;
            float d = Vector3.Distance(transform.position, other.transform.position);
            if (d < closestDistance)
            {
                closestDistance = d;
                closestEnemy = other;
            }
        }
        if (closestEnemy != null)
        {
            targetEnemyNetId = closestEnemy.GetComponent<NetworkIdentity>().netId;
            Debug.Log($"[{gameObject.name}] 가장 가까운 적 발견: {closestEnemy.name}");
        }
    }

    [Server]
    private void FindTargetByName(string nameFilter)
    {
        var allUnits = FindObjectsOfType<Unit>();
        float bestDist = Mathf.Infinity;
        Unit best = null;
        foreach (var other in allUnits)
        {
            if (other.teamIndex == unitInfo.teamIndex) continue;
            if (other.name.Contains(nameFilter))
            {
                float d = Vector3.Distance(transform.position, other.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = other;
                }
            }
        }
        if (best != null)
        {
            targetEnemyNetId = best.GetComponent<NetworkIdentity>().netId;
            Debug.Log($"[{gameObject.name}] 이름으로 타겟 발견: {best.name}");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 이름 '{nameFilter}'로 타겟을 찾을 수 없음. 가장 가까운 적 탐색");
            FindNearestEnemy();
        }
    }

    [Server]
    private void MoveTowards(Vector3 pos)
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.SetDestination(pos);
        targetDestination = pos;
        isMoving = true;
        RpcUpdateAnimationState(true, isAttacking);
    }

    [Server]
    private void Attack()
    {
        var tgt = GetTargetEnemy();
        if (attackTimer > 0 || tgt == null) return;
        Vector3 dir = tgt.transform.position - transform.position; dir.y = 0;
        transform.rotation = Quaternion.LookRotation(dir);
        var stats = tgt.GetComponent<TheOneAndOnlyStats>();
        if (stats != null)
        {
            stats.TakeDamage(unitStats.attackDamage);
            if (stats.IsDead()) targetEnemyNetId = 0;
        }
        attackTimer = unitStats.GetAttackCooldown();
        isAttacking = true;
        UpdateAnimationState(isMoving, true);
        RpcUpdateAnimationState(isMoving, true);
        StartCoroutine(AttackCooldown());
        Debug.Log($"[{gameObject.name}] 공격 실행: {tgt.name}");
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(unitStats.GetAttackCooldown());
        performMeleeAttack = true;
    }

    [ClientRpc]
    private void RpcUpdateAnimationState(bool move, bool attack)
    {
        if (!isServer)
        {
            isMoving = move;
            isAttacking = attack;
            clientIsMoving = move;
            UpdateAnimationState(move, attack);
        }
    }

    private void UpdateAnimationState(bool move, bool attack)
    {
        if (anim == null) return;
        anim.SetFloat("Blend", move ? 1f : 0f, motionSmoothTime, Time.deltaTime);
        anim.SetBool("isAttacking", attack);
    }

    private IEnumerator ResetAttackAnimation()
    {
        yield return new WaitForSeconds(1f);
        isAttacking = false;
        UpdateAnimationState(isMoving, false);
        RpcUpdateAnimationState(isMoving, false);
    }

    // 클라이언트에서 수동 이동 처리
    [ClientCallback]
    private void Update()
    {
        if (isServer) return;

        // 클라이언트에서 목적지로 이동 처리
        if (clientIsMoving && targetDestination != Vector3.zero)
        {
            Vector3 dir = targetDestination - transform.position;
            float dist = dir.magnitude;
            if (dist > 0.5f) // 정지 거리 조정
            {
                dir.Normalize();
                dir.y = 0;

                // 회전 처리
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * 5f
                    );
                }

                // 이동 처리
                transform.position += dir * unitStats.moveSpeed * Time.deltaTime;
            }
            else
            {
                clientIsMoving = false;
                UpdateAnimationState(false, isAttacking);
            }
        }

        // 애니메이션 상태 업데이트
        UpdateAnimationState(clientIsMoving || isMoving, isAttacking);
    }

    private void OnDrawGizmosSelected()
    {
        if (unitStats == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, unitStats.detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, unitStats.attackRange);

        // 현재 목적지 표시
        if (targetDestination != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(targetDestination, 1f);
            Gizmos.DrawLine(transform.position, targetDestination);
        }
    }

    public void MeleeAttack() { /* 애니메이션 이벤트 콜백 */ }
}