using UnityEngine;
using Unity.Cinemachine;
using Mirror;

public class CameraManager : MonoBehaviour
{
    public CinemachineCamera kingTrackingCamera; // 왕 추적용 시네머신 카메라
    public Camera mainCamera;

    [Header("Team-based Camera Settings")]
    [SerializeField] private float leftTeamInitialXPosition = -80f; // 왼쪽 팀 초기 X 위치
    [SerializeField] private float rightTeamInitialXPosition = 70f; // 오른쪽 팀 초기 X 위치
    [SerializeField] private float cameraHeight = 30f; // 탑뷰 높이
    [SerializeField] private float cameraTiltAngle = 80f; // 기울어진 각도

    [Header("Movement Constraints")]
    [SerializeField] private float xMovementRange = 10f; // X축 이동 범위 (팀 위치 기준 ±)
    [SerializeField] private float minZPosition = -80f; // 최소 Z 위치
    [SerializeField] private float maxZPosition = 50f; // 최대 Z 위치
    [SerializeField] private float moveSpeed = 50f; // 카메라 이동 속도

    [Header("Input Settings")]
    [SerializeField] private KeyCode moveUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode moveDownKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private bool useMouseEdgeMovement = true;
    [SerializeField] private float edgeScrollThreshold = 20f;

    private bool usingCinemachineCamera = true;
    private bool isLeftTeam = true; // 기본값, 이후 팀 정보로 설정됨
    private float baseXPosition; // 팀 기준 X 위치
    private float minXPosition; // 최소 X 위치 (계산됨)
    private float maxXPosition; // 최대 X 위치 (계산됨)

    void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Headless 서버 모드에서 비활성화됨");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // 플레이어 팀 정보 가져오기
        StartCoroutine(FindPlayerTeam());

        // 기본 설정 - 배치 단계에서 시작
        SetPlacementCamera();

        // BattleSceneManager 이벤트 구독
        BattleSceneManager.OnPhaseChanged += OnBattlePhaseChanged;
    }

    private System.Collections.IEnumerator FindPlayerTeam()
    {
        // 로컬 플레이어가 준비될 때까지 대기
        yield return new WaitUntil(() => NetworkClient.localPlayer != null);

        // 플레이어 팀 정보 가져오기
        PlayerInfo playerInfo = NetworkClient.localPlayer.GetComponent<PlayerInfo>();
        if (playerInfo != null)
        {
            isLeftTeam = playerInfo.teamId == TeamIndex.Left;
            Debug.Log($"[CameraManager] Player is on {(isLeftTeam ? "Left" : "Right")} team");

            // 팀에 따라 기준 X 위치 설정
            baseXPosition = isLeftTeam ? leftTeamInitialXPosition : rightTeamInitialXPosition;

            // 이동 제한 범위 계산
            minXPosition = baseXPosition - xMovementRange;
            maxXPosition = baseXPosition + xMovementRange;

            Debug.Log($"[CameraManager] Camera X range: {minXPosition} to {maxXPosition}");

            // 카메라 위치 업데이트
            UpdatePlacementCameraPosition();
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        BattleSceneManager.OnPhaseChanged -= OnBattlePhaseChanged;
    }

    private void OnBattlePhaseChanged(BattleSceneManager.BattlePhase phase)
    {
        switch (phase)
        {
            case BattleSceneManager.BattlePhase.UnitPlacement:
                SetPlacementCamera();
                break;

            case BattleSceneManager.BattlePhase.BattleInProgress:
                SetBattleCamera();
                break;
        }
    }

    public void SetPlacementCamera()
    {
        // 시네머신 카메라 비활성화
        usingCinemachineCamera = false;
        kingTrackingCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);

        // 카메라 위치 및 회전 설정
        UpdatePlacementCameraPosition();

        Debug.Log("[CameraManager] Switched to Placement Camera");
    }

    private void UpdatePlacementCameraPosition()
    {
        // 현재 위치 가져오기
        Vector3 currentPos = mainCamera.transform.position;

        // 초기 설정 시에는 팀 기준 X 위치 사용
        if (Mathf.Approximately(currentPos.x, 0))
        {
            currentPos.x = baseXPosition;
        }

        // 새 위치 계산 (제한 내에서)
        Vector3 newPosition = new Vector3(
            Mathf.Clamp(currentPos.x, minXPosition, maxXPosition),
            cameraHeight,
            Mathf.Clamp(currentPos.z, minZPosition, maxZPosition)
        );

        // 카메라 위치 및 회전 설정
        mainCamera.transform.position = newPosition;
        mainCamera.transform.rotation = Quaternion.Euler(cameraTiltAngle, 0, 0);
    }

    public void SetBattleCamera()
    {
        // 시네머신 카메라 활성화 (왕 추적)
        usingCinemachineCamera = true;
        kingTrackingCamera.gameObject.SetActive(true);

        Debug.Log("[CameraManager] Switched to Battle Camera (King tracking)");
    }

    void Update()
    {
        // 배치 단계에서만 카메라 제어
        if (!usingCinemachineCamera)
        {
            MoveCamera();
        }
    }

    private void MoveCamera()
    {
        Vector3 moveDirection = Vector3.zero;

        // 키보드 입력으로 위/아래 이동
        if (Input.GetKey(moveUpKey))
        {
            moveDirection += Vector3.forward;
        }
        if (Input.GetKey(moveDownKey))
        {
            moveDirection += Vector3.back;
        }

        // 키보드 입력으로 좌/우 이동
        if (Input.GetKey(moveLeftKey))
        {
            moveDirection += Vector3.left;
        }
        if (Input.GetKey(moveRightKey))
        {
            moveDirection += Vector3.right;
        }

        // 마우스 가장자리 이동
        if (useMouseEdgeMovement)
        {
            float mouseY = Input.mousePosition.y;
            float mouseX = Input.mousePosition.x;

            // 위아래 이동
            if (mouseY < edgeScrollThreshold)
            {
                moveDirection += Vector3.back;
            }
            else if (mouseY > Screen.height - edgeScrollThreshold)
            {
                moveDirection += Vector3.forward;
            }

            // 좌우 이동
            if (mouseX < edgeScrollThreshold)
            {
                moveDirection += Vector3.left;
            }
            else if (mouseX > Screen.width - edgeScrollThreshold)
            {
                moveDirection += Vector3.right;
            }
        }

        // 이동이 있을 경우 카메라 위치 업데이트
        if (moveDirection != Vector3.zero)
        {
            // 현재 위치
            Vector3 currentPos = mainCamera.transform.position;

            // 새 위치 계산 (x와 z 모두 변경)
            Vector3 newPosition = currentPos + moveDirection * moveSpeed * Time.deltaTime;

            // x, z값 제한
            newPosition.x = Mathf.Clamp(newPosition.x, minXPosition, maxXPosition);
            newPosition.z = Mathf.Clamp(newPosition.z, minZPosition, maxZPosition);

            // 높이 유지
            newPosition.y = cameraHeight;

            // 카메라 위치 설정
            mainCamera.transform.position = newPosition;
        }
    }
}