// A UI (uGUI) shader for panels and buttons: vertical gradient + resolution-independent
// rounded corners that respect the element's pixel size, so a wide button and a small
// square get the SAME corner radius (no stretching).
//
// Setup:
//   1. Create a Material and set its shader to "UI/Gradient Rounded".
//   2. Assign the material to an Image's Material slot; set the Image type to Simple.
//   3. Add the UiRectSize component to the same object - it feeds _RectSize so the
//      corners stay circular at any size. Without it, corners use the default size.
//
// Works with Canvas UI under both the Built-in pipeline and URP (uGUI keeps its own
// rendering path), and supports UI masking (RectMask2D / Mask).
Shader "UI/Gradient Rounded"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorTop ("Top Color", Color) = (1, 1, 1, 1)
        _ColorBottom ("Bottom Color", Color) = (0.75, 0.75, 0.75, 1)
        _CornerRadius ("Corner Radius (px)", Float) = 16
        _Softness ("Edge Softness (px)", Float) = 1.5
        _RectSize ("Rect Size (px) - set by UiRectSize", Vector) = (100, 100, 0, 0)
        _RectCenter ("Rect Centre (local) - set by UiRectSize", Vector) = (0, 0, 0, 0)

        // Standard UI plumbing (masking, stencil, color mask).
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ColorTop;
            fixed4 _ColorBottom;
            float  _CornerRadius;
            float  _Softness;
            float4 _RectSize;
            float4 _RectCenter;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            // Signed distance to a rounded box, in pixels. Negative = inside.
            float RoundedBoxDistance(float2 pointFromCenter, float2 halfSize, float radius)
            {
                float2 q = abs(pointFromCenter) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Work from the element's own local geometry, NOT sprite UVs: an atlased
                // sprite only covers a sub-rect of UV space, which would squash the
                // gradient into a single flat colour.
                float2 size = max(_RectSize.xy, float2(1.0, 1.0));
                float2 halfSize = size * 0.5;
                float2 p = i.worldPosition.xy - _RectCenter.xy; // pixels from the centre

                // Vertical gradient: 0 at the bottom edge, 1 at the top edge.
                float t = saturate(p.y / size.y + 0.5);
                fixed4 col = lerp(_ColorBottom, _ColorTop, t) * i.color;

                // Keep any sprite's own alpha (lets sliced/masked sprites still work).
                col.a *= tex2D(_MainTex, i.texcoord).a;

                // Size-aware rounded corners in pixel space, so the radius is uniform
                // regardless of the element's aspect ratio.
                float radius = min(_CornerRadius, min(halfSize.x, halfSize.y));
                float dist = RoundedBoxDistance(p, halfSize, radius);
                float softness = max(_Softness, 0.0001);
                col.a *= saturate(0.5 - dist / softness);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
