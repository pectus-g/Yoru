#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

UNITY_INSTANCING_BUFFER_START(XFurProps)
UNITY_DEFINE_INSTANCED_PROP(float, _XFurInstancedCurrentPass)
UNITY_INSTANCING_BUFFER_END(XFurProps)




sampler2D _XFurSelfStrandsPattern;

half4 _XFurVFX1Color;

half4 _XFurVFX2Color;

half4 _XFurVFX3Color;

half4 _XFurVFX4Color;

half _XFurVFX1Penetration;

half _XFurVFX2Penetration;

half _XFurVFX3Penetration;

half _XFurVFX1Smoothness;

half _XFurVFX2Smoothness;

half _XFurVFX3Smoothness;


float _XFurForceNonInstanced;

//The absolute length of the fur
half _XFurLength[4];

//THe absolute thickness of the fur strands
half _XFurThickness[4];

//The curve used to control the ratio of the fur strand from the root to the tip
half _XFurThicknessCurve[4];

//The strength of the self-shadowing / occlusion applied to the fur
half _XFurOcclusion[4];

//The curve that controls how the shadowing goes from the roots to the tips of the fur
half _XFurOcclusionCurve[4];

half _XFurSelfSmoothness;

//Tint to be applied to the fur self-shadowing
half4 _XFurShadowsTint[4];

half _XFurSelfCurlAmountX;

half _XFurSelfCurlAmountY;

half _XFurSelfCurlSizeX;

half _XFurSelfCurlSizeY;

half _XFurSelfUnderColorMod, _XFurSelfOverColorMod;

half _XFurSelfRimBoost;

half4 _XFurSelfRimColor;

half4 _XFurSelfSpecularTint;

float _XFurLODArea;

float _XFurLODStrength;

float _XFurSelfTransmission;

float _XFurSelfGroomStrength;


//The spherical colliders that can interact with this XFur instance
float4 _XFurColliders[8];

float _XFurBendingPower;

float _XFurBendingStrength;

#if USE_VERTEXBUFFER

ByteAddressBuffer _XFurVertexBuffer;
ByteAddressBuffer _XFurOldVertexBuffer;
float4 _XFurVertexBufferStridePosNormal;
float _XFurVertexBufferTexcoord4;

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

float3 GetOldPosition(uint vertexID)
{
	return asfloat(_XFurOldVertexBuffer.Load3((vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.y )));

}


float3 GetOldNormal( uint vertexID){
	
	return asfloat( _XFurOldVertexBuffer.Load3(( vertexID * _XFurVertexBufferStridePosNormal.x +  _XFurVertexBufferStridePosNormal.z )));
}

float4 GetOldTangent( uint vertexID){
	
	return asfloat( _XFurOldVertexBuffer.Load4(( vertexID * _XFurVertexBufferStridePosNormal.x + _XFurVertexBufferStridePosNormal.w )));
}




#endif

float3 CalculateHighlights(float3 worldPos, float3 viewDir, float3 normal, float3 albedo, float occlusion, float smoothness, float3 anisoTan, float anisoOffsetA, float anisoOffsetB, float3 lightDir, float3 attenuatedLightColor ) {
	float3 specFinal = 0;

	viewDir = normalize(viewDir);

	float3 h = normalize( lightDir + viewDir);
	float NdotL = saturate(dot( normal, lightDir));
	float HdotA = dot(normalize(normal + anisoTan), h);
	float aniso = max(0, sin(radians((HdotA + anisoOffsetA) * 180)));

	specFinal = pow(aniso, 128 * pow(smoothness, 5)) * 3 * pow( smoothness, 3 ) * lerp( _XFurSelfSpecularTint.xyz, saturate( albedo * _XFurSelfSpecularTint.xyz * 1.5 ), 0.35);

	aniso = max(0, sin(radians((HdotA + anisoOffsetB) * 180)));

	specFinal += pow(aniso, 64 * pow(smoothness, 5)) * 1.5 * pow( smoothness, 3 ) * lerp( _XFurSelfSpecularTint.xyz, saturate( albedo * _XFurSelfSpecularTint.xyz * 1.5 ), 0.75);

	specFinal *= attenuatedLightColor * NdotL;

	specFinal = saturate(specFinal) * lerp( 1, 3, smoothness ) * occlusion;

	return specFinal;

}




float3 CalculateTranslucency(float3 worldPos, float3 viewDir, float3 normal, float3 albedo, float occlusion, float3 lightDir, float3 attenuatedLightColor, float fakePoints = 0 ) {

	float3 finalLight = 0;

	viewDir = normalize(viewDir);

	float rim = 1.0 - saturate(dot( viewDir, normal));

	finalLight = saturate( pow( abs( lerp( albedo, half3(0.75, 0.6, 0.4), 0.8 ) ), 2.5 ) * pow( abs( rim ), 5 )* max( pow( abs( occlusion ), 2), 0.15) * saturate(dot( normal, -lightDir ) ) );

	return finalLight * 3 * attenuatedLightColor * _XFurSelfTransmission;

}




void XFurLighting_half(float3 worldPos, float3 viewDir, float3 normal, float3 anisoTan, float anisoOffsetA, float anisoOffsetB, float3 albedo, float occlusion, float smoothness, float3 furData, out float3 finalColor ) {

	
#if SHADERGRAPH_PREVIEW

	finalColor = 0;

#else
	finalColor = 0;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
	float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(worldPos));
	#else
	float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
	#endif
	   
    half4 shadowmask;

    #ifdef SHADERGRAPH_PREVIEW
		shadowmask = half4(1,1,1,1);
	#else
		OUTPUT_LIGHTMAP_UV(lightmapUV, unity_LightmapST, lightmapUV);
		shadowmask = SAMPLE_SHADOWMASK(lightmapUV);
	#endif
	
	smoothness = 1-smoothness;

	Light light;
	float4 screenPos = ComputeScreenPos(TransformWorldToHClip(worldPos));
    InputData inputData = (InputData) 0;
    inputData.normalizedScreenSpaceUV = screenPos.xy/screenPos.w;
    inputData.positionWS = worldPos;

    half shadowAtten = MainLightShadow(shadowCoord, worldPos, shadowmask, _MainLightOcclusionProbes);

    uint numAdditionalLights = GetAdditionalLightsCount();

	#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
	#endif

    #ifdef _FORWARD_PLUS
    
    
    light = GetMainLight( shadowCoord, worldPos, 1 );
	

	light.color *= light.distanceAttenuation * light.shadowAttenuation;
	finalColor = CalculateHighlights(worldPos, viewDir, normal, albedo, occlusion, smoothness, anisoTan, anisoOffsetA, anisoOffsetB, normalize(light.direction), light.color);
	finalColor += CalculateTranslucency(worldPos, viewDir, normal, albedo, occlusion, light.direction, light.color );
	
	

	for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
    
        Light light = GetAdditionalLight(lightIndex, worldPos, shadowmask);

    #ifdef _LIGHT_LAYERS
		if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
	#endif
        {
        light.color *= light.shadowAttenuation * light.distanceAttenuation;
		finalColor += CalculateHighlights(worldPos, viewDir, normal, albedo, occlusion, smoothness, anisoTan, anisoOffsetA, anisoOffsetB, normalize(light.direction), light.color);
        }
	}
	

    LIGHT_LOOP_BEGIN(numAdditionalLights)
    
        light = GetAdditionalLight(lightIndex, worldPos, shadowmask);

    #ifdef _LIGHT_LAYERS
		if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
	#endif
        {
        light.color *= light.shadowAttenuation * light.distanceAttenuation;
        finalColor += CalculateHighlights(worldPos, viewDir, normal, albedo, occlusion, smoothness, anisoTan, anisoOffsetA, anisoOffsetB, normalize(light.direction), light.color);
        }
    LIGHT_LOOP_END

    #else           
        light = GetMainLight( );
		light.color *= light.distanceAttenuation * light.shadowAttenuation;
        finalColor += CalculateHighlights(worldPos, viewDir, normal, albedo, occlusion, smoothness, anisoTan, anisoOffsetA, anisoOffsetB, normalize(light.direction), light.color);


        for (uint lightI = 0; lightI < numAdditionalLights; lightI++) {
           light = GetAdditionalLight(lightI, worldPos, 1);
           light.color *= light.shadowAttenuation * light.distanceAttenuation;
           finalColor += CalculateHighlights(worldPos, viewDir, normal, albedo, occlusion, smoothness, anisoTan, anisoOffsetA, anisoOffsetB, normalize(light.direction), light.color);
        }

    #endif

	finalColor = saturate(finalColor) * furData.r * furData.g;

#endif

}




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
	float4x4 BW_vx;

	//uv transformed into triangular grid space with UV scaled by approximation of 2*sqrt(3)
	float2 skewUV = mul(float2x2 (1.0, 0.0, -0.57735027, 1.15470054), UV * 3.464);

	//vertex IDs and barycentric coords
	float2 vxID = float2 (floor(skewUV));
	float3 barry = float3 (frac(skewUV), 0);
	barry.z = 1.0 - barry.x - barry.y;

	BW_vx = ((barry.z > 0) ?
		float4x4(float4(vxID, 0, 0), float4(vxID + float2(0, 1), 0, 0), float4(vxID + float2(1, 0), 0, 0), float4( barry.zyx, 0 ) ) :
		float4x4(float4(vxID + float2 (1, 1), 0, 0), float4(vxID + float2 (1, 0), 0, 0), float4(vxID + float2 (0, 1), 0, 0), float4(-barry.z, 1.0 - barry.y, 1.0 - barry.x, 0)));

	//calculate derivatives to avoid triangular grid artifacts
	float2 dx = ddx(UV);
	float2 dy = ddy(UV);

	//blend samples with calculated weights
	return mul(tex2D(tex, UV + hash2D2D(BW_vx[0].xy), dx, dy), BW_vx[3].x) +
		mul(tex2D(tex, UV + hash2D2D(BW_vx[1].xy), dx, dy), BW_vx[3].y) +
		mul(tex2D(tex, UV + hash2D2D(BW_vx[2].xy), dx, dy), BW_vx[3].z);
}


float3 tangentNormal(float3 normal, float4 tangent, float3 worldNormal, float3 worldPos) {

	float3 worldTangent = tangent.xyz;
	float3 worldBinormal = cross(worldNormal, worldTangent);

	float4 TtoW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x);
	float4 TtoW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y);
	float4 TtoW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);

	return float3(dot(TtoW0.xyz, normal), dot(TtoW1.xyz, normal), dot(TtoW2.xyz, normal));

}


void BasicShellVert_float( float3 vertexPos, float3 wPos, float3 vertexNormal, float4 vertexTangent, float3 worldNormal, float4 texcoord1, float4 furData, float4 groomData, float4 physicsData, float4 vfxData, float4 profilesSplat, out float3 finalPos, out float3 finalNormal, out float4 finalTangent, out float3 finalDir, out float3 motionVector ) {
#if SHADERGRAPH_PREVIEW
	finalPos = vertexPos;
	finalDir = vertexPos;
	finalNormal = vertexNormal;
	finalTangent = vertexTangent;
	motionVector = 0;
#endif

	//o.xfurUVs = float4(v.texcoord.xy, texcoord1.xy);

#ifdef INSTANCING_ON
	uint fPass = UNITY_ACCESS_INSTANCED_PROP(XFurProps, _XFurInstancedCurrentPass);
	fPass = lerp(fPass, _XFurCurrentPass, _XFurForceNonInstanced);
#else
	uint fPass = _XFurCurrentPass;
#endif



	float3 skinnedMotion = 0;
	groomData.xyz = groomData.xyz * 2.0 - 1.0;
	groomData *= _XFurSelfGroomStrength;

#if USE_VERTEXBUFFER	

		vertexPos = GetPosition( texcoord1.z );
		
		skinnedMotion = GetOldPosition( texcoord1.z );

		skinnedMotion = skinnedMotion - vertexPos;

		vertexPos = GetOldPosition( texcoord1.z );

		wPos = mul( unity_ObjectToWorld, float4( vertexPos, 1 ) );

		vertexNormal = normalize( GetOldNormal( texcoord1.z ));

		worldNormal = mul( unity_ObjectToWorld, float4( vertexNormal, 0 )) ;

		vertexTangent = GetOldTangent( texcoord1.z );	
	
#endif


	motionVector = vertexPos;
	

#if FURPROFILES_BLENDED

	half baseLength = _XFurSelfLength * (1 - saturate(length(profilesSplat)));
	half redLength = _XFurLength[0] * profilesSplat.r * (1 - saturate(length(profilesSplat.gba)));
	half greenLength = _XFurLength[1] * profilesSplat.g * (1 - saturate(length(profilesSplat.ba)));
	half blueLength = _XFurLength[2] * profilesSplat.b * (1 - saturate(length(profilesSplat.a)));
	half alphaLength = _XFurLength[3] * profilesSplat.a;

	half totalLength = (baseLength + redLength + greenLength + blueLength + alphaLength) * furData.g;
#else			
	half totalLength = _XFurSelfLength * furData.g;
#endif


	float3 windSim = float3(0, 0, 0);
	float windFreq = sin(8 * ((wPos.x % 1) + (wPos.z % 1)) + _Time.y * _XFurWindDirectionFreq.w);

	windSim = _XFurWindDirectionFreq.xyz * 2;

	windSim.x += lerp(dot(vertexNormal, float3(0, 1, 0)), 1, abs(_XFurWindDirectionFreq.x)) * lerp(windFreq, sign(_XFurWindDirectionFreq.x) * saturate(abs(windFreq)), abs(_XFurWindDirectionFreq.x));
	windSim.y += lerp(dot(vertexNormal, float3(1, 0, 0)), 1, abs(_XFurWindDirectionFreq.y)) * lerp(windFreq, sign(_XFurWindDirectionFreq.y) * saturate(abs(windFreq)), abs(_XFurWindDirectionFreq.y));
	windSim.z += lerp(dot(vertexNormal, float3(0, 0, 1)), 1, abs(_XFurWindDirectionFreq.z)) * lerp(windFreq, sign(_XFurWindDirectionFreq.z) * saturate(abs(windFreq)), abs(_XFurWindDirectionFreq.z));


	windSim *= _XFurSelfWindStrength * _XFurWindStrength * pow( abs( (totalLength / _XFurTotalPasses) * (1 + fPass) ), 1.5);


	float3 furDir = lerp( float3(0,0,0), tangentNormal(groomData.xyz, vertexTangent, vertexNormal, mul(unity_ObjectToWorld, float4( vertexPos,1) ).xyz), _XFurHasGroomData);
	
	

	windSim *= 1 - saturate(dot(normalize(furDir), windSim));

	furDir += mul(UNITY_MATRIX_I_M, float4( windSim.xyz, 0 ) ).xyz * _XFurWindStrength;	
	
	finalDir = vertexPos;

	motionVector = 0;	

	vertexPos += 0.5 * normalize(vertexNormal + furDir ) * (totalLength / _XFurTotalPasses) * (1 + fPass);

	finalDir = normalize( vertexPos - finalDir );

	motionVector = 0; 

	finalPos = vertexPos;
	finalNormal = vertexNormal;
	finalTangent = vertexTangent;

}






void ShellSurfacePass_float(float4 furColor, float4 furData, float2 furUV, float4 colorVariation, float4 vfxMap, float3 normal, float3 viewDir, float4 profilesSplat, out float3 albedo, out float metallic, out float smoothness, out float occlusion, out float alpha )
{


#ifdef INSTANCING_ON
	uint fPass = UNITY_ACCESS_INSTANCED_PROP(XFurProps, _XFurInstancedCurrentPass) + 1;
	fPass = lerp(fPass, _XFurCurrentPass, _XFurForceNonInstanced) + 1;
#else
	uint fPass = _XFurCurrentPass + 1;
#endif



#if FEATURESET_MOBILE

	half4 mixedColors = half4(0, 0, 0, 0);

#else


	half4 rColor = _XFurSelfColorA * colorVariation.r;
	half4 gColor = _XFurSelfColorB * saturate(colorVariation.g * (1 - colorVariation.r));
	half4 bColor = _XFurSelfColorC * saturate(colorVariation.b * (1 - colorVariation.r - colorVariation.g));
	half4 aColor = _XFurSelfColorD * saturate(colorVariation.a * (1 - colorVariation.r - colorVariation.g - colorVariation.b));

	half4 mixedColors = rColor + gColor + bColor + aColor;

#endif

	float2 curl = float2(sin((fPass / _XFurTotalPasses) * 16 * _XFurSelfCurlAmountX) * abs(furUV.x) * (_XFurSelfCurlSizeX / (_XFurUVTiling)), sin((fPass / _XFurTotalPasses) * 16 * _XFurSelfCurlAmountY) * abs(furUV.y) * (_XFurSelfCurlSizeY / (_XFurUVTiling)));

	half4 furStrands = tex2DStochastic(_XFurSelfStrandsPattern, _XFurUVTiling * (furUV + curl));

	half4 legacyColorV = furColor * (1 - saturate(colorVariation.r + colorVariation.g + colorVariation.b + colorVariation.a)) + furColor * mixedColors;

	half4 newColorV = furColor * lerp( _XFurSelfColorA, _XFurSelfColorB , colorVariation.r );

	furColor = lerp( newColorV, legacyColorV, _XFurLegacyVariation );

	half underOver = saturate( ceil( furStrands.g * 2 ) );
	half furClip = lerp( furStrands.r, furStrands.g, underOver);



#if FURPROFILES_BLENDED

	half baseThickness = _XFurSelfThickness * (1 - saturate(length(profilesSplat)));
	half redThickness = _XFurThickness[0] * profilesSplat.r * (1 - saturate(length(profilesSplat.gba)));
	half greenThickness = _XFurThickness[1] * profilesSplat.g * (1 - saturate(length(profilesSplat.ba)));
	half blueThickness = _XFurThickness[2] * profilesSplat.b * (1 - saturate(length(profilesSplat.a)));
	half alphaThickness = _XFurThickness[3] * profilesSplat.a;

	half totalThickness = (baseThickness + redThickness + greenThickness + blueThickness + alphaThickness) * furData.a * (1 + 0.25 * vfxMap.g * (1 - vfxMap.r));

	half baseThicknessC = _XFurSelfThicknessCurve * (1 - saturate(length(profilesSplat)));
	half redThicknessC = _XFurThicknessCurve[0] * profilesSplat.r * (1 - saturate(length(profilesSplat.gba)));
	half greenThicknessC = _XFurThicknessCurve[1] * profilesSplat.g * (1 - saturate(length(profilesSplat.ba)));
	half blueThicknessC = _XFurThicknessCurve[2] * profilesSplat.b * (1 - saturate(length(profilesSplat.a)));
	half alphaThicknessC = _XFurThicknessCurve[3] * profilesSplat.a;

	half thicknessCurve = (baseThicknessC + redThicknessC + greenThicknessC + blueThicknessC + alphaThicknessC);

	thicknessCurve = pow( abs( furClip ), lerp(8, 2, totalThickness) * pow( abs( fPass / _XFurTotalPasses ), 8 * thicknessCurve));
#else
	half totalThickness = _XFurSelfThickness * furData.a * (1 + 0.25 * vfxMap.g * (1 - vfxMap.r));

	half thicknessCurve = pow( abs( furClip ), lerp(8, 2, totalThickness) * pow( abs( fPass / _XFurTotalPasses ), 8 * _XFurSelfThicknessCurve));
#endif

	alpha = furData.r * thicknessCurve - lerp(0.05, 0.025, underOver) * _XFurSelfLength * (fPass / _XFurTotalPasses);

	int mod = ceil(_XFurTotalPasses / _XFurLODStrength);

	float aValue = fPass % mod < 0.1 ? 1 : 0;

	aValue = lerp(aValue, 1, pow( abs( 1.0 - saturate(dot(normalize(viewDir), normal)) ), _XFurLODArea));


		clip(alpha - 0.005);
		half4 occlusionColor = half4(0, 0, 0, 0);

#if FURPROFILES_BLENDED

		half furIndex = profilesSplat.g + (2 * profilesSplat.b - profilesSplat.g) + (3 * profilesSplat.a - 2 * profilesSplat.b - profilesSplat.g);

		half baseOcclusion = _XFurSelfOcclusion * (1 - saturate(length(profilesSplat)));
		half redOcclusion = _XFurOcclusion[0] * profilesSplat.r * (1 - saturate(length(profilesSplat.gba)));
		half greenOcclusion = _XFurOcclusion[1] * profilesSplat.g * (1 - saturate(length(profilesSplat.ba)));
		half blueOcclusion = _XFurOcclusion[2] * profilesSplat.b * (1 - saturate(length(profilesSplat.a)));
		half alphaOcclusion = _XFurOcclusion[3] * profilesSplat.a;

		half totalOcclusion = (baseOcclusion + redOcclusion + greenOcclusion + blueOcclusion + alphaOcclusion) * furData.b;

		half baseOcclusionC = _XFurSelfOcclusionCurve * (1 - saturate(length(profilesSplat)));
		half redOcclusionC = _XFurOcclusionCurve[0] * profilesSplat.r * (1 - saturate(length(profilesSplat.gba)));
		half greenOcclusionC = _XFurOcclusionCurve[1] * profilesSplat.g * (1 - saturate(length(profilesSplat.ba)));
		half blueOcclusionC = _XFurOcclusionCurve[2] * profilesSplat.b * (1 - saturate(length(profilesSplat.a)));
		half alphaOcclusionC = _XFurOcclusionCurve[3] * profilesSplat.a;

		half occlusionCurve = (baseOcclusionC + redOcclusionC + greenOcclusionC + blueOcclusionC + alphaOcclusionC);


		half furOcclusion = lerp(1, (pow( abs(fPass / _XFurTotalPasses), 12 * occlusionCurve)), totalOcclusion * furData.b);
		occlusionColor = lerp(_XFurSelfShadowsTint, _XFurShadowsTint[furIndex], length(profilesSplat));
#else
		half furOcclusion = lerp(1, (pow( abs(fPass / _XFurTotalPasses), 12 * _XFurSelfOcclusionCurve)), _XFurSelfOcclusion * furData.b);
		occlusionColor = _XFurSelfShadowsTint;
#endif


		occlusion = furOcclusion;


		half4 fColor = lerp( occlusionColor, furColor * lerp( _XFurSelfColorD * _XFurSelfUnderColorMod, _XFurSelfColorC * _XFurSelfOverColorMod, underOver), saturate( furOcclusion + (furStrands.b) ) );

		half4 oldFColor = lerp(occlusionColor, furColor * lerp( _XFurSelfUnderColorMod, _XFurSelfOverColorMod, underOver), furOcclusion);

		fColor = lerp( fColor, oldFColor, _XFurLegacyVariation );

		half4 blood = lerp(_XFurVFX1Color * 0.75 * fColor, saturate(_XFurVFX1Color * 1.5 * fColor), vfxMap.r) * saturate(occlusion * 2) * vfxMap.r * saturate(lerp(1, (pow( abs(fPass / _XFurTotalPasses), 12 * _XFurVFX1Penetration)), 1) * 2);

		half4 snow = _XFurVFX2Color * saturate(lerp(1, (pow( abs(fPass / _XFurTotalPasses), 12 * _XFurVFX2Penetration)), 1) * 2) * vfxMap.g * (1 - vfxMap.r);

		half4 fxColor = blood + snow;

		metallic = max( blood.r * 0.25, vfxMap.b * 0.5);

		smoothness = _XFurSelfSmoothness;

		smoothness = lerp( smoothness, (1-_XFurVFX1Smoothness), blood.r );
		smoothness = lerp( smoothness, vfxMap.b * (1-_XFurVFX3Smoothness), vfxMap.b );

		smoothness *= lerp( occlusion * saturate( pow( furClip * 4, 2 )), 1, _XFurURPRenderingMode );

		fColor *= lerp(1, 0.45, vfxMap.b * saturate(lerp(1, (pow( abs(fPass / _XFurTotalPasses), 2 * _XFurVFX3Penetration)), 1) * 4));

		albedo = lerp( fColor.xyz, fxColor.xyz, saturate(vfxMap.r + vfxMap.g ));


		half rim = abs( 1 - saturate(dot(normalize(viewDir), normal)) );
		albedo += lerp( _XFurSelfRimColor.xyz * saturate( lerp( fColor * 2, 1, saturate(_XFurSelfRimBoost - 1) * 0.65) ).xyz, saturate(albedo * 2), 0.25) * _XFurSelfRimBoost * pow(rim, _XFurSelfRimPower);
		albedo = saturate(albedo);

	
}
#endif