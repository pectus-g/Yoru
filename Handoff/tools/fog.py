import re,os
H=os.environ["HOME"]
p=os.path.join(H,"mnt/Yoru/Assets/Scenes 1/CaveScene_Oni_Boss1.unity")
txt=open(p,encoding="utf-8",errors="replace").read()
docs=re.split(r"\n--- ",txt)
objs={}
for d in docs[1:]:
    m=re.match(r"!u!(\d+) &(\d+)",d)
    if m: objs[m.group(2)]=(m.group(1),d)
go={}
for fid,(cid,body) in objs.items():
    if cid=="1":
        n=re.search(r"m_Name: (.*)",body)
        go[fid]=(n.group(1).strip() if n else "?", re.findall(r"component: \{fileID: (\d+)\}",body))
for fid,(name,comps) in go.items():
    if name in ("mainCamera (1)","mainCamera","Cozy Weather Sphere"):
        print("#### GameObject:",name)
        for c in comps:
            if c not in objs: continue
            cid,b=objs[c]
            if cid!="114": 
                print("   [class",cid,"]")
                continue
            g=re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]+)",b)
            guid=g.group(1) if g else "?"
            # resolve script name via .meta search later; print all non-boilerplate fields
            print("   --- MonoBehaviour guid",guid)
            for line in b.splitlines():
                s=line.strip()
                if not s or s.startswith("m_ObjectHideFlags") or s.startswith("m_CorrespondingSource") or s.startswith("m_Prefab") or s.startswith("m_GameObject") or s.startswith("m_Enabled: ")==False and s.startswith("m_EditorHideFlags") or s.startswith("m_Name:") or s.startswith("m_EditorClassIdentifier") or s.startswith("m_Script:"):
                    if s.startswith("m_Enabled:"): print("      ",s)
                    continue
                print("      ",s)
