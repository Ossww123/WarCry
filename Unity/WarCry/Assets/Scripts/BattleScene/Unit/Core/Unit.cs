using System;
using UnityEngine;
using Mirror;

public class Unit : NetworkBehaviour
{
    [SyncVar] public uint ownerNetId;
    [SyncVar] public TeamIndex teamIndex;
    [SyncVar(hook = nameof(OnPaletteChanged))]
    public Palettes palette;

    [SerializeField] private Renderer unitRenderer;
    [SerializeField] private Material[] teamMaterials;

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 적 유닛인 경우 Enemy 태그 설정
        if (!IsOwnedByLocalPlayer())
        {
            gameObject.tag = "Enemy";
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {gameObject.name}에 Enemy 태그 설정됨");
        }

        ApplyTeamMaterial(); // 반드시 호출되어야 함
    }

    // 이 유닛의 주인 플레이어인지 확인
    public bool IsOwnedByLocalPlayer()
    {
        // 서버에서는 localPlayer 개념이 없으므로 다른 방식으로 확인
        if (isServer && !isClient)
        {
            // 서버 전용 로직: 서버에서는 항상 false 반환
            return false;
        }

        // 클라이언트 로직
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null)
        {
            if (Time.frameCount % 600 == 0)
            {
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] localPlayer가 아직 준비되지 않았습니다.");
            }
            return false;
        }

        bool result = ownerNetId == localPlayer.netId;
        // Debug.Log($"[Unit] ownerNetId: {ownerNetId}, localPlayer.netId: {NetworkClient.localPlayer?.netId}, 소유 여부: {result}");
        return result;
    }

    // 같은 팀인지 확인
    public bool IsSameTeam(Unit otherUnit)
    {
        return teamIndex == otherUnit.teamIndex;
    }

    // 아군인지 확인
    public bool IsFriendly()
    {
        var localPlayerInfo = NetworkClient.localPlayer?.GetComponent<PlayerInfo>();
        if (localPlayerInfo == null) return false;

        return teamIndex == localPlayerInfo.teamId; // 올바른 팀 비교

    }

    private void OnPaletteChanged(Palettes oldColor, Palettes newColor)
    {
        ApplyTeamMaterial();
    }

    private void ApplyTeamMaterial()
    {
        if (teamMaterials == null || teamMaterials.Length == 0)
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 머터리얼 배열이 비어 있습니다.");
            return;
        }
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.gameObject.CompareTag("NoReplaceTexture"))
            {
                continue;
            }
            r.material = teamMaterials[(Int32)palette];
        }

        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {renderers.Length}개 렌더러에 {palette} 머터리얼 적용됨");
    }


}