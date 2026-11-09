// Coast water for the Race1 world. The surface is a flat sea: its colour blends from deep water to reflected sky
// with the Fresnel term (grazing angles reflect more), and foam comes from the vertex colour alpha, which the
// mesh builder sets from the depth at the waterline.
//
// Not compiled in the development container. It follows the URP 17 shader layout used by Race1/Sky.
Shader "Race1/Water"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.03, 0.14, 0.20, 1)
        _SkyColor ("Reflected Sky", Color) = (0.55, 0.70, 0.85, 1)
        _FoamColor ("Foam Color", Color) = (0.95, 0.97, 1.0, 1)
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 5
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _SkyColor;
                float4 _FoamColor;
                float _FresnelPower;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float foam : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.foam = IN.color.a;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // The sea is flat, so the normal is straight up.
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float facing = saturate(viewDir.y);
                float fresnel = pow(1.0 - facing, _FresnelPower);

                float3 water = lerp(_DeepColor.rgb, _SkyColor.rgb, fresnel);
                float foam = saturate(IN.foam);
                water = lerp(water, _FoamColor.rgb, foam);

                float alpha = lerp(_Opacity, 1.0, foam);
                return half4(water, alpha);
            }
            ENDHLSL
        }
    }
}
