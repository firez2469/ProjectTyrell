Shader "Custom/EdgeGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _EdgeWidth ("Edge Width", Range(0,0.1)) = 0.01
        _EdgeColor ("Edge Color", Color) = (1,1,0,1)
        _EdgeGlow ("Edge Glow", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _EdgeWidth;
        fixed4 _EdgeColor;
        half _EdgeGlow;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Calculate distance from edge using derivatives
            float2 uv = IN.uv_MainTex;
            float2 ddx_uv = ddx(uv);
            float2 ddy_uv = ddy(uv);
            
            // Calculate edge factor based on UV coordinates
            float2 uvToEdge = min(uv, 1.0 - uv);
            float edgeDist = min(uvToEdge.x, uvToEdge.y);
            float edgeFactor = smoothstep(0.0, _EdgeWidth, edgeDist);
            
            // Alternate calculation that uses mesh edges more precisely
            float uvDeriv = max(length(ddx_uv), length(ddy_uv));
            float borderDist = min(uvToEdge.x, uvToEdge.y) / uvDeriv;
            float borderFactor = smoothstep(0.0, _EdgeWidth * 10.0, borderDist);
            
            // Use the more precise calculation
            edgeFactor = borderFactor;
            
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Blend with edge color
            c = lerp(_EdgeColor, c, edgeFactor);
            
            o.Albedo = c.rgb;
            o.Emission = _EdgeGlow * _EdgeColor.rgb * (1.0 - edgeFactor);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}