using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightScript : MonoBehaviour {

    private Light light;

	// Use this for initialization
	void Start () {
        light = GetComponent<Light>();
	}
	
	// Update is called once per frame
	void Update () {
        //light.intensity = Mathf.Lerp(light.intensity, 0, 0.5f * Time.deltaTime);
	}
}
