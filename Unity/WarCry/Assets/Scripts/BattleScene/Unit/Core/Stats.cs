using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// This is a deprecated class, and you should ALWAYS use TheOneAndOnlyStats class.
/// </summary>
[Obsolete("This is a deprecated class. You should use TheOneAndOnlyStats class.")]
public class Stats : NetworkBehaviour
{
    [Header("Base Stats")]
    [FormerlySerializedAs("health")]
    [SyncVar(hook = nameof(OnMaxHealthChanged))] public float maxHealth;
    [FormerlySerializedAs("damage")]
    [SyncVar] public float attackDamage;
    [SyncVar] public float attackSpeed;

    public float damageLerpDuration;
    
    [SyncVar(hook = nameof(OnCurrentHealthChanged))]
    public float currentHealth;
    
    [SyncVar]
    private float targetHealth;
    
    private Coroutine damageCoroutine;

    public HealthDisplay _healthDisplay;
    private Boolean isPlayerKing;

    private void Awake() // ??????
    {
        currentHealth = maxHealth;
        targetHealth = maxHealth; // ????????
        UpdateHealthUI();
    }

    private void Start()
    {
        var kingFindResult = LocalPlayerLocator.TryFindPlayerKing(out var myKingUnit);
        if (kingFindResult && myKingUnit == gameObject)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] I am the king unit!");
            isPlayerKing = true;
        }
    }
    
    private void Update()
    {
        if (isLocalPlayer && Input.GetKeyDown(KeyCode.V))
        {
            CmdTakeDamage(gameObject, attackDamage);
        }
    }

    [Command]
    public void CmdTakeDamage(GameObject target, float damageAmount)
    {
        ApplyDamage(target, damageAmount);
    }

    public void ApplyDamage(GameObject target, float damageAmount)
    {
        if(!isServer)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] ApplyDamage should only be called on the server");
            return;
        }
        
        Stats targetStats = target.GetComponent<Stats>();
        if (targetStats != null)
        {
            float previousHealth = targetStats.targetHealth;
            
            targetStats.targetHealth -= damageAmount;
            
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Enemy health reduced from {previousHealth} to {targetStats.targetHealth}");

            if (targetStats.targetHealth <= 0)
            {
                targetStats.targetHealth = 0;
                RpcHandleDeath(target);
                if (target.CompareTag("Player"))
                {
                    targetStats.UpdatePlayerHealthWhenDead();
                }
            }
            else if (targetStats.damageCoroutine == null)
            {
                targetStats.StartLerpHealth();
            }
            else
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Damage coroutine already running, updating target health to {targetStats.targetHealth}");
            }
        }
        else if (target.CompareTag("Enemy"))
        {
            // Enemy 태그를 가진 객체지만 Stats 컴포넌트가 없는 경우
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] Enemy 태그 오브젝트 {target.name}에 Stats 컴포넌트가 없어 데미지를 적용할 수 없습니다.");
            
            // 나중에 Enemy 스크립트 추가 시 아래 코드 활성화 가능
            // Enemy enemyScript = target.GetComponent<Enemy>();
            // if (enemyScript != null)
            // {
            //    Debug.Log($"Applying damage to legacy Enemy: {(int)damageAmount}");
            //    enemyScript.TakeDamage((int)damageAmount);
            // }
            
            // 현재는 임시로 오브젝트 파괴 처리
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Enemy {target.name}에 데미지 {damageAmount} 적용됨 (테스트용 즉시 파괴)");
            RpcHandleDeath(target);
        }
    }

    [ClientRpc]
    private void RpcHandleDeath(GameObject target)
    {
        if (target != null)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Handling death of {target.name}");
            if (target.CompareTag("Player"))
            {
                target.SetActive(false);
            }
            else
            {
                Destroy(target, 0.5f); // 약간의 지연 추가
            }
        }
    }

    private void UpdatePlayerHealthWhenDead()
    {
        PlayerStatsDisplay.TriggerHealthChanged(maxHealth: maxHealth, currentHealth: 0);
    }

    private void StartLerpHealth()
    {
        if (damageCoroutine == null)
        {
            damageCoroutine = StartCoroutine(LerpHealth());
        }
    }

    private System.Collections.IEnumerator LerpHealth()
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

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Health lerp completed, final health: {currentHealth}");
        damageCoroutine = null;
    }

    private void UpdateHealthUI()
    {
        if (_healthDisplay != null)
        {
            _healthDisplay.Update3DSlider(currentHealth);
        }
        
        if (isPlayerKing)
        {
            PlayerStatsDisplay.TriggerHealthChanged(maxHealth: maxHealth, currentHealth: currentHealth);
        }
    }

    void OnMaxHealthChanged(float oldValue, float newValue)
    {
        maxHealth = newValue;
        UpdateHealthUI();
    }

    void OnCurrentHealthChanged(float oldValue, float newValue)
    {
        currentHealth = newValue;
        UpdateHealthUI();
    }
}