using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public Color activeTextColor;
    public Color normalTextColor;
    
    [SerializeField]
    private TMP_Text[] items;
    private int currentIndex;

    [Header("Components")]
    public GameObject student;

    public void OnUp(InputAction.CallbackContext ctx)
    {
        if (currentIndex - 1 < 0)
        {
            return;
        }

        items[currentIndex].color = normalTextColor;
        currentIndex -= 1;
        items[currentIndex].color = activeTextColor;
    }

    public void OnDown(InputAction.CallbackContext ctx)
    {
        if (currentIndex + 1 >= items.Length)
        {
            return;
        }

        items[currentIndex].color = normalTextColor;
        currentIndex += 1;
        items[currentIndex].color = activeTextColor;
    }

    
    public void OnSelect(InputAction.CallbackContext ctx)
    {
        switch (currentIndex)
        {
            case 0:
                StartCoroutine(start());
                break;
            case 1:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    IEnumerator start()
    {
        student.GetComponent<Player>().Jump();
        yield return new WaitForSeconds(0.55f);

        SceneManager.LoadScene("Game");

    }
}