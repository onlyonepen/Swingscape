using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Script.Player.States;
using UnityEngine;
using VInspector;

public class PlayerHpManager : MonoBehaviour
{
    [SerializeField] private GameObject HpContainer;
    [SerializeField] private int maxHp = 3;
    [ReadOnly] public int CurrentHp;
    [ReadOnly] public bool IsDead;
    
    [Header("Invulnerability")]
    public bool isInvulnerable = false;
    [SerializeField] private GameObject InvulnerabilityEfffect;
    [SerializeField] private float iFrameDuration = 0.5f;

    private List<Transform> hpList = new List<Transform>();
    
    private void Start()
    {
        CurrentHp = maxHp;
        for (int i = 0; i < HpContainer.transform.childCount; i++)
        {
            hpList.Add(HpContainer.transform.GetChild(i));
        }
    }

    public void TurnOnInvulnerability()
    {
        InvulnerabilityEfffect.SetActive(true);
        isInvulnerable = true;
    }

    public void TurnOffInvulnerability()
    {
        InvulnerabilityEfffect.SetActive(false);
        isInvulnerable = false;
    }
    
    [Button]
    public void takedamage(int damage = 1)
    {
        // Cancel out if already dead or protected
        if (isInvulnerable || IsDead) return;

        GlobalReference.Instance.player.Cam.DOShakePosition(0.6f, 1f);
        
        CurrentHp -= damage;
        RefreshHp();

        // Trigger the I-frame window if the player survived the hit
        if (!IsDead)
        {
            StartCoroutine(IFrameRoutine());
        }
    }

    [Button]
    public void Heal(int amount = 1)
    {
        CurrentHp += amount;
        if (CurrentHp > maxHp) CurrentHp = maxHp; // Cap healing at maxHp
        RefreshHp();
    }

    /// <summary>Raised when the player dies. The locomotion state machine subscribes
    /// to switch into its DiedState, so HP no longer reaches into the state machine.</summary>
    public event Action OnDied;

    public void Died()
    {
        IsDead = true;
        OnDied?.Invoke();
    }

    private void RefreshHp()
    {
        for (int i = 0; i < hpList.Count; i++)
        {
            if (i < CurrentHp)
            {
                hpList[i].gameObject.SetActive(true);
            }
            else hpList[i].gameObject.SetActive(false);
        }
        
        if(CurrentHp <= 0 && !IsDead)
        {
            Died();
            CurrentHp = 0;
        }
    }

    private IEnumerator IFrameRoutine()
    {
        TurnOnInvulnerability();
        
        // Use Realtime so the Matrix-dodge slow-mo doesn't accidentally give you infinite I-frames
        yield return new WaitForSecondsRealtime(iFrameDuration);
        
        // Ensure we don't accidentally turn off invulnerability if a separate mechanic 
        // (like grappling) is managing it, or if the player died during the I-frames
        if (!IsDead)
        {
            TurnOffInvulnerability();
        }
    }
}