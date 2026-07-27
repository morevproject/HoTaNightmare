using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SliceRenderer : MonoBehaviour
{
    public Shader sliceShader;

    void Start()
    {
        if (sliceShader != null)
        {
            // Камера срезов будет рендерить все объекты нашим шейдером
            GetComponent<Camera>().SetReplacementShader(sliceShader, "");
        }
    }
}