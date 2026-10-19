"""Original FN FAL and UZI game meshes; preserves the first set's exported files."""
import json
import numpy as np
from build_models import Model, box, profile, lathe, hollow, beam, export_glb, make_previews, ROOT, PALETTE


def fnfal():
    m=Model('fnfal','FN FAL','木质护木 / 直弹匣 / 提把')
    profile(m,'Wood_stock',[
        (-.65,-.075),(-.659,.045),(-.631,.058),(-.532,.045),(-.444,.057),
        (-.367,.095),(-.333,.111),(-.312,.102),(-.315,.045),(-.405,.016),(-.53,-.043)
    ],.065,'darkwood')
    profile(m,'Stock_comb',[
        (-.643,.047),(-.535,.042),(-.444,.055),(-.367,.091),(-.335,.105),
        (-.338,.089),(-.438,.038),(-.534,.029),(-.643,.034)
    ],.066,'wood')
    profile(m,'Butt_pad',[(-.667,-.078),(-.676,.046),(-.658,.049),(-.65,-.074)],.073,'charcoal')
    profile(m,'Receiver',[
        (-.337,.052),(-.342,.131),(-.31,.161),(-.047,.161),(-.025,.14),
        (-.024,.068),(-.107,.037),(-.274,.044)
    ],.071,'steel')
    profile(m,'Top_cover',[(-.324,.148),(-.309,.177),(-.046,.177),(-.027,.151)],.064,'charcoal')
    box(m,'Upper_receiver_side',(-.189,.13,.037),(.216,.039,.005),'steel')
    box(m,'Receiver_seam',(-.213,.094,.040),(.23,.006,.004),'charcoal')
    profile(m,'Ejection_port',[(-.116,.137),(-.107,.156),(-.034,.156),(-.034,.135)],.005,'charcoal',.038)
    box(m,'Bolt',(-.072,.137,.043),(.067,.006,.006),'steel')
    profile(m,'Pistol_grip',[
        (-.278,.046),(-.226,.043),(-.233,-.015),(-.284,-.104),(-.325,-.081),(-.281,-.014)
    ],.043,'darkwood')
    profile(m,'Grip_facet',[
        (-.274,.024),(-.242,.021),(-.248,-.019),(-.288,-.083),(-.309,-.074),(-.272,-.01)
    ],.003,'wood',.023)
    beam(m,'Trigger_guard_rear',(-.221,.043),(-.227,-.014),.009,.022,'charcoal')
    beam(m,'Trigger_guard_base',(-.227,-.014),(-.17,-.023),.009,.022,'charcoal')
    beam(m,'Trigger_guard_front',(-.17,-.023),(-.154,.041),.009,.022,'charcoal')
    beam(m,'Trigger',(-.189,.031),(-.2,.004),.010,.016,'steel')
    profile(m,'Straight_magazine',[
        (-.133,.062),(-.035,.062),(-.033,-.132),(-.123,-.145)
    ],.057,'charcoal')
    for z in [-.030,.030]:
        profile(m,'Magazine_face',[
            (-.125,.037),(-.044,.037),(-.042,-.122),(-.117,-.132)
        ],.003,'steel',z)
    for x in [-.108,-.063]:
        for z in [-.033,.033]:
            box(m,'Magazine_rib',(x,-.047,z),(.008,.151,.005),'charcoal')
    profile(m,'Magazine_floor',[
        (-.128,-.147),(-.029,-.133),(-.028,-.121),(-.129,-.134)
    ],.063,'steel')
    lathe(m,'Handguard',[(.003,.042),(.016,.049),(.235,.046),(.25,.037)],'darkwood',cy=.127,sides=8)
    box(m,'Handguard_ridge',(.127,.171,0),(.213,.007,.027),'wood')
    lathe(m,'Handguard_rear_band',[(-.008,.044),(.006,.044)],'steel',cy=.127)
    lathe(m,'Handguard_front_band',[(.245,.04),(.263,.04)],'charcoal',cy=.127)
    for x in [.142,.176,.21]:
        for z in [-.044,.044]:
            profile(m,'Cooling_vent',[(x-.006,.143),(x-.006,.151),(x+.006,.151),(x+.006,.143)],.003,'charcoal',z)
    lathe(m,'Barrel',[(.26,.015),(.555,.014)],'charcoal',cy=.119)
    lathe(m,'Barrel_step',[(.265,.02),(.294,.02)],'steel',cy=.119)
    hollow(m,'Flash_hider',.548,.595,.019,.02,'steel',cy=.119,wall=.006)
    for x in [.56,.576]:
        box(m,'Flash_hider_slot',(x,.125,.019),(.008,.009,.003),'charcoal')
        box(m,'Flash_hider_slot',(x,.125,-.019),(.008,.009,.003),'charcoal')
    profile(m,'Gas_block',[(.263,.106),(.288,.106),(.288,.17),(.267,.186),(.263,.17)],.038,'steel')
    for z in [-.019,.019]:
        profile(m,'Front_sight_ear',[(.268,.175),(.274,.206),(.286,.206),(.29,.175)],.008,'steel',z)
    box(m,'Front_sight_post',(.28,.188,0),(.007,.023,.007),'charcoal')
    box(m,'Rear_sight_base',(-.318,.178,0),(.045,.014,.048),'steel')
    for z in [-.018,.018]: box(m,'Rear_sight_ear',(-.316,.196,z),(.013,.025,.008),'charcoal')
    beam(m,'Carry_handle_stem',(-.018,.157),(-.02,.255),.009,.012,'steel')
    profile(m,'Carry_handle',[
        (-.128,.269),(-.128,.245),(-.112,.242),(-.101,.247),(-.087,.241),
        (-.071,.247),(-.054,.241),(-.037,.247),(-.02,.244),(-.017,.267)
    ],.025,'olive')
    box(m,'Selector',(-.184,.065,.043),(.032,.009,.012),'charcoal')
    box(m,'Charging_handle',(-.217,.123,-.053),(.034,.013,.035),'charcoal')
    # Separate removable sling, behind the grip and magazine.
    path=np.array([[-.619,-.078],[-.604,-.106],[-.502,-.122],[-.382,-.111],[-.229,-.119],[-.082,-.138],[.055,-.137],[.171,-.12],[.217,-.075],[.263,.083]])
    top=[]; bottom=[]
    for i,p in enumerate(path):
        tangent=path[min(i+1,len(path)-1)]-path[max(0,i-1)]
        normal=np.array([-tangent[1],tangent[0]])/np.linalg.norm(tangent)
        width=.012 if i<5 else .006
        top.append((p+normal*width).tolist()); bottom.append((p-normal*width).tolist())
    profile(m,'Sling',top+bottom[::-1],.006,'olive',-.046)
    m.markers={'Muzzle':(.596,.119,0),'Grip':(-.272,-.033,0),'Support':(.135,.123,0)}
    return m


def uzi():
    m=Model('uzi','UZI','短机匣 / 握把弹匣 / 折叠托')
    # Reference faces left; mesh shares the collection's -Z forward convention.
    profile(m,'Receiver',[
        (-.224,.055),(-.224,.137),(-.204,.155),(.076,.155),(.105,.134),(.105,.055)
    ],.073,'steel')
    profile(m,'Top_cover',[
        (-.217,.146),(-.203,.169),(.069,.169),(.094,.145)
    ],.061,'charcoal')
    box(m,'Cover_ridge',(-.062,.17,0),(.238,.008,.032),'steel')
    profile(m,'Receiver_side_panel',[
        (-.21,.076),(-.21,.129),(.079,.129),(.091,.117),(.091,.076)
    ],.003,'steel',.039)
    box(m,'Lower_receiver_rail',(-.052,.066,.040),(.294,.012,.005),'charcoal')
    box(m,'Lower_receiver_rail',(-.052,.066,-.040),(.294,.012,.005),'charcoal')
    profile(m,'Ejection_port',[
        (-.079,.112),(-.079,.14),(-.008,.14),(.003,.129),(.003,.111)
    ],.004,'charcoal',.039)
    box(m,'Bolt_visible',(-.035,.114,.043),(.064,.006,.004),'steel')
    profile(m,'Foregrip',[
        (-.008,.063),(.101,.063),(.097,.012),(.076,-.003),(-.005,.006)
    ],.080,'charcoal')
    for z in [-.041,.041]:
        profile(m,'Foregrip_panel',[(.009,.048),(.086,.048),(.086,.012),(.009,.017)],.004,'olive',z)
        for x in [.027,.048,.069]: box(m,'Foregrip_groove',(x,.032,z*1.07),(.006,.029,.003),'charcoal')
    profile(m,'Grip',[
        (-.158,.066),(-.083,.066),(-.083,-.087),(-.098,-.105),(-.153,-.105),(-.166,-.087)
    ],.059,'charcoal')
    for z in [-.031,.031]:
        profile(m,'Grip_panel',[
            (-.15,.012),(-.096,.012),(-.095,-.071),(-.105,-.091),(-.145,-.091),(-.154,-.069)
        ],.004,'olive',z)
        for y in [-.026,-.043,-.060]: box(m,'Grip_groove',(-.125,y,z*1.1),(.039,.005,.0025),'charcoal')
    box(m,'Grip_safety',(-.164,-.023,0),(.01,.114,.033),'steel')
    profile(m,'Straight_magazine',[
        (-.144,-.082),(-.103,-.082),(-.103,-.236),(-.143,-.236)
    ],.039,'charcoal')
    box(m,'Magazine_edge',(-.14,-.164,.021),(.005,.131,.003),'steel')
    box(m,'Magazine_floor',(-.124,-.237,0),(.051,.012,.047),'charcoal')
    beam(m,'Trigger_guard_rear',(-.084,.052),(-.079,-.025),.009,.023,'charcoal')
    beam(m,'Trigger_guard_base',(-.079,-.025),(-.029,-.025),.009,.023,'charcoal')
    beam(m,'Trigger_guard_front',(-.029,-.025),(-.012,.046),.009,.023,'charcoal')
    beam(m,'Trigger',(-.048,.048),(-.062,.005),.010,.016,'steel')
    lathe(m,'Barrel_nut',[(.095,.032),(.126,.032)],'charcoal',cy=.102,sides=8)
    lathe(m,'Barrel',[(.121,.014),(.189,.014)],'steel',cy=.102,sides=8)
    hollow(m,'Muzzle',.184,.204,.017,.017,'charcoal',cy=.102,wall=.006)
    for x in [-.193,.068]:
        box(m,'Sight_base',(x,.171,0),(.035,.014,.063),'steel')
        for z in [-.027,.027]:
            profile(m,'Sight_ear',[(x-.017,.17),(x-.013,.204),(x+.010,.204),(x+.017,.17)],.009,'steel',z)
        box(m,'Sight_post',(x,.185,0),(.007,.025,.008),'charcoal')
    box(m,'Charging_handle_base',(-.054,.182,0),(.043,.025,.017),'steel')
    box(m,'Charging_handle',(-.054,.195,0),(.037,.014,.046),'charcoal')
    box(m,'Selector',(-.171,.072,.044),(.033,.009,.012),'charcoal')
    box(m,'Stock_hinge',(-.232,.101,0),(.026,.043,.075),'charcoal')
    for z in [-.026,.026]:
        profile(m,'Stock_arm',[
            (-.232,.114),(-.292,.114),(-.332,.084),(-.475,.044),
            (-.473,.031),(-.326,.071),(-.288,.101),(-.232,.101)
        ],.012,'charcoal',z)
    profile(m,'Stock_butt',[
        (-.483,.044),(-.466,.039),(-.475,-.063),(-.493,-.062)
    ],.074,'charcoal')
    box(m,'Stock_butt_top',(-.475,.038,0),(.02,.012,.076),'steel')
    m.markers={'Muzzle':(.205,.102,0),'Grip':(-.123,-.04,0),'Support':(.045,.032,0)}
    return m


def main():
    models=[fnfal(),uzi()]
    new_records=[export_glb(m) for m in models]
    mesh_path=ROOT/'source'/'mesh_data.json'
    previous=json.loads(mesh_path.read_text(encoding='utf-8')) if mesh_path.exists() else {'palette':PALETTE,'models':[]}
    names={m.name for m in models}
    previous['models']=[r for r in previous['models'] if r['id'] not in names]+new_records
    mesh_path.write_text(json.dumps(previous,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    manifest={'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in previous['models']]}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    make_previews(models,collection_name='collection_02.png',edition='02',first_index=4)
    for m in models: print(f'{m.name}: {m.triangle_count} triangles / {len(m.parts)} parts / {len(set(p.material for p in m.parts))} colors')

if __name__=='__main__': main()
