﻿// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/DisplayNormals" {
	SubShader {
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			struct v2f{
				float4 pos : SV_POSITION;
				float3 color : COLOR0;
			};
			
			v2f vert(appdata_base v){
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.color = v.normal*0.5 + 0.5;
				return o;
			}
			
			half4 frag (v2f i) : COLOR {
				return half4(i.color, 1);
			}
			ENDCG
			
		}
	}
	Fallback "VertexLit"
}	
