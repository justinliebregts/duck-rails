"""Create a 2D train layout and illustrated reference PDF. Requires ReportLab."""
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.lib.utils import ImageReader

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf/duck-rails-train-production-v5.pdf'
W, H = 1190, 842
INK, MUTED, TEAL = '#192D35', '#52656B', '#287477'

OUT.parent.mkdir(parents=True, exist_ok=True)
c = canvas.Canvas(str(OUT), pagesize=(W, H))
c.setTitle('Duck Rails Train Production Study v5')
c.setAuthor('Duck Rails')

def text(x, y, value, size=11, color=INK, bold=False):
    c.setFillColor(HexColor(color))
    c.setFont('Helvetica-Bold' if bold else 'Helvetica', size)
    c.drawString(x, y, value)

def line(x1, y1, x2, y2, color=MUTED, width=1):
    c.setStrokeColor(HexColor(color)); c.setLineWidth(width)
    c.line(x1,y1,x2,y2)

def rect(x,y,w,h,fill,stroke=INK):
    c.setFillColor(HexColor(fill)); c.setStrokeColor(HexColor(stroke))
    c.setLineWidth(1)
    c.rect(x,y,w,h,fill=1,stroke=1)

def page(title, subtitle, n):
    rect(0,0,W,H,'#FAF8F2','#FAF8F2')
    text(48,792,title,25,bold=True)
    text(48,768,subtitle,11,MUTED)
    line(48,42,W-48,42,'#CED7D3')
    text(48,25,'DUCK RAILS  /  October 30, 2026  /  2D design only; no model or Unity changes',9,MUTED)
    text(W-75,25,f'{n:02d}',10,TEAL,bold=True)

def dimv(x,y0,y1,label,labelx=None):
    line(x,y0,x,y1,TEAL)
    line(x-4,y0,x+4,y0,TEAL);line(x-4,y1,x+4,y1,TEAL)
    text(labelx if labelx is not None else x+8,(y0+y1)/2-3,label,10,TEAL,bold=True)

page('Train layout and interaction heights',
     'Provisional arrangement. Use this plan and the written brief for placement; rendered views are illustrative.',1)

# Plan uses a 10 x 5 m rectangle, 65 drawing points per metre.
px,py,s = 75,355,65
rect(px,py,10*s,5*s,'#EFE8D7',TEAL)
text(px,724,'TOP PLAN / FRONT AT LEFT',12,TEAL,True)
line(px,py+5*s+20,px+10*s,py+5*s+20)
line(px,py+5*s+15,px,py+5*s+25); line(px+10*s,py+5*s+15,px+10*s,py+5*s+25)
text(px+200,py+5*s+28,'10.0 m deck / builder reference',11,bold=True)
dimv(px-23,py,py+5*s,'5.0 m',px-67)
text(px+10,py+5*s-18,'TRAIN RIGHT',9,TEAL,True)
text(px+10,py+7,'TRAIN LEFT',9,TEAL,True)
def zone(z0,z1,x0,x1,label,color):
    x=px+(5-z1)*s; y=py+(x0+2.5)*s
    rect(x,y,(z1-z0)*s,(x1-x0)*s,color,TEAL)
    text(x+6,y+(x1-x0)*s/2-4,label,10,bold=True)

# Shelter is a roof zone, not a solid obstruction across the floor.
c.setDash(5,4); c.setStrokeColor(HexColor(TEAL))
c.rect(px,py,2*s,5*s,fill=0,stroke=1); c.setDash()
text(px+12,py+5*s-45,'FRONT ROOF',10,TEAL,True)
zone(4.45,4.85,-.45,.45,'T','#E9AE45')
zone(4.45,4.85,-1.1,-.6,'F','#DFC9C3')
zone(4.45,4.85,.6,1.1,'H','#E9AE45')
zone(3.25,4.15,-.35,.35,'SEAT','#C8D4CA')
zone(1.5,2.7,1.6,2.2,'RADIO','#DFC9C3')
zone(1.1,2.7,-2.3,-.7,'FURNACE','#D9C1B5')
# One inward-facing mouth; both stations are fully aft of the roof.
line(px+2.6*s,py+1.8*s,px+3.6*s,py+1.8*s,'#E9AE45',6)
c.setLineWidth(1);c.setStrokeColor(HexColor(INK));c.circle(px+3.1*s,py+.6*s,9,fill=0,stroke=1)
text(px+3.1*s+13,py+.6*s-3,'STACK',8,bold=True)
c.setFillColor(HexColor('#E9AE45'));c.circle(px-5,py+2.5*s,5,fill=1,stroke=0)
text(px+6,py+2.5*s+100,'T throttle / F fuel',8,MUTED)
text(px+6,py+2.5*s+87,'H horn',8,MUTED)
text(px+6,py+2.5*s-100,'Headlight at nose',8,MUTED)
# Amber denotes an opening in the rail, not a raised barrier.
line(px+10*s,py+2,px+10*s,py+5*s-2,'#E9AE45',4)
text(px+7.4*s,py+2.5*s+43,'REAR BOARDING',11,TEAL,True)
text(px+7.4*s,py+2.5*s+25,'No rear rail or cargo',10,MUTED)

# Gates are openings at the deck edges, not gaps in the floor.
gatex=px+4*s
for gy in [py,py+5*s]:
    line(gatex,gy,gatex+2*s,gy,'#E9AE45',7)
text(gatex+20,py+5*s-29,'BOARDING',10,TEAL,True)
text(gatex+20,py+20,'BOARDING',10,TEAL,True)
text(px+275,py+164,'OPEN CREW DECK',15,TEAL,True)
text(px+275,py+145,'Clear route from the rear',11,MUTED)
text(px+275,py+126,'2.0 m aisle target',10,MUTED)

text(75,319,'PROPOSED PLACEMENT',12,TEAL,True)
for j,t in enumerate([
    'Center seat; dash left-to-right: fuel gauge, throttle, horn. Forward headlight.',
    'Furnace and radio outside cockpit. Stack rises directly from furnace into open sky.',
    'Rear edge open for boarding: no transverse rail, displayed bags or cargo.',
    'Keep opposed sideboarding gaps too. Amber plan edges mark rail openings.',
    'Front roof only; two side lamps. No hanging lamp, coal bin or decorative coal.'
]): text(75,298-j*19,t,11)

# Height diagram 1: all dimensions measured from the same deck zero.
sx,sy,ss = 850,485,250
text(800,724,'FRONT SIGHTLINE / HEIGHT STUDY',12,TEAL,True)
line(800,sy,1138,sy,INK,2);text(800,sy-18,'DECK = 0',10,bold=True)
rect(sx+80,sy,18,.2*ss,'#287477')
line(sx+80,sy+.2*ss,sx+80,sy+.78*ss,TEAL,3)
line(sx+80,sy+.78*ss,1100,sy+.78*ss,TEAL,3)
c.setDash(5,4);line(815,sy+.5*ss,1135,sy+.5*ss,TEAL);c.setDash()
c.setFillColor(HexColor('#E9AE45'));c.circle(850,sy+.5*ss,5,fill=1,stroke=0)
text(810,sy+.5*ss+15,'EYE POINT',10,TEAL,True)
dimv(1120,sy,sy+.5*ss,'0.50 m',1050)
dimv(970,sy,sy+.2*ss,'0.20 m sill')
text(800,451,'Eye height: user reference. Sill: proposed starting height.',10,MUTED)
text(800,435,'This diagram checks height only; test downward sight to track.',10,MUTED)

# Height diagram 2 uses identical scale and zero-based measurements.
ty=230
text(800,404,'RADIO TABLE / HEIGHT STUDY',12,TEAL,True)
line(800,ty,1138,ty,INK,2);text(800,ty-18,'DECK = 0',10,bold=True)
top=ty+.35*ss
rect(900,top-8,200,8,'#A88F6D');line(920,ty,920,top-8,INK,4);line(1080,ty,1080,top-8,INK,4)
rect(1010,top,70,40,'#287477')
rect(925,top,30,9,'#E9AE45');line(941,top+9,941,top+39,INK,3)
rect(931,top+36,20,20,'#287477')
c.setDash(5,4);line(800,ty+.5*ss,1135,ty+.5*ss,TEAL);c.setDash()
dimv(815,ty,ty+.5*ss,'0.50 m eye')
dimv(1110,ty,top,'0.35 m',1037)
text(904,top-26,'Mic-base button faces inward',10,MUTED)

text(48,161,'REFERENCE AND VALIDATION',12,TEAL,True)
notes=[
    '10 x 5 m is the authored floor size in RailSceneBuilder.cs, not a fresh measurement of the active Unity scene.',
    'Enlarge the single furnace mouth; old 0.5 x 0.5 m target is superseded. Stack: 3x previous height above casing.',
    'Table proposal: 1.2 x 0.6 x 0.35 m high. Plain receiver, texture-only details; mic-base TALK is the physical button.',
    'Seat posture, low sightlines, hand reach and eight-player circulation require rig review; do not raise the camera to fit.',
    'Low-poly forms and pixel textures are approved; polygon, texture and lighting budgets still require Quest 3 profiling.'
]
for j,n in enumerate(notes):text(48,141-j*18,n,10.5)
c.showPage()

for n,title,name,caption in [
    (2,'Open-deck stations and rear boarding','duck-rails-train-top-plan-v3-centered.png',
     'Illustrative proportions only. Use page 1 for the proposed equipment layout and dimension references.'),
    (3,'Cockpit / center seat and forward view','duck-rails-train-cockpit-v3.png',
     'Throttle ahead of seat; headlight reveals the track. Two side lamps only. Camera is illustrative, not an eye-height test.'),
    (4,'Approved radio / earlier furnace for comparison','duck-rails-train-radio-furnace-v2.png',
     'Keep the radio on the left. Furnace on the right is superseded by page 5: larger opening and a stack three times taller.'),
    (5,'Furnace / open mouth and hollow firebox','duck-rails-furnace-v5.png',
     'Real opening into a hollow cube interior; fire is separate particle FX. Preserve the approved larger mouth and tall stack.')
]:
    page(title,caption,n)
    im=ImageReader(str(ROOT/'output/imagegen'/name));iw,ih=im.getSize()
    scale=min((W-96)/iw,690/ih);dw,dh=iw*scale,ih*scale
    c.drawImage(im,(W-dw)/2,62+(690-dh)/2,dw,dh)
    c.showPage()
c.save()
print(OUT)
