// Assets/Scripts/Army/Common/EnemyDetector.cs
using UnityEngine;

public class EnemyDetector : MonoBehaviour
{
    [Header("Stats (TheOneAndOnlyStats에 trackingRange, attackRange 포함)")]
    public TheOneAndOnlyStats stats;

    /// <summary>
    /// Tracking Range 안에서 가장 가까운 Enemy 태그를 가진 Transform 반환.
    /// 없다면 null.
    /// </summary>
    public Transform GetNearestEnemy()
    {
        // LayerMask는 enemyLayers 변수가 없어졌으므로 모든 레이어를 통해 Enemy 태그로 찾음
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            stats.trackingRange,
            -1  // 모든 레이어 검색
        );

        float minDist = Mathf.Infinity;
        Transform nearest = null;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = hit.transform;
                }
            }
        }

        return nearest;
    }

    // 씬 뷰에서 추적/공격 범위 시각화
    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.trackingRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}