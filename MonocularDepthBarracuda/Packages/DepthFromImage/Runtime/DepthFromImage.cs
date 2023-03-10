using Unity.Barracuda;
using UnityEngine;
using UnityEngine.Events;
using static Unity.Barracuda.BarracudaTextureUtils;

namespace UnchartedLimbo.NN.Depth
{
    public class DepthFromImage : MonoBehaviour
    {
        [Header("Object References")]
        public NNModel neuralNetworkModel;
        public Texture       inputTexture;

        [Header("Parameters")]
        public bool calculateDepthExtents;
        
        [Header("Events")]
        public UnityEvent<RenderTexture> OnColorReady;
        public UnityEvent<RenderTexture> OnDepthSolved;
        public UnityEvent<float>         OnImageResized;
        public UnityEvent<Vector2>       OnDepthExtentsCalculated;
        
        public Texture InputTexture
        {
            get => inputTexture;
            set => inputTexture = value;
        }

        private Model         _model;
        private IWorker       _engine;
        private RenderTexture _input, _output;
        private int           _width, _height;

        private void Start()
        {
            InitializeNetwork();
            AllocateObjects();
        }

        private void Update()
        {
            if (inputTexture == null)
                return;

            // Fast resize
            Graphics.Blit(inputTexture, _input);

            OnColorReady.Invoke(_input);
            
            if (neuralNetworkModel == null)
                return;
            
            RunModel(_input);

            OnImageResized.Invoke(inputTexture.height / (float) inputTexture.width);
            OnDepthSolved.Invoke(_output);
        }

        private void OnDestroy() => DeallocateObjects();

        /// <summary>
        /// Loads the <see cref="NNModel"/> asset in memory and creates a Barracuda <see cref="IWorker"/>
        /// </summary>
        private void InitializeNetwork()
        {
            if (neuralNetworkModel == null)
                return;

            // Load the model to memory
            _model = ModelLoader.Load(neuralNetworkModel);

            // Create a worker
            _engine = WorkerFactory.CreateWorker(_model, WorkerFactory.Device.GPU);

            // Get Tensor dimensionality ( texture width/height )
            // In Barracuda 1.0.4 the width and height are in channels 1 & 2.
            // In later versions in channels 5 & 6
            #if _CHANNEL_SWAP
                _width  = _model.inputs[0].shape[5];
                _height = _model.inputs[0].shape[6];
            #else
                _width  = _model.inputs[0].shape[1];
                _height = _model.inputs[0].shape[2];
            #endif
        }

        /// <summary>
        /// Allocates the necessary <see cref="RenderTexture"/> objects.
        /// </summary>
        private void AllocateObjects()
        {
            if (inputTexture == null)
                return;

            // Check for accidental memory leaks
            if (_input  != null) _input.Release();
            if (_output != null) _output.Release();
            
            // Declare texture resources
            _input  = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32);
            _output = new RenderTexture(_width, _height, 0, RenderTextureFormat.RFloat);
            
            // Initialize memory
            _input.Create();
            _output.Create();
        }

        /// <summary>
        /// Releases all unmanaged objects
        /// </summary>
        private void DeallocateObjects()
        {
            _engine?.Dispose();
            _engine = null;

            if (_input != null) _input.Release();
            _input = null;

            if (_output != null) _output.Release();
            _output = null;

        }

        /// <summary>
        /// Performs Inference on the Neural Network Model
        /// </summary>
        /// <param name="source"></param>
        private void RunModel(Texture source)
        {
            using (var tensor = new Tensor(source, 3))
            {
                _engine.Execute(tensor);
            }
            
            // In Barracuda 1.0.4 the output of MiDaS can be passed  directly to a texture as it is shaped correctly.
            // In later versions we have to reshape the tensor. Don't ask why...
            #if _CHANNEL_SWAP
                var to = _engine.PeekOutput().Reshape(new TensorShape(1, _width, _height, 1));
            #else
                 var to = _engine.PeekOutput();
            #endif
         
              TensorToRenderTexture(to, _output, fromChannel:0);


              if (calculateDepthExtents)
              {
                  var data     = to.data.SharedAccess(out var o);
                  var minDepth = data.Min();
                  var maxDepth = data.Max();  
                  OnDepthExtentsCalculated.Invoke(new Vector2(minDepth,maxDepth));
              }
            

            to?.Dispose();
        }


    }
}
