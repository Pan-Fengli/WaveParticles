Shader "FluidSim/Water_02"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        //_Color ("Color", Color) = (1,1,1,1)
        _Skybox ("Skybox", Cube) = "defaulttexture" {}
        //_LineColor("Line Color", COLOR) = (0,1,0,1)
        _OceanColorShallow("Ocean Color Shallow", Color) = (1, 1, 1, 1)
        _ShoreColorShallow("Shore Color Shallow", Color) = (1, 1, 1, 1)
        _OceanColorDeep("Ocean Color Deep", Color) = (1, 1, 1, 1)
        _BubblesColor("Bubble Color", Color) = (1, 1, 1, 1)
        _Specular("Specular", Color) = (1, 1, 1, 1)
        _Gloss("Gloss", Range(8.0, 256)) = 20
        _FresnelScale("Fresnel Scale", Range(0, 1)) = 0.5
        _BubbleScale("Bubble Scale", Range(0, 1)) = 0.5
        _BubblesThreshold("Bubble Threshold", Range(0, 1)) = 0.5
        [Toggle]_selected("Bubble On", Int) = 0
        [Toggle]_switch("Shore", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase"}
        LOD 100
        //Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma shader_feature _SELECTED_ON
            #pragma shader_feature _SWITCH_ON

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            RWStructuredBuffer<float3> buffer : register(u1);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldViewDir : TEXCOORD1;
                float3 worldPos: TEXCOORD2;
            };
            fixed4 _OceanColorShallow;
            fixed4 _OceanColorDeep;
            fixed4 _ShoreColorShallow;
            fixed4 _Specular;
            float _Gloss;
            fixed _FresnelScale;
            fixed _BubbleScale;
            fixed _BubblesThreshold;
            fixed4 _BubblesColor;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            uniform samplerCUBE _Skybox;

            inline half3 SamplerReflectProbe(UNITY_ARGS_TEXCUBE(tex), half3 refDir, half roughness, half4 hdr)
            {
                roughness = roughness * (1.7 - 0.7 * roughness);
                half mip = roughness * 6;
                //对反射探头进行采样
                //UNITY_SAMPLE_TEXCUBE_LOD定义在HLSLSupport.cginc，用来区别平台
                half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(tex, refDir, mip);
                //采样后的结果包含HDR,所以我们需要将结果转换到RGB
                //定义在UnityCG.cginc
                return DecodeHDR(rgbm, hdr);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 div = tex2Dlod(_MainTex, float4(o.uv, 0, 0));
                float4 vertexPos = v.vertex + div;
                //float4 vertexPos = v.vertex;
                //vertexPos.y += div.r;
                o.vertex = UnityObjectToClipPos(vertexPos);
                o.worldViewDir = WorldSpaceViewDir(vertexPos);
                o.worldPos = mul(unity_ObjectToWorld, vertexPos).xyz;
                if (o.worldPos.x > -0.1 && o.worldPos.x<0.1 && o.worldPos.z > -0.1 && o.worldPos.z<0.1)
                {
                    float3 p = o.worldPos;//要输出的值
                    buffer[0] = p;
                }
                //float3 p = float3(o.uv.x,o.uv.y,0);//要输出的值
                return o;
            }

            fixed4 _Color;
            fixed4 frag(v2f i) : SV_Target
            {
                float eps = 0.001;
                float hL = tex2D(_MainTex, float2(i.uv.x + eps, i.uv.y)).y;
                float hR = tex2D(_MainTex, float2(i.uv.x - eps, i.uv.y)).y;
                float hT = tex2D(_MainTex, float2(i.uv.x, i.uv.y - eps)).y;
                float hB = tex2D(_MainTex, float2(i.uv.x, i.uv.y + eps)).y;

                float3 norm = normalize( float3( hL - hR, 2 * eps * 10, hB - hT ) );//这里的10是和模拟的区域大小有关
#ifdef _SELECTED_ON              
                float3 x1D = tex2D(_MainTex, float2(i.uv.x + eps, i.uv.y));
                float3 x2D = tex2D(_MainTex, float2(i.uv.x - eps, i.uv.y));
                float3 z1D = tex2D(_MainTex, float2(i.uv.x, i.uv.y - eps));
                float3 z2D = tex2D(_MainTex, float2(i.uv.x, i.uv.y + eps));
                //计算泡沫
                float3 ddx = (x2D - x1D) / (20 * eps);
                float3 ddz = (z2D - z1D) / (20 * eps);
                float jacobian = (1.0f + ddx.x) * (1.0f + ddz.z) - ddx.z * ddz.x;
                fixed bubbles = saturate(max(0, _BubblesThreshold - saturate(jacobian)) * _BubbleScale);
#endif
                /*float3 viewDir = normalize(i.worldViewDir);
                float3 reflectVec = reflect(-viewDir,norm);
                float4 reflectCol = texCUBE(_Skybox, reflectVec);
                reflectCol = lerp(reflectCol, 0, pow(dot(norm, viewDir), 1));
                return float4(0.8, 0.9, 1.0,1.0) * reflectCol;
                return float4(0.4, 0.6, 0.6, 1.0) * reflectCol;*/
                
                fixed3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                fixed3 reflectDir = reflect(-viewDir, norm);
                //采样反射探头
                half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectDir, 0);
                half3 sky = DecodeHDR(rgbm, unity_SpecCube0_HDR);

                //菲涅尔
                //fixed FS = lerp(1-_FresnelScale, _FresnelScale, 1 - i.uv.x);//左边的FS会更大
#ifdef _SWITCH_ON
                //fixed FS = lerp(0.4f, 0, 1 - i.uv.x);//左边的FS会更大
                fixed FS = 0.1f * i.uv.x * i.uv.x;
                fixed fresnel = saturate(FS + (1 - FS) * pow(1 - dot(norm, viewDir), 5));
#else
                fixed fresnel = saturate(_FresnelScale + (1 - _FresnelScale) * pow(1 - dot(norm, viewDir), 5));
#endif
                half facing = saturate(dot(viewDir, norm));
#ifdef _SWITCH_ON
                float newx = i.uv.x * i.uv.x;
                fixed4 DeepColor = lerp(_ShoreColorShallow, _OceanColorDeep, 1- i.uv.x);
                fixed3 oceanColor = lerp(_OceanColorShallow, DeepColor, facing);
#else
                fixed3 oceanColor = lerp(_OceanColorShallow, _OceanColorDeep, facing);
#endif

                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                //泡沫颜色
                fixed3 bubblesDiffuse = _BubblesColor.rbg * _LightColor0.rgb * saturate(dot(lightDir, norm));//

                //海洋颜色
                fixed3 oceanDiffuse = oceanColor * _LightColor0.rgb * saturate(dot(lightDir, norm));
                fixed3 halfDir = normalize(lightDir + viewDir);
                fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(norm, halfDir)), _Gloss);

#ifdef _SELECTED_ON
                fixed3 diffuse = lerp(oceanDiffuse, bubblesDiffuse, bubbles);
#else
                fixed3 diffuse = oceanDiffuse;
#endif
                
                //fixed3 diffuse = oceanDiffuse;

                fixed3 col = ambient + lerp(diffuse, sky, fresnel) + specular;
                //col = DeepColor;
//#ifdef _SELECTED_ON
//                col = (bubbles, bubbles, bubbles);
//#endif
                //return fixed4(i.uv.x, i.uv.x, i.uv.x, 1);
                return fixed4(col, 1);
                //return fixed4(bubblesDiffuse, 1);
                
            }
            ENDCG
        }

        //Pass
        //{
        //    Tags { "RenderType" = "Opaque" }
        //    LOD 100

        //    CGPROGRAM
        //    #pragma target 5.0
        //    #pragma vertex vert
        //    #pragma fragment frag
        //    //几何着色器
        //    #pragma geometry geo
        //    #include "UnityCG.cginc"

        //    fixed4 _LineColor;
        //    struct v2g {
        //        //顶点位置
        //        float4  pos       : POSITION;
        //        //法线
        //        float3  normal    : NORMAL;
        //        //纹理坐
        //        float2  tex0        : TEXCOORD0;
        //    };
        //    struct g2f {
        //        //像素位置
        //        float4  pos       : POSITION;
        //        //纹理
        //        float2  tex0        : TEXCOORD0;
        //    };
        //    
        //    v2g vert(appdata_base v)
        //    {
        //        v2g o;
        //        o.pos = UnityObjectToClipPos(v.vertex);
        //        o.normal = v.normal;
        //        //初始化纹理信息
        //        o.tex0 = float2(0, 0);
        //        return o;
        //    }
        //    [maxvertexcount(3)]
        //    void geo(triangle v2g vg[3], inout LineStream<g2f> ls)
        //    {
        //        for (int i = 0; i < 3; i++)
        //        {
        //            g2f g2f_1;
        //            g2f_1.pos = vg[i].pos;
        //            //初始化纹理
        //            g2f_1.tex0 = float2(0.0f, 0.0f);
        //            ls.Append(g2f_1);
        //        }
        //        ls.RestartStrip();
        //    }
        //    fixed4 frag(g2f input) :COLOR
        //    {
        //        return _LineColor;
        //    }
        //    ENDCG
        //}
    }
}
