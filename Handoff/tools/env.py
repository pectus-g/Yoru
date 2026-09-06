import re,os
p=os.path.join(os.environ["HOME"],"mnt/Yoru/Assets/Scenes 1/CaveScene_Oni_Boss1.unity")
txt=open(p,encoding="utf-8",errors="replace").read()
docs=re.split(r"\n--- ",txt)
for d in docs[1:]:
    m=re.match(r"!u!(\d+) &(\d+)",d)
    if m and m.group(1)=="104":
        print("=== RenderSettings ===")
        for line in d.splitlines():
            if re.search(r"m_(Fog|AmbientSky|AmbientEquator|AmbientGround|AmbientIntensity|AmbientMode|SkyboxMaterial|Sun|SubtractiveShadowColor|IndirectSpecularColor|ReflectionIntensity|UseRadianceAmbient)",line):
                print("  "+line.strip())
    if m and m.group(1)=="157":
        print("=== LightmapSettings ===")
        for line in d.splitlines():
            if re.search(r"(m_BakeResolution|m_Resolution|m_LightmapsMode|m_MixedBakeMode|m_EnableBakedLightmaps|m_EnableRealtimeLightmaps|m_LightmapEditorSettings|m_AtlasSize|m_Padding|m_LightingDataAsset|m_LightingSettings)",line):
                print("  "+line.strip())
print()
print("=== LightShaft / WarmLight / Cozy names present? ===")
for n in ["LightShaft","WarmLight_Rim","ColdLight_Rim","Cozy","ONI_KEY","mainCamera","Brazier","Torch","Fire","Lamp","Lantern"]:
    c=len(re.findall(r"m_Name: .*"+n,txt))
    print(f"  {n}: {c} gameobjects with that in the name")
