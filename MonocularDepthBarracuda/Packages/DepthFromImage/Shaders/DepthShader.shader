Shader "Uncharted Limbo/Unlit/MiDaS Depth Visualization"
{
    Properties
{
        }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Cull Off    
       
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // ----------------------------------------------------------------
            // PROPERTIES
            // ----------------------------------------------------------------
            // Minimum recorded depth
            float _Min;

            // Maximum recorded depth
            float _Max;

            // Depth Exaggeration
            float _DepthMultiplier;

            // Log-Normalization factor
            float _LogNorm;

            // Should vertex displacement be applied?
            int _Displace;

            // Should vertex displacement be applied?
            int _ColorIsDepth;

            int _SwapChannels;
            
            // Depth Texture
            sampler2D _MainTex;
            sampler2D _DepthTex;

            
            // ----------------------------------------------------------------
            // VANILLA STRUCTS - NOTHING TO SEE HERE
            // ----------------------------------------------------------------
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

         
            v2f vert (appdata v)
            {
                v2f o;

                // Newer versions of Barracuda mess up the X and Y directions. Therefore the UV has to be swapped
                o.uv = lerp(v.uv, float2(1 - v.uv.y, v.uv.x), _SwapChannels);

                if (_Displace == 1)
                {
                    // Vertex displacement
                    float depth = tex2Dlod(_DepthTex,float4(o.uv.xy,0,0));

                    o.vertex = UnityObjectToClipPos(float4(v.vertex.xy, depth * _DepthMultiplier,1));
               
                }
                else
                {
                    // Normal vertex conversion
                    o.vertex = UnityObjectToClipPos(v.vertex);
                }
          
             
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col;
                
                if (_ColorIsDepth == 1)
                {
                 // sample the texture
                float depth = tex2D(_MainTex, i.uv).x;

                float normalizedDepth = saturate((depth - _Min)/ (_Max-_Min)) ;

                float a = normalizedDepth;
                float b = log( _LogNorm * (normalizedDepth + 1))  / log(_LogNorm + 1);
                
                // Log normalization
                 col = _LogNorm >= 1.0 ? b : a;
                }
                else
                {
                    // The color texture is sampled normally, so we have to flip the coordinates back
                    float2 uv = lerp(i.uv, float2(1- i.uv.y, 1-i.uv.x), _SwapChannels);
                    
                    col = tex2D(_MainTex, uv);
                }

                return col;
            }
            ENDCG
        }
    }
}
