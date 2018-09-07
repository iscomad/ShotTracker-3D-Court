using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickSetPosition : MonoBehaviour {

    public GameObject player;

    void OnMouseDown()
    {
        print("onMouseDown: " + Input.mousePosition);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        Physics.Raycast(ray, out hit);

        if (hit.collider.gameObject == gameObject)
        {
            Vector3 newTarget = new Vector3(0.5f, 0, 0);
            player.transform.position += newTarget;
        }
    }
}
