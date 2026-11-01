"""Build the art guide PDF from docs/art-guide.md. Requires ReportLab."""
from pathlib import Path
import html
import re

from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer, PageBreak, Flowable

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf/duck-rails-art-guide.pdf'
SOURCE = (ROOT / 'docs/art-guide.md').read_text(encoding='utf-8')
VERSION = re.search(r'^Version ([\d.]+)', SOURCE, re.MULTILINE).group(1)
INK = colors.HexColor('#15232D')
TEAL = colors.HexColor('#287477')
MUTED = colors.HexColor('#485B63')
PAPER = colors.HexColor('#FAF8F2')
AMBER = colors.HexColor('#E9AE45')
PALETTE = [
    ('Night ink', '#15232D'), ('Weathered steel', '#485B63'),
    ('Railway teal', '#287477'), ('Warm ivory', '#E8DDC0'),
    ('Safety amber', '#E9AE45'), ('Alarm red', '#C74C3C'),
    ('Dawn peach', '#EFB58D'),
]

font_dir = Path('C:/Windows/Fonts')
if (font_dir / 'segoeui.ttf').exists():
    for name, file in [('Guide', 'segoeui.ttf'), ('GuideBold', 'segoeuib.ttf'), ('GuideItalic', 'segoeuii.ttf')]:
        pdfmetrics.registerFont(TTFont(name, str(font_dir / file)))
    pdfmetrics.registerFontFamily('Guide', normal='Guide', bold='GuideBold', italic='GuideItalic', boldItalic='GuideBold')
else:
    pdfmetrics.registerFontFamily('Helvetica', normal='Helvetica', bold='Helvetica-Bold', italic='Helvetica-Oblique', boldItalic='Helvetica-BoldOblique')

FONT = 'Guide' if 'Guide' in pdfmetrics.getRegisteredFontNames() else 'Helvetica'
BOLD = 'GuideBold' if FONT == 'Guide' else 'Helvetica-Bold'

styles = {
    'body': ParagraphStyle('body', fontName=FONT, fontSize=10, leading=14, textColor=INK, spaceAfter=7),
    'bullet': ParagraphStyle('bullet', fontName=FONT, fontSize=10, leading=14, textColor=INK, leftIndent=11, firstLineIndent=-10, spaceAfter=5),
    'heading': ParagraphStyle('heading', fontName=BOLD, fontSize=12.3, leading=16, textColor=TEAL, spaceBefore=8, spaceAfter=5, keepWithNext=True),
    'title': ParagraphStyle('title', fontName=BOLD, fontSize=26, leading=31, textColor=INK, spaceAfter=13),
    'lead': ParagraphStyle('lead', fontName=BOLD, fontSize=13, leading=18, textColor=INK, spaceAfter=10),
    'small': ParagraphStyle('small', fontName=FONT, fontSize=8, leading=11, textColor=MUTED, spaceAfter=7),
}

class Swatches(Flowable):
    def __init__(self):
        Flowable.__init__(self)
        self.width = 507
        self.height = 77

    def draw(self):
        c = self.canv
        for i, (label, value) in enumerate(PALETTE):
            x = i * 73
            c.setFillColor(colors.HexColor(value))
            c.rect(x, 26, 65, 43, stroke=0, fill=1)
            c.setFillColor(INK)
            c.setFont(BOLD, 7)
            c.drawString(x, 14, label)
            c.setFont(FONT, 7)
            c.drawString(x, 3, value)

class MoodStrip(Flowable):
    def __init__(self):
        Flowable.__init__(self)
        self.width = 507
        self.height = 70

    def draw(self):
        c = self.canv
        items = [('GATHER', '#287477', 'A welcoming compound'), ('VENTURE', '#15232D', 'A warm train in a cold world'), ('RETURN', '#EFB58D', 'An open horizon at dawn')]
        for i, (label, color, caption) in enumerate(items):
            x = i * 171
            c.setFillColor(colors.HexColor(color))
            c.rect(x, 18, 165, 46, fill=1, stroke=0)
            c.setFillColor(INK if i == 2 else colors.HexColor('#E8DDC0'))
            c.setFont(BOLD, 10)
            c.drawString(x + 11, 36, label)
            c.setFillColor(MUTED)
            c.setFont(FONT, 8)
            c.drawString(x, 4, caption)

def markup(text):
    text = html.escape(text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'`(.+?)`', r'\1', text)
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'\1 (\2)', text)
    return text

def furniture(c, doc):
    width, height = doc.pagesize
    c.saveState()
    c.setFillColor(PAPER)
    c.rect(0, 0, width, height, fill=1, stroke=0)
    c.setFillColor(TEAL)
    c.rect(44, height - 34, 25, 3, stroke=0, fill=1)
    c.setFont(BOLD, 8)
    c.drawString(79, height - 35, 'DUCK RAILS / ART GUIDE')
    c.setFillColor(MUTED)
    c.setFont(FONT, 8)
    c.drawRightString(width - 44, height - 35, f'CREATIVE DIRECTION DRAFT / {VERSION}')
    c.setStrokeColor(colors.HexColor('#D6DCD8'))
    c.line(44, 37, width - 44, 37)
    c.setFont(FONT, 7.5)
    c.drawString(44, 23, 'October 30, 2026  |  Proposed art standard; preserve confirmed gameplay')
    c.drawRightString(width - 44, 23, f'{doc.page:02d}')
    c.restoreState()

def build():
    source = SOURCE
    sections = source.split('\n---\n')
    story = []
    for index, section in enumerate(sections):
        if index:
            story.append(PageBreak())
        blocks = re.split(r'\n\s*\n', section.strip())
        lead_used = False
        for block in blocks:
            block = block.strip()
            if block.startswith('# Duck Rails') or block.startswith('Version ') or block.startswith('The editable source'):
                continue
            if block.startswith('## '):
                title = re.sub(r'^## \d+ ', '', block)
                story.append(Paragraph(title, styles['title']))
            elif block.startswith('### '):
                story.append(Paragraph(markup(block[4:]), styles['heading']))
                if block == '### Working palette':
                    story.append(Swatches())
                    story.append(Spacer(1, 5))
            elif block.startswith('- ') or re.match(r'^\d+\. ', block):
                for line in block.splitlines():
                    if line.startswith('- '):
                        line = '&#8226; ' + markup(line[2:])
                    else:
                        line = markup(line)
                    story.append(Paragraph(line, styles['bullet']))
            else:
                lead = block.startswith('**') and not lead_used
                style = styles['lead'] if lead else styles['body']
                if block.startswith('Design authority:') or block.startswith('The editable source'):
                    style = styles['small']
                story.append(Paragraph(markup(block.replace('\n', ' ')), style))
                if lead:
                    lead_used = True
                    if index == 0:
                        story.append(MoodStrip())
                        story.append(Spacer(1, 8))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc = BaseDocTemplate(str(OUT), pagesize=(595.28, 841.89), leftMargin=44, rightMargin=44,
                          topMargin=58, bottomMargin=49, title='Duck Rails Art Guide',
                          author='Duck Rails', subject=f'Creative direction draft {VERSION}')
    frame = Frame(44, 49, 507.28, 734.89, leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    doc.addPageTemplates(PageTemplate(id='guide', frames=frame, onPage=furniture))
    doc.build(story)
    print(OUT)

if __name__ == '__main__':
    build()
