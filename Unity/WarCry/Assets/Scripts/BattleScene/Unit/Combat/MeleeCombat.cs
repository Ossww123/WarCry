using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Movement)), RequireComponent(typeof(Stats))]

public class MeleeCombat : NetworkBehaviour
{
    private Movement moveScript;
    private Stats stats;
    private Animator anim;

    [Header("Target")]
    [SyncVar]
    public GameObject targetEnemy;

    [Header("Melee Attack Variables")]
    [SyncVar] public bool performMeleeAttack = true;
    private float attackInterval;
    private float nextAttackTime = 0;
    
    [Header("Rotation Before Attack")]
    public float rotateSpeedBeforeAttack = 0.1f; // 공격 전 회전 속도
    public float minAngleToAttack = 5f; // 공격 가능한 최소 각도 (5도 이내면 공격 가능)
    [SyncVar] private bool isRotatingToAttack = false;

    // 디버깅을 위한 변수
    [SyncVar] public bool isAttacking = false;
    
    void Start()
    {
        moveScript = GetComponent<Movement>();
        stats = GetComponent<Stats>();
        anim = GetComponent<Animator>();
        
        // 디버깅 로그
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] MeleeCombat Start - isLocalPlayer: {isLocalPlayer}, isServer: {isServer}");
    }

    void Update()
    {
        attackInterval = stats.attackSpeed / ((500 + stats.attackSpeed) * 0.01f);

        targetEnemy = moveScript.targetEnemy;

        // 로컬 플레이어만 전투 로직 실행
        if (isLocalPlayer && targetEnemy != null && performMeleeAttack && Time.time > nextAttackTime)
        {
            if (Vector3.Distance(transform.position, targetEnemy.transform.position) <= moveScript.stoppingDistance)
            {
                // 디버깅 로그
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Trying to attack from client - distance: {Vector3.Distance(transform.position, targetEnemy.transform.position)}, stoppingDistance: {moveScript.stoppingDistance}");
                
                if (!isRotatingToAttack && !isAttacking)
                {
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Sending attack command to server");
                    CmdRequestAttack(targetEnemy);
                    // 클라이언트 측에서도 쿨다운 적용
                    nextAttackTime = Time.time + attackInterval;
                }
            }
        }
    }

    [Command]
    private void CmdRequestAttack(GameObject enemy)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Server received attack request for enemy: {enemy?.name ?? "null"}");
        
        // 잘못된 enemy 참조 방지
        if (enemy == null)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] Server rejected attack - enemy is null");
            return;
        }
        
        // 서버에서 공격 가능 여부 체크
        if (!performMeleeAttack || isRotatingToAttack || isAttacking)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] Server rejected attack - invalid state (performMeleeAttack: {performMeleeAttack}, isRotatingToAttack: {isRotatingToAttack}, isAttacking: {isAttacking})");
            return;
        }

        // 서버에서 거리 체크 (클라이언트가 속이는 것 방지)
        float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Distance to enemy: {distanceToEnemy}, stoppingDistance: {moveScript.stoppingDistance}");
        
        // 정확한 거리 비교 대신 여유를 두고 체크 (네트워크 지연 고려)
        if (distanceToEnemy > moveScript.stoppingDistance * 1.5f)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] Server rejected attack - enemy too far");
            return;
        }

        // 서버에서 공격 시작
        targetEnemy = enemy;
        isRotatingToAttack = true;
        performMeleeAttack = false; // 공격 중 다른 공격 요청 방지
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Server starting attack sequence");
        StartCoroutine(ServerRotateAndAttack());
    }

    // 서버에서만 실행하는 회전 및 공격 코루틴
    private IEnumerator ServerRotateAndAttack()
    {
        if (targetEnemy == null) 
        {
            isRotatingToAttack = false;
            performMeleeAttack = true;
            yield break;
        }
        
        // 공격 전 회전 과정
        Vector3 targetDirection = targetEnemy.transform.position - transform.position;
        targetDirection.y = 0;
        
        Vector3 currentDirection = transform.forward;
        currentDirection.y = 0;
        
        float angleToTarget = Vector3.Angle(currentDirection, targetDirection);
        
        // 서버에서 회전 동작 시작을 모든 클라이언트에 알림
        RpcStartRotation();
        
        // 원하는 방향으로 회전
        float rotationDuration = 0;
        float maxRotationTime = 1.0f; // 최대 1초까지만 회전 시도
        
        while (angleToTarget > minAngleToAttack && rotationDuration < maxRotationTime)
        {
            if (targetEnemy == null)
            {
                isRotatingToAttack = false;
                performMeleeAttack = true;
                RpcEndRotation(false); // 회전 중단을 알림
                yield break;
            }
            
            targetDirection = targetEnemy.transform.position - transform.position;
            targetDirection.y = 0;
            
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                Time.deltaTime / rotateSpeedBeforeAttack
            );
            
            currentDirection = transform.forward;
            currentDirection.y = 0;
            angleToTarget = Vector3.Angle(currentDirection, targetDirection);
            
            rotationDuration += Time.deltaTime;
            yield return null;
        }
        
        // 회전 완료
        isRotatingToAttack = false;
        
        // 회전이 완료되었음을 알림
        RpcEndRotation(true);
        
        // 모든 클라이언트에게 공격 시작을 알림
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Server initiating attack animation");
        RpcStartAttackAnimation();
        
        // 서버에서 데미지 처리 지연 (애니메이션 타이밍에 맞춤)
        yield return new WaitForSeconds(attackInterval * 0.3f); // 애니메이션 중간쯤에 데미지 적용
        
        // 데미지 처리
        if (targetEnemy != null)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Server applying damage to {targetEnemy.name}");
            
            // Stats 컴포넌트가 있는지 확인
            float adjustedDamage = stats.attackDamage * 0.1f;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Applying adjusted damage: {adjustedDamage} instead of {stats.attackDamage}");
            
            // 타겟에 데미지 적용
            stats.ApplyDamage(targetEnemy, adjustedDamage);
        }
        
        // 공격 종료 및 쿨다운
        yield return new WaitForSeconds(attackInterval * 0.3f);
        
        // 모든 클라이언트에게 공격 종료를 알림
        RpcEndAttackAnimation();
        
        // 약간의 지연 후 상태 초기화 (연속 공격 방지)
        yield return new WaitForSeconds(0.2f);
        performMeleeAttack = true;
    }

    [ClientRpc]
    private void RpcStartRotation()
    {
        // 클라이언트에서 회전 애니메이션 시작
        isRotatingToAttack = true;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Client received rotation start");
    }

    [ClientRpc]
    private void RpcEndRotation(bool success)
    {
        // 클라이언트에서 회전 애니메이션 종료
        isRotatingToAttack = false;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Client received rotation end - success: {success}");
    }

    [ClientRpc]
    private void RpcStartAttackAnimation()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Client received attack animation start");
        isAttacking = true;
        performMeleeAttack = false;
        
        // 공격 애니메이션 시작
        if (anim != null)
        {
            anim.SetBool("isAttacking", true);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Animation state set to attacking: true");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Animator reference is null!");
        }
    }

    [ClientRpc]
    private void RpcEndAttackAnimation()
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Client received attack animation end");
        isAttacking = false;
        
        // 공격 애니메이션 종료
        if (anim != null)
        {
            anim.SetBool("isAttacking", false);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Animation state set to attacking: false");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Animator reference is null!");
        }
        
        // 약간의 지연 후 다음 공격 허용
        StartCoroutine(ResetAttackState());
    }
    
    private IEnumerator ResetAttackState()
    {
        yield return new WaitForSeconds(0.2f);
        performMeleeAttack = true;
    }

    // 애니메이션 이벤트에서 호출되는 메서드
    // 이제 데미지는 서버 측 코루틴에서 처리하므로 이 메서드는 실제 데미지를 주지 않음
    public void MeleeAttack()
    {
        // 디버깅 로그만 남김
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Animation event MeleeAttack called - isLocalPlayer: {isLocalPlayer}, isServer: {isServer}, targetEnemy: {(targetEnemy != null ? targetEnemy.name : "null")}, isAttacking: {isAttacking}");
    }
}
