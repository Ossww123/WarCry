using System;
using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// This is a deprecated class, and you should ALWAYS use TheOneAndOnlyStats class.
/// This class used to manage damage reception, health, and death status for a battle
/// unit in a networked environment.
/// </summary>
[Obsolete("This is a deprecated class. You should use TheOneAndOnlyStats class.")]
public class BattleDamageReceiver : NetworkBehaviour
{
    /// <summary>
    /// Represents the maximum health value for the unit. This value is used to initialize the unit's health
    /// and is the upper limit for health restoration or adjustment. The value is set in the Unity Inspector
    /// and can be managed in the HealthDisplay for visual representation. It is assigned during server initialization
    /// and remains constant during the unit's lifecycle.
    /// </summary>
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;

    /// <summary>
    /// Represents the current health value of the unit. It indicates the unit's real-time health
    /// status during gameplay, decreasing as the unit takes damage. Synchronized across the network
    /// using a hook method to provide consistent state updates to all connected clients. This variable
    /// is critical for determining whether the unit is alive or has been defeated in combat.
    /// </summary>
    [SyncVar(hook = nameof(OnCurrentHealthChanged))]
    private int currentHealth;

    /// <summary>
    /// Determines the delay in seconds before the object is destroyed after death.
    /// This value is used to provide a grace period for any death-related effects,
    /// such as animations or sound, before the object is removed from the network.
    /// </summary>
    [Header("Death Settings")]
    [SerializeField]
    private float destroyDelay = 2f;

    /// <summary>
    /// Indicates whether the unit is dead. This synchronized variable is managed across the network
    /// to reflect the current death state of the unit. It is updated on the server and propagated
    /// to clients, triggering networked events like visual effects or gameplay logic adjustments.
    /// </summary>
    [SyncVar(hook = nameof(OnDeadStateChanged))]
    private bool isDead = false;

    /// <summary>
    /// Indicates whether the unit is classified as a castle within the game.
    /// This boolean value determines specific behaviors and conditions, such as the necessity of the Unit component
    /// and handling unique mechanics like castle destruction during gameplay. It is primarily configured in the Unity Inspector
    /// and used to initialize or control castle-specific logic, including gameplay-critical events.
    /// </summary>
    [Header("Unit Properties")]
    [SerializeField] private bool isCastle = false;
    [SerializeField] private bool debugMode = true;

    // 컴포넌트 참조
    private Animator animator;
    private Unit unitInfo;
    private HealthDisplay _healthDisplay;

    // 클라이언트 전용 이벤트 (로컬 이펙트, UI 변경 등에 사용)
    public delegate void DeathEventHandler(GameObject victim);
    public event DeathEventHandler OnDeath;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        unitInfo = GetComponent<Unit>();
        _healthDisplay = GetComponent<HealthDisplay>();

        // Unit 컴포넌트 확인 (성채인 경우 반드시 필요)
        if (isCastle && unitInfo == null)
        {
            Debug.LogError($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 성채에 Unit 컴포넌트가 없습니다!");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // 서버에서 초기 체력 설정
        currentHealth = maxHealth;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 클라이언트에서 자동 태그 설정 (적 유닛인 경우)
        SetupEnemyTag();

        // 체력 UI 초기화
        if (_healthDisplay != null)
        {
            _healthDisplay.Start3DSlider(maxHealth);
        }

        // 디버그 정보 출력
        if (debugMode)
        {
            DebugLog($"초기화: 체력={currentHealth}/{maxHealth}, 팀={unitInfo?.teamIndex}, 태그={gameObject.tag}");
        }
    }

    // 적 유닛인 경우 Enemy 태그 설정
    private void SetupEnemyTag()
    {
        if (unitInfo != null && !unitInfo.IsOwnedByLocalPlayer() && unitInfo.teamIndex != GetLocalPlayerTeam())
        {
            gameObject.tag = "Enemy";
            DebugLog($"Enemy 태그 설정됨");
        }
    }

    // 로컬 플레이어의 팀 인덱스 가져오기
    private TeamIndex GetLocalPlayerTeam()
    {
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerInfo>();
        return localPlayer != null ? localPlayer.teamId : TeamIndex.Unknown;
    }

    // 디버그 로그 출력
    private void DebugLog(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}]: {message}");
        }
    }

    // 서버에서만 호출 가능한 대미지 처리 메서드
    [Server]
    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 데미지 무시됨: 이미 사망 상태");
            return;
        }

        // 대미지 적용
        int actualDamage = Mathf.Max(1, damage);
        currentHealth -= actualDamage;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] {actualDamage} 데미지 적용! 현재 체력: {currentHealth}/{maxHealth}");

        // 사망 처리
        if (currentHealth <= 0)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 사망 처리 시작");
            Die();
        }
    }

    // 서버에서 호출하는 사망 처리 메서드
    [Server]
    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 사망 처리 완료");

        // 성채인 경우 게임 종료 처리
        if (isCastle)
        {
            HandleCastleDestruction();
        }

        // 일정 시간 후 오브젝트 제거
        if (!isCastle)
        {
            StartCoroutine(DestroyAfterDelay());
        }
    }

    // 성채 파괴 시 승리 처리
    [Server]
    private void HandleCastleDestruction()
    {
        if (unitInfo == null)
        {
            Debug.LogError($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 성채인데 Unit 컴포넌트를 찾지 못했습니다!");
            return;
        }

        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 성채 파괴! 팀={unitInfo.teamIndex}");

        // 상대방 플레이어를 승자로 선언
        var players = FindObjectsOfType<PlayerInfo>();
        bool foundWinner = false;

        foreach (var player in players)
        {
            if (player.teamId != unitInfo.teamIndex)
            {
                Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] {player.playerName}의 승리를 선언합니다.");
                player.CmdDeclareVictory();
                foundWinner = true;
                break;
            }
        }

        if (!foundWinner)
        {
            Debug.LogWarning($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 성채가 파괴되었지만 승자를 찾지 못했습니다!");
        }
    }

    // 지연 후 오브젝트 제거 (서버 전용)
    [Server]
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);

        // 네트워크를 통해 오브젝트 제거
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 네트워크 오브젝트 제거");
        NetworkServer.Destroy(gameObject);
    }

    // 체력 변화 시 호출되는 훅 (서버, 클라이언트 모두에서 실행됨)
    private void OnCurrentHealthChanged(int oldHealth, int newHealth)
    {
        // 변경 로깅
        Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] 체력 변경: {oldHealth} → {newHealth}");

        // UI 업데이트
        if (_healthDisplay != null)
        {
            _healthDisplay.Update3DSlider(newHealth);
        }

        // 피해 시각 효과 등 (필요시 추가)
        if (newHealth < oldHealth && !isDead)
        {
            PlayDamageEffect();
        }
    }

    // 사망 상태 변화 시 호출되는 훅 (서버, 클라이언트 모두에서 실행됨)
    private void OnDeadStateChanged(bool oldState, bool newState)
    {
        if (newState == true && oldState == false)
        {
            DebugLog($"사망 상태로 변경됨: 애니메이션 및 이펙트 실행");

            // 사망 애니메이션 및 효과
            PlayDeathAnimation();

            // 콜라이더 비활성화 (최적화 고려사항: 유닛 수가 많은 경우 캐싱 검토)
            DisableColliders();

            // 클라이언트 전용 사망 이벤트 발생 (로컬 이펙트, UI 변경 등에 사용)
            OnDeath?.Invoke(gameObject);
        }
    }

    // 피해 효과 재생
    private void PlayDamageEffect()
    {
        // 피격 애니메이션 (있는 경우)
        if (animator != null)
        {
            DebugLog($"피격 애니메이션 트리거");
            animator.SetTrigger("Hit");
        }

        // 추가 효과 (필요시)
        // 예: 파티클, 사운드 등
    }

    // 사망 애니메이션 재생
    private void PlayDeathAnimation()
    {
        if (animator != null)
        {
            DebugLog($"사망 애니메이션 트리거");
            animator.SetTrigger("Death");
        }
    }

    // 콜라이더 비활성화
    private void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }
        DebugLog($"{colliders.Length}개 콜라이더 비활성화");
    }

    // 현재 체력 비율 반환 (UI 등에서 사용)
    public float GetHealthPercent()
    {
        return (float)currentHealth / maxHealth;
    }

    // 현재 체력 반환
    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    // 최대 체력 반환
    public int GetMaxHealth()
    {
        return maxHealth;
    }

    // 사망 여부 반환
    public bool IsDead()
    {
        return isDead;
    }

    // 디버깅용 - 온라인/오프라인 상태 확인
    public void CheckNetworkState()
    {
        DebugLog($"네트워크 상태: isServer={isServer}, isClient={isClient}, isLocalPlayer={isLocalPlayer}, ");
    }

    // 강제 데미지 테스트용 (디버그 전용)
    [ContextMenu("Test Take 10 Damage")]
    private void TestTakeDamage()
    {
        if (isServer)
        {
            TakeDamage(10);
        }
        else
        {
            DebugLog("서버에서만 데미지 테스트 가능");
        }
    }
}