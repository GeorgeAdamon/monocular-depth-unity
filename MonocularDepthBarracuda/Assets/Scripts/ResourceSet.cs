using System.Collections;
using System.Collections.Generic;
using Unity.Barracuda;
using UnityEngine;

[CreateAssetMenu(menuName = "Depth from Image/Resource Set")]
public class ResourceSet : ScriptableObject
{
    public NNModel model;
}
