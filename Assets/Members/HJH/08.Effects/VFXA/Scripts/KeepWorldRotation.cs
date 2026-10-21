using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeepWorldRotation : MonoBehaviour
{

    public Quaternion fixedRotation;

    private void Awake() {
        fixedRotation = this.transform.localRotation;
    }

    void Update() {
        this.transform.rotation = fixedRotation;
    }
}
