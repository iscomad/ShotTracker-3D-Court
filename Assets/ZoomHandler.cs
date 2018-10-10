using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ZoomHandler : MonoBehaviour {

	private Text zoomText;

	// Use this for initialization
	void Start () {
		Button button = GetComponent<Button>();
		button.onClick.AddListener(OnButtonClick);
		zoomText = button.GetComponentInChildren<Text>();
	}

    private void OnButtonClick()
    {
        switch (zoomText.text) {
			case "1x":
				ZoomIn();
				zoomText.text = "2x";
				break;
			case "2x":
				ZoomOut();
				zoomText.text = "1x";
				break;
		}
    }

    private void ZoomIn() {
        Camera.main.fieldOfView = 25;
        Camera.main.transform.rotation = Quaternion.Euler(30, 0, 0);
		Camera.main.transform.position = new Vector3(0, 15, -22);
    }

    private void ZoomOut() {
        Camera.main.fieldOfView = 33;
        Camera.main.transform.rotation = Quaternion.Euler(23, 0, 0);
		Camera.main.transform.position = new Vector3(0, 13, -22);
    }
}
