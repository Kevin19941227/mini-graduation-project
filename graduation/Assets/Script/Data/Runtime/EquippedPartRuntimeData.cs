using UnityEngine;

[System.Serializable]
public struct EquippedPartRuntimeData
{
    public int equipIndex;
    public int partID;

    public string attachPointID;

    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale;

    public EquippedPartRuntimeData(
        int equipIndex,
        int partID,
        string attachPointID,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        this.equipIndex = equipIndex;
        this.partID = partID;
        this.attachPointID = attachPointID;
        this.localPosition = localPosition;
        this.localEulerAngles = localEulerAngles;
        this.localScale = localScale;
    }
}