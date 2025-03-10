using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class PrevviewMaterials : MonoBehaviour
{
    [SerializeField] private Material[] _materialArray;
    [SerializeField] private MeshRenderer[] _meshRendererArray;

    [SerializeField] private TMP_Text _materialNameText;
    private int _materialIndex = 0;

    private void Start()
    {
        if (_materialArray == null || _materialArray.Length == 0)
        {
            Debug.LogWarning("Ball Material array is null or empty");
            return;
        }

        _materialIndex = 0;
        UpdateMaterials(_materialIndex);
    }

    private void UpdateMaterials(int materialIndex)
    {
        if (_materialArray == null || _materialArray.Length == 0)
        {
            Debug.LogWarning("Ball Material array is null or empty");
            return;
        }
        
        foreach (var meshRenderer in _meshRendererArray)
        {
            meshRenderer.material = _materialArray[materialIndex];
            _materialNameText.text = _materialArray[materialIndex].name;
        }
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (_materialIndex <= 0)
            {
                return;
            }

            _materialIndex--;
            UpdateMaterials(_materialIndex);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (_materialIndex >= _materialArray.Length - 1)
            {
                return;
            }

            _materialIndex++;
            UpdateMaterials(_materialIndex);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _materialIndex = _materialArray.Length - 1;
            UpdateMaterials(_materialIndex);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _materialIndex = 0;
            UpdateMaterials(_materialIndex);
        }
    }

    private void FixedUpdate()
    {
        RotateMeshRenderers();
    }

    private void RotateMeshRenderers()
    {
        foreach (var meshRenderer in _meshRendererArray)
        {
            if (meshRenderer != null)
            {
                meshRenderer.transform.Rotate(Vector3.up, 50f * Time.deltaTime, Space.World);
            }
        }
    }
}