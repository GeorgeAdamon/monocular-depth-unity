using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Barracuda;
using UnityEngine;
using static Unity.Barracuda.BarracudaTextureUtils;

public class DepthFromImage : MonoBehaviour
{
    public ResourceSet resources;
    public IWorker     engine;

    public Texture2D t2d;

    public RenderTexture rt;

    private void Start()
    {
        AllocateObjects();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            RunModel(t2d);
        }
    }

    private void OnDestroy()
    {
        DeallocateObjects();
    }

    private void AllocateObjects()
    {
        var model = ModelLoader.Load(resources.model);

        engine = WorkerFactory.CreateWorker(model, WorkerFactory.Device.Compute);
    }

    private void DeallocateObjects()
    {
        engine?.Dispose();
        engine = null;
    }


    public void RunModel(Texture source)
    {
        // Run the BlazeFace model.
        using (var tensor = new Tensor(source))
        {
            Debug.Log(tensor.shape);
            engine.Execute(tensor);
        }

        var to = engine.PeekOutput();
        rt = TensorToRenderTexture(to);
        to?.Dispose();
    }
}