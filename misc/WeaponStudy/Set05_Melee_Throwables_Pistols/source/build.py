"""Six original lowpoly exterior game props, modeled from the supplied pictures.
No downloaded geometry, internal mechanisms, or manufacturing dimensions.
"""
import json, math, shutil
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import model_tools as mt
from model_tools import Model, box, profile, lathe, hollow, beam, export_glb, render, font, ROOT
from detail_helpers import rod

PALETTE = {
    'charcoal':'#283238', 'steel':'#626E70', 'silver':'#B9C0BB',
    'wood':'#BF9566', 'darkwood':'#76513B', 'red':'#A34338',
    'olive':'#737953', 'teal':'#648B7D', 'sand':'#B59C70', 'yellow':'#D4BC64'
}
mt.PALETTE=PALETTE
mt.RGB={k:np.array([int(v[i:i+2],16)/255 for i in (1,3,5)]) for k,v in PALETTE.items()}

def turn_part(p, matrix, translation=(0,0,0)):
    p.vertices=(np.array(p.vertices)@np.asarray(matrix).T+translation).tolist()
    p.normals=(np.array(p.normals)@np.asarray(matrix).T).tolist()
    return p

def upright(m,name,rings,mat,sides=10,cx=0,cz=0):
    return turn_part(lathe(m,name,rings,mat,sides=sides),[[0,-1,0],[1,0,0],[0,0,1]],(cx,0,cz))

def ring(m,name,center,rx,ry,tube,mat,segments=10):
    """Low-poly closed pull ring in the XY plane, with four-sided wire."""
    p=m.part(name,mat);center=np.asarray(center);loops=[]
    for i in range(segments):
        a=i*2*math.pi/segments;radial=np.array([math.cos(a),math.sin(a),0.])
        c=center+radial*[rx,ry,0]
        loops.append([c+radial*tube,c+[0,0,tube],c-radial*tube,c-[0,0,tube]])
    for i in range(segments):
        j=(i+1)%segments
        for k in range(4):
            n=(k+1)%4;p.face([loops[i][k],loops[j][k],loops[j][n],loops[i][n]])
    return p

def bat():
    m=Model('baseball_bat','棒球棍','长木柄 / 八边棱面');m.english='BASEBALL BAT';m.category='melee'
    lathe(m,'Wood_body',[(-.115,.015),(-.075,.014),(.06,.017),(.22,.025),(.41,.040),(.64,.047),(.70,.045),(.72,.036)],'wood',sides=8)
    lathe(m,'Handle_knob',[(-.145,.022),(-.139,.028),(-.12,.028),(-.111,.016)],'darkwood',sides=8)
    lathe(m,'End_grain',[(.717,.036),(.722,.032)],'darkwood',sides=8)
    m.markers={'Grip':(0,0,0),'Support':(.12,0,0),'Hit':(.58,0,0)}
    return m

def axe():
    m=Model('axe','斧头','红色斧头 / 木质弯柄');m.english='WOOD AXE';m.category='melee'
    profile(m,'Curved_wood_handle',[(-.17,-.035),(-.18,-.005),(-.15,.015),(-.02,.022),(.13,.012),(.27,.003),(.39,.011),(.52,.041),(.54,.02),(.52,-.011),(.39,-.031),(.25,-.034),(.11,-.025),(-.03,-.015),(-.14,-.019)],.033,'wood')
    for z in [-.017,.017]:
        profile(m,'Handle_facet',[(-.16,-.024),(-.02,-.006),(.12,-.017),(.26,-.025),(.40,-.021),(.52,.004),(.52,-.007),(.39,-.030),(.25,-.032),(.11,-.024),(-.03,-.014),(-.15,-.028)],.0015,'darkwood',z)
    profile(m,'Painted_axe_head',[(.413,-.054),(.563,-.054),(.568,.069),(.62,.207),(.411,.258),(.436,.088)],.059,'red')
    # A tapered cutting-edge strip; the outer edge is a thin facet, not a flat slab.
    a=(.411,.258);b=(.62,.207);c=(.641,.27);d=(.389,.326)
    vv=[(a[0],a[1],-.0295),(b[0],b[1],-.0295),(c[0],c[1],-.003),(d[0],d[1],-.003),
        (a[0],a[1],.0295),(b[0],b[1],.0295),(c[0],c[1],.003),(d[0],d[1],.003)]
    p=m.part('Steel_cutting_edge','silver')
    for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:p.face([vv[i] for i in f])
    box(m,'Handle_end_in_head',(.49,-.055,0),(.057,.007,.023),'wood')
    m.markers={'Grip':(-.05,-.006,0),'Support':(.18,-.01,0),'Hit':(.515,.294,0)}
    return m

def flashbang():
    m=Model('flashbang','闪光弹','细长罐体 / 镂空感护罩');m.english='FLASHBANG';m.category='throwables'
    r=.038;sides=8
    upright(m,'Faceted_canister',[(.028,r),(.157,r)],'sand',sides)
    upright(m,'Lower_cap',[(.006,.030),(.015,.043),(.036,.043),(.043,r)],'sand',sides)
    upright(m,'Upper_cap',[(.149,r),(.157,.043),(.180,.043),(.188,.029)],'sand',sides)
    upright(m,'Green_identification_band',[(.078,.0385),(.112,.0385)],'teal',sides)
    # Dark flat hexagons read as openings in the stylized exterior casing.
    for i in range(sides):
        a=(i+1)*2*math.pi/sides;n=np.array([-math.sin(a),0,math.cos(a)])
        tangent=np.array([math.cos(a),0,math.sin(a)])
        for y in [.056,.095,.134]:
            c=n*(r*math.cos(math.pi/sides)+.0011)+[0,y,0]
            points=[c+tangent*(.010*math.cos(k*math.pi/3))+[0,.012*math.sin(k*math.pi/3),0] for k in range(6)]
            p=m.part('Casing_opening','charcoal');p.face(points)
    upright(m,'Top_neck',[(.182,.023),(.218,.023)],'olive',sides=6)
    profile(m,'Safety_lever',[(0,.224),(.042,.214),(.054,.187),(.059,.043),(.047,.032),(.043,.040),(.050,.048),(.046,.182),(.035,.204),(-.010,.213)],.013,'olive',-.016)
    rod(m,'Ring_attachment',(-.011,.210,.016),(.022,.210,.016),.005,'olive')
    ring(m,'Pull_ring',(-.036,.212,.017),.022,.029,.0027,'olive')
    m.markers={'Grip':(0,.096,0),'Throw':(0,.112,0)}
    return m

def grenade():
    m=Model('grenade','手雷','圆形弹体 / 黄色识别带');m.english='FRAG GRENADE';m.category='throwables'
    upright(m,'Faceted_body',[(.009,.017),(.024,.038),(.049,.052),(.081,.054),(.111,.045),(.129,.027),(.132,.016)],'charcoal',sides=12)
    upright(m,'Yellow_band',[(.067,.0542),(.080,.0547)],'yellow',sides=12)
    upright(m,'Top_neck',[(.129,.014),(.155,.013)],'steel',sides=8)
    profile(m,'Top_cap',[(-.020,.15),(-.019,.179),(.010,.190),(.032,.18),(.029,.16)],.031,'olive')
    profile(m,'Safety_lever',[(-.013,.192),(.034,.186),(.040,.174),(.045,.141),(.063,.040),(.058,.028),(.050,.030),(.053,.041),(.035,.140),(.030,.173),(-.013,.181)],.014,'olive')
    ring(m,'Pull_ring',(-.032,.171,.002),.021,.025,.0025,'steel')
    m.markers={'Grip':(0,.072,0),'Throw':(0,.078,0)}
    return m

def deagle():
    m=Model('deagle','沙漠之鹰','厚重套筒 / 黑色握把');m.english='DESERT EAGLE';m.category='pistol'
    profile(m,'Frame',[(-.119,.031),(-.117,.043),(-.084,.047),(-.072,.067),(.169,.067),(.171,.040),(.027,.036),(.016,.021),(-.026,.015),(-.033,-.027),(-.068,-.111),(-.120,-.111),(-.126,-.097),(-.096,-.008),(-.095,.019)],.036,'steel')
    profile(m,'Heavy_slide',[(-.091,.048),(-.09,.090),(-.077,.109),(.011,.109),(.026,.097),(.069,.097),(.082,.107),(.176,.107),(.188,.093),(.188,.050)],.045,'silver')
    box(m,'Slide_top_flat',(-.038,.108,0),(.082,.006,.026),'steel')
    box(m,'Ejection_port',(.041,.0985,0),(.049,.005,.028),'charcoal')
    profile(m,'Barrel_upper_facet',[(.080,.108),(.177,.108),(.187,.094),(.084,.094)],.029,'silver')
    hollow(m,'Muzzle',.180,.196,.016,.016,'silver',cy=.076,sides=8,wall=.005)
    lathe(m,'Muzzle_dark_recess',[(.170,.011),(.174,.011)],'charcoal',cy=.076,sides=8)
    box(m,'Underbarrel_rail',(.115,.037,0),(.115,.013,.031),'charcoal')
    for x in [.072,.095,.118,.141,.164]:box(m,'Rail_tooth',(x,.031,0),(.012,.008,.034),'steel')
    profile(m,'Black_grip',[(-.083,.017),(-.040,.017),(-.039,-.019),(-.072,-.105),(-.117,-.105),(-.119,-.095),(-.09,-.008)],.041,'charcoal')
    profile(m,'Magazine_floor',[(-.119,-.105),(-.07,-.105),(-.068,-.116),(-.121,-.116)],.045,'steel')
    beam(m,'Trigger_guard_front',(.025,.034),(.030,-.021),.009,.026,'steel')
    beam(m,'Trigger_guard_bottom',(.030,-.021),(-.030,-.021),.009,.026,'steel')
    profile(m,'Curved_trigger',[(-.008,.028),(-.001,.026),(-.009,.007),(-.003,-.010),(-.010,-.009),(-.017,.006)],.012,'charcoal')
    for z in [-.024,.024]:
        for x in [-.072,-.061,-.050,-.039]:profile(m,'Slide_serration',[(x,.057),(x+.009,.095),(x+.012,.095),(x+.003,.057)],.0014,'steel',z)
        box(m,'Grip_screw',(-.080,-.061,z*.91),(.008,.008,.002),'steel')
        box(m,'Safety_selector',(-.072,.089,z),(.024,.010,.004),'charcoal')
    box(m,'Rear_sight',(-.071,.117,0),(.020,.013,.030),'charcoal')
    box(m,'Front_sight',(.174,.117,0),(.011,.016,.008),'charcoal')
    profile(m,'Hammer',[(-.091,.068),(-.104,.086),(-.109,.085),(-.106,.073),(-.096,.061)],.015,'charcoal')
    m.markers={'Grip':(-.081,-.046,0),'Support':(-.075,-.035,.024),'Muzzle':(.197,.076,0)}
    return m

def m1911():
    m=Model('m1911','M1911','经典套筒 / 棕色握把');m.english='M1911';m.category='pistol'
    profile(m,'Frame',[(-.104,.027),(-.103,.038),(-.078,.047),(.141,.047),(.141,.021),(.023,.020),(.013,.009),(-.026,.003),(-.042,-.033),(-.062,-.114),(-.107,-.111),(-.111,-.101),(-.086,-.009),(-.086,.016)],.027,'steel')
    profile(m,'Classic_slide',[(-.084,.041),(-.084,.073),(-.077,.088),(.137,.088),(.147,.079),(.147,.045),(.14,.040)],.035,'silver')
    box(m,'Slide_top',(.029,.088,0),(.209,.004,.023),'steel')
    hollow(m,'Muzzle',.140,.151,.0115,.0115,'steel',cy=.061,sides=8,wall=.004)
    lathe(m,'Muzzle_dark_recess',[(.134,.0075),(.137,.0075)],'charcoal',cy=.061,sides=8)
    lathe(m,'Recoil_plug',[(.141,.008),(.150,.008)],'steel',cy=.037,sides=8)
    profile(m,'Grip_frame',[(-.073,.028),(-.031,.026),(-.036,-.019),(-.067,-.112),(-.108,-.107),(-.083,-.009)],.032,'charcoal')
    for z in [-.018,.018]:
        profile(m,'Wood_grip_panel',[(-.071,.015),(-.039,.014),(-.043,-.018),(-.069,-.101),(-.099,-.098),(-.078,-.012)],.005,'darkwood',z)
        for x,y in [(-.057,-.007),(-.081,-.077)]:
            rod(m,'Grip_screw',(x,y,z-.003),(x,y,z+.003),.0035,'steel',sides=6)
        for x in [-.073,-.065,-.057,-.049]:box(m,'Rear_slide_serration',(x,.063,z*.99),(.0025,.030,.0015),'steel')
    profile(m,'Beavertail',[(-.079,.033),(-.10,.032),(-.123,.043),(-.125,.037),(-.105,.022),(-.084,.016)],.028,'steel')
    profile(m,'Hammer',[(-.082,.058),(-.095,.077),(-.103,.072),(-.097,.063),(-.106,.054),(-.1,.050),(-.088,.050)],.014,'steel')
    # Open polygonal trigger guard, following the classic rounded profile.
    points=[(.019,.023),(.028,.009),(.029,-.008),(.020,-.022),(.003,-.027),(-.021,-.024),(-.030,-.011)]
    for i,(a,b) in enumerate(zip(points,points[1:])):beam(m,f'Trigger_guard_{i}',a,b,.0055,.019,'steel')
    profile(m,'Trigger',[(-.011,.020),(-.002,.019),(-.009,.004),(-.005,-.010),(-.013,-.008),(-.017,.005)],.012,'charcoal')
    profile(m,'Magazine_floor',[(-.110,-.109),(-.064,-.114),(-.064,-.119),(-.111,-.114)],.036,'steel')
    profile(m,'Ejection_port',[(.013,.063),(.047,.063),(.052,.07),(.052,.085),(.013,.085)],.0015,'charcoal',.018)
    box(m,'Rear_sight',(-.069,.094,0),(.015,.009,.021),'charcoal')
    box(m,'Front_sight',(.133,.093,0),(.008,.008,.005),'charcoal')
    box(m,'Slide_stop',(-.025,.032,.020),(.026,.006,.005),'steel')
    m.markers={'Grip':(-.071,-.046,0),'Support':(-.065,-.034,.021),'Muzzle':(.152,.061,0)}
    return m

def paste(board,m,rect,eye=(1.0,.65,4.2)):
    x,y,w,h=rect
    im=render(m,w*2,h*2,eye).resize((w,h),Image.Resampling.LANCZOS);board.paste(im,(x,y),im)

def previews(models):
    board=Image.new('RGB',(2000,2110),'#eeeae1');d=ImageDraw.Draw(board)
    d.text((75,48),'FIELD / ARMORY',font=font(66,True),fill='#283238')
    d.text((78,137),'LOW-POLY PROP COLLECTION / 05',font=font(25),fill='#767E75')
    d.text((78,181),'近战武器 · 投掷物 · 手枪',font=font(26,chinese=True),fill='#767E75')
    for i,m in enumerate(models):
        x=60+(i%2)*960;y=260+(i//2)*590
        d.rounded_rectangle((x,y,x+920,y+554),radius=16,fill='#E0E1D6')
        d.text((x+30,y+23),f'{i+1:02} / {m.english}',font=font(34),fill='#283238')
        d.text((x+31,y+76),m.label+' · '+m.subtitle,font=font(22,chinese=True),fill='#767E75')
        paste(board,m,(x+30,y+105,860,370))
        keys=list(dict.fromkeys(p.material for p in m.parts))
        d.text((x+31,y+505),f'{m.triangle_count:,} TRIANGLES / {len(keys)} COLORS',font=font(21),fill='#68716D')
        for j,key in enumerate(keys):d.rounded_rectangle((x+704+j*35,y+503,x+729+j*35,y+528),radius=3,fill=PALETTE[key])
        sheet=Image.new('RGB',(1800,1220),'#eeeae1');sd=ImageDraw.Draw(sheet)
        sd.text((70,43),m.english,font=font(61,True),fill='#283238')
        sd.text((72,125),m.label+' / '+m.subtitle,font=font(26,chinese=True),fill='#767E75')
        sd.text((72,202),'01 / THREE-QUARTER',font=font(20),fill='#767E75')
        paste(sheet,m,(40,244,1020,835))
        sd.text((1120,202),'02 / SIDE',font=font(20),fill='#767E75')
        paste(sheet,m,(1090,235,650,400),(0,0,4))
        sd.text((1120,704),'03 / TOP',font=font(20),fill='#767E75')
        paste(sheet,m,(1090,742,650,370),(.2,4,1.4))
        sd.text((72,1164),f'{m.triangle_count:,} TRIANGLES / {len(keys)} COLORS / ORIGINAL GEOMETRY / GLB',font=font(21),fill='#767E75')
        sheet.save(ROOT/'previews'/f'{m.name}.png')
    d.text((78,2056),'ORIGINAL MESHES / SOLID COLORS / EDITABLE PARTS / 6 SEPARATE GLB ASSETS',font=font(22),fill='#767E75')
    board.save(ROOT/'previews/collection.png')

def main():
    models=[bat(),axe(),flashbang(),grenade(),deagle(),m1911()]
    records=[]
    for m in models:
        record=export_glb(m);record.update(category=m.category,markers={k:list(v) for k,v in m.markers.items()});records.append(record)
    data={'palette':PALETTE,'models':records}
    (ROOT/'source/mesh_data.json').write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    (ROOT/'manifest.json').write_text(json.dumps({'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]},ensure_ascii=False,indent=2),encoding='utf-8')
    template=(ROOT/'source/viewer_template.html').read_text(encoding='utf-8-sig')
    (ROOT/'preview.html').write_text(template.replace('__ASSET_DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
    previews(models)
    project=ROOT.parents[1]
    for m in models:
        if (project/'project.godot').is_file():
            destination=project/'assets/weapons'/m.category
            destination.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(ROOT/'models'/f'{m.name}.glb',destination/f'{m.name}.glb')
        print(f'{m.name}: {m.triangle_count} triangles / {len(m.parts)} parts / {len(set(p.material for p in m.parts))} colors')

if __name__=='__main__':main()
