using System;
using System.Collections.Generic;
using Script.Enemy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using VInspector;

public class CheckpointManager : MonoBehaviour
{
    public TextMeshProUGUI ObjectiveText;
    public static CheckpointManager Instance;

    public Transform StartPoint;
    public StageData[] AllFloor;
    
    [SerializeField] private SlidingDoors finalDoors;

    [Button]
    public void debugNextFloor()
    {
        GameValue.CurrentFloor++;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void Awake()
    {
        // Standard Singleton setup
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        foreach (StageData stage in AllFloor) //Get all enemy setUp
        {
            List<BaseEnemy> childList = new List<BaseEnemy>();
            foreach (Transform child in stage.EnemyParentGroup)
            {
                childList.Add(child.gameObject.GetComponent<BaseEnemy>());
            }
            stage.EnemyList = childList.ToArray();
        }
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.P)) debugNextFloor();
        CheckEnemy();
    }

    private void Start()
    {
        //Respawn
        SpawnTower();
    }

    public void NextFloor()
    {
        if (GameValue.CurrentFloor == AllFloor.Length)
        {
            Debug.Log("Game clear");
            finalDoors.OpenDoor();
            this.enabled = false;
            return;
        }
        
        AllFloor[GameValue.CurrentFloor - 1].SlidingDoors.OpenDoor();
        AllFloor[GameValue.CurrentFloor].StageGameObject.SetActive(true);

        GameValue.CurrentFloor++;
    }

    private void SpawnTower()
    {
        for (int i = 0; i < AllFloor.Length; i++)
        {
            StageData thisFloor = AllFloor[i];
            if (i + 1 < GameValue.CurrentFloor)
            {
                thisFloor.StageGameObject.SetActive(true);
                foreach (var enemy in thisFloor.EnemyList)
                {
                    enemy.gameObject.SetActive(false);
                }
                thisFloor.SlidingDoors.OpenDoor(true);
            }
            else if (i + 1 == GameValue.CurrentFloor)
            {
                thisFloor.StageGameObject.SetActive(true);
                SetPlayerPosAndRot(thisFloor.RespawnPoint);
            }
            else
            {
                thisFloor.StageGameObject.SetActive(false);
            }
        }
    }

    private void SetPlayerPosAndRot(Transform spawnTransform)
    {
        GlobalReference.Instance.player.transform.position = spawnTransform.position;
        GlobalReference.Instance.player.transform.rotation = spawnTransform.rotation;
    }
    
    private void CheckEnemy()
    {
        int activeCount = 0; 

        foreach (BaseEnemy enemy in AllFloor[GameValue.CurrentFloor - 1].EnemyList)
        {
            if (enemy.gameObject.activeInHierarchy)
            {
                activeCount++;
            }
        }

        int enemyAllCount = AllFloor[GameValue.CurrentFloor - 1].EnemyList.Length;
        int activeLeft = enemyAllCount - activeCount;
        ObjectiveText.text = "Destroy all security drone (" + activeLeft + "/" +  enemyAllCount + ")";
        
        if (activeCount == 0)
        {
            NextFloor();
        }
    }
}

[Serializable]
public class StageData
{
    public GameObject StageGameObject;
    public Transform RespawnPoint;
    public Transform EnemyParentGroup;
    [HideInInspector] public BaseEnemy[] EnemyList; 
    public SlidingDoors SlidingDoors;
}

public static class GameValue
{
    public static int CurrentFloor = 1;
    public static bool ObtainedGrapple =  false;
}