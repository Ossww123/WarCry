using System;
using System.Linq;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineCamera))]
public class PlayerPerspectiveManager : MonoBehaviour
{
    private CinemachineCamera cinemachineCamera;
    private GameObject player;

    private void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Headless 서버 모드에서 비활성화됨");
            enabled = false;
            return;
        }
        // Cache the component reference
        cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    private void Update()
    {
        // 기존 코드
        if (cinemachineCamera.Target.TrackingTarget == null)
        {
            // 수정: 왕 유닛만 명확하게 찾도록 변경
            GameObject kingUnit = FindKingUnit();
            if (kingUnit != null)
            {
                player = kingUnit;
                Debug.Log($"[PlayerPerspectiveManager] 왕 유닛 찾음: {player.name}");
                SetPerspective();
            }
        }
        else
        {
            // 기존과 동일
            Destroy(this);
        }
    }

    // 새 메서드: 왕 유닛을 명확히 찾는 함수
    private GameObject FindKingUnit()
    {
        // 로컬 플레이어 확인
        if (NetworkClient.localPlayer == null)
            return null;

        uint localPlayerNetId = NetworkClient.localPlayer.GetComponent<NetworkIdentity>().netId;

        // 방법 1: KingController로 찾기
        KingController[] kingControllers = FindObjectsOfType<KingController>();
        foreach (var kingController in kingControllers)
        {
            Unit unit = kingController.GetComponent<Unit>();
            if (unit != null && unit.ownerNetId == localPlayerNetId)
            {
                Debug.Log($"[PlayerPerspectiveManager] 왕 유닛을 KingController로 찾음: {kingController.name}");
                return kingController.gameObject;
            }
        }

        // 방법 2: 이름으로 찾기
        Unit[] allUnits = FindObjectsOfType<Unit>();
        foreach (Unit unit in allUnits)
        {
            if (unit.name.Contains("_King") && unit.ownerNetId == localPlayerNetId)
            {
                Debug.Log($"[PlayerPerspectiveManager] 왕 유닛을 이름으로 찾음: {unit.name}");
                return unit.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// Configures the Cinemachine camera's target to follow the player's GameObject.
    /// If the player GameObject is found, the camera's tracking target is updated to the player's transform.
    /// If the player GameObject is not found, logs an error message for debugging purposes.
    /// </summary>
    /// <remarks>
    /// This method uses the Cinemachine camera to start tracking the player's transform.
    /// If the player GameObject is null, an error log is generated. This method is designed
    /// to be invoked during the initialization phase or when the player's GameObject becomes available.
    /// </remarks>
    /// <exception cref="NullReferenceException">
    /// Thrown if the Cinemachine camera component is missing or not properly initialized.
    /// </exception>
    private void SetPerspective()
    {
        if (player != null)
        {
            cinemachineCamera.Target.TrackingTarget = player.transform;
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] Local player king unit not found");
        }
    }
}