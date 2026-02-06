Shader "SVFramework/OutlineShader"
{
    Properties
    {
        _BlitTexture ("Texture", 2D) = "white" {}
    }
    SubShader
    {

        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }
        ZTest Always
        Cull Off
        ZWrite Off

        Blend SrcAlpha OneMinusSrcAlpha

        Fog
        {
            Mode off
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // struct Attributes
        // {
        //     float4 vertex: POSITION;
        //     half2 texcoord: TEXCOORD0;
        // };
        //
        // struct Varyings
        // {
        //     float4 pos : SV_POSITION;
        //     float2 uv : TEXCOORD0;
        // };

        struct Attributes_Convolution
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings_Convolution
        {
            float4 positionCS : SV_POSITION;
            half2 uv : TEXCOORD0;
            half2 offsets[8] : TEXCOORD1;
        };

        //用于模糊  
        struct Varyings_Blur
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 uv01 : TEXCOORD1;
            float4 uv23 : TEXCOORD2;
            float4 uv45 : TEXCOORD3;
        };

        TEXTURE2D_X(_PreDrawObjectsTexture);
        SAMPLER(sampler_linear_clamp_PreDrawObjectsTexture);

        TEXTURE2D_X(_BlurOutlineTexture);


        float4 _OutlineColor;
        float4 _Offsets;
        float _OutlineStrength;
        float _OutlineWidth;

        Varyings_Blur Vert_Blur(Attributes input)
        {
            Varyings_Blur o;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            #if SHADER_API_GLES
            float4 pos = input.positionOS;
            float2 uv  = input.uv;
            #else
            float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
            float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
            #endif

            o.positionCS = pos;
            o.uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
            o.uv01 = o.uv.xyxy + _Offsets.xyxy * float4(1, 1, -1, -1);
            o.uv23 = o.uv.xyxy + _Offsets.xyxy * float4(1, 1, -1, -1) * 2.0;
            o.uv45 = o.uv.xyxy + _Offsets.xyxy * float4(1, 1, -1, -1) * 3.0;
            return o;
        }

        float4 Frag_Blur(Varyings_Blur i) : SV_Target
        {
            float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv);
            col += 0.40 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv);
            col += 0.15 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv01.xy);
            col += 0.15 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv01.zw);
            col += 0.10 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv23.xy);
            col += 0.10 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv23.zw);
            col += 0.05 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv45.xy);
            col += 0.05 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.uv45.zw);
            return col;
        }


        half4 Frag_Cull(Varyings i) : SV_TARGET
        {
            half4 baseCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.texcoord);
            half4 preOutline = SAMPLE_TEXTURE2D_X(_PreDrawObjectsTexture, sampler_PointClamp, i.texcoord);

            half3 col = baseCol.rgb - half3(step(0.0001, preOutline.rgb));
            return half4(col, 1);
        }

        half4 Frag_Add(Varyings i) :SV_TARGET
        {
            half4 baseCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.texcoord);
            half4 outLineCol = SAMPLE_TEXTURE2D_X(_BlurOutlineTexture, sampler_PointClamp, i.texcoord);

            half3 col = outLineCol.rgb * _OutlineStrength + baseCol.rgb;
            return half4(col, 1);
        }

        Varyings_Convolution Vert_Convolution(Attributes_Convolution input)
        {
            Varyings_Convolution o;
            #if SHADER_API_GLES
            float4 pos = input.positionOS;
            float2 uv  = input.uv;
            #else
            float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
            float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
            #endif

            o.positionCS = pos;
            o.uv = uv;
            //o.uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;

            const half correction = _ScreenParams.x / _ScreenParams.y;

            o.offsets[0] = half2(-1, correction) * 0.707 * _OutlineWidth;
            o.offsets[1] = half2(0, correction) * _OutlineWidth;
            o.offsets[2] = half2(1, correction) * 0.707 * _OutlineWidth;
            o.offsets[3] = half2(-1, 0) * _OutlineWidth;

            o.offsets[4] = half2(1, 0) * _OutlineWidth;
            o.offsets[5] = half2(-1, -correction) * 0.707 * _OutlineWidth;
            o.offsets[6] = half2(0, -correction) * _OutlineWidth;
            o.offsets[7] = half2(1, -correction) * 0.707 * _OutlineWidth;

            return o;
        }

        half4 Frag_Outline(Varyings_Convolution input) : SV_TARGET
        {
            const half kernelX[8] = {
                -1, 0, 1,
                -2, 2,
                -1, 0, 1
            };

            const half kernelY[8] = {
                -1, -2, -1,
                0, 0,
                1, 2, 1
            };

            half gx = 0;
            half gy = 0;
            for (int i = 0; i < 8; i++)
            {
                half mask = SAMPLE_TEXTURE2D_X(_PreDrawObjectsTexture, sampler_linear_clamp_PreDrawObjectsTexture, input.uv + input.offsets[i]).a;
                gx += mask * kernelX[i];
                gy += mask * kernelY[i];
            }

            half4 col = _OutlineColor;
            const half alpha = SAMPLE_TEXTURE2D_X(_PreDrawObjectsTexture, sampler_linear_clamp_PreDrawObjectsTexture, input.uv);
            col.a = saturate(abs(gx) + abs(gy)) * saturate(1.0 - alpha);
            return col;
        }
        ENDHLSL

        Pass
        {

            Name "Gaussian_Blur"
            //pass 0: 高斯模糊 Horizontal  
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert_Blur
            #pragma fragment Frag_Blur
            ENDHLSL
        }

        Pass
        {

            Name "Outline_Cull"
            //pass 1: 剔除中心部分   
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag_Cull
            ENDHLSL
        }

        Pass
        {
            Name "Outline_FinalAdd"
            //pass 2: 最终叠加  
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag_Add
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            //pass 3: 卷积描边
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert_Convolution
            #pragma fragment Frag_Outline
            ENDHLSL
        }
    }
}