"""Original Kar98k and MP40 visual game assets, modeled from the supplied side references.
Authoring: muzzle +X, up +Y. GLB export: muzzle -Z, up +Y, origin at main grip.
"""
import json
import math
import numpy as np
from model_tools import Model, box, profile, lathe, hollow, beam, export_glb, make_previews, ROOT, PALETTE


def rod(m,name,a,b,radius,material,sides=6):
    """A faceted cylinder oriented along an arbitrary segment."""
    a,b=np.array(a,float),np.array(b,float)
    delta=b-a; length=np.linalg.norm(delta); x=delta/length
    seed=np.array([0,1,0]) if abs(x[1])<.9 else np.array([0,0,1])
    y=seed-x*np.dot(seed,x); y/=np.linalg.norm(y); z=np.cross(x,y)
    basis=np.array([x,y,z]).T
    p=lathe(m,name,[(0,radius),(length,radius)],material,sides=sides)
    p.vertices=(np.array(p.vertices)@basis.T+a).tolist()
    p.normals=(np.array(p.normals)@basis.T).tolist()
    return p


def knob(m,name,center,r,material):
    """An eight-sided, low-poly knob with two rings and pyramidal ends."""
    x,y,z=center
    p=m.part(name,material)
    low=[]; high=[]
    for i in range(8):
        theta=i*math.pi/4
        low.append((x+r*.82*math.cos(theta),y-r*.52,z+r*.82*math.sin(theta)))
        high.append((x+r*.82*math.cos(theta),y+r*.52,z+r*.82*math.sin(theta)))
    for i in range(8):
        j=(i+1)%8
        p.face([low[i],high[i],high[j],low[j]])
        p.face([high[i],(x,y+r,z),high[j]])
        p.face([low[j],(x,y-r,z),low[i]])
    return p


def kar98k():
    m=Model('kar98k','Kar98k','长木托 / 弯柄枪栓 / 机械瞄具')
    # One uninterrupted stock profile captures the slender silhouette of the reference.
    profile(m,'Wood_stock',[
        (-.637,-.097),(-.643,.024),(-.606,.039),(-.468,.046),(-.417,.033),
        (-.364,.055),(-.318,.071),(-.231,.061),(-.14,.052),(.104,.045),
        (.332,.037),(.456,.025),(.465,.010),(.454,-.011),(.309,-.012),
        (.104,-.008),(-.144,-.002),(-.276,-.001),(-.319,-.021),
        (-.351,-.028),(-.385,-.020),(-.467,-.054),(-.594,-.088)
    ],.065,'wood')
    profile(m,'Stock_cheek_facet',[
        (-.628,-.009),(-.473,.012),(-.42,.009),(-.369,.033),(-.324,.046),
        (-.322,.026),(-.379,-.001),(-.467,-.035),(-.625,-.068)
    ],.003,'darkwood',.034)
    profile(m,'Stock_reverse_facet',[
        (-.628,-.009),(-.473,.012),(-.42,.009),(-.369,.033),(-.324,.046),
        (-.322,.026),(-.379,-.001),(-.467,-.035),(-.625,-.068)
    ],.003,'darkwood',-.034)
    profile(m,'Butt_plate',[
        (-.650,-.100),(-.658,.027),(-.642,.032),(-.635,-.096)
    ],.073,'charcoal')
    lathe(m,'Receiver', [(-.325,.023),(-.133,.023)],'charcoal',cy=.081,sides=8)
    lathe(m,'Bolt', [(-.309,.017),(-.15,.017)],'steel',cy=.091,sides=8)
    box(m,'Ejection_port',(-.202,.096,.020),(.092,.019,.008),'charcoal')
    box(m,'Bolt_visible',(-.204,.097,.025),(.083,.008,.004),'steel')
    lathe(m,'Receiver_ring',[(-.155,.025),(-.126,.025)],'charcoal',cy=.083)
    lathe(m,'Bolt_shroud',[(-.347,.021),(-.32,.021)],'steel',cy=.087)
    box(m,'Safety',(-.339,.113,0),(.012,.023,.023),'charcoal')
    # Separate bolt handle and knob on the viewer-facing side of the receiver.
    rod(m,'Bolt_handle_root',(-.29,.091,.01),(-.29,.102,.064),.008,'steel')
    rod(m,'Bolt_handle_bend',(-.29,.102,.064),(-.28,.056,.088),.009,'steel')
    knob(m,'Bolt_knob',(-.28,.049,.092),.019,'charcoal')
    lathe(m,'Barrel',[(-.129,.017),(.524,.013)],'charcoal',cy=.066,sides=8)
    profile(m,'Upper_wood_handguard',[
        (-.121,.060),(-.11,.083),(.141,.066),(.258,.058),(.258,.031),(-.121,.035)
    ],.046,'darkwood')
    box(m,'Handguard_ridge',(.047,.071,0),(.292,.007,.021),'wood')
    profile(m,'Fore_end_upper_facet',[
        (.254,.035),(.253,.062),(.425,.059),(.455,.052),(.458,.025)
    ],.043,'wood')
    # Bands wrap the wood rather than becoming isolated floating details.
    for x,w in [(.244,.019),(.445,.023)]:
        profile(m,'Stock_band',[(x-w/2,-.016),(x+w/2,-.016),(x+w/2,.071),(x-w/2,.071)],.072,'steel')
        box(m,'Band_top',(x,.075,0),(w,.012,.043),'charcoal')
    lathe(m,'Exposed_barrel',[(.46,.013),(.585,.012)],'charcoal',cy=.066)
    hollow(m,'Muzzle',.577,.608,.015,.015,'steel',cy=.066,wall=.005)
    rod(m,'Cleaning_rod',(.449,-.003,0),(.574,-.003,0),.004,'steel')
    box(m,'Bayonet_lug',(.487,.015,0),(.052,.015,.021),'charcoal')
    box(m,'Rear_sight_base',(-.081,.102,0),(.083,.017,.032),'charcoal')
    profile(m,'Rear_sight_ramp',[(-.124,.108),(-.06,.131),(-.038,.131),(-.038,.109)],.023,'steel')
    box(m,'Rear_sight_notch',(-.057,.135,0),(.016,.013,.032),'charcoal')
    box(m,'Front_sight_base',(.562,.085,0),(.034,.017,.032),'charcoal')
    for z in [-.015,.015]:
        profile(m,'Front_sight_ear',[(.551,.091),(.554,.12),(.566,.126),(.576,.119),(.577,.09)],.006,'steel',z)
    box(m,'Front_sight_post',(.563,.104,0),(.006,.021,.005),'charcoal')
    profile(m,'Magazine_floor',[
        (-.288,-.004),(-.153,-.004),(-.151,-.017),(-.282,-.019)
    ],.038,'charcoal')
    beam(m,'Trigger_guard_rear',(-.307,-.009),(-.304,-.047),.009,.022,'charcoal')
    beam(m,'Trigger_guard_bottom',(-.304,-.047),(-.259,-.047),.008,.022,'charcoal')
    beam(m,'Trigger_guard_front',(-.259,-.047),(-.242,-.011),.009,.022,'charcoal')
    beam(m,'Trigger',(-.278,-.013),(-.285,-.034),.008,.013,'steel')
    box(m,'Stock_sling_slot',(-.53,-.017,.038),(.032,.008,.003),'charcoal')
    box(m,'Stock_sling_slot',(-.53,-.017,-.038),(.032,.008,.003),'charcoal')
    m.markers={'Muzzle':(.609,.066,0),'Grip':(-.352,.005,0),'Support':(.118,.019,0)}
    return m


def mp40():
    m=Model('mp40','MP40','筒形机匣 / 前置弹匣 / 折叠枪托')
    # Muzzle points right in the viewer; reference mirrored to the shared forward axis.
    lathe(m,'Receiver_tube',[(-.255,.036),(.139,.036)],'steel',cy=.126,sides=8)
    lathe(m,'Receiver_rear_cap',[(-.273,.038),(-.253,.038)],'charcoal',cy=.126)
    lathe(m,'Receiver_front_collar',[(.123,.04),(.149,.04)],'steel',cy=.126)
    profile(m,'Lower_frame',[
        (-.254,.123),(.099,.123),(.114,.107),(.108,.058),(-.212,.045),(-.256,.063)
    ],.069,'charcoal')
    for z in [-.037,.037]:
        profile(m,'Bakelite_side_panel',[
            (-.243,.111),(.082,.111),(.096,.097),(.09,.066),(-.212,.059),(-.243,.071)
        ],.004,'darkwood',z)
        box(m,'Panel_lower_seam',(-.072,.068,z*1.12),(.283,.006,.003),'charcoal')
        for x in [-.212,.068]: box(m,'Panel_fastener',(x,.086,z*1.12),(.009,.009,.004),'steel')
    box(m,'Receiver_slot',(-.014,.144,.032),(.151,.012,.006),'charcoal')
    box(m,'Bolt_visible',(-.027,.147,.035),(.119,.006,.003),'steel')
    rod(m,'Charging_handle',(.014,.138,.025),(.014,.138,.062),.009,'charcoal')
    knob(m,'Charging_knob',(.014,.138,.066),.012,'steel')
    box(m,'Receiver_top_rib',(-.036,.163,0),(.222,.007,.017),'charcoal')
    profile(m,'Grip_frame',[
        (-.243,.057),(-.185,.046),(-.196,.005),(-.279,-.125),
        (-.321,-.111),(-.323,-.093),(-.27,-.003)
    ],.058,'charcoal')
    for z in [-.031,.031]:
        profile(m,'Grip_panel',[
            (-.246,.032),(-.202,.025),(-.214,-.011),(-.283,-.106),
            (-.307,-.099),(-.305,-.088),(-.258,-.006)
        ],.004,'darkwood',z)
        box(m,'Grip_screw',(-.262,-.041,z*1.12),(.011,.011,.004),'steel')
    beam(m,'Trigger_guard_rear',(-.194,.04),(-.188,-.019),.009,.025,'charcoal')
    beam(m,'Trigger_guard_base',(-.188,-.019),(-.139,-.021),.009,.025,'charcoal')
    beam(m,'Trigger_guard_front',(-.139,-.021),(-.116,.041),.009,.025,'charcoal')
    beam(m,'Trigger',(-.149,.031),(-.166,-.006),.009,.015,'steel')
    profile(m,'Magazine_well',[
        (.035,.09),(.112,.09),(.12,.04),(.106,-.021),(.036,-.021)
    ],.077,'charcoal')
    for z in [-.040,.040]:
        box(m,'Magazine_well_panel',(.075,.032,z),(.058,.077,.004),'steel')
        for y in [.012,.03,.048]: box(m,'Well_rib',(.075,y,z*1.1),(.055,.007,.004),'charcoal')
    profile(m,'Straight_magazine',[
        (.047,-.014),(.103,-.014),(.105,-.281),(.05,-.283)
    ],.043,'charcoal')
    for z in [-.023,.023]:
        box(m,'Magazine_side',(.075,-.153,z),(.044,.243,.003),'charcoal')
        box(m,'Magazine_rib',(.058,-.153,z*1.13),(.006,.241,.003),'steel')
    box(m,'Magazine_floor',(.077,-.284,0),(.064,.012,.052),'charcoal')
    lathe(m,'Barrel_nut',[(.143,.032),(.172,.032)],'steel',cy=.126)
    lathe(m,'Barrel',[(.166,.02),(.338,.017)],'charcoal',cy=.126,sides=8)
    lathe(m,'Barrel_front_band',[(.329,.023),(.348,.023)],'steel',cy=.126)
    hollow(m,'Muzzle',.344,.372,.02,.021,'charcoal',cy=.126,wall=.006)
    profile(m,'Barrel_rest',[(.261,.111),(.321,.111),(.318,.083),(.3,.081),(.291,.101),(.26,.102)],.021,'charcoal')
    box(m,'Front_sight_base',(.331,.151,0),(.036,.015,.029),'charcoal')
    for z in [-.019,.019]:
        profile(m,'Front_sight_guard',[(.315,.153),(.318,.187),(.332,.2),(.346,.185),(.346,.151)],.007,'charcoal',z)
    box(m,'Front_sight_post',(.332,.171,0),(.007,.023,.006),'steel')
    box(m,'Rear_sight_base',(-.157,.169,0),(.049,.014,.029),'charcoal')
    profile(m,'Rear_sight_leaf',[(-.176,.174),(-.172,.193),(-.153,.193),(-.148,.174)],.025,'steel')
    # Folded stock is made of separate side arms and an open butt loop, as in the photo.
    for z in [-.045,.045]:
        rod(m,'Stock_hinge_axle',(-.257,.063,z-.006),(-.257,.063,z+.006),.025,'charcoal',sides=8)
        rod(m,'Stock_hinge_face',(-.257,.063,z-.008),(-.257,.063,z+.008),.017,'steel',sides=8)
        rod(m,'Folded_stock_arm',(-.25,.048,z),(.012,.022,z),.007,'steel')
        rod(m,'Folded_stock_return',(.012,.022,z),(.041,.043,z),.006,'charcoal')
    rod(m,'Folded_butt_crossbar',(.041,.043,-.045),(.041,.043,.045),.008,'charcoal')
    rod(m,'Folded_butt_underbar',(.012,.022,-.045),(.012,.022,.045),.006,'steel')
    m.markers={'Muzzle':(.373,.126,0),'Grip':(-.251,-.034,0),'Support':(.075,.005,0)}
    return m


def main():
    models=[kar98k(),mp40()]
    for folder in ['models','previews']:(ROOT/folder).mkdir(exist_ok=True)
    records=[export_glb(m) for m in models]
    data={'palette':PALETTE,'models':records}
    (ROOT/'source'/'mesh_data.json').write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    manifest={'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    make_previews(models,edition='03',first_index=1)
    template=(ROOT/'source'/'viewer_template.html').read_text(encoding='utf-8-sig')
    (ROOT/'preview.html').write_text(template.replace('__ASSET_DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
    for m in models:print(f'{m.name}: {m.triangle_count} triangles / {len(m.parts)} parts / {len(set(p.material for p in m.parts))} colors')

if __name__=='__main__':main()
