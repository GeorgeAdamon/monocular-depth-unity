using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetMaterialTexture : MonoBehaviour
{
    public Material mat;

    public string TexturePropertyName;

    public void ApplyTexture(Texture tex)
    {
        mat.SetTexture(TexturePropertyName, tex);
    }

}
