using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Provides utility methods to locate the local player's game objects and information
/// within a multiplayer environment. This class is designed to address issues related
/// to retrieving the local player's king unit and corresponding username due to limitations
/// in network synchronization or other environmental factors.
/// </summary>
public static class LocalPlayerLocator
{
    // 플레이어 킹 유닛 캐싱을 위한 정적 변수
    private static GameObject cachedPlayerKingUnit = null;
    private static bool hasInitializedKing = false;
    
    /// <summary>
    /// 강제로 플레이어 킹 유닛 참조를 초기화합니다.
    /// 플레이어 킹 캐시를 리셋해야 할 때 사용합니다.
    /// </summary>
    public static void ResetPlayerKingCache()
    {
        cachedPlayerKingUnit = null;
        hasInitializedKing = false;
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 킹 캐시가 초기화되었습니다.");
    }
    
    /// <summary>
    /// 사망 후 부활한 플레이어 킹을 다시 캐싱합니다.
    /// </summary>
    /// <param name="playerKing">새로 부활한 플레이어 킹 참조</param>
    public static void UpdatePlayerKingCache(GameObject playerKing)
    {
        if (playerKing != null)
        {
            cachedPlayerKingUnit = playerKing;
            hasInitializedKing = true;
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 플레이어 킹 캐시가 업데이트되었습니다: {playerKing.name}");
        }
    }

    /// <summary>
    /// Attempts to find the player's king object by looking at the Unit component
    /// of the local player.
    /// If that fails, it returns false.
    /// </summary>
    /// <param name="playerKingUnit">
    /// The output parameter that will hold the player's king object
    /// if found. If player's king object was not found, null will be assigned to the slot.
    /// </param>
    /// <remarks>
    /// This method is changed because getting from network has been screwed
    /// and doesn't get the proper player's king object.
    /// Also, it seems that I can't get the username by searching for the player king's unit object,
    /// so it is handled separately.
    /// </remarks>
    /// <returns>
    /// Returns true if the local player's king unit was successfully found;
    /// otherwise, returns false.
    /// </returns>
    public static Boolean TryFindPlayerKing(out GameObject playerKingUnit)
    {
        // 캐싱된 플레이어 킹이 있고 여전히 유효하면 그대로 사용
        if (hasInitializedKing && cachedPlayerKingUnit != null)
        {
            // 추가 검증: 캐싱된 킹이 아직 활성 상태인지 확인
            TheOneAndOnlyStats stats = cachedPlayerKingUnit.GetComponent<TheOneAndOnlyStats>();
            
            // 사망 중이거나 부활 중인 경우에도 캐시된 참조 유지
            if (stats != null && (stats.isDead || stats.isResurrecting))
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐싱된 플레이어 킹 사용 (사망/부활 중): {cachedPlayerKingUnit.name}");
                playerKingUnit = cachedPlayerKingUnit;
                return true;
            }
            
            // 정상 활성 상태인 경우
            if (cachedPlayerKingUnit.activeInHierarchy)
            {
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 캐싱된 플레이어 킹 사용: {cachedPlayerKingUnit.name}");
                playerKingUnit = cachedPlayerKingUnit;
                return true;
            }
            else
            {
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 캐싱된 플레이어 킹이 비활성화됨. 캐시 초기화 및 재검색");
                cachedPlayerKingUnit = null;
                hasInitializedKing = false;
            }
        }

        // 1단계: Unit 컴포넌트를 통해 로컬 플레이어의 유닛 찾기
        playerKingUnit = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None)
            .Select(x =>
            {
                // 로컬 플레이어의 유닛인지 확인하고, KingController 컴포넌트가 있는지 확인
                if (x.IsOwnedByLocalPlayer() && x.GetComponent<KingController>() != null)
                {
                    return x.gameObject;
                }
                return null;
            })
            .FirstOrDefault(x => x != null);

        if (playerKingUnit != null)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어의 킹 유닛을 찾았습니다: {playerKingUnit.name}");
            // 찾은 플레이어 킹 캐싱
            cachedPlayerKingUnit = playerKingUnit;
            hasInitializedKing = true;
            return true;
        }

        // 2단계: 이전 검색으로 찾지 못했다면, KingController를 통해 직접 검색
        playerKingUnit = GameObject.FindObjectsByType<KingController>(FindObjectsSortMode.None)
            .Select(x => 
            {
                var unit = x.GetComponent<Unit>();
                if (unit != null && unit.IsOwnedByLocalPlayer())
                {
                    return x.gameObject;
                }
                return null;
            })
            .FirstOrDefault(x => x != null);

        if (playerKingUnit != null)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] KingController를 통해 로컬 플레이어의 킹을 찾았습니다: {playerKingUnit.name}");
            // 찾은 플레이어 킹 캐싱
            cachedPlayerKingUnit = playerKingUnit;
            hasInitializedKing = true;
            return true;
        }
        
        // 3단계: 모든 방법이 실패하면 마지막으로 NetworkIdentity.isLocalPlayer로 직접 킹 찾기 시도
        playerKingUnit = GameObject.FindObjectsByType<KingController>(FindObjectsSortMode.None)
            .Select(x => 
            {
                var netIdentity = x.GetComponent<Mirror.NetworkIdentity>();
                if (netIdentity != null && netIdentity.isLocalPlayer)
                {
                    return x.gameObject;
                }
                return null;
            })
            .FirstOrDefault(x => x != null);
            
        if (playerKingUnit != null)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] NetworkIdentity를 통해 로컬 플레이어의 킹을 찾았습니다: {playerKingUnit.name}");
            // 찾은 플레이어 킹 캐싱
            cachedPlayerKingUnit = playerKingUnit;
            hasInitializedKing = true;
            return true;
        }

        // 모든 검색 방법이 실패한 경우 로그
        if (Time.frameCount % 60 == 0) // 매 프레임마다 로그가 아니라 60프레임마다 로그
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 로컬 플레이어의 킹 유닛을 찾지 못했습니다.");
        }
        return false;
    }

    /// <summary>
    /// Attempts to find a player's username based on the given player king unit object.
    /// This method checks the team index of the provided player king unit against all available player information
    /// in the scene to identify the matching player's username.
    /// </summary>
    /// <param name="playerKingUnit">
    /// The player king unit object used to determine the team of the player whose username is being searched.
    /// </param>
    /// <param name="username">
    /// The output parameter that will hold the found player's username, if successful. If not found, an empty string is assigned.
    /// </param>
    /// <returns>
    /// Returns true if a matching player's username is successfully found; otherwise, returns false.
    /// If the provided player king unit is null, the method returns false and logs an error.
    /// </returns>
    public static Boolean TryFindForUsername(GameObject playerKingUnit, out String username)
    {
        if (playerKingUnit == null)
        {
            Debug.LogError(
                $"[{DebugUtils.ResolveCallerMethod()}] Player is null. You can't get name from null player.");
            username = "";
            return false;
        }

        Unit myKingUnit = playerKingUnit.GetComponent<Unit>();
        var playerInfos = GameObject.FindObjectsByType<PlayerInfo>(FindObjectsSortMode.None);

        foreach (var playerInfo in playerInfos)
        {
            if (playerInfo.teamId == myKingUnit.teamIndex)
            {
                Debug.Log(
                    $"[{DebugUtils.ResolveCallerMethod()}] Found player name: {playerInfo.playerName}.");
                username = playerInfo.playerName;
                return true;
            }
        }

        Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Could not find player name.");
        username = "";
        return false;
    }
}