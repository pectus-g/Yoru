import re,math,datetime,os
H=os.environ["HOME"]; ROOT=os.path.join(H,"mnt/Yoru")
t=open(os.path.join(ROOT,"Assets/Scenes 1/CaveScene_Oni_Boss1.unity"),encoding="utf-8",errors="replace").read()
docs=re.split(r"\n--- ",t); objs={}
for d in docs[1:]:
    m=re.match(r"!u!(\d+) &(\d+)(\s+stripped)?",d)
    if m: objs[m.group(2)]=(m.group(1),d,bool(m.group(3)))
def g1(p,b,d="?"):
    m=re.search(p,b); return m.group(1) if m else d
def vec(s):
    m=re.search(r"x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)",s); return [float(m.group(i)) for i in (1,2,3)] if m else [0,0,0]
def quat(s):
    m=re.search(r"x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+), w: ([-\d.e]+)",s); return [float(m.group(i)) for i in (1,2,3,4)] if m else [0,0,0,1]
def rot(q,v):
    x,y,z,w=q
    def cross(a,b): return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
    c=cross([x,y,z],v); c=[c[0]+w*v[0],c[1]+w*v[1],c[2]+w*v[2]]; c2=cross([x,y,z],c)
    return [v[0]+2*c2[0],v[1]+2*c2[1],v[2]+2*c2[2]]
go={fid:g1(r"m_Name: (.*)",b) for fid,(c,b,s) in objs.items() if c=="1" and not s}
tr={fid:dict(father=g1(r"m_Father: \{fileID: (\d+)\}",b,"0"),pos=vec(g1(r"m_LocalPosition: (.*)",b,"")),rot=quat(g1(r"m_LocalRotation: (.*)",b,"")),scale=vec(g1(r"m_LocalScale: (.*)",b,"x: 1, y: 1, z: 1")),go=g1(r"m_GameObject: \{fileID: (\d+)\}",b),stripped=s,inst=g1(r"m_PrefabInstance: \{fileID: (\d+)\}",b,"0")) for fid,(c,b,s) in objs.items() if c=="4"}
inst={}
for fid,(c,b,s) in objs.items():
    if c=="1001":
        pos={k:float(v) for k,v in re.findall(r"propertyPath: m_LocalPosition\.([xyz])\s*\n\s*value: (\S*)",b)}
        rq={k:float(v) for k,v in re.findall(r"propertyPath: m_LocalRotation\.([xyzw])\s*\n\s*value: (\S*)",b)}
        sc={k:float(v) for k,v in re.findall(r"propertyPath: m_LocalScale\.([xyz])\s*\n\s*value: (\S*)",b)}
        inst[fid]=dict(name=g1(r"propertyPath: m_Name[\s\S]{0,90}?value: (.*)",b,"").strip(),parent=g1(r"m_TransformParent: \{fileID: (\d+)\}",b,"0"),pos=[pos.get("x",0),pos.get("y",0),pos.get("z",0)],rot=[rq.get("x",0),rq.get("y",0),rq.get("z",0),rq.get("w",1)],scale=[sc.get("x",1),sc.get("y",1),sc.get("z",1)])
def compose(parent_tid, local):
    d=tr[parent_tid]
    if d["stripped"] and d["inst"] in inst and inst[d["inst"]]["parent"]==d["father"]:
        I=inst[d["inst"]]; s=I["scale"]; v=rot(I["rot"],[local[0]*s[0],local[1]*s[1],local[2]*s[2]]); v=[v[0]+I["pos"][0],v[1]+I["pos"][1],v[2]+I["pos"][2]]
        return v if (I["parent"]=="0" or I["parent"] not in tr) else compose(I["parent"],v)
    s=d["scale"]; v=rot(d["rot"],[local[0]*s[0],local[1]*s[1],local[2]*s[2]]); v=[v[0]+d["pos"][0],v[1]+d["pos"][1],v[2]+d["pos"][2]]
    return v if (d["father"]=="0" or d["father"] not in tr) else compose(d["father"],v)
def world_t(tid):
    d=tr[tid]; return d["pos"] if (d["father"]=="0" or d["father"] not in tr) else compose(d["father"],d["pos"])
def world_i(iid):
    I=inst[iid]; return I["pos"] if (I["parent"]=="0" or I["parent"] not in tr) else compose(I["parent"],I["pos"])
FC=(478.0,433.0)
TY={"0":"Spot","1":"Directional","2":"Point"}; SH={"0":"none","1":"hard","2":"soft"}; RM={"0":"Auto","1":"Important","2":"Not Important"}
def f(b,k): return g1(r"\n  "+k+r": (.*)",b)
rows=[]
for fid,(c,b,s) in objs.items():
    if c!="1" or s: continue
    name=go[fid]; comps=re.findall(r"component: \{fileID: (\d+)\}",b); act=g1(r"m_IsActive: (\d)",b)
    tid=[x for x in comps if x in tr]
    for x in comps:
        if x in objs and objs[x][0]=="108":
            lb=objs[x][1]; sh=re.search(r"m_Shadows:\s*\n\s*m_Type: (\d)",lb); col=re.search(r"m_Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)",lb)
            w=world_t(tid[0]) if tid else [0,0,0]
            chain=[]; cur=tr[tid[0]]["father"] if tid else "0"; paroff=False
            while cur!="0" and cur in tr and len(chain)<4:
                d=tr[cur]; chain.append(go.get(d["go"],inst.get(d["inst"],{}).get("name","?")))
                if d["go"] in objs and g1(r"m_IsActive: (\d)",objs[d["go"]][1])=="0": paroff=True
                cur=d["father"]
            if paroff: act="0"
            rows.append("| `%s` | %s | %s | %s | %s | %s | %s | %s | %s | %s |"%(name," < ".join(chain) if chain else "scene root",TY.get(f(lb,"m_Type"),"?"),"on" if (act=="1" and f(lb,"m_Enabled")=="1") else ("OFF (parent inactive)" if paroff else "OFF"),f(lb,"m_Intensity"),f(lb,"m_Range") if f(lb,"m_Type")!="1" else "-","%d %d %d"%tuple(int(float(col.group(i))*255) for i in (1,2,3)) if col else "?",SH.get(sh.group(1),"?") if sh else "?",RM.get(f(lb,"m_RenderMode"),"?"),"%.0f, %.1f, %.0f"%tuple(w)))
props=[]
for iid,I in inst.items():
    if re.search(r"Brazier|Lantern|Lamp|Torch",I["name"]):
        w=world_i(iid); props.append((math.hypot(w[0]-FC[0],w[2]-FC[1]),"| `%s` | %.1f, %.2f, %.1f | %.0f m |"%(I["name"],w[0],w[1],w[2],math.hypot(w[0]-FC[0],w[2]-FC[1]))))
c255=lambda m:"%d %d %d"%tuple(int(float(m.group(i))*255) for i in (1,2,3))
amb=re.search(r"m_AmbientSkyColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)",t); eq=re.search(r"m_AmbientEquatorColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)",t); gr=re.search(r"m_AmbientGroundColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)",t)
plc=re.search(r"name: Ultra[\s\S]*?pixelLightCount: (\d+)",open(os.path.join(ROOT,"ProjectSettings/QualitySettings.asset")).read()).group(1)
q=open(os.path.join(ROOT,"Assets/Scenes 1/Cave/Cave_Oni_Profile.asset")).read(); i=q.find("m_Name: AutoExposure")
ae="on" if re.search(r"active: (\d)",q[i:i+300]).group(1)=="1" else "off"
stamp=datetime.datetime.utcnow().strftime("%Y-%m-%d %H:%M UTC")
state=("## 8. CURRENT STATE (generated from the scene file, %s)\n\nRegenerated by Claude after each step. If this disagrees with the plan above, the plan is the target and this is reality. Positions are WORLD positions. Fight centre is (478, 433).\n\n"
"Ambient (Lighting window, Trilight): Sky %s / Equator %s / Ground %s. Pixel Light Count (Ultra): %s. Auto Exposure: %s.\n\n"
"| Light | Under | Type | State | Intensity | Range | Colour (RGB) | Shadows | Render Mode | World pos |\n|---|---|---|---|---|---|---|---|---|---|\n")%(stamp,c255(amb),c255(eq),c255(gr),plc,ae)+"\n".join(sorted(rows))+"\n\n| Light-carrying prop | World position (x, y, z) | From fight centre |\n|---|---|---|\n"+"\n".join(r for _,r in sorted(props))+"\n"
P=os.path.join(ROOT,"Handoff/CAVE_LIGHTING_PLAN.md"); s=open(P,encoding="utf-8").read()
k=s.find("## 8. CURRENT STATE"); s=(s[:k] if k>=0 else s+"\n")+state
open(P,"w",encoding="utf-8",newline="").write(s); print("section 8 regenerated,",len(rows),"lights,",len(props),"props; em-dashes:",s.count("—"))
