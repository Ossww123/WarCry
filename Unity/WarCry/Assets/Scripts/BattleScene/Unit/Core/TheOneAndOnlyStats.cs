using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// THE ONLY STATS CLASS YOU NEED
/// Replaces: Stats.cs, UnitStats.cs, BattleDamageReceiver.cs, DamageReceiver.cs
/// </summary>
public class TheOneAndOnlyStats : NetworkBehaviour
{
    [Header("Health")]
    [SyncVar(hook = nameof(OnMaxHealthChanged))] 
    [Tooltip("유닛의 최대 체력")]
    public float maxHealth = 100f;
    
    [SyncVar(hook = nameof(OnCurrentHealthChanged))] 
    [Tooltip("유닛의 현재 체력. 0 이하가 되면 사망")]
    public float currentHealth;

    /// <summary>
    /// Represents the amount of damage dealt to an enemy per attack.
    /// </summary>
    [Header("Combat Stats")]
    [Tooltip("유닛의 공격력. 대상의 Armor를 뺀 나머지만큼 대상 체력 감소")]
    [SyncVar] public float attackDamage = 10f;

    /// <summary>
    /// Defines the maximum distance within which an attack can hit a target.
    /// </summary>
    [SyncVar]
    [Tooltip("유닛의 사거리. 이 거리 내에 있어야 공격 가능")]
    public float attackRange = 2f;

    /// <summary>
    /// Defines the number of attacks that can be executed per second.
    /// </summary>
    [SyncVar]
    [Tooltip("유닛의 공격속도 (재사용 대기시간의 역수)")]
    public float attackSpeed = 1.5f; // attacks per second
    [Tooltip("유닛의 방어력. 들어오는 데미지에서 이 값을 뺀만큼만 체력 감소")]
    [SyncVar] public float armor = 0f;
    
    [Header("Movement & Detection\nDisengage > Tracking > Detention > Attack 순서대로 배치할 것")]
    [Tooltip("유닛의 이동속도")]
    [SyncVar] public float moveSpeed = 5f;
    [Tooltip("타겟이 벗어나면 추적을 중단할 범위")]
    [SyncVar] public float disengageRange = 6f;
    [Tooltip("자동 추적반응을 시작할 범위")]
    [SyncVar] public float trackingRange = 5f;
    [Tooltip("명령받았을 때 적을 찾을 탐지 범위")]
    [SyncVar] public float detectionRange = 3f;
    
    [Header("Unit Properties")]
    [SerializeField] private bool isCastle = false;
    [Tooltip("")]
    [SerializeField] private LayerMask enemyLayers = -1;
    
    [Header("Death & Animation")]
    [SerializeField] private float destroyDelay = 2f;
    [SerializeField] private string deathAnimParam = "Death";
    [SerializeField] private float damageLerpDuration = 1f;
    
    [Header("Player Resurrection")]
    [SerializeField] private bool canResurrect = false;  // 플레이어 킹 유닛은 true로 설정
    [SerializeField] private float resurrectionTime = 10f;  // 부활 시간 (초)
    [SerializeField] private Vector3 respawnOffset = new Vector3(0, 1, 0);  // 부활 위치 오프셋
    [SyncVar] private Vector3 initialPosition;  // 초기 위치 (서버와 클라이언트 간 동기화)
    [SyncVar] private Quaternion initialRotation;  // 초기 회전값 (서버와 클라이언트 간 동기화)
    
    [Header("State")]
    [SyncVar(hook = nameof(OnDeathStateChanged))] 
    public bool isDead = false;
    [SyncVar] public bool isResurrecting = false;  // 부활 중 상태
    
    // Components (auto-found)
    private Animator animator;
    private Unit unitInfo;
    private HealthDisplay healthDisplay;
    private UnityEngine.AI.NavMeshAgent agent;
    
    // Health lerping
    [SyncVar] private float targetHealth;
    private Coroutine damageCoroutine;
    private bool isPlayerKing;
    
    // Events
    public delegate void DeathEventHandler(GameObject victim);
    public static event DeathEventHandler OnDeath;
    
    // 개별 유닛 사망 이벤트 (사망한 플레이어의 마지막 공격 문제 해결용)
    public delegate void UnitDeathHandler();
    public event UnitDeathHandler OnUnitDeath;
    
    // 부활 이벤트
    public delegate void ResurrectionHandler();
    public event ResurrectionHandler OnResurrection;

    #region Unity & Network Lifecycle
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
        targetHealth = maxHealth;
        
        // 초기 위치와 회전 저장 (서버에서만 유효)
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버: 초기 위치 저장 = {initialPosition}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Initialize health if not set
        if (currentHealth == 0 && maxHealth > 0)
        {
            currentHealth = maxHealth;
            targetHealth = maxHealth;
        }
        
        // Setup enemy tag
        SetupEnemyTag();
        
        // Initialize health display
        InitializeHealthDisplay();
        
        // Update all UI
        UpdateHealthUI();
    }

    private void Awake()
    {
        // Auto-find all components
        animator = GetComponent<Animator>();
        unitInfo = GetComponent<Unit>();
        healthDisplay = GetComponent<HealthDisplay>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        
        // Apply movement speed to NavMeshAgent
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }
        
        // Castle validation
        if (isCastle && unitInfo == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Castle missing Unit component!");
        }
        
        // 여기서 초기화 코드 제거 (서버에서 처리)
    }

    private void Start()
    {
        // 킹 유닛 확인 및 부활 설정
        SetupResurrectionForKing();
    }

    // 플레이어 킹 유닛에 대한 부활 설정
    private void SetupResurrectionForKing()
    {
        // 킹 컨트롤러가 있는지 확인
        var kingController = GetComponent<KingController>();
        
        // 킹 유닛 확인
        if (kingController != null)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 킹 유닛 감지됨: {gameObject.name}");
            
            // Check if this is the player's king unit
            var kingResult = LocalPlayerLocator.TryFindPlayerKing(out var myKingUnit);
            if (kingResult && myKingUnit == gameObject)
            {
                isPlayerKing = true;
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] This is the player's king unit!");
            }
            
            // 네트워크 ID 확인
            var netId = GetComponent<Mirror.NetworkIdentity>();
            
            // 호스트+클라이언트 모드에서는 부활 설정
            bool isHostClient = isServer && netId != null && netId.isLocalPlayer;
            
            // 클라이언트 모드에서는 로컬 플레이어인 경우 부활 설정
            bool isClientMode = !isServer && netId != null && netId.isLocalPlayer;
            
            // 유닛이 로컬 플레이어 소유인지 확인
            bool isLocalUnit = unitInfo != null && unitInfo.IsOwnedByLocalPlayer();
            
            // 부활 설정 적용
            if (isServer)
            {
                canResurrect = true; // 서버에서는 모든 킹 유닛에 부활 가능 설정 (클라이언트들도 각자의 킹으로 판단)
                
                if (isHostClient)
                {
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 호스트+클라이언트 모드: 플레이어 킹 부활 활성화");
                }
                else
                {
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 모드: 모든 킹 유닛 부활 활성화 (클라이언트 포함)");
                }
            }
            else if (isClientMode)
            {
                // 클라이언트에서는 자신의 플레이어 킹을 확인만 함 (실제 부활은 서버에서 처리)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 모드: 로컬 플레이어 킹 감지됨 (서버에서 부활 처리 대기)");
            }
            
            // 디버그 로깅
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 부활 설정 상태: isServer={isServer}, isLocalPlayer={netId?.isLocalPlayer}, isLocalUnit={isLocalUnit}, canResurrect={canResurrect}");
        }
        
        // 부활 가능 여부 상태 로깅
        if (isServer)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 유닛 '{gameObject.name}'의 부활 가능 상태: {canResurrect}");
        }
    }

    #endregion

    #region The One True Damage System

    /// <summary>
    /// THE ONLY DAMAGE METHOD YOU EVER NEED
    /// Replaces all TakeDamage(), ApplyDamage(), etc.
    /// </summary>
    [Server]
    public void TakeDamage(float damage)
    {
        if (isDead) 
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name} is already dead, ignoring damage");
            return;
        }
        
        // 데미지 계산 - 방어력을 뺌 (armor 값은 이미 버프가 적용되어 있음)
        float actualDamage = Mathf.Max(0.1f, damage - armor);
        float oldHealth = targetHealth;
        float newHealth = Mathf.Max(0, currentHealth - actualDamage);
        currentHealth = newHealth;

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {damage} 데미지 → 방어력 {armor} → 실제 데미지 {actualDamage} → 체력 {oldHealth}에서 {currentHealth}로 감소");

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // REMOVED: Health lerping system - caused race conditions in multiplayer.
            // With only 3 days until deadline, opted for reliable direct health updates
            // rather than debugging complex lerp timing issues. Immediate UI feedback
            // is actually better UX anyway.
            // StartLerpHealth(); // TODO: Maybe reconsider post-launch if needed
        }
    }

    #endregion

    #region Death System

    [Server]
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        currentHealth = 0;
        targetHealth = 0;
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name} died");
        
        // 체력바 UI 비활성화 (추가)
        if (healthDisplay != null && healthDisplay.healthBar != null)
        {
            healthDisplay.healthBar.gameObject.SetActive(false);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (서버) {gameObject.name}의 체력바 UI 비활성화");
        }
        
        // 클라이언트에 죽음 상태 동기화 (애니메이션 등을 위해)
        // 초기 위치도 함께 전달
        Vector3 respawnPosition = initialPosition + respawnOffset;
        RpcDied(respawnPosition, initialRotation);
        
        // Fire death event
        OnDeath?.Invoke(gameObject);
        
        // Fire individual unit death event
        OnUnitDeath?.Invoke();
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] OnUnitDeath 이벤트 발생: {gameObject.name}");
        
        // Special logic for castles
        if (isCastle)
        {
            HandleCastleDestruction();
        }
        // 부활 가능한 플레이어 유닛인 경우
        else if (CanResurrect())
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 사망, {resurrectionTime}초 후 부활 예정");
            StartCoroutine(ResurrectAfterDelay());
        }
        // 일반 유닛은 지연 후 파괴
        else
        {
            StartCoroutine(DestroyAfterDelay());
        }
    }

    [Server]
    private void HandleCastleDestruction()
    {
        if (unitInfo == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Castle has no Unit component!");
            return;
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Castle destroyed! Team: {unitInfo.teamIndex}");

        // BattleSceneManager를 통해 승리 처리 (권장)
        BattleSceneManager battleManager = FindObjectOfType<BattleSceneManager>();
        if (battleManager != null)
        {
            // 승리 팀의 플레이어 찾기
            TeamIndex winningTeam = (unitInfo.teamIndex == TeamIndex.Left) ? TeamIndex.Right : TeamIndex.Left;
            string winnerName = "Unknown";

            // NetworkServer.connections를 사용하여 플레이어 검색
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn?.identity == null) continue;

                PlayerInfo playerInfo = conn.identity.GetComponent<PlayerInfo>();
                if (playerInfo != null && playerInfo.teamId == winningTeam)
                {
                    winnerName = playerInfo.playerName;
                    break;
                }
            }

            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버에서 직접 승리 선언: {winnerName}");
            battleManager.DeclareWinner(winnerName);
            return;
        }

        Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] BattleSceneManager를 찾을 수 없음!");
    }

    // 서버에서 직접 승리 처리용 메서드
    [Server]
    private void HandleVictory(string winnerName)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버에서 승리 처리: {winnerName}");

        // BattleSceneManager 찾기
        BattleSceneManager battleManager = FindObjectOfType<BattleSceneManager>();
        if (battleManager != null)
        {
            battleManager.DeclareWinner(winnerName);
        }
        else
        {
            // 폴백: GameNetworkManager를 통한 처리
            GameNetworkManager networkManager = NetworkManager.singleton as GameNetworkManager;
            if (networkManager != null)
            {
                // 모든 클라이언트에 승자 알림
                RpcShowWinner(winnerName);

                // 결과 씬으로 전환
                StartCoroutine(DelayedSceneTransition("ResultScene", 3f));
            }
        }
    }

    // 씬 전환 코루틴
    [Server]
    private IEnumerator DelayedSceneTransition(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        NetworkManager.singleton.ServerChangeScene(sceneName);
    }

    // 클라이언트에 승자 알림
    [ClientRpc]
    private void RpcShowWinner(string winnerName)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 승리 알림 수신: {winnerName}");

        // UI 찾아서 표시
        BattleUIManager uiManager = FindObjectOfType<BattleUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowWinner(winnerName);
        }
    }

    [Server]
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Destroying {gameObject.name} after delay");
        NetworkServer.Destroy(gameObject);
    }

    // 플레이어 부활 처리 코루틴
    [Server]
    private IEnumerator ResurrectAfterDelay()
    {
        isResurrecting = true;
        
        // 먼저 사망 애니메이션이 재생될 시간을 줌 (대략 2초)
        yield return new WaitForSeconds(2f);
        
        // 사망 즉시 원래 위치로 이동시킴 (부활 위치 미리 계산)
        Vector3 respawnPosition = initialPosition + respawnOffset;
        transform.position = respawnPosition;
        transform.rotation = initialRotation;
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name}을(를) 원래 위치로 즉시 이동: {respawnPosition}");
        
        // 이제 유닛을 비활성화 (렌더러, 콜라이더 등 비활성화)
        SetActiveState(false);
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name}의 부활 대기 중... ({resurrectionTime}초)");
        
        // 부활 시간만큼 대기 (사망 애니메이션 시간을 뺌)
        yield return new WaitForSeconds(resurrectionTime - 2f);
        
        // 부활 처리
        ResurrectPlayer();
    }
    
    // 플레이어 부활 메서드
    [Server]
    private void ResurrectPlayer()
    {
        // 플레이어 유닛이 바로 인식되도록 잠시 대기
        if (isPlayerKing)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 킹 유닛 부활 시작");
        }

        // 위치는 이미 ResurrectAfterDelay에서 설정했으므로 현재 위치 사용
        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        
        // 상태 초기화
        isDead = false;
        isResurrecting = false;
        currentHealth = maxHealth;
        targetHealth = maxHealth;
        
        // 체력바 UI 재활성화 (추가)
        if (healthDisplay != null && healthDisplay.healthBar != null)
        {
            healthDisplay.healthBar.gameObject.SetActive(true);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (서버) {gameObject.name}의 체력바 UI 재활성화");
        }
        
        // 유닛 활성화
        SetActiveState(true);
        
        // 모든 클라이언트에 부활 알림 - 위치 정보 포함
        RpcResurrected(currentPosition, currentRotation);
        
        // 부활 이벤트 발생
        OnResurrection?.Invoke();
        
        // 부활 이후 로컬 플레이어 킹 유닛이 즉시 인식되도록 추가 로그
        if (isPlayerKing)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 킹 부활 완료: {gameObject.name}, NetworkIdentity 상태 검증 필요");
            var netId = GetComponent<Mirror.NetworkIdentity>();
            if (netId != null)
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkIdentity 상태: isLocalPlayer={netId.isLocalPlayer}, isOwned={netId.isOwned}");
                
                // 플레이어 킹 캐시 업데이트
                if (netId.isLocalPlayer)
                {
                    LocalPlayerLocator.UpdatePlayerKingCache(gameObject);
                    Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 킹 캐시 업데이트됨: {gameObject.name}");
                }
            }
        }
    }
    
    // 유닛 활성화/비활성화 메서드
    [Server]
    private void SetActiveState(bool active)
    {
        // 렌더러 활성화/비활성화
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = active;
        }
        
        // 콜라이더 활성화/비활성화
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = active;
        }
        
        // NavMeshAgent 활성화/비활성화
        if (agent != null)
        {
            agent.enabled = active;
        }
        
        // Animator 활성화/비활성화 (추가)
        if (animator != null)
        {
            animator.enabled = active;
        }
        
        // Unit 컴포넌트 활성화/비활성화는 하지 않음 - 네트워크 식별이 유지되도록
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name}의 컴포넌트 {(active ? "활성화" : "비활성화")} 처리됨 (GameObject 자체는 활성 상태 유지)");
        
        // 네트워크를 통해 활성화 상태 동기화
        RpcSetActiveState(active);
    }
    
    // 클라이언트에 활성화 상태 동기화
    [ClientRpc]
    private void RpcSetActiveState(bool active)
    {
        if (isServer) return; // 서버에서는 이미 처리됨
        
        // 렌더러 활성화/비활성화
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = active;
        }
        
        // 콜라이더 활성화/비활성화
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = active;
        }
        
        // Animator 활성화/비활성화 (추가)
        if (animator != null)
        {
            animator.enabled = active;
        }
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (클라이언트) {gameObject.name}의 컴포넌트 {(active ? "활성화" : "비활성화")} 처리됨");
    }
    
    // 클라이언트에 부활 알림
    [ClientRpc]
    private void RpcResurrected(Vector3 respawnPosition, Quaternion respawnRotation)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name} 부활! 위치: {respawnPosition}");
        
        // 클라이언트에서 위치 직접 설정 (SyncVar로 동기화된 위치 무시)
        transform.position = respawnPosition;
        transform.rotation = respawnRotation;
        
        // 체력바 UI 다시 활성화 (추가)
        if (healthDisplay != null && healthDisplay.healthBar != null)
        {
            healthDisplay.healthBar.gameObject.SetActive(true);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name}의 체력바 UI 재활성화");
        }
        
        // 애니메이션 초기화
        if (animator != null)
        {
            // 모든 사망 관련 파라미터 초기화
            foreach (var param in animator.parameters)
            {
                // Bool 타입 파라미터 초기화
                if (param.type == AnimatorControllerParameterType.Bool)
                {
                    if (param.name == deathAnimParam || param.name.Contains("Death") || param.name.Contains("Dead") || param.name.Contains("Die"))
                    {
                        animator.SetBool(param.name, false);
                        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 부활: Bool 파라미터 초기화 - {param.name}=false");
                    }
                }
                
                // Trigger 타입 파라미터 리셋
                if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    if (param.name.Contains("Revive") || param.name.Contains("Resurrect"))
                    {
                        animator.SetTrigger(param.name);
                        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 부활: Trigger 활성화 - {param.name}");
                    }
                }
            }
            
            // 이동 애니메이션 리셋
            animator.SetFloat("Blend", 0);
            
            // Animator 컨트롤러 리셋 (강제 초기화)
            animator.Rebind();
            animator.Update(0f);
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 부활: 애니메이터 컨트롤러 완전 리셋됨");
        }
        
        // 체력 UI 업데이트
        UpdateHealthUI();
        
        // 부활 상태 로컬 설정
        isDead = false;
        isResurrecting = false;
        
        // 클라이언트 측에서도 부활 이벤트 직접 발생
        OnResurrection?.Invoke();
        
        // NavMeshAgent 재활성화
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }
        
        // 클라이언트 측에서도 플레이어 킹 캐시 업데이트
        if (isPlayerKing)
        {
            var netId = GetComponent<Mirror.NetworkIdentity>();
            if (netId != null && netId.isLocalPlayer)
            {
                LocalPlayerLocator.UpdatePlayerKingCache(gameObject);
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (클라이언트) 플레이어 킹 캐시 업데이트됨: {gameObject.name}");
            }
        }
    }

    // 클라이언트에 사망 상태 동기화
    [ClientRpc]
    private void RpcDied(Vector3 respawnPosition, Quaternion respawnRotation)
    {
        if (isServer) return; // 서버에서는 이미 처리됨
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (클라이언트) {gameObject.name} 사망 상태 동기화");
        
        // 클라이언트에서 사망 애니메이션 재생 (중요: 렌더러는 비활성화하지 않음)
        PlayDeathAnimation();
        
        // NavMeshAgent 멈춤
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        
        // 체력바 UI 비활성화 (추가)
        if (healthDisplay != null && healthDisplay.healthBar != null)
        {
            healthDisplay.healthBar.gameObject.SetActive(false);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (클라이언트) {gameObject.name}의 체력바 UI 비활성화");
        }
        
        // 사망 이벤트 발생 - 클라이언트에서도 처리
        OnUnitDeath?.Invoke();
        
        // 클라이언트에서도 부활 대기 코루틴 시작
        if (isLocalPlayer || (unitInfo != null && unitInfo.IsOwnedByLocalPlayer()))
        {
            StartCoroutine(ClientResurrectAfterDelay(respawnPosition, respawnRotation));
        }
    }

    // 클라이언트 측 부활 대기 코루틴
    private IEnumerator ClientResurrectAfterDelay(Vector3 respawnPosition, Quaternion respawnRotation)
    {
        // 사망 애니메이션이 재생될 시간을 줌 (대략 2초)
        yield return new WaitForSeconds(2f);
        
        // 원래 위치로 이동
        transform.position = respawnPosition;
        transform.rotation = respawnRotation;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] (클라이언트) {gameObject.name}을(를) 원래 위치로 이동: {respawnPosition}");
    }

    #endregion

    #region Health Animation & UI

    private void InitializeHealthDisplay()
    {
        if (healthDisplay != null)
        {
            healthDisplay.Start3DSlider(maxHealth);
            healthDisplay.Update3DSlider(currentHealth);
        }
    }

    private void StartLerpHealth()
    {
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
        }
        damageCoroutine = StartCoroutine(LerpHealth());
    }

    private IEnumerator LerpHealth()
    {
        float elapsedTime = 0;
        float initialHealth = currentHealth;
        float target = targetHealth;

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Starting health lerp from {initialHealth} to {target}");

        while (elapsedTime < damageLerpDuration)
        {
            currentHealth = Mathf.Lerp(initialHealth, target, elapsedTime / damageLerpDuration);
            UpdateHealthUI();
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        currentHealth = target;
        UpdateHealthUI();
        damageCoroutine = null;
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Health lerp completed. Final health: {currentHealth}");
    }

    private void UpdateHealthUI()
    {
        // Update 3D health bar
        if (healthDisplay != null)
        {
            healthDisplay.Update3DSlider(currentHealth);
        }
        
        // Update player UI if this is the king
        if (isPlayerKing)
        {
            Debug.Log($"[{DebugUtils.CombineObjectPathAndCallerMethod(gameObject)}] Triggering health update: {currentHealth}/{maxHealth}");  // Add this debug
            PlayerStatsDisplay.TriggerHealthChanged(maxHealth, currentHealth);
        }
    }

    #endregion

    #region Utility Methods

    private void SetupEnemyTag()
    {
        if (unitInfo != null && !unitInfo.IsOwnedByLocalPlayer())
        {
            gameObject.tag = "Enemy";
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Set Enemy tag on {gameObject.name}");
        }
    }

    // Get attack cooldown in seconds
    public float GetAttackCooldown()
    {
        return attackSpeed > 0 ? 1f / attackSpeed : 1f;
    }

    #endregion

    #region SyncVar Hooks

    void OnMaxHealthChanged(float oldValue, float newValue)
    {
        maxHealth = newValue;
        
        // Update NavMeshAgent speed if it exists
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }
        
        UpdateHealthUI();
    }

    void OnCurrentHealthChanged(float oldValue, float newValue)
    {
        currentHealth = newValue;
        UpdateHealthUI();
        
        // Play damage effect if health decreased
        if (newValue < oldValue && !isDead)
        {
            PlayDamageEffect();
        }
    }

    void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        if (newValue && !oldValue)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Death state changed for {gameObject.name}");
            
            // Play death animation
            PlayDeathAnimation();
            
            // Disable colliders
            DisableColliders();
            
            // Fire death event
            OnDeath?.Invoke(gameObject);
        }
    }

    #endregion

    #region Animation & Effects

    private void PlayDamageEffect()
    {
        if (animator != null)
        {
            // Try to play hit animation
            foreach (var param in animator.parameters)
            {
                if (param.name == "Hit" && param.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger("Hit");
                    break;
                }
            }
        }
    }

    private void PlayDeathAnimation()
    {
        if (animator != null)
        {
            // Handle both trigger and bool death parameters
            foreach (var param in animator.parameters)
            {
                if (param.name == deathAnimParam)
                {
                    if (param.type == AnimatorControllerParameterType.Trigger)
                    {
                        animator.SetTrigger(deathAnimParam);
                        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Triggered death animation: {deathAnimParam}");
                    }
                    else if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool(deathAnimParam, true);
                        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Set death bool: {deathAnimParam}");
                    }
                    break;
                }
            }
        }
    }

    private void DisableColliders()
    {
        // Disable all colliders
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }
        
        // Stop physics simulation
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) 
        {
            rb.isKinematic = true;
        }
        
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Disabled {colliders.Length} colliders and physics for {gameObject.name}");
    }

    #endregion

    #region Public Getters

    public float GetHealthPercent() => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead() => isDead;
    public int GetCurrentHealthInt() => Mathf.RoundToInt(currentHealth);
    public int GetMaxHealthInt() => Mathf.RoundToInt(maxHealth);

    #endregion

    #region Debug Methods

    [ContextMenu("Test Take 10 Damage")]
    private void TestTakeDamage()
    {
        if (isServer)
        {
            TakeDamage(10f);
        }
        else
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Can only test damage on server!");
        }
    }

    [ContextMenu("Kill Unit")]
    private void TestKill()
    {
        if (isServer)
        {
            TakeDamage(maxHealth + 1000);
        }
        else
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Can only kill unit on server!");
        }
    }

    #endregion

    #region Helper Methods

    // 부활 가능한지 확인하는 메서드
    private bool CanResurrect()
    {
        // 기본 조건: 부활 플래그가 켜져 있어야 함
        if (!canResurrect) return false;
        
        // 유닛 정보가 없으면 부활 불가
        if (unitInfo == null) return false;
        
        // 부활 가능 조건 체크:
        // 1) 킹 유닛이거나
        var kingController = GetComponent<KingController>();
        bool isKing = kingController != null;
        
        // 2) NetworkIdentity가 플레이어 소유이거나
        var netId = GetComponent<Mirror.NetworkIdentity>();
        bool isNetworkPlayer = netId != null && (netId.isLocalPlayer || netId.isOwned);
        
        // 3) 플레이어 킹으로 설정되어 있거나
        bool isPKing = isPlayerKing;
        
        // 4) Unit.IsOwnedByLocalPlayer()가 호스트-클라이언트 모드에서 true를 반환하거나
        bool isLocalPlayerUnit = unitInfo.IsOwnedByLocalPlayer();
        
        // 로그로 모든 상태 출력
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 부활 조건 체크: isKing={isKing}, isNetworkPlayer={isNetworkPlayer}, isPKing={isPKing}, isLocalPlayerUnit={isLocalPlayerUnit}");
        
        // 위 조건 중 하나라도 만족하면 부활 가능
        return isKing || isNetworkPlayer || isPKing || isLocalPlayerUnit;
    }

    #endregion
}