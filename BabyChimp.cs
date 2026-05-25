using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class BabyChimpAi : MonoBehaviour
{
    public bool allowOfflineMovement = true;
    [SerializeField] private NavMeshAgent agent;

    public bool navAiPoints = false;
    public Transform[] points;
    public float wanderRange = 15f;

    public string tagString = "Player";

    public NavMeshAgent NavAgentToCallWhenInRange;

    public float detectionRange = 10f;
    public float wanderSpeed = 5f;
    public float fleeSpeed = 7.5f;

    public Transform teleportLocation;
    public float timeToTeleport = 1.0f;

    public string antiNavTag = "NoNoNav";

    private float stuckTimer = 0f;
    private Vector3 lastPosition;
    private bool isTeleporting = false;

    private bool isPlayerDetected = false;
    private bool isFleeing = false;
    private float detectionTimer = 0f;
    public float fleeDelay = 9f;



    public RandomSoundPlayer_Triggered soundPlayer;

    private void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        agent.speed = wanderSpeed;
        lastPosition = transform.position;
        Wander();
    }

    private void Update()
    {
        bool isOwner = (!PhotonNetwork.IsConnected && allowOfflineMovement) ||
                       (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient);

        if (isOwner && !isTeleporting)
        {
            agent.enabled = true;
            AvoidAntiNavTags();
            HandleStuckCheck();

            GameObject[] players = GameObject.FindGameObjectsWithTag(tagString);
            GameObject closestPlayer = null;
            float minDistance = float.MaxValue;

            foreach (GameObject p in players)
            {
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist < detectionRange && dist < minDistance)
                {
                    minDistance = dist;
                    closestPlayer = p;
                }
            }

            if (closestPlayer != null)
            {
                if (!isPlayerDetected)
                {
                    isPlayerDetected = true;
                    detectionTimer = 0f;
                    if (soundPlayer != null) soundPlayer.PlayOneRandom();
                }

                detectionTimer += Time.deltaTime;

                if (detectionTimer >= fleeDelay)
                {
                    if (!isFleeing)
                    {
                        isFleeing = true;
                        if (NavAgentToCallWhenInRange != null)
                            NavAgentToCallWhenInRange.SetDestination(transform.position);
                    }

                    agent.speed = fleeSpeed;

                    Vector3 fleeDirection = (transform.position - closestPlayer.transform.position).normalized;
                    Vector3 fleeTarget = transform.position + fleeDirection * detectionRange;

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(fleeTarget, out hit, detectionRange, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                }
            }
            else
            {
                isPlayerDetected = false;
                isFleeing = false;
                detectionTimer = 0f;


                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    agent.speed = wanderSpeed;
                    Wander();
                }
            }
        }
        else if (!isOwner)
        {
            agent.enabled = false;
        }
    }

    private void Wander()
    {
        if (navAiPoints && points != null && points.Length > 0)
        {
            int num = Random.Range(0, points.Length);
            if (points[num] != null) agent.destination = points[num].position;
        }
        else
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRange + transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRange, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void HandleStuckCheck()
    {
        if (agent.enabled && agent.hasPath && Vector3.Distance(transform.position, lastPosition) < 0.05f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= timeToTeleport)
            {
                if (teleportLocation != null) StartCoroutine(TeleportSequence());
                stuckTimer = 0;
            }
        }
        else
        {
            stuckTimer = 0;
        }
        lastPosition = transform.position;
    }

    private IEnumerator TeleportSequence()
    {
        isTeleporting = true;
        agent.enabled = false;

        transform.position = teleportLocation.position;
        transform.rotation = teleportLocation.rotation;

        yield return new WaitForSeconds(0.1f);

        agent.enabled = true;
        agent.Warp(teleportLocation.position);
        isTeleporting = false;
    }

    private void AvoidAntiNavTags()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 3f))
        {
            if (hit.collider.CompareTag(antiNavTag))
            {
                Vector3 avoidanceDir = transform.position - hit.point;
                if (NavMesh.SamplePosition(transform.position + avoidanceDir.normalized * 5f, out var navHit, 5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(navHit.position);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (!navAiPoints)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, wanderRange);
        }
    }
}
