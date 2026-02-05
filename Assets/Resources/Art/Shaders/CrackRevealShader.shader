Shader "Custom/CrackRevealShader"
{
    Properties
    {
        [Header(Crack Texture)]
        _CrackTexture("Crack Texture", 2D) = "white" {}
        
        [Header(Reveal Settings)]
        [Range(0, 1)] _RevealAmount("Reveal Amount", Float) = 0
        [Range(0.01, 0.5)] _RevealSoftness("Reveal Softness", Float) = 0.1
        
        [Header(Appearance)]
        _CrackColor("Crack Color", Color) = (0, 0, 0, 1)
        [Toggle] _InvertCrack("Invert Crack (White cracks instead of black)", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            // Properties
            TEXTURE2D(_CrackTexture);
            SAMPLER(sampler_CrackTexture);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _CrackTexture_ST;
                float _RevealAmount;
                float _RevealSoftness;
                float4 _CrackColor;
                float _InvertCrack;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _CrackTexture);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample the crack texture
                half4 crackSample = SAMPLE_TEXTURE2D(_CrackTexture, sampler_CrackTexture, input.uv);
                
                // Get the crack intensity from the texture
                // We'll use the red channel, but you could also use grayscale average
                half crackIntensity = crackSample.r;
                
                // If invert is enabled, flip the crack (for white cracks instead of black)
                if (_InvertCrack > 0.5)
                {
                    crackIntensity = 1.0 - crackIntensity;
                }
                
                // Create circular reveal mask
                // Remap UV from (0,1) to (-1,1) to center it
                float2 centeredUV = input.uv * 2.0 - 1.0;
                
                // Calculate distance from center
                float distanceFromCenter = length(centeredUV);
                
                // Create the reveal mask
                // When _RevealAmount is 0, nothing is revealed
                // When _RevealAmount is 1, everything is revealed
                float revealMask = (_RevealAmount - distanceFromCenter) / _RevealSoftness;
                revealMask = saturate(revealMask);
                
                // Combine crack intensity with reveal mask
                half finalAlpha = crackIntensity * revealMask;
                
                // Apply the crack color
                half3 finalColor = _CrackColor.rgb;
                
                // Apply fog
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
