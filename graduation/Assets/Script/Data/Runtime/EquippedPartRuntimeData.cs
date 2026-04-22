using UnityEngine;

[System.Serializable]
public class EquippedPartRuntimeData
{
    public int partID;

    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
}