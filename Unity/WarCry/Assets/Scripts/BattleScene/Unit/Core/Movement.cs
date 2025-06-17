using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Movement : NetworkBehaviour
{
    public NavMeshAgent agent;
    public float rotateSpeedMovement = 0.05f;
    private float rotateVelocity;

    public Animator anim;
    float motionSmoothTime = 0.1f;

    [Header("Enemy Targeting")]
    [SyncVar]
    public GameObject targetEnemy;
    public float stoppingDistance;
    private HighlightManager hmScript;

    void Start()
    {
        agent = gameObject.GetComponent<NavMeshAgent>();
        hmScript = GetComponent<HighlightManager>();
    }

    void Update()
    {
        Animation();
        
        if (isLocalPlayer)
        {
            Move();
        }
    }

    public void Animation()
    {
        float speed = agent.velocity.magnitude / agent.speed;
        anim.SetFloat("Blend", speed, motionSmoothTime, Time.deltaTime);
    }

    public void Move()
    {
        if(Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            
            if(Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, Mathf.Infinity))
            {
                if(hit.collider.CompareTag("Ground"))
                {
                    CmdMoveToPosition(hit.point);
                }
                else if(hit.collider.CompareTag("Enemy"))
                {
                    CmdMoveTowardsEnemy(hit.collider.gameObject);
                }
            }
        }

        if(targetEnemy != null)
        {
            if(Vector3.Distance(transform.position, targetEnemy.transform.position) > stoppingDistance)
            {
                CmdUpdateDestination(targetEnemy.transform.position);
            }
        }
    }

    [Command]
    public void CmdMoveToPosition(Vector3 position)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Sending command to move to {position}");
        RpcMoveToPosition(position);
    }

    [ClientRpc]
    public void RpcMoveToPosition(Vector3 position)
    {
        agent.SetDestination(position);
        agent.stoppingDistance = 0;
        
        Rotation(position);

        if(targetEnemy != null)
        {
            if(hmScript != null)
            {
                hmScript.DeselectHighlight();
            }
            targetEnemy = null;
        }
    }

    [Command]
    public void CmdMoveTowardsEnemy(GameObject enemy)
    {
        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Sending command to move toward {SceneNavigator.RetrievePath(enemy)}");
        targetEnemy = enemy;
        RpcMoveTowardsEnemy(enemy);
    }

    [ClientRpc]
    public void RpcMoveTowardsEnemy(GameObject enemy)
    {
        targetEnemy = enemy;
        agent.SetDestination(targetEnemy.transform.position);
        agent.stoppingDistance = stoppingDistance;
        
        Rotation(targetEnemy.transform.position);
        
        if(hmScript != null)
        {
            hmScript.SelectedHighlight();
        }
    }

    [Command]
    public void CmdUpdateDestination(Vector3 position)
    {
        RpcUpdateDestination(position);
    }

    [ClientRpc]
    public void RpcUpdateDestination(Vector3 position)
    {
        agent.SetDestination(position);
    }

    public void Rotation(Vector3 lookAtPosition)
    {
        Quaternion rotationToLookAt = Quaternion.LookRotation(lookAtPosition - transform.position);
        float rotationY = Mathf.SmoothDampAngle(transform.eulerAngles.y, rotationToLookAt.eulerAngles.y, 
        ref rotateVelocity, rotateSpeedMovement * (Time.deltaTime * 5));
        
        transform.eulerAngles = new Vector3(0, rotationY, 0);
    }
} 