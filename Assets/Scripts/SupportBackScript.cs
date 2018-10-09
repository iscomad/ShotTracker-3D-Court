using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SupportBackScript: MonoBehaviour {

    void Start()
    {
        transform.GetComponent<Button>().onClick.AddListener(HandleBackButton);
    }

    private void HandleBackButton()
    {
        Application.Quit();
    }

    void FixedUpdate()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }
        }
    }
}
