import math
import numpy as np
from model_tools import lathe

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
