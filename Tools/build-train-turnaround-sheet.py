"""Package generated concept views and a proposed material atlas; no 3D authoring."""
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.lib.utils import ImageReader

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf/duck-rails-train-turnaround-and-atlas-v6.pdf'
W, H = 1190, 842
c = canvas.Canvas(str(OUT), pagesize=(W, H))
c.setTitle('Duck Rails / Five views and texture atlas')
c.setAuthor('Duck Rails')

def text(x,y,s,size=11,bold=False,color='#233C43'):
    c.setFillColor(HexColor(color))
    c.setFont('Helvetica-Bold' if bold else 'Helvetica',size)
    c.drawString(x,y,s)

def page(title,subtitle,n):
    c.setFillColor(HexColor('#FAF8F2'));c.rect(0,0,W,H,fill=1,stroke=0)
    text(48,793,title,25,True)
    text(48,769,subtitle,11)
    text(48,25,'DUCK RAILS / October 30, 2026 / 2D concept reference; no Unity or mesh changes',9)
    text(W-75,25,str(n),10,True)

def picture(name,x,y,w,h):
    im=ImageReader(str(ROOT/'output/imagegen'/name));iw,ih=im.getSize()
    scale=min(w/iw,h/ih);dw,dh=iw*scale,ih*scale
    c.drawImage(im,x+(w-dw)/2,y+(h-dh)/2,dw,dh)

page('Train turnaround / five views',
     'Front, back, right side, top and rear-right three-quarter. Same station arrangement; generated proportions remain illustrative.',1)
picture('duck-rails-train-turnaround-v6.png',48,65,W-96,690)
c.showPage()

page('Texture atlas / proposed material tiles',
     'Flat color/material draft for later UV mapping. Fire and smoke remain separate particle effects.',2)
picture('duck-rails-train-texture-atlas-v1.png',48,85,650,650)
rows=[
    ('ROW 1 / TOP', ['1  Teal hull paint','2  Ivory cockpit trim','3  Amber rail trim','4  Dark chassis steel']),
    ('ROW 2', ['1  Deck planks','2  Furnace casing','3  Sooty firebox interior','4  Seat rubber']),
    ('ROW 3', ['1  Radio casing','2  Speaker grille','3  Exposed metal','4  Table wood']),
    ('ROW 4 / BOTTOM', ['1  Lamp lens color','2  Horn button red','3  Control panel background','4  Neutral metal trim'])
]
y=716
for title,items in rows:
    text(746,y,title,12,True);y-=22
    for s in items:
        text(746,y,s,11);y-=18
    y-=15
text(746,264,'Columns read left to right.',10)
text(746,237,'AUTHORING NOTES',12,True)
for i,s in enumerate([
    'Not yet UV-bound to a model.',
    'Assign tiles to the separate modules.',
    'Add UV gutters and edge padding.',
    'Choose final pixel density and texture size.',
    'Author gauge/icon decals and masks separately.',
    'Test sampling, compression and mip behavior.',
    'Grille holes are texture details, not geometry.',
    'No fire or smoke baked into this atlas.'
]):text(746,216-i*18,s,10.5)
c.showPage()
c.save()
print(OUT)
