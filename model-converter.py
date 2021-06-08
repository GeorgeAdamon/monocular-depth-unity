import numpy as np
import onnx
from onnx import checker, helper
from onnx import AttributeProto, TensorProto, GraphProto
from onnx import numpy_helper as np_helper

# Get a shape tensor
def get_shape_tensor(model, shape):
  # Initializer name
  name = 'shape_{0}x{1}x{2}x{3}'.format(*shape)

  # If the initializer already exists, simply use it.
  exists = any(x for x in model.graph.initializer if x.name == name)
  if exists: return name

  # Add the initializer for the tensor.
  tensor = helper.make_tensor(name, TensorProto.INT64, (4,), shape)
  model.graph.initializer.append(tensor)
  return name

# Main converter function
def convert_model(model):
  i = 0
  while i < len(model.graph.node):
    # We only modify Pad nodes.
    node = model.graph.node[i]
    if node.op_type != 'Pad': i += 1; continue

    # Determine the padding shape.
    input = next(n for n in model.graph.value_info if n.name == node.input[0])
    pads = next(n for n in model.graph.initializer if n.name == node.input[1])

    # Get the shape tensor.
    dim = tuple(map(lambda x: x.dim_value, input.type.tensor_type.shape.dim))
    ext = np_helper.to_array(pads)[5]
    shape_tensor = get_shape_tensor(model, (1, ext, dim[2], dim[3]))

    # Create replacement nodes.
    const_out = node.name + '_pad'
    const_node = helper.make_node('ConstantOfShape', (shape_tensor,), (const_out,))
    concat_node = helper.make_node('Concat', (node.input[0], const_out), (node.output[0],), axis = 1)

    # Replace the Pad node.
    model.graph.node.insert(i, const_node)
    model.graph.node.insert(i + 1, concat_node)
    model.graph.node.remove(node)
    i += 2


model = onnx.load("C:/Users/GeorgeAdamon/Documents/GitHub/GeorgeAdamon/monocular-depth-unity/MonocularDepthBarracuda/Assets/ONNX/model-f6b98070.onnx")
convert_model(model)
checker.check_model(model)
onnx.save(model, "C:/Users/GeorgeAdamon/Documents/GitHub/GeorgeAdamon/monocular-depth-unity/MonocularDepthBarracuda/Assets/ONNX/model-f6b98070_barracuda.onnx")