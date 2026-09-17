using UnityEngine;

public class GlobalReference : MonoBehaviour
{
    //ChangeToStatic
    public PlayerManager player;
    public LayerMask playerLayer;
    public LayerMask TerrainLayer;
    public LayerMask EnemyLayer;
    
    public static GlobalReference Instance { get; private set; }
    private void Awake()
    {
        // 2. Proper Singleton check
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy the entire duplicate GameObject, not just the script
            return; // CRITICAL: Stop executing the rest of Awake
        }
        
        Instance = this;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null; 
        }
    }
}
