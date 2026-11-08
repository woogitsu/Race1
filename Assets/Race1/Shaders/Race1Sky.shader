Shader "Race1/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.05, 0.14, 0.38, 1)
        _HorizonColor ("Horizon Color", Color) = (0.62, 0.76, 0.90, 1)
        _GroundColor ("Ground Color", Color) = (0.22, 0.20, 0.18, 1)
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.82, 1)
        _SunDirection ("Sun Direction", Vector) = (0.3, 0.6, 0.5, 0)
        _SunSize ("Sun Size (radians)", Range(0.001, 0.2)) = 0.03
        _SunGlow ("Sun Glow", Float) = 0.5
        _StarVisibility ("Star Visibility", Range(0, 1)) = 0
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.5
        _CloudDensity ("Cloud Density", Range(0, 1)) = 0.5
        _CloudBrightness ("Cloud Brightness", Range(0, 2)) = 1
        _CloudSpeed ("Cloud Speed", Float) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }

        Pass
        {
            Name "Sky"

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _GroundColor;
                float4 _SunColor;
                float4 _SunDirection;
                float _SunSize;
                float _SunGlow;
                float _StarVisibility;
                float _CloudCoverage;
                float _CloudDensity;
                float _CloudBrightness;
                float _CloudSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // Pin the sky to the far plane so it only shows where nothing was drawn.
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = 0.0;
                #else
                    OUT.positionCS.z = OUT.positionCS.w;
                #endif

                // The skybox mesh is centred on the camera, so its object-space position is the view direction.
                OUT.directionOS = IN.positionOS.xyz;
                return OUT;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Hashes of the four corners of a lattice cell, one per lane: (0,0), (1,0), (0,1), (1,1).
            float4 CornerHash4(float4 x, float4 y)
            {
                return frac(sin(x * 127.1 + y * 311.7) * 43758.5453);
            }

            float ValueNoise2D(float2 p)
            {
                float2 cell = floor(p);
                float2 f = p - cell;
                float2 u = f * f * (3.0 - 2.0 * f);

                float4 h = CornerHash4(
                    float4(cell.x, cell.x + 1.0, cell.x, cell.x + 1.0),
                    float4(cell.y, cell.y, cell.y + 1.0, cell.y + 1.0));

                float2 edge = lerp(h.xz, h.yw, u.x);
                return lerp(edge.x, edge.y, u.y);
            }

            // Three octaves of value noise, normalised to roughly 0..1.
            float CloudFbm3(float2 p)
            {
                float n = 0.5 * ValueNoise2D(p);
                p = p * 2.03 + 17.1;
                n += 0.25 * ValueNoise2D(p);
                p = p * 2.01 + 31.7;
                n += 0.125 * ValueNoise2D(p);
                return n / 0.875;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.directionOS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDot = saturate(dot(dir, sunDir));

                // Gradient from zenith to horizon.
                float g = 1.0 - max(dir.y, 0.0);
                float3 sky = lerp(_ZenithColor.rgb, _HorizonColor.rgb, g * g * g);

                // Sun: soft-edged disk plus a wide glow.
                float sunCos = cos(_SunSize);
                float sunEdge = _SunSize * _SunSize * 0.25;
                float disk = smoothstep(sunCos - sunEdge, sunCos, sunDot);
                float glow = pow(sunDot, 64.0) * _SunGlow;
                sky += _SunColor.rgb * (disk + glow);

                // Stars: one hashed candidate per cell of a 400-per-unit grid, drawn as a small round dot.
                float aboveT = smoothstep(0.0, 0.15, dir.y);
                float3 starPos = dir * 400.0;
                float3 starCell = floor(starPos);
                float3 starLocal = starPos - starCell - 0.5;
                float starMask = smoothstep(0.997, 1.0, Hash13(starCell));
                float starDot = saturate(1.0 - dot(starLocal, starLocal) * 20.0);
                sky += starMask * starDot * aboveT * _StarVisibility;

                // Clouds: fbm on the sky-projected direction, scrolled in time, lit from the sun side.
                float3 cloudCol = float3(0.0, 0.0, 0.0);
                float cloudAlpha = 0.0;
                if (dir.y > 0.0 && _CloudCoverage > 0.0)
                {
                    float2 cloudUV = dir.xz / max(dir.y + 0.2, 0.05) * 2.0;
                    cloudUV.x += _Time.y * _CloudSpeed;

                    float n = CloudFbm3(cloudUV);
                    float threshold = 1.0 - _CloudCoverage;
                    float softness = lerp(0.35, 0.05, _CloudDensity);
                    cloudAlpha = smoothstep(threshold, threshold + softness, n);
                    cloudAlpha *= lerp(0.5, 1.0, _CloudDensity) * aboveT;

                    float sunSide = saturate(dot(dir, sunDir) * 0.5 + 0.5);
                    float3 cloudTint = lerp(float3(1.0, 1.0, 1.0), _SunColor.rgb, 0.5);
                    cloudCol = _CloudBrightness * cloudTint * lerp(0.7, 1.0, sunSide);
                }
                sky = lerp(sky, cloudCol, cloudAlpha);

                // Below the horizon: blend into the ground colour.
                float groundT = 1.0 - smoothstep(-0.1, 0.0, dir.y);
                sky = lerp(sky, _GroundColor.rgb, groundT);

                return float4(saturate(sky), 1.0);
            }
            ENDHLSL
        }
    }
}
