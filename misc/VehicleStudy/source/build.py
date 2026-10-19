"""Four original, exterior-only lowpoly vehicle meshes. No imported assets.

Authoring coordinates: +X front, +Y up, +Z right. GLB: -Z front, +Y up.
"""
import json, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from geometry import Model, box, profile, export_glb, render, font, ROOT, PALETTE


def face(p, vertices, outward):
    a=np.asarray(vertices,float)
    n=np.cross(a[1]-a[0],a[2]-a[0])
    p.face(vertices if np.dot(n,outward)>0 else list(reversed(vertices)))


def surface(m, name, points, mat, outward):
    p=m.part(name,mat); face(p,points,outward); return p


def line(m,name,points,z,mat='charcoal',width=.012):
    p=m.part(name,mat)
    for a,b in zip(points,points[1:]):
        a,b=np.array(a,float),np.array(b,float)
        v=b-a; n=np.array([-v[1],v[0]])/np.linalg.norm(v)*width/2
        face(p,[(q[0],q[1],z) for q in [a+n,a-n,b-n,b+n]],(0,0,np.sign(z)))
    return p


def arch(m,name,x,cy,r,z,paint):
    p=m.part(name,paint); angles=np.linspace(math.pi,0,9)
    for a,b in zip(angles,angles[1:]):
        pts=[(x+rr*math.cos(t),cy+rr*math.sin(t),z)
             for rr,t in [(r,a),(r,b),(r+.045,b),(r+.045,a)]]
        face(p,pts,(0,0,np.sign(z)))
        # The lip has a short return surface into the wheel well.
        rim=[pts[0],pts[1],(pts[1][0],pts[1][1],z-np.sign(z)*.035),(pts[0][0],pts[0][1],z-np.sign(z)*.035)]
        face(p,rim,(-math.cos((a+b)/2),-math.sin((a+b)/2),0))


def hull(m, length, width, top, wheels, radius, paint):
    cy=radius; clearance=radius+.065; half=width/2
    rear,front=-length/2,length/2
    lower=[(rear+.045,cy)]
    for x in wheels:
        lower += [(x+clearance*math.cos(t),cy+clearance*math.sin(t)) for t in np.linspace(math.pi,0,9)]
    lower += [(front-.05,cy),(front,cy+.12)]
    poly=lower+[(x,y-.085) for x,y in reversed(top)]+[(rear,cy+.14)]
    profile(m,'Body_shell',poly,width,paint)
    deck=m.part('Body_upper_facets',paint)
    rings=[[(x,y-.085,-half),(x,y,-half+.09),(x,y,half-.09),(x,y-.085,half)] for x,y in top]
    for a,b in zip(rings,rings[1:]):
        for i in range(3):
            face(deck,[a[i],b[i],b[i+1],a[i+1]],(0,1,0))
    face(deck,rings[0],(-1,0,0)); face(deck,rings[-1],(1,0,0))
    box(m,'Underbody',((wheels[0]+wheels[1])/2,cy+.035,0),(wheels[1]-wheels[0]-.20,.10,width-.28),'charcoal')
    for x in wheels:
        for side in [-1,1]:
            arch(m,'Wheel_arch',x,cy,clearance,side*(half+.014),paint)
            surface(m,'Wheel_well_liner',[(x+clearance*math.cos(t),cy+clearance*math.sin(t),side*(half-.19)) for t in np.linspace(math.pi,0,9)],'charcoal',(0,0,side))


def cylinder(m,name,cx,cy,rings,mat,n=12):
    p=m.part(name,mat)
    rs=[[(cx+r*math.cos(t*math.tau/n),cy+r*math.sin(t*math.tau/n),z) for t in range(n)] for z,r in rings]
    for a,b in zip(rs,rs[1:]):
        for i in range(n):
            j=(i+1)%n
            normal=(math.cos((i+.5)*math.tau/n),math.sin((i+.5)*math.tau/n),0)
            face(p,[a[i],a[j],b[j],b[i]],normal)
    face(p,rs[0],(0,0,-1)); face(p,rs[-1],(0,0,1))
    return p


def wheel(m,group,x,r,z):
    start=len(m.parts); side=np.sign(z); cy=r
    cylinder(m,group+'_Tire',x,cy,[(z-.13,r-.045),(z-.09,r),(z+.09,r),(z+.13,r-.045)],'charcoal')
    # Ivory hub with dark central cap; bevels make the 12-sided tire easy to read.
    outer=z+side*.132
    low,high=sorted([outer,outer+side*.022])
    cylinder(m,group+'_Rim',x,cy,[(low,r*.53),(high,r*.53)],'ivory')
    low,high=sorted([outer+side*.023,outer+side*.030])
    cylinder(m,group+'_Hub',x,cy,[(low,r*.19),(high,r*.19)],'charcoal',n=8)
    # Six dark vent slots share the tire material.
    for i in range(6):
        t=(i+.5)*math.tau/6; rr=r*.37; tang=.024; radial=.030
        center=np.array([x+rr*math.cos(t),cy+rr*math.sin(t)])
        u=np.array([math.cos(t),math.sin(t)])*radial
        v=np.array([-math.sin(t),math.cos(t)])*tang
        surface(m,group+'_Rim_vent',[(q[0],q[1],outer+side*.0235) for q in [center-u-v,center+u-v,center+u+v,center-u+v]],'charcoal',(0,0,side))
    m.groups[group]=(x,cy,z)
    for p in m.parts[start:]:
        p.name += '_'+str(m.parts.index(p))
        m.part_groups[p.name]=group


def running_gear(m,wheel_x,r,width):
    for axle,x in zip(['Rear','Front'],wheel_x):
        for side,z in [('Left',-width/2+.015),('Right',width/2-.015)]:
            wheel(m,'Wheel_'+axle+'_'+side,x,r,z)


def quad_inset(m,name,quad,mat,outward,margin=.09,offset=.004):
    a,b,c,d=[np.asarray(v,float) for v in quad]
    n=np.array(outward,float); n/=np.linalg.norm(n)
    def pt(u,v): return ((1-u)*(1-v)*a+u*(1-v)*b+u*v*c+(1-u)*v*d+n*offset).tolist()
    return surface(m,name,[pt(margin,margin),pt(1-margin,margin),pt(1-margin,1-margin),pt(margin,1-margin)],mat,outward)


def cabin(m,paint,rear,roof_rear,roof_front,front,bottom,roof,half,roofhalf,split=None):
    # Explicit tapered cabin mesh: painted pillars are real geometry behind opaque windows.
    rings={}
    for side in [-1,1]:
        rings[side]=[(rear,bottom,side*half),(front,bottom,side*half),
                     (roof_front,roof,side*roofhalf),(roof_rear,roof,side*roofhalf)]
        surface(m,'Cabin_side',rings[side],paint,(0,0,side))
    a,b=rings[-1],rings[1]
    surface(m,'Cabin_roof',[a[3],a[2],b[2],b[3]],paint,(0,1,0))
    frontquad=[a[1],b[1],b[2],a[2]]
    rearquad=[b[0],a[0],a[3],b[3]]
    surface(m,'Windshield_frame',frontquad,paint,(1,1,0))
    surface(m,'Rear_window_frame',rearquad,paint,(-1,1,0))
    quad_inset(m,'Windshield',frontquad,'glass',(roof-bottom,front-roof_front,0),.075)
    quad_inset(m,'Rear_window',rearquad,'glass',(-(roof-bottom),roof_rear-rear,0),.10)
    yb,yt=bottom+.075,roof-.075
    def boundary(xb,xt,y): return xb+(xt-xb)*(y-bottom)/(roof-bottom)
    leftbottom=boundary(rear,roof_rear,yb)+.07; lefttop=boundary(rear,roof_rear,yt)+.07
    rightbottom=boundary(front,roof_front,yb)-.07; righttop=boundary(front,roof_front,yt)-.07
    for side in [-1,1]:
        def patch(name,xy):
            pts=[(x,y,side*(half+(roofhalf-half)*(y-bottom)/(roof-bottom)+.004)) for x,y in xy]
            surface(m,name,pts,'glass',(0,0,side))
        if split is None:
            patch('Side_window',[(leftbottom,yb),(rightbottom,yb),(righttop,yt),(lefttop,yt)])
        else:
            patch('Quarter_window',[(leftbottom,yb),(split-.035,yb),(split-.035,yt),(lefttop,yt)])
            patch('Front_side_window',[(split+.035,yb),(rightbottom,yb),(righttop,yt),(split+.035,yt)])
    return rings


def trim(m,length,width,y,paint,mirrorx=.5,mirrory=1.13,tall_tail=False):
    front=length/2; rear=-length/2; half=width/2
    # Simplified front grille, square lamps and matching rear fascia.
    box(m,'Front_grille',(front+.013,y-.18,0),(.038,.17,width*.82),'charcoal')
    for z in [-width*.33,width*.33]:
        box(m,'Headlight',(front+.038,y-.17,z),(.045,.125,width*.19),'ivory')
    for h in [-.045,.015]: box(m,'Grille_bar',(front+.036,y-.17+h,0),(.012,.012,width*.34),'ivory')
    box(m,'Front_bumper',(front+.025,.46,0),(.13,.13,width+.035),'charcoal')
    box(m,'Front_plate',(front+.096,.47,0),(.009,.08,.29),'ivory')
    box(m,'Rear_bumper',(rear-.032,.46,0),(.13,.13,width+.035),'charcoal')
    box(m,'Rear_plate',(rear-.102,.48,0),(.01,.08,.29),'ivory')
    for z in [-width*.36,width*.36]:
        box(m,'Tail_light_surround',(rear-.018,y-.18,z),(.04,.19 if tall_tail else .14,.19 if tall_tail else .25),'charcoal')
        box(m,'Tail_light',(rear-.041,y-.17,z),(.013,.14 if tall_tail else .09,.14 if tall_tail else .19),'terracotta')
    for side in [-1,1]:
        box(m,'Mirror_stalk',(mirrorx,mirrory,side*(half-.02)),(.085,.055,.34),'charcoal')
        box(m,'Mirror_housing',(mirrorx,mirrory+.035,side*(half+.15)),(.18,.115,.115),paint)
        box(m,'Mirror_glass',(mirrorx-.093,mirrory+.035,side*(half+.15)),(.006,.077,.08),'glass')


def handles(m,xs,y,width):
    for z in [-width/2-.012,width/2+.012]:
        for x in xs: box(m,'Door_handle',(x,y,z),(.16,.03,.025),'ivory')


def finish(m,length,width):
    m.markers={'GroundOrigin':(0,0,0),'FrontSocket':(length/2, .60,0),'RearSocket':(-length/2,.60,0)}
    return m


def sedan():
    m=Model('sedan','SEDAN','四门轿车 / 陶土红'); paint='terracotta'
    L,W,R=4.22,1.76,.355; wheels=[-1.32,1.30]
    hull(m,L,W,[(-2.11,.88),(-1.94,1.00),(-.96,1.04),(.94,1.04),(1.90,.97),(2.11,.90)],wheels,R,paint)
    cabin(m,paint,-1.08,-.66,.38,.99,1.035,1.57,.77,.65,split=-.14)
    for z in [-W/2-.015,W/2+.015]:
        line(m,'Door_seam',[(-.14,.44),(-.14,1.03)],z,width=.009)
        line(m,'Front_door_edge',[(.84,.56),(.84,.91),(.95,1.01)],z,width=.009)
        box(m,'Sill_trim',(-.04,.43,z),(1.62,.055,.023),'charcoal')
    handles(m,[-.75,.02],.93,W)
    trim(m,L,W,.95,paint,.77,1.075)
    running_gear(m,wheels,R,W)
    return finish(m,L,W)


def hatchback():
    m=Model('hatchback','HATCHBACK','短尾掀背车 / 鼠尾草绿');paint='sage'
    L,W,R=3.48,1.65,.34;wheels=[-1.04,1.03]
    hull(m,L,W,[(-1.74,.91),(-1.60,1.04),(.67,1.04),(1.57,.95),(1.74,.89)],wheels,R,paint)
    cabin(m,paint,-1.53,-1.15,.21,.82,1.035,1.61,.735,.65,split=-.63)
    box(m,'Rear_roof_spoiler',(-1.16,1.61,0),(.20,.065,1.38),paint)
    for z in [-W/2-.015,W/2+.015]:
        line(m,'Door_rear_edge',[(-.62,.66),(-.62,1.035)],z,width=.010)
        box(m,'Sill_trim',(0,.41,z),(1.12,.06,.026),'charcoal')
    handles(m,[-.38],.925,W)
    trim(m,L,W,.95,paint,.63,1.075,tall_tail=True)
    box(m,'Hatch_handle',(-1.747,1.00,0),(.014,.035,.22),'charcoal')
    running_gear(m,wheels,R,W)
    return finish(m,L,W)


def pickup():
    m=Model('pickup','PICKUP','开放货斗皮卡 / 沙黄色');paint='ochre'
    L,W,R=4.66,1.87,.40;wheels=[-1.46,1.47]
    hull(m,L,W,[(-2.33,.99),(-.34,.99),(-.20,1.08),(1.38,1.08),(2.19,1.01),(2.33,.94)],wheels,R,paint)
    cabin(m,paint,-.33,-.26,.67,1.30,1.075,1.85,.83,.72)
    # Open bed: recessed floor, thick side rails and tailgate, no hidden lid.
    box(m,'Cargo_bed_floor',(-1.30,1.005,0),(1.83,.035,1.63),'charcoal')
    for z in [-.88,.88]:
        profile(m,'Bed_side_wall',[(-2.32,.98),(-2.32,1.34),(-.40,1.34),(-.40,.98)],.12,paint,z)
        box(m,'Bed_rail',(-1.36,1.345,z),(1.98,.045,.16),'charcoal')
    box(m,'Tailgate',(-2.28,1.16,0),(.11,.35,1.77),paint)
    box(m,'Bed_front_wall',(-.40,1.16,0),(.09,.35,1.77),paint)
    for z in [-.55,-.275,0,.275,.55]: box(m,'Bed_floor_rib',(-1.34,1.028,z),(1.65,.026,.035),paint)
    box(m,'Tailgate_handle',(-2.344,1.24,0),(.015,.045,.23),'charcoal')
    for z in [-W/2-.016,W/2+.016]:
        line(m,'Door_seam',[(-.25,.62),(-.25,1.06)],z,width=.012)
        box(m,'Cab_step',(.36,.43,z),(.93,.10,.14),'charcoal')
    handles(m,[-.09],1.005,W)
    trim(m,L,W,1.00,paint,1.05,1.16,tall_tail=True)
    running_gear(m,wheels,R,W)
    return finish(m,L,W)


def van():
    m=Model('van','VAN','高顶厢式货车 / 灰蓝色');paint='slate'
    L,W,R=4.32,1.84,.37;wheels=[-1.36,1.34]
    hull(m,L,W,[(-2.16,1.02),(-2.06,1.09),(1.90,1.09),(2.16,.98)],wheels,R,paint)
    # Full height closed cargo box plus short, raked windshield at the nose.
    cabin(m,paint,-2.09,-2.04,1.37,2.05,1.085,2.16,.845,.78)
    # Replace full-length side windows with a small cab window, leaving solid cargo panels.
    m.parts=[p for p in m.parts if p.name!='Side_window']
    for side in [-1,1]:
        pts=[]
        for x,y in [(.63,1.18),(1.91,1.18),(1.32,2.04),(.63,2.04)]:
            z=side*(.845+(.78-.845)*(y-1.085)/(2.16-1.085)+.004)
            pts.append((x,y,z))
        surface(m,'Cab_window',pts,'glass',(0,0,side))
        # Cargo-panel seams follow the tapered wall; dark line width remains understated.
        cargo=m.part('Sliding_cargo_door_seam','charcoal')
        outline=[(-1.84,1.13),(-1.84,2.03),(.44,2.03),(.44,1.13),(-1.84,1.13)]
        for a,b in zip(outline,outline[1:]):
            a,b=np.array(a),np.array(b);v=b-a;n=np.array([-v[1],v[0]])/np.linalg.norm(v)*.006
            verts=[]
            for x,y in [a+n,a-n,b-n,b+n]:
                z=side*(.845+(.78-.845)*(y-1.085)/(2.16-1.085)+.006)
                verts.append((x,y,z))
            face(cargo,verts,(0,0,side))
        box(m,'Sliding_door_handle',(.27,1.42,side*.838),(.045,.16,.028),'charcoal')
        box(m,'Cab_door_handle',(.76,1.105,side*.855),(.18,.032,.035),'ivory')
        box(m,'Cargo_lower_strip',(-.14,.56,side*.93),(1.53,.075,.03),'charcoal')
        box(m,'Roof_gutter',(-.35,2.165,side*.77),(3.45,.034,.045),paint)
    # A central strip divides the rear window into the two cargo doors.
    rearstrip=m.part('Rear_door_center','charcoal')
    face(rearstrip,[(-2.098,1.09,-.009),(-2.048,2.15,-.009),(-2.048,2.15,.009),(-2.098,1.09,.009)],(-1,0,0))
    box(m,'Rear_door_handle',(-2.10,1.21,.12),(.024,.16,.035),'ivory')
    trim(m,L,W,1.02,paint,1.69,1.25,tall_tail=True)
    running_gear(m,wheels,R,W)
    return finish(m,L,W)


def paste_car(board,m,rect,eye=(5,3.5,7),shadow=True):
    x,y,w,h=rect
    if shadow:
        shade=Image.new('RGBA',(w,h)); d=ImageDraw.Draw(shade)
        d.ellipse((w*.16,h*.69,w*.87,h*.88),fill=(41,54,59,38))
        shade=shade.filter(ImageFilter.GaussianBlur(17));board.paste(shade,(x,y),shade)
    img=render(m,w*2,h*2,eye=eye).resize((w,h),Image.Resampling.LANCZOS)
    board.paste(img,(x,y),img)


def previews(models):
    board=Image.new('RGB',(2000,1700),'#eeebe2');d=ImageDraw.Draw(board)
    d.text((80,52),'BLOCK / MOTORS',font=font(68,True),fill='#29363b')
    d.text((84,143),'ORIGINAL LOW-POLY VEHICLES     /     COLLECTION 01',font=font(23),fill='#6f7b77')
    d.text((84,190),'四款原创汽车 · 少色材质 · 独立车轮',font=font(26,chinese=True),fill='#6f7b77')
    for i,c in enumerate(PALETTE.values()):d.rounded_rectangle((1510+i*55,78,1552+i*55,120),radius=4,fill=c)
    for i,m in enumerate(models):
        x=60+(i%2)*960;y=275+(i//2)*665
        d.rounded_rectangle((x,y,x+920,y+630),radius=18,fill='#e0e2d8')
        d.text((x+34,y+29),f'0{i+1} / {m.label}',font=font(34,True),fill='#29363b')
        d.text((x+35,y+79),m.subtitle,font=font(22,chinese=True),fill='#6f7b77')
        paste_car(board,m,(x+10,y+123,900,425))
        d.text((x+35,y+570),f'{m.triangle_count:,} TRIANGLES   /   {len(set(p.material for p in m.parts))} COLORS',font=font(21),fill='#687671')
        used=list(dict.fromkeys(p.material for p in m.parts))
        for j,k in enumerate(used):d.rounded_rectangle((x+700+j*33,y+569,x+724+j*33,y+593),radius=3,fill=PALETTE[k])
        sheet=Image.new('RGB',(1800,1280),'#eeebe2');sd=ImageDraw.Draw(sheet)
        sd.text((70,46),m.label,font=font(62,True),fill='#29363b')
        sd.text((72,128),f'{m.triangle_count:,} TRIANGLES / {len(used)} COLORS / GLB',font=font(24),fill='#6f7b77')
        paste_car(sheet,m,(45,210,1070,640))
        sd.text((1135,230),'02 / SIDE',font=font(20),fill='#6f7b77')
        paste_car(sheet,m,(1110,280,620,300),eye=(0,0,4),shadow=False)
        sd.text((1135,650),'03 / REAR',font=font(20),fill='#6f7b77')
        paste_car(sheet,m,(1130,690,600,350),eye=(-5,2.5,6),shadow=False)
        sd.line((72,1100,1728,1100),fill='#c7cec2',width=2)
        sd.text((72,1140),'独立车轮 · 地面原点 · 纯色材质 · 无外部贴图',font=font(25,chinese=True),fill='#6f7b77')
        sheet.save(ROOT/'previews'/f'{m.name}.png')
    d.text((82,1636),'GLB ASSETS  /  FLAT SHADING  /  FOUR SEPARATE WHEELS',font=font(23),fill='#687671')
    board.save(ROOT/'previews'/'collection.png')


def main():
    models=[sedan(),hatchback(),pickup(),van()]
    for folder in ['models','previews']:(ROOT/folder).mkdir(exist_ok=True)
    records=[export_glb(m) for m in models]
    data={'palette':PALETTE,'models':records}
    (ROOT/'source'/'mesh_data.json').write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    manifest={'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    template=(ROOT/'source'/'viewer_template.html').read_text(encoding='utf-8')
    (ROOT/'preview.html').write_text(template.replace('__ASSET_DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
    previews(models)
    for m in models:print(f'{m.name}: {m.triangle_count} triangles / {len(m.parts)} parts / {len(set(p.material for p in m.parts))} colors')

if __name__=='__main__':main()
