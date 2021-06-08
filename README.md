# monocular-depth-unity
 Depth from Monocular Image using the MiDaS v2 library with Unity's Barracuda inference framework


## Installation

### Unity Package Manager
Add this line to your `manifest.json`:
```json
"ulc-nn-depth":"https://github.com/GeorgeAdamon/monocular-depth-unity.git?path=/MonocularDepthBarracuda/Packages/DepthFromImage#main",
```

## Usage

### Step 0
Find the `DEPTH_FROM_IMAGE` prefab
![](img/step0.png)

### Step 1
Use the Texture you like in the `Input Texture` slot. Works with RenderTextures and Texture2D objects. V
ideo is supported through RenderTextures.
![](img/step1.png)

### Step 2
Parameterize the visual output in the `Depth Mesher` object. Use `Shader` method for best performance, or `Mesh` to get an actual tangible mesh.
If `Color Texture` is left blank, the mesh will be colorized with the depth data by default.
![](img/step2.png)
