// Assets/Scripts/Army/UnitController.cs

using UnityEngine;

public class UnitController : MonoBehaviour
{
    [Header("Core Components")]
    public TheOneAndOnlyStats stats;                   // 유닛 스탯 (TheOneAndOnlyStats로 변경)
    public LayerMask enemyLayers;             // 적 레이어

    [Header("Animator")]
    public string walkParam = "Walk";
    public string attackParam = "Attack";

    [Header("Debug")]
    public bool showDebugLogs = true;  // 디버그 로그 표시 여부

    private enum State { Idle, Moving, Chasing, Attacking, Retreating }
    private State currentState = State.Idle;

    private Transform targetEnemy;
    private bool disableAutoTargeting = false;
    private float attackTimer = 0f;

    // **로컬 명령 캐싱 필드**
    private string currentDirection = "";
    private string currentTarget = "";
    private Vector3 moveDirection = Vector3.zero;

    // 후퇴 모드 상태
    private bool isRetreating = false;

    private Animator animator;
    private Rigidbody rb;

    // 적 감지 관련 필드 추가
    private float enemyDetectionTimer = 0f;
    private float enemyDetectionInterval = 0.5f; // 0.5초마다 적 감지

    void Start()
    {
        if (stats == null)
            stats = GetComponent<TheOneAndOnlyStats>();  // TheOneAndOnlyStats로 변경

        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // EnemyLayers 값이 설정되어 있는지 확인
        if (enemyLayers.value == 0)
        {
            DebugLog("경고: enemyLayers가 설정되지 않았습니다!");
            // Enemy 레이어(7번)를 기본값으로 설정
            enemyLayers = 1 << 7;
        }

        DebugLog($"UnitController initialized with EnemyLayers: {enemyLayers.value}");
    }

    /// <summary>
    /// CommandProcessor에서 호출하는 메서드.
    /// direction, target을 내부에 저장하고 이동벡터를 계산.
    /// </summary>
    public void ApplyCommand(string direction, string target)
    {
        // "뒤" 명령 처리 - 다른 모든 명령 취소 및 후퇴 모드 활성화
        if (direction == "뒤")
        {
            isRetreating = true;
            disableAutoTargeting = true;
            targetEnemy = null;
            currentDirection = direction;
            currentTarget = "";  // 타겟 명령 취소
            moveDirection = ParseDirectionToVector(direction);

            // 상태 업데이트
            currentState = State.Retreating;
            animator.SetBool(walkParam, true);

            DebugLog($"ApplyCommand → Retreat command received. 모든 명령 취소 및 후퇴 모드 활성화.");
            return;
        }

        // 후퇴 모드 해제 (새 명령 들어옴)
        if (isRetreating && (!string.IsNullOrEmpty(direction) || !string.IsNullOrEmpty(target)))
        {
            // 먼저 Idle 상태로 리셋하고 새 명령 처리
            DebugLog($"후퇴 모드 중 새 명령 수신: 방향 [{direction}] 타겟 [{target}] - Idle로 전환 후 처리");
            EnterIdle();
            isRetreating = false;
            disableAutoTargeting = false;

            // 여기서 바로 return하지 않고 아래 코드로 진행하여 새 명령 처리
        }

        // 일반 명령 처리
        currentDirection = direction;
        currentTarget = target;
        moveDirection = ParseDirectionToVector(direction);

        // 새로운 타겟 명령이 들어오면 기존 타겟 무시하고 새로 찾기
        if (!string.IsNullOrEmpty(target))
        {
            DebugLog($"ApplyCommand → 새 공격 타겟 명령: {target}, 재탐색 시작");
            // 기존 타겟 설정 초기화하고 새로 탐색 시작
            targetEnemy = null;
            TryAcquireTarget(stats.detectionRange, target);  // detectionRange로 변경
        }

        if (!string.IsNullOrEmpty(direction))
            DebugLog($"ApplyCommand → Moving: {direction}");
        if (!string.IsNullOrEmpty(target))
            DebugLog($"ApplyCommand → Attack target: {target}");
    }

    void Update()
    {
        // 타이머 업데이트
        if (attackTimer > 0)
            attackTimer -= Time.deltaTime;

        // 1) 후퇴 모드 처리 - 다른 모든 로직 무시하고 후퇴만 실행
        if (isRetreating)
        {
            // 후퇴 중일 경우 다른 모든 로직 무시
            if (currentState != State.Retreating)
            {
                currentState = State.Retreating;
                animator.SetBool(walkParam, true);
            }

            // 후퇴 이동 처리는 FixedUpdate에서 수행되므로 여기서는 상태만 관리
            return;
        }

        // 2) 적 추적 또는 공격 중이면 해당 로직을 지속 수행
        if (targetEnemy != null && (currentState == State.Chasing || currentState == State.Attacking))
        {
            HandleChaseAndAttack();
            return;
        }

        // 3) 명령이 없고 Idle 상태가 아니면 Idle 상태로 복귀
        if (string.IsNullOrEmpty(currentDirection) && string.IsNullOrEmpty(currentTarget) && currentState != State.Idle)
        {
            EnterIdle();
            return;
        }

        // 4) 타겟 공격 명령
        if (!string.IsNullOrEmpty(currentTarget))
        {
            TryAcquireTarget(stats.detectionRange, currentTarget);  // detectionRange로 변경
            if (targetEnemy != null)
            {
                HandleChaseAndAttack();
            }
            return;
        }

        // 5) 일반 이동 명령 (이동 중에도 자동 추적 기능 활성화)
        if (!string.IsNullOrEmpty(currentDirection) && currentDirection != "정지")
        {
            if (currentState != State.Moving && targetEnemy == null)
            {
                currentState = State.Moving;
                animator.SetBool(walkParam, true);
                DebugLog($"Moving: {currentDirection}");
            }
        }

        // 6) 주기적으로 적 감지 실행 (자동 타게팅이 비활성화되지 않은 경우에만)
        if (!disableAutoTargeting)
        {
            enemyDetectionTimer -= Time.deltaTime;
            if (enemyDetectionTimer <= 0)
            {
                enemyDetectionTimer = enemyDetectionInterval;

                // 적이 없을 경우에만 새로운 적 감지
                if (targetEnemy == null)
                {
                    CheckForEnemies();
                }
            }
        }
    }

    // 적 자동 감지 메서드
    private void CheckForEnemies()
    {
        // 이미 타겟이 있거나 후퇴 모드, 자동 감지 비활성화 상태면 무시
        if (targetEnemy != null || isRetreating || disableAutoTargeting)
            return;

        DebugLog($"자동 적 감지 시작 - TrackingRange: {stats.trackingRange}");  // trackingRange로 변경

        // 범위 내 모든 콜라이더 검색
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.trackingRange, enemyLayers);  // trackingRange로 변경
        DebugLog($"적 감지 OverlapSphere 검색 결과: {hits.Length}개 발견");

        // 가장 가까운 적을 찾기 위한 초기화
        float bestDist = Mathf.Infinity;
        Transform nearest = null;

        foreach (var hit in hits)
        {
            // Enemy 태그 확인
            if (hit.CompareTag("Enemy"))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                // 더 가까운 적이면 저장
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = hit.transform;
                }
            }
        }

        // 가장 가까운 적이 있으면 타겟 설정 및 추적 시작
        if (nearest != null)
        {
            DebugLog($"가장 가까운 적 선택: {nearest.name}, 거리: {bestDist}");
            targetEnemy = nearest;
            currentState = State.Chasing;
            animator.SetBool(walkParam, true);    // 걷기 애니메이션
            HandleChaseAndAttack();               // 즉시 추적/공격 로직 실행
        }
    }

    void FixedUpdate()
    {
        // 이동 로직은 FixedUpdate에서 처리
        if (currentState == State.Moving || currentState == State.Retreating)
        {
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                // 회전 및 위치 이동
                Quaternion targetRot = Quaternion.LookRotation(moveDirection);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, stats.moveSpeed * Time.deltaTime));  // moveSpeed로 변경
                rb.MovePosition(rb.position + moveDirection.normalized * stats.moveSpeed * Time.deltaTime);  // moveSpeed로 변경
            }
        }
    }

    /// <summary>
    /// 지정 범위 내에서 이름에 filter를 포함하는 가장 가까운 적을 찾아 Chasing 상태로 전환
    /// </summary>
    void TryAcquireTarget(float range, string nameFilter)
    {
        // 후퇴 모드이면 타겟 획득 중단
        if (isRetreating)
        {
            DebugLog("후퇴 모드에서는 타겟 획득을 시도하지 않습니다.");
            return;
        }

        DebugLog($"TryAcquireTarget 호출 - 범위: {range}, 필터: {nameFilter}");

        Collider[] hits = Physics.OverlapSphere(transform.position, range, enemyLayers);
        DebugLog($"OverlapSphere 결과: {hits.Length}개");

        float bestDist = Mathf.Infinity;
        Transform pick = null;

        foreach (var hit in hits)
        {
            DebugLog($"검토 중: {hit.name}, 태그: {hit.tag}");

            // 추가 - 태그 체크를 먼저 하고, Enemy 태그가 있는 경우에만 이름 필터 적용
            if (hit.CompareTag("Enemy"))
            {
                DebugLog($"Enemy 태그 확인됨: {hit.name}");

                // 이름 필터가 비어있거나 hit.name에 필터가 포함되어 있으면 처리
                if (string.IsNullOrEmpty(nameFilter) || hit.name.Contains(nameFilter))
                {
                    float d = Vector3.Distance(transform.position, hit.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        pick = hit.transform;
                        DebugLog($"새 최근접 적: {hit.name}, 거리: {d}");
                    }
                }
                else
                {
                    DebugLog($"이름 필터 미일치 (Enemy 태그 있음): {hit.name}");

                    // 이름 필터가 있지만 정확히 일치하지 않는 경우에도 적 태그가 있으면 선택
                    // 특정 유닛 타입을 명확히 지정하려는 의도인 경우가 많으므로 이 부분을 활성화
                    if (pick == null) // 더 나은 대상이 없는 경우에만
                    {
                        float d = Vector3.Distance(transform.position, hit.transform.position);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            pick = hit.transform;
                            DebugLog($"필터 일치하지 않지만 Enemy 태그 있어 선택: {hit.name}, 거리: {d}");
                        }
                    }
                }
            }
            else
            {
                DebugLog($"Enemy 태그 없음: {hit.name}, 태그: {hit.tag}");
            }
        }

        if (pick != null)
        {
            State prev = currentState;
            targetEnemy = pick;
            currentState = State.Chasing;
            animator.SetBool(walkParam, true);  // 추적할 때는 걷기 모션 활성화
            DebugLog($"타겟 획득: {pick.name} (이전 상태: {prev})");
        }
        else
        {
            DebugLog("적합한 타겟을 찾지 못했습니다.");
        }
    }

    /// <summary>
    /// Chasing 또는 Attacking 상태에서 실제 추적 및 공격 로직 수행
    /// </summary>
    void HandleChaseAndAttack()
    {
        // 후퇴 모드이면 추적/공격 중단
        if (isRetreating)
        {
            DebugLog("후퇴 모드에서는 추적 및 공격을 수행하지 않습니다.");
            targetEnemy = null;
            return;
        }

        if (targetEnemy == null)
        {
            DebugLog($"타겟이 null입니다, Idle 상태로 전환");
            EnterIdle();
            return;
        }

        // 새로운 타겟 명령이 들어왔는지 확인
        if (!string.IsNullOrEmpty(currentTarget))
        {
            // 현재 타겟의 이름과 명령의 타겟 이름이 일치하지 않으면 새로운 타겟 찾기
            if (!targetEnemy.name.Contains(currentTarget))
            {
                DebugLog($"새 타겟 명령 {currentTarget}에 따라 타겟 재설정 (현재: {targetEnemy.name})");
                TryAcquireTarget(stats.detectionRange, currentTarget);  // detectionRange로 변경
                // 새 타겟을 찾지 못했으면 기존 타겟 유지
                if (targetEnemy == null)
                {
                    DebugLog($"새 타겟 {currentTarget}을 찾지 못했습니다. 기존 작업 계속");
                    return;
                }
            }
        }

        float dist = Vector3.Distance(transform.position, targetEnemy.position);
        DebugLog($"타겟과의 거리: {dist}, 공격범위: {stats.attackRange}, 이탈범위: {stats.disengageRange}");  // attackRange, disengageRange로 변경

        // 타겟이 DisengageRange를 벗어나면 추적 중단
        if (dist > stats.disengageRange)  // disengageRange로 변경
        {
            DebugLog($"타겟이 이탈 범위를 벗어남 (거리: {dist}, 최대 범위: {stats.disengageRange}), Idle로 전환");  // disengageRange로 변경
            EnterIdle();
            return;
        }

        Vector3 toTar = (targetEnemy.position - transform.position).normalized;

        // 항상 적 방향을 바라보도록 회전
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTar), stats.moveSpeed * Time.deltaTime);  // moveSpeed로 변경

        if (dist > stats.attackRange)  // attackRange로 변경
        {
            // 공격 범위 밖이면 추적
            if (currentState != State.Chasing)
            {
                currentState = State.Chasing;
                animator.SetBool(walkParam, true);
                DebugLog($"추적 중 (거리: {dist}, 공격 범위: {stats.attackRange})");  // attackRange로 변경
            }

            // 적 방향으로 이동
            rb.MovePosition(rb.position + toTar * stats.moveSpeed * Time.deltaTime);  // moveSpeed로 변경
        }
        else
        {
            // 공격 범위 안에 들어오면 공격
            if (currentState != State.Attacking)
            {
                currentState = State.Attacking;
                animator.SetBool(walkParam, false);
                animator.SetTrigger(attackParam);
                attackTimer = GetAttackCooldown();  // 공격 쿨다운 계산 메서드 사용
                DebugLog($"공격 시작! (거리: {dist}, 공격 범위: {stats.attackRange})");  // attackRange로 변경
            }
            else if (attackTimer <= 0f)
            {
                animator.SetTrigger(attackParam);
                attackTimer = GetAttackCooldown();  // 공격 쿨다운 계산 메서드 사용
                DebugLog($"공격 재시작");
            }
        }
    }

    // 공격 쿨다운 계산 메서드 추가
    private float GetAttackCooldown()
    {
        return stats.GetAttackCooldown();  // TheOneAndOnlyStats의 메서드 사용
    }

    /// <summary>
    /// 애니메이션 이벤트 또는 수동 호출로 데미지 적용
    /// </summary>
    public void OnAttackHit()
    {
        // 후퇴 모드이면 공격하지 않음
        if (isRetreating || targetEnemy == null) return;

        if (Vector3.Distance(transform.position, targetEnemy.position) <= stats.attackRange)  // attackRange로 변경
        {
            var targetStats = targetEnemy.GetComponent<TheOneAndOnlyStats>();  // TheOneAndOnlyStats로 변경
            if (targetStats != null)
            {
                // TheOneAndOnlyStats의 TakeDamage 메서드 사용
                if (targetStats.isServer)  // isServer인 경우에만 데미지 적용
                {
                    targetStats.TakeDamage(stats.attackDamage);  // attackDamage로 변경
                    DebugLog($"데미지 {stats.attackDamage} 적용됨 → {targetEnemy.name}");  // attackDamage로 변경
                }
                else
                {
                    DebugLog($"경고: {targetEnemy.name}에 서버 권한이 없어 데미지를 줄 수 없습니다.");
                }
            }
            else
            {
                DebugLog($"경고: {targetEnemy.name}에 TheOneAndOnlyStats 컴포넌트가 없습니다.");  // TheOneAndOnlyStats로 변경
            }
        }
    }

    public void OnAttackStart() => DebugLog($"공격 애니메이션 시작");
    public void OnAttackEnd() => DebugLog($"공격 애니메이션 종료");

    /// <summary>
    /// Idle 상태로 강제 복귀하고 후퇴 모드 해제
    /// </summary>
    public void ForceIdle()
    {
        isRetreating = false;
        disableAutoTargeting = false;
        EnterIdle();
    }

    void EnterIdle()
    {
        State prev = currentState;
        currentState = State.Idle;
        targetEnemy = null;
        animator.SetBool(walkParam, false);
        DebugLog($"Idle 상태로 전환 (이전 상태: {prev})");
    }

    /// <summary>
    /// 문자열 방향을 벡터로 변환 (CommandProcessor와 동일 매핑)
    /// </summary>
    private Vector3 ParseDirectionToVector(string dir)
    {
        switch (dir)
        {
            case "앞": return Vector3.right;
            case "뒤": return Vector3.left;
            case "위": return Vector3.forward;
            case "아래": return Vector3.back;
            default: return Vector3.zero;
        }
    }

    // 디버그 로그 래퍼 - 필요시 간단히 켜고 끌 수 있음
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] {message}");
    }
}