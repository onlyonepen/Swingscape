using System;
using UnityEngine;
using UnityEngine.UI;

public class EnergyBar : MonoBehaviour
{
    public float MaxBar = 0.4f;

    private PlayerEnergy energy;
    private Image bar;

    private void Start()
    {
        energy = GlobalReference.Instance.player.Energy;
        bar = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateEnergyBar();
        
    }

    public void UpdateEnergyBar()
    {
        float barAmount = (energy.currentEnergy / energy.MaxEnergy) * MaxBar;
        //AddTween
        bar.fillAmount = barAmount;
    }
}
