// A simple sky made in code: one color overhead fading to another at the horizon.
// The fog uses the horizon color too, so far-away mountains melt softly into the sky.
Shader "RollOfLegends/GradientSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.38, 0.66, 0.98, 1)
        _HorizonColor ("Horizon", Color) = (0.8, 0.9, 0.98, 1)
        _Exponent ("Fade (higher = thinner horizon band)", Range(0.2, 6)) = 1.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            half _Exponent;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float up = saturate(normalize(i.direction).y);
                return lerp(_HorizonColor, _TopColor, pow(up, 1.0 / _Exponent));
            }
            ENDCG
        }
    }
}
