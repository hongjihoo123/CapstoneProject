using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereMaskHelper : MonoBehaviour
{
    public Material sphereMaskMaterial;
    public Transform centerTransform;
    public string sphereMaskCenterProperty;
    private void OnValidate() {
        SetProperty();
    }
    private void Awake() {
        SetProperty();
    }

    private void SetProperty()
    {
        if(sphereMaskMaterial != null)
            sphereMaskMaterial.SetVector(sphereMaskCenterProperty, centerTransform.localPosition);
    }
}
