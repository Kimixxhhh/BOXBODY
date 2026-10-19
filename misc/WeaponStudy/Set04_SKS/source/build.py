"""Original, stylized SKS game model from the supplied side-view reference.
Pure-color flat-shaded exterior geometry. No external meshes or image textures.
"""
import json
from model_tools import Model, box, profile, lathe, hollow, beam, export_glb, make_previews, ROOT, PALETTE
from detail_helpers import rod, knob


def sks():
    m=Model('sks','SKS','长木托 / 固定弹仓 / 折叠刺刀')
    # Stock is one continuous silhouette, with a shallow semi-pistol grip at the wrist.
    profile(m,'Wood_stock',[
        (-.638,-.107),(-.650,.019),(-.630,.031),(-.505,.038),(-.433,.039),
        (-.397,.032),(-.359,.045),(-.307,.075),(-.266,.089),(-.130,.084),
        (.025,.086),(.168,.095),(.287,.098),(.302,.087),(.296,.052),
        (.184,.046),(.081,.038),(.015,.021),(-.116,.009),(-.232,.007),
        (-.296,-.004),(-.338,-.026),(-.365,-.046),(-.403,-.057),(-.526,-.09)
    ],.072,'darkwood')
    # A small warm facet breaks up the wood without any grain texture or extra colors.
    for z in [-.037,.037]:
        profile(m,'Stock_upper_facet',[
            (-.629,.015),(-.507,.025),(-.432,.025),(-.395,.020),(-.349,.036),
            (-.296,.063),(-.263,.074),(-.129,.069),(.025,.071),(.169,.079),(.279,.084),
            (.281,.07),(.165,.064),(.025,.057),(-.13,.055),(-.268,.057),
            (-.299,.046),(-.354,.019),(-.397,.003),(-.438,.009),(-.624,-.003)
        ],.002,'wood',z)
    profile(m,'Butt_plate',[
        (-.648,-.111),(-.662,.021),(-.65,.027),(-.636,-.108)
    ],.078,'charcoal')
    profile(m,'Receiver',[
        (-.304,.086),(-.303,.134),(-.285,.149),(-.174,.149),(-.16,.136),
        (.075,.137),(.094,.123),(.091,.091),(-.043,.082)
    ],.064,'charcoal')
    profile(m,'Receiver_cover',[
        (-.303,.129),(-.296,.157),(-.177,.157),(-.164,.145),(-.164,.128)
    ],.058,'charcoal')
    box(m,'Cover_upper_facet',(-.239,.157,0),(.114,.006,.039),'steel')
    profile(m,'Bolt_carrier',[
        (-.165,.143),(-.023,.143),(-.017,.13),(-.026,.114),(-.124,.114),(-.14,.13)
    ],.062,'steel')
    box(m,'Ejection_port',(-.092,.149,0),(.080,.008,.043),'charcoal')
    box(m,'Bolt_edge',(-.102,.148,.026),(.107,.005,.006),'steel')
    box(m,'Receiver_lower_seam',(-.123,.10,.035),(.151,.006,.004),'charcoal')
    # Charging handle projects from the side; its faceted knob reads at gameplay scale.
    rod(m,'Charging_handle',(-.027,.137,.02),(-.02,.15,.071),.008,'steel')
    knob(m,'Charging_knob',(-.02,.15,.077),.014,'steel')
    lathe(m,'Barrel',[(.062,.018),(.569,.015)],'charcoal',cy=.102,sides=8)
    lathe(m,'Gas_tube_rear',[(.074,.017),(.099,.017)],'steel',cy=.148)
    # Distinct wooden upper handguard above the barrel, shorter than the lower stock.
    profile(m,'Upper_wood_handguard',[
        (.095,.121),(.095,.16),(.111,.17),(.282,.168),(.294,.158),(.294,.12)
    ],.062,'wood')
    box(m,'Handguard_top',(.193,.168,0),(.176,.008,.043),'darkwood')
    for z in [-.033,.033]:
        for x in [.137,.191,.246]:
            profile(m,'Handguard_vent',[(x-.013,.12),(x-.013,.128),(x-.006,.132),(x+.012,.132),(x+.016,.12)],.003,'charcoal',z)
    box(m,'Handguard_rear_band',(.092,.143,0),(.014,.057,.067),'charcoal')
    box(m,'Handguard_front_band',(.297,.14,0),(.015,.051,.067),'charcoal')
    lathe(m,'Exposed_gas_tube',[(.305,.019),(.399,.019),(.414,.014)],'charcoal',cy=.148)
    profile(m,'Gas_block',[
        (.397,.091),(.431,.091),(.432,.121),(.418,.147),(.398,.147)
    ],.039,'charcoal')
    lathe(m,'Gas_block_collar',[(.409,.022),(.435,.022)],'charcoal',cy=.102)
    # A compact angled fixed magazine, rather than a long detachable rifle magazine.
    profile(m,'Fixed_magazine',[
        (-.106,.020),(.020,.025),(.014,-.003),(-.083,-.053),(-.105,-.047),(-.117,.003)
    ],.058,'charcoal')
    for z in [-.030,.030]:
        profile(m,'Magazine_facet',[
            (-.101,.009),(.006,.016),(.002,-.002),(-.084,-.041),(-.096,-.034)
        ],.003,'steel',z)
    profile(m,'Magazine_floor',[
        (-.104,-.047),(-.084,-.055),(.015,-.006),(.014,.003),(-.085,-.043)
    ],.063,'charcoal')
    box(m,'Magazine_hinge',(.016,.022,0),(.018,.02,.067),'steel')
    beam(m,'Trigger_guard_rear',(-.286,.007),(-.283,-.032),.008,.023,'charcoal')
    beam(m,'Trigger_guard_lower',(-.283,-.032),(-.231,-.032),.008,.023,'charcoal')
    beam(m,'Trigger_guard_front',(-.231,-.032),(-.219,.012),.008,.023,'charcoal')
    beam(m,'Trigger',(-.258,.004),(-.265,-.019),.008,.013,'steel')
    box(m,'Trigger_group_plate',(-.235,.004,0),(.103,.01,.043),'charcoal')
    box(m,'Rear_sight_base',(.053,.152,0),(.066,.02,.038),'charcoal')
    profile(m,'Rear_sight_ramp',[(.017,.158),(.029,.177),(.068,.185),(.086,.171),(.086,.153)],.027,'steel')
    box(m,'Rear_sight_leaf',(.043,.181,0),(.054,.009,.023),'charcoal')
    for z in [-.016,.016]: box(m,'Rear_sight_notch',(.065,.188,z),(.012,.016,.008),'charcoal')
    lathe(m,'Muzzle_base',[(.546,.021),(.568,.021)],'charcoal',cy=.102)
    hollow(m,'Muzzle',.562,.596,.017,.017,'steel',cy=.102,wall=.006)
    profile(m,'Front_sight_block',[
        (.553,.089),(.579,.089),(.577,.147),(.564,.157),(.554,.145)
    ],.027,'charcoal')
    # Open, octagonal hood, with a real visible central post.
    hollow(m,'Front_sight_hood',.557,.577,.026,.026,'charcoal',cy=.168,sides=8,wall=.006)
    box(m,'Front_sight_post',(.567,.161,0),(.009,.026,.006),'steel')
    # Folded bayonet runs under the barrel. Four facets form a blade, not a solid cylinder.
    blade=m.part('Folded_bayonet_blade','steel')
    tip=(.072,.025,0)
    ring=[(.444,.049,0),(.444,.034,.012),(.444,.019,0),(.444,.034,-.012)]
    end=[(.493,.046,0),(.493,.034,.010),(.493,.022,0),(.493,.034,-.010)]
    for i in range(4):
        j=(i+1)%4
        blade.face([tip,ring[i],ring[j]])
        blade.face([ring[i],end[i],end[j],ring[j]])
    blade.face(list(reversed(end)))
    lathe(m,'Bayonet_sleeve',[(.481,.02),(.526,.02)],'charcoal',cy=.034)
    profile(m,'Bayonet_mount',[
        (.488,.09),(.536,.09),(.541,.066),(.525,.043),(.491,.043)
    ],.041,'charcoal')
    rod(m,'Bayonet_pivot',(.517,.051,-.032),(.517,.051,.032),.015,'steel',sides=8)
    profile(m,'Bayonet_latch',[(.508,.032),(.527,.032),(.523,-.006),(.512,-.011),(.504,-.001)],.028,'charcoal')
    rod(m,'Cleaning_rod',(.294,.065,-.018),(.552,.065,-.018),.004,'steel')
    # Sling loops are understated editable geometry.
    beam(m,'Rear_sling_loop_left',(-.507,-.079),(-.511,-.099),.006,.017,'charcoal')
    beam(m,'Rear_sling_loop_bottom',(-.511,-.099),(-.484,-.097),.006,.017,'charcoal')
    beam(m,'Rear_sling_loop_right',(-.484,-.097),(-.48,-.073),.006,.017,'charcoal')
    for z in [-.039,.039]:
        box(m,'Stock_crossbolt',(.016,.072,z),(.013,.013,.004),'steel')
    m.markers={'Muzzle':(.597,.102,0),'Grip':(-.326,.017,0),'Support':(.161,.071,0)}
    return m


def main():
    model=sks()
    for folder in ['models','previews']:(ROOT/folder).mkdir(exist_ok=True)
    records=[export_glb(model)]
    data={'palette':PALETTE,'models':records}
    (ROOT/'source'/'mesh_data.json').write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    manifest={'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    make_previews([model],edition='04',first_index=1)
    template=(ROOT/'source'/'viewer_template.html').read_text(encoding='utf-8-sig')
    (ROOT/'preview.html').write_text(template.replace('__ASSET_DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
    print(f'{model.name}: {model.triangle_count} triangles / {len(model.parts)} parts / {len(set(p.material for p in model.parts))} colors')

if __name__=='__main__':main()
