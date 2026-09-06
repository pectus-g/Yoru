import re
p="Assets/Scenes 1/CaveScene_Oni_Boss1.unity"
s=open(p,encoding="utf-8",errors="replace").read()
objs={}
for b in re.split(r'\n--- !u!',s)[1:]:
    m=re.match(r'(\d+) &(-?\d+)',b)
    if m: objs[m.group(2)]=(m.group(1),b)
def field(b,k):
    m=re.search(r'\n  '+re.escape(k)+r': (.*)',b); return m.group(1).strip() if m else None
def fid(v):
    m=re.search(r'fileID: (-?\d+)',v or ''); return m.group(1) if m else None
def go_of_transform(tid):
    if tid in objs: return fid(field(objs[tid][1],'m_GameObject'))
    return None
def transform_of_go(goid):
    b=objs[goid][1]
    for c in re.findall(r'component: \{fileID: (-?\d+)\}',b):
        if c in objs and objs[c][0] in ('4','224'): return c
    return None
def chain(goid):
    out=[];cur=goid;n=0
    while cur in objs and n<40:
        n+=1;cls,b=objs[cur]
        out.append((field(b,'m_Name'),field(b,'m_IsActive')))
        tr=transform_of_go(cur)
        if not tr: out.append(('<no transform>',None)); break
        f=fid(field(objs[tr][1],'m_Father'))
        if not f or f=='0': break
        if f not in objs:
            out.append(('<parent in prefab instance %s>'%f,None)); break
        cur=go_of_transform(f)
    return out
SH=re.compile(r'm_Shadows:\n    m_Type: (\d)')
print("=== Yoru_Dim_light and friends")
for oid,(cls,b) in objs.items():
    if cls=='1' and field(b,'m_Name') in ('Yoru_Dim_light','PlayerYoru_1.1','YoruRim','Environment','GameObject'):
        print(oid, field(b,'m_Name'), 'active=',field(b,'m_IsActive'))
print("=== scene-level Light components with active chains and render mode (0 Auto,1 Important,2 NotImportant)")
for oid,(cls,b) in objs.items():
    if cls=='108':
        go=fid(field(b,'m_GameObject'))
        ch=chain(go) if go in objs else [('<stripped GO>',None)]
        allon = all(a in ('1',None) for _,a in ch)
        m=SH.search(b); sh=m.group(1) if m else '?'
        chs=' < '.join('%s(%s)'%(n,a) for n,a in ch)
        print("%-18s enabled=%s chainActive=%s int=%s range=%s renderMode=%s shadows=%s cookie=%s  chain=%s"%(ch[0][0],field(b,'m_Enabled'),'YES' if allon else 'NO',field(b,'m_Intensity'),field(b,'m_Range'),field(b,'m_RenderMode'),sh,field(b,'m_Cookie'),chs))
print("=== PrefabInstance light-related overrides (Brazier / lantern)")
for oid,(cls,b) in objs.items():
    if cls=='1001':
        nm=re.search(r"propertyPath: m_Name\n\s+value: (.*)",b)
        nm=nm.group(1) if nm else '?'
        if re.search(r'Brazier|Lantern|lantern',nm):
            mods=re.findall(r"propertyPath: (m_Intensity|m_Range|m_RenderMode|m_IsActive|m_Enabled|cullDistance|fadeDistance)\n\s+value: ([^\n]*)",b)
            par=fid(re.search(r'm_TransformParent: \{fileID: (-?\d+)\}',b).group(0))
            parname=None
            if par in objs and go_of_transform(par): parname=field(objs[go_of_transform(par)][1],'m_Name')
            print(oid,nm,'parent=',parname,'mods=',mods[:12])
