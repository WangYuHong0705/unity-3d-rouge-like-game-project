using UnityEngine;

public class TeleportTarget : MonoBehaviour
{
    public SceneController controller;
    public int targetLevelIndex = 0;
    public float playerTriggerRadius = 2f;

    // Map node link (optional)
    public int nodeRow = -1;
    public int nodeIndex = -1;
    
    // Stage transition flag
    [Tooltip("Marks this portal as a special stage transition portal (e.g., 1-5 to 2-0)")]
    public bool isStageTransition = false;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isStageTransition ? Color.magenta : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, playerTriggerRadius);
    }
}
