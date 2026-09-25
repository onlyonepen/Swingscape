using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitAndEnding : MonoBehaviour
{
    public GameObject EndScreen;
    
    bool isWin = false;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            EndScreen.SetActive(true);
            StartCoroutine(waitTillExitable());
        }
    }

    private void Update()
    {
        if (isWin && Input.anyKeyDown)
        {
            GameValue.CurrentFloor = 1;
            GameValue.ObtainedGrapple = false;
            SceneManager.LoadScene("MainMenu");
        }
    }

    IEnumerator waitTillExitable()
    {
        yield return new WaitForSecondsRealtime(2f);
        isWin = true;
    }
}
