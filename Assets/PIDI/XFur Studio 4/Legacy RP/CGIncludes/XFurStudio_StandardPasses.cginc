#include "XFurStudio_Core.cginc"

// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles

#if USE_VERTEXBUFFER

#if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
float3 GetPosition(uint vertexID)
{
	return asfloat(_XFurVertexBuffer.Load3((vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.y )));

}


float3 GetNormal( uint vertexID){
	
	return asfloat( _XFurVertexBuffer.Load3(( vertexID * _XFurVertexBufferStridePosNormal.x +  _XFurVertexBufferStridePosNormal.z )));
}

float4 GetTangent( uint vertexID){
	
	return asfloat( _XFurVertexBuffer.Load4(( vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.w )));
}
#endif
#endif



float2 hash2D2D(float2 s)
{
	//magic numbers
	return frac(sin(fmod(float2(dot(s, float2(127.1, 311.7)), dot(s, float2(269.5, 183.3))), 3.14159)) * 43758.5453);
}

//stochastic sampling
float4 tex2DStochastic(sampler2D tex, float2 UV)
{
	//triangle vertices and blend weights
	//BW_vx[0...2].xyz = triangle verts
	//BW_vx[3].xy = blend weights (z is unused)
	float4x3 BW_vx;

	//uv transformed into triangular grid space with UV scaled by approximation of 2*sqrt(3)
	float2 skewUV = mul(float2x2 (1.0, 0.0, -0.57735027, 1.15470054), UV * 3.464);

	//vertex IDs and barycentric coords
	float2 vxID = float2 (floor(skewUV));
	float3 barry = float3 (frac(skewUV), 0);
	barry.z = 1.0 - barry.x - barry.y;

	BW_vx = ((barry.z > 0) ?
		float4x3(float3(vxID, 0), float3(vxID + float2(0, 1), 0), float3(vxID + float2(1, 0), 0), barry.zyx) :
		float4x3(float3(vxID + float2 (1, 1), 0), float3(vxID + float2 (1, 0), 0), float3(vxID + float2 (0, 1), 0), float3(-barry.z, 1.0 - barry.y, 1.0 - barry.x)));

	//calculate derivatives to avoid triangular grid artifacts
	float2 dx = ddx(UV);
	float2 dy = ddy(UV);

	//blend samples with calculated weights
	return mul(tex2D(tex, UV + hash2D2D(BW_vx[0].xy), dx, dy), BW_vx[3].x) +
		mul(tex2D(tex, UV + hash2D2D(BW_vx[1].xy), dx, dy), BW_vx[3].y) +
		mul(tex2D(tex, UV + hash2D2D(BW_vx[2].xy), dx, dy), BW_vx[3].z);
}


struct Input
{
	float3 viewDir;
	float3 worldNormal;
	float4 xfurUVs;
	float3 anisoTan;
	float4 color:COLOR;
	INTERNAL_DATA
};


float3 tangentNormal(float3 normal, float4 tangent, float3 worldNormal, float3 worldPos) {

	float3 worldTangent = tangent.xyz;
	float3 worldBinormal = cross(worldNormal, worldTangent);

	float4 TtoW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x);
	float4 TtoW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y);
	float4 TtoW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);

	return float3(dot(TtoW0.xyz, normal), dot(TtoW1.xyz, normal), dot(TtoW2.xyz, normal));

}




void SimpleVert(inout appdata_full v, out Input o) {

	UNITY_INITIALIZE_OUTPUT(Input, o);

	o.xfurUVs = float4(v.texcoord.xy, v.texcoord1.xy);

}





void XFShellVert(inout appdata_full v, out Input o) {

	UNITY_INITIALIZE_OUTPUT(Input, o);

	o.xfurUVs = float4(v.texcoord.xy, v.texcoord1.xy);

#ifdef INSTANCING_ON
	uint fPass = UNITY_ACCESS_INSTANCED_PROP(XFurProps, _XFurInstancedCurrentPass);
	fPass = lerp(fPass, _XFurCurrentPass, _XFurForceNonInstanced);
#else
	uint fPass = _XFurCurrentPass;
#endif


#if USE_VERTEXBUFFER
#if defined(SHADER_API_D3D11) || defined( SHADER_API_VULKAN ) || defined (SHADER_API_METAL) || defined (SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
		v.vertex.xyz = GetPosition( v.texcoord1.z );

		v.normal = normalize( GetNormal( v.texcoord1.z ));

		v.tangent = GetTangent( v.texcoord1.z );	
#endif
#endif

	half4 furData = tex2Dlod(_XFurParamData, float4(v.texcoord.xy, 0, 0));
	float4 groomData = tex2Dlod(_XFurGroomData, float4(v.texcoord.xy, 0, 0));

	furData = lerp( furData, v.color, _XFurUseBakedData );
	groomData = lerp( groomData, v.texcoord3, _XFurUseBakedData );

	groomData.xyz = groomData.xyz * 2 - 1;

	groomData *= _XFurSelfGroomStrength;

#if FURPROFILES_BLENDED
	half4 profilesSplat = tex2Dlod(_XFurProfilesSplat, float4(v.texcoord.xy, 0, 0));

	half baseLength = _XFurSelfLength * (1 - saturate(length(profilesSplat)));
	half redLength = _XFurLength[0] * profilesSplat.r * (1 - saturate(length(profilesSplat.gba)));
	half greenLength = _XFurLength[1] * profilesSplat.g * (1 - saturate(length(profilesSplat.ba)));
	half blueLength = _XFurLength[2] * profilesSplat.b * (1 - saturate(length(profilesSplat.a)));
	half alphaLength = _XFurLength[3] * profilesSplat.a;

	half totalLength = (baseLength + redLength + greenLength + blueLength + alphaLength) * furData.g;
#else			
	half totalLength = _XFurSelfLength * furData.g;
#endif

	half3 wPos = mul(unity_ObjectToWorld, v.vertex).xyz;

	float3 windSim = float3(0, 0, 0);
	float windFreq = sin(8 * ((wPos.x % 1) + (wPos.z % 1)) + _Time.y * _XFurWindDirectionFreq.w);

	windSim = _XFurWindDirectionFreq.xyz * 2;

	windSim.x += lerp(dot(v.normal, float3(0, 1, 0)), 1, abs(_XFurWindDirectionFreq.x)) * lerp(windFreq, sign(_XFurWindDirectionFreq.x) * saturate(abs(windFreq)), abs(_XFurWindDirectionFreq.x));
	windSim.y += lerp(dot(v.normal, float3(1, 0, 0)), 1, abs(_XFurWindDirectionFreq.y)) * lerp(windFreq, sign(_XFurWindDirectionFreq.y) * saturate(abs(windFreq)), abs(_XFurWindDirectionFreq.y));
	windSim.z += lerp(dot(v.normal, float3(0, 0, 1)), 1, abs(_XFurWindDirectionFreq.z)) * lerp(windFreq, sign(_XFurWindDirectionFreq.z) * saturate(abs(windFreq)), abs(_XFurWindDirectionFreq.z));


	windSim *= _XFurSelfWindStrength * _XFurWindStrength * pow((totalLength / _XFurTotalPasses) * (1 + fPass), 1.5);

	half4 vfxMask = tex2Dlod(_XFurVFXMask, float4(v.texcoord.xy, 0, 1));

	half3 furDir = lerp(0, tangentNormal(groomData.xyz, float4( v.tangent.xyz, 1 ), v.normal.xyz, mul(unity_ObjectToWorld, float4( v.vertex.xyz, 1 ) ).xyz ), _XFurHasGroomData);

	float4 physicsForce = tex2Dlod(_XFurPhysics, float4(v.texcoord.xy, 0, 0));

	float3 physDir = physicsForce.xyz + float3(0, -0.35 * vfxMask.b, 0);


	//furDir = mul(unity_WorldToObject, furDir);

	physDir = mul(unity_WorldToObject, physDir) * 10 * pow((totalLength / _XFurTotalPasses) * (1 + fPass), 2);

	windSim *= 1 - saturate(dot(normalize(furDir + physDir), windSim));

	furDir += mul(unity_WorldToObject, windSim.xyz) * _XFurWindStrength;
	physDir += furDir;
	physDir *= lerp(1, 0.5, vfxMask.g);


	for (int i = 0; i < 8; i++){
	
		half distance = length(_XFurColliders[i].xyz-wPos);
		half normDistance = saturate( max(_XFurColliders[i].w,0.0001) - distance ) / max(_XFurColliders[i].w,0.0001);
		physDir += normalize( wPos - _XFurColliders[i].xyz) * pow( abs( _XFurBendingStrength ), _XFurBendingPower ) * saturate( normDistance );
		

	}

	float3 finalDir = v.vertex.xyz;

	v.vertex.xyz += 0.5 * normalize(v.normal + physDir) * (totalLength / _XFurTotalPasses) * (1 + fPass);

	finalDir = normalize(v.vertex.xyz - finalDir);

	//finalDir = cross(mul(unity_WorldToObject, float3(0, 1, 0)), finalDir);

	//finalDir = normalize(v.vertex.xyz - finalDir);

	//finalDir = cross(mul(unity_WorldToObject, float3(0, 1, 0)), finalDir);

	o.anisoTan = mul(unity_ObjectToWorld, finalDir);


}




float3 ShiftTangent(float3 T, float3 N, float shift)
{
	float3 shiftedT = T + (shift * N);
	return normalize(shiftedT);
}

float StrandSpecular(float3 T, float3 V, float3 L, float exponent)
{
	float3 halfDir = normalize(L + V);
	float dotTH = dot(T, halfDir);
	float sinTH = max(0.01, sqrt(1 - pow(dotTH, 2)));
	float dirAtten = smoothstep(-1, 0, dotTH);
	return dirAtten * pow(sinTH, exponent);
}


float2 xfur_gradientNoise_dir(float2 p)
{
    p = p % 289;
    float x = (34 * p.x + 1) * p.x % 289 + p.y;
    x = (34 * x + 1) * x % 289;
    x = frac(x / 41) * 2 - 1;
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}

float xfur_gradientNoise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(xfur_gradientNoise_dir(ip), fp);
    float d01 = dot(xfur_gradientNoise_dir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(xfur_gradientNoise_dir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(xfur_gradientNoise_dir(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
    return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x);
}

void XFurGradientNoise(float2 UV, float Scale, out float Out)
{
    Out = xfur_gradientNoise(UV * Scale) + 0.5;
}


void XFShellSurfacePass(Input IN, inout SurfaceOutputStandard o)
{
#ifdef INSTANCING_ON
	uint fPass = UNITY_ACCESS_INSTANCED_PROP(XFurProps, _XFurInstancedCurrentPass) + 1;
	fPass = lerp(fPass, _XFurCurrentPass + 1, _XFurForceNonInstanced);
#else
	uint fPass = _XFurCurrentPass + 1;
#endif

	half4 furData = tex2D(_XFurParamData, IN.xfurUVs.xy);

	furData = lerp( furData, IN.color, _XFurUseBakedData );

	furData.r *= lerp( 1, IN.color.r, _XFurUseVertexColor );
	furData.r *= lerp( 1, IN.color.a, _XFurUseVertexColor );

	float2 curl = float2(sin((fPass / _XFurTotalPasses) * 16 * _XFurSelfCurlAmountX) * abs(IN.xfurUVs.z) * (_XFurSelfCurlSizeX / (_XFurUVTiling)), sin((fPass / _XFurTotalPasses) * 16 * _XFurSelfCurlAmountY) * abs(IN.xfurUVs.w) * (_XFurSelfCurlSizeY / (_XFurUVTiling)));





	half dir = 1;// -saturate(ceil(tex2DStochastic(_XFurSelfStrandsPattern, _XFurUVTiling * (IN.xfurUVs.zw)).b * 2)) * 2;

	half4 fur = tex2DStochastic(_XFurSelfStrandsPattern, _XFurUVTiling * (IN.xfurUVs.zw + curl ));


	half underOver = saturate(ceil(fur.g * 2));
	half furClip = lerp(fur.r, fur.g, underOver);

	half4 vfxMap = tex2D(_XFurVFXMask, IN.xfurUVs.xy);


	half totalThickness = _XFurSelfThickness * furData.a * (1 + 0.15 * vfxMap.g * (1 - vfxMap.r));

	half thicknessCurve = pow(furClip, lerp(8, 2, totalThickness) * pow(fPass / _XFurTotalPasses, 8 * _XFurSelfThicknessCurve));


	furClip = furData.r * thicknessCurve - lerp(0.05, 0.025, underOver) * _XFurSelfLength * (fPass / _XFurTotalPasses);

	int mod = ceil(_XFurTotalPasses / _XFurLODStrength);

	float aValue = fPass % mod < 0.1 ? 1 : 0;


	aValue = lerp(aValue, 1, pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), _XFurLODArea));


	if (aValue < 0.1) {
		clip(-1);
	}
	else {

		o.Alpha = furClip;
		clip(furClip);


		half4 colorVariation = half4(1, 1, 1, 1);

		colorVariation = tex2D(_XFurSelfColorVariation, IN.xfurUVs.xy);

		half4 rColor = _XFurSelfColorA * colorVariation.r;
		half4 gColor = _XFurSelfColorB * saturate(colorVariation.g * (1 - colorVariation.r));
		half4 bColor = _XFurSelfColorC * saturate(colorVariation.b * (1 - colorVariation.r - colorVariation.g));
		half4 aColor = _XFurSelfColorD * saturate(colorVariation.a * (1 - colorVariation.r - colorVariation.g - colorVariation.b));

		half4 mixedColors = rColor + gColor + bColor + aColor;

		half4 color = tex2D(_XFurMainColorMap, IN.xfurUVs.xy * _XFurSelfBaseTiling.xy + _XFurSelfBaseTiling.zw) * _XFurSelfColor;

		//color = lerp( color, Luminance(color), 0.5 );

		half furNoise = 0;
		XFurGradientNoise( IN.xfurUVs.xy, _XFurNoiseShadingTiling * 8, furNoise );

		colorVariation = lerp( colorVariation, half4( 1, 1, 1, 1 ), 1 - _XFurLegacyVariation );
		mixedColors = lerp( mixedColors, lerp( _XFurSelfColorA, _XFurSelfColorB, furNoise ), 1 - _XFurLegacyVariation );


		half4 furColor = color * ( 1 - saturate(colorVariation.r + colorVariation.g + colorVariation.b + colorVariation.a) ) + color * mixedColors;



		half4 occlusionColor = half4(0, 0, 0, 0);


		half occlusionCurve = _XFurSelfOcclusionCurve;
		half occlusion = lerp(1, (pow((fPass / _XFurTotalPasses), 12 * occlusionCurve)), _XFurSelfOcclusion * furData.b);
		occlusionColor = _XFurSelfShadowsTint;



		half4 fColor = lerp( occlusionColor, furColor * lerp( _XFurSelfColorD * _XFurSelfUnderColorMod, _XFurSelfColorC * _XFurSelfOverColorMod, underOver), saturate( occlusion + (fur.b) ) );

		half4 oldFColor = lerp(occlusionColor, furColor * lerp( _XFurSelfUnderColorMod, _XFurSelfOverColorMod, underOver), occlusion);

		fColor = lerp( fColor, oldFColor, _XFurLegacyVariation );

		half4 blood = lerp(_XFurVFX1Color * 0.75 * fColor, saturate(_XFurVFX1Color * 1.5 * fColor), vfxMap.r) * saturate(occlusion * 2) * vfxMap.r * saturate(lerp(1, (pow((fPass / _XFurTotalPasses), 12 * _XFurVFX1Penetration)), 1) * 2);

		half4 snow = _XFurVFX2Color * saturate(lerp(1, (pow((fPass / _XFurTotalPasses), 24 * _XFurVFX2Penetration)), 1) * 2) * vfxMap.g * (1 - vfxMap.r);

		half4 fxColor = blood + snow;

		o.Metallic = max(blood * 0.25, vfxMap.b * 0.5);

		float smoothness = 0;

		smoothness = saturate( ( 1 - _XFurSelfSmoothness ) + saturate(blood * _XFurVFX1Smoothness + vfxMap.b * _XFurVFX3Smoothness));

		o.Smoothness = saturate( smoothness * max( fur.r, fur.g ) * 4 + saturate( smoothness - 0.35 ) + pow (max(fur.r, fur.g),4) * smoothness * 16 ) * lerp( pow((fPass / _XFurTotalPasses), 12 * occlusionCurve), 1, 0.65 );//

		fColor *= lerp(1, 0.55, vfxMap.b * saturate(lerp(1, (pow((fPass / _XFurTotalPasses), 2 * _XFurVFX3Penetration)), 1) * 4));

		o.Albedo = lerp(fColor, fxColor, saturate(vfxMap.r + vfxMap.g ));

		float3 specFinal = 0;

		fixed3 h = normalize(-_XFurMainStandardLightDir + IN.viewDir);
		float NdotL = saturate(dot(o.Normal, -_XFurMainStandardLightDir));

		fixed HdotA = dot(normalize(o.Normal + IN.anisoTan), h);
		float aniso = max(0, sin(radians((HdotA - 0.35 + length(curl)) * 180)));

		specFinal = pow(aniso, 128 * pow(smoothness, 5)) * 5 * smoothness * lerp(_XFurSelfSpecularTint, o.Albedo, 0.35);

		aniso = max(0, sin(radians((HdotA - 0.275 + length(curl)) * 180)));

		specFinal += pow(aniso, 64 * pow(smoothness, 5)) * 2 * smoothness * lerp(_XFurSelfSpecularTint, o.Albedo, 0.65);

		//specFinal +=  specFu;

		specFinal *= _XFurMainStandardLightColor * NdotL * occlusion;

		specFinal = saturate(specFinal);

		o.Albedo = saturate(o.Albedo + specFinal);

		o.Occlusion = occlusion;

		//o.Smoothness = smoothness;

#if USE_NORMALMAP
		o.Normal = UnpackNormal(tex2D(_XFurNormalmap, IN.xfurUVs.xy * _XFurSelfBaseTiling.xy + _XFurSelfBaseTiling.zw));
#endif

		half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
		half3 rimColor = lerp(_XFurSelfRimColor * saturate(lerp(fColor * 2, saturate(occlusion * 2), saturate(_XFurSelfRimBoost - 1) * 0.65)), saturate(o.Albedo * 2), 0.25) * _XFurSelfRimBoost * pow(rim, _XFurSelfRimPower) * (1 - length(vfxMap));
		o.Albedo += rimColor;
		o.Albedo = saturate(o.Albedo);

		o.Emission = tex2D(_XFurEmissionMap, IN.xfurUVs.xy) * _XFurEmissionColor + saturate(pow(half4(0.75, 0.6, 0.4, 1), 2.5) * pow(rim, 5) * max(pow(occlusion, 2), 0.05) * saturate(dot(o.Normal, _XFurMainStandardLightDir)) * _XFurMainStandardLightColor * _XFurSelfTransmission * 2);


	}
}



void BasicShellSurfacePass(Input IN, inout SurfaceOutputStandard o)
{

	uint fPass = FUR_PASS + 1;

	half4 furData = tex2D(_XFurParamData, IN.xfurUVs.xy);

	furData.r *= lerp( 1, IN.color.r, _XFurUseVertexColor );
	furData.r *= lerp( 1, IN.color.a, _XFurUseVertexColor );

	float2 curl = float2(sin((fPass / _XFurTotalPasses) * 16 * _XFurSelfCurlAmountX) * abs(IN.xfurUVs.z) * (_XFurSelfCurlSizeX / (_XFurUVTiling)), sin((fPass / _XFurTotalPasses) * 16 * _XFurSelfCurlAmountY) * abs(IN.xfurUVs.w) * (_XFurSelfCurlSizeY / (_XFurUVTiling)));

	half dir = 1;

	half4 fur = tex2DStochastic(_XFurSelfStrandsPattern, _XFurUVTiling * (IN.xfurUVs.zw + curl * dir));

	half underOver = saturate(ceil(fur.g * 2));
	half furClip = lerp(fur.r, fur.g, underOver);

	half4 colorVariation = half4(1, 1, 1, 1);


	colorVariation = tex2D(_XFurSelfColorVariation, IN.xfurUVs.xy);

	half4 rColor = _XFurSelfColorA * colorVariation.r;
	half4 gColor = _XFurSelfColorB * saturate(colorVariation.g * (1 - colorVariation.r));
	half4 bColor = _XFurSelfColorC * saturate(colorVariation.b * (1 - colorVariation.r - colorVariation.g));
	half4 aColor = _XFurSelfColorD * saturate(colorVariation.a * (1 - colorVariation.r - colorVariation.g - colorVariation.b));

	half4 mixedColors = rColor + gColor + bColor + aColor;

	half4 color = tex2D(_XFurMainColorMap, IN.xfurUVs.xy * _XFurSelfBaseTiling.xy + _XFurSelfBaseTiling.zw) * _XFurSelfColor;
	half4 furColor = color * (1 - saturate(colorVariation.r + colorVariation.g + colorVariation.b + colorVariation.a)) + color * mixedColors;



	half4 occlusionColor = half4(0, 0, 0, 0);

	half occlusionCurve = _XFurSelfOcclusionCurve;
	half occlusion = lerp(1, (pow((fPass / _XFurTotalPasses), 12 * occlusionCurve)), _XFurSelfOcclusion * furData.b);
	occlusionColor = _XFurSelfShadowsTint;


	half4 vfxMap = tex2D(_XFurVFXMask, IN.xfurUVs.xy);

	half4 fColor = lerp(occlusionColor, saturate(furColor * lerp(1.0 - _XFurSelfUnderColorMod, 1.0 + _XFurSelfOverColorMod, underOver)), saturate(occlusion));

	half4 blood = lerp(_XFurVFX1Color * 0.75 * fColor, saturate(_XFurVFX1Color * 1.5 * fColor), vfxMap.r) * saturate(occlusion * 2) * vfxMap.r;

	half4 snow = _XFurVFX2Color * saturate(occlusion * 2) * vfxMap.g * (1 - vfxMap.r);

	half4 fxColor = blood + snow;

	o.Metallic = max(blood * 0.25, vfxMap.b * 0.5);



	float smoothness = 0;

	smoothness = saturate(_XFurSelfSmoothness + saturate(blood * _XFurVFX1Smoothness + vfxMap.b * _XFurVFX3Smoothness));

	fColor *= lerp(1, 0.55, vfxMap.b);

	o.Albedo = lerp(fColor, fxColor, saturate(vfxMap.r + vfxMap.g + vfxMap.a));

	float3 specFinal = 0;

	fixed3 h = normalize(-_XFurMainStandardLightDir + IN.viewDir);
	float NdotL = saturate(dot(IN.worldNormal, -_XFurMainStandardLightDir));

	fixed HdotA = dot(normalize(IN.worldNormal + IN.anisoTan), h);
	float aniso = max(0, sin(radians((HdotA - 0.35 + length(curl)) * 180)));

	specFinal = pow(aniso, 128 * pow(smoothness, 5)) * 5 * smoothness * lerp(_XFurSelfSpecularTint, o.Albedo, 0.35);

	aniso = max(0, sin(radians((HdotA - 0.275 + length(curl)) * 180)));

	specFinal += pow(aniso, 64 * pow(smoothness, 5)) * 2 * smoothness * lerp(_XFurSelfSpecularTint, o.Albedo, 0.65);

	//specFinal +=  specFu;

	specFinal *= _XFurMainStandardLightColor * NdotL * occlusion;

	specFinal = saturate(specFinal);

	o.Albedo = saturate(o.Albedo + specFinal);


	o.Occlusion = occlusion;

	half totalThickness = _XFurSelfThickness * furData.a * (1 + 0.25 * vfxMap.g * (1 - vfxMap.r));

	half thicknessCurve = pow(furClip, lerp(8, 2, totalThickness) * pow(fPass / _XFurTotalPasses, 8 * _XFurSelfThicknessCurve));

	half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
	half3 rimColor = lerp(_XFurSelfRimColor, o.Albedo * 2, 0.25) * pow(rim, _XFurSelfRimPower);
	o.Albedo += rimColor;
	o.Albedo = saturate(o.Albedo);

#if USE_NORMALMAP
	o.Normal = UnpackNormal(tex2D(_XFurNormalmap, IN.xfurUVs.xy * _XFurSelfBaseTiling.xy + _XFurSelfBaseTiling.zw));
#endif


	o.Emission = tex2D(_XFurEmissionMap, IN.xfurUVs.xy) * _XFurEmissionColor + saturate(pow(half4(0.75, 0.6, 0.4, 1), 2.5) * pow(rim, 4) * max(occlusion, 0.25) * saturate(dot(o.Normal, _XFurMainStandardLightDir)) * _XFurMainStandardLightColor * 2);


	clip(furData.r * thicknessCurve - lerp(0.05, 0.025, underOver) * _XFurSelfLength * (fPass / _XFurTotalPasses));

}