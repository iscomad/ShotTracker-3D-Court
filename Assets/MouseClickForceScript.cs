using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseClickForceScript : MonoBehaviour {

    void OnMouseDown()
    {
        GetComponent<Rigidbody>().AddForce(-transform.up * 500f);
        GetComponent<Rigidbody>().useGravity = true;
    }
}
