Shader "Custom/FogFade"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.1, 0.15, 0.2, 1)
        _FogDensity ("Fog Density", Range(0, 1)) = 0.8
        _FadeStartDistance ("Fade Start Distance", Float) = 50.0
        _FadeEndDistance ("Fade End Distance", Float) = 10.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off  // Render both sides so it looks solid from any angle
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            
            float4 _FogColor;
            float _FogDensity;
            float _FadeStartDistance;
            float _FadeEndDistance;
            float _PlayerYPosition;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate vertical distance (Y-axis only) between player and this pixel
                float verticalDistance = abs(_PlayerYPosition - i.worldPos.y);
                
                // Calculate fade factor based on distance
                // When player is far (distance > _FadeStartDistance): alpha = _FogDensity (fully visible)
                // When player is close (distance < _FadeEndDistance): alpha = 0 (invisible)
                float fadeFactor = smoothstep(_FadeEndDistance, _FadeStartDistance, verticalDistance);
                
                // Apply fog density to the fade factor
                float finalAlpha = fadeFactor * _FogDensity;
                
                // Return the fog color with calculated alpha
                return float4(_FogColor.rgb, finalAlpha);
            }
            ENDCG
        }
    }
    
    FallBack "Transparent/Diffuse"
}
