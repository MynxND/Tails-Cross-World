from pathlib import Path
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, HRFlowable

root = Path(__file__).resolve().parents[1]
out = root / 'output' / 'pdf' / '12-tails-development-status.pdf'
out.parent.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('Tahoma', r'C:\Windows\Fonts\tahoma.ttf'))
pdfmetrics.registerFont(TTFont('Tahoma-Bold', r'C:\Windows\Fonts\tahomabd.ttf'))
s = getSampleStyleSheet()
s.add(ParagraphStyle(name='T', parent=s['Title'], fontName='Tahoma-Bold', fontSize=22, leading=28, alignment=TA_CENTER, textColor=colors.HexColor('#12304A')))
s.add(ParagraphStyle(name='Sub', parent=s['Normal'], fontName='Tahoma', fontSize=10, leading=15, alignment=TA_CENTER, textColor=colors.HexColor('#52616B'), spaceAfter=12))
s.add(ParagraphStyle(name='H', parent=s['Heading1'], fontName='Tahoma-Bold', fontSize=15, leading=21, textColor=colors.HexColor('#0B587A'), spaceBefore=8, spaceAfter=6))
s.add(ParagraphStyle(name='H2', parent=s['Heading2'], fontName='Tahoma-Bold', fontSize=11.5, leading=17, textColor=colors.HexColor('#12304A'), spaceBefore=6, spaceAfter=3))
s.add(ParagraphStyle(name='B', parent=s['BodyText'], fontName='Tahoma', fontSize=9.2, leading=14.5, textColor=colors.HexColor('#263238'), spaceAfter=5))
s.add(ParagraphStyle(name='C', parent=s['BodyText'], fontName='Tahoma', fontSize=8.2, leading=11.5, textColor=colors.HexColor('#263238')))
s.add(ParagraphStyle(name='CB', parent=s['BodyText'], fontName='Tahoma-Bold', fontSize=8.2, leading=11.5, textColor=colors.HexColor('#12304A')))
P = lambda x, y='B': Paragraph(x, s[y])
def footer(c, d):
    c.saveState(); c.setStrokeColor(colors.HexColor('#D6E2EA')); c.line(18*mm, 14*mm, 192*mm, 14*mm); c.setFont('Tahoma', 7.5); c.setFillColor(colors.HexColor('#607D8B')); c.drawString(18*mm, 9*mm, '12 Tails Offline Development Status'); c.drawRightString(192*mm, 9*mm, f'หน้า {d.page}'); c.restoreState()

story = [P('12 Tails Offline', 'T'), P('สรุปสิ่งที่ทำแล้วและแผนงานที่เหลือ', 'Sub'), HRFlowable(width='100%', thickness=1.2, color=colors.HexColor('#2C9AB7')), Spacer(1, 7)]
story += [P('ภาพรวมปัจจุบัน', 'H'), P('โครงการมี vertical slice ที่เล่นได้ใน Unity 6 และมี authoritative LAN server แล้ว ขณะนี้กำลังย้ายจาก asset จำลองไปใช้ asset ต้นฉบับ โดยเก็บ asset ต้นฉบับไว้ในโฟลเดอร์ private ที่ไม่ถูก commit เข้า Git', 'B')]
rows = [['สถานะ', 'Phase', 'ผลลัพธ์'], ['เสร็จแล้ว', '0-3', 'inventory, contracts, offline gameplay, save/load, LAN authority และ tests'], ['เสร็จส่วนใหญ่', '4', 'data-driven content, roster 12 ตัว, prefab pipeline และ original character import'], ['กำลังทำ', '4 production', 'shader/material conversion, equipment/accessory และ visual parity'], ['ยังเหลือ', '5-6', 'online alpha, security, operations และ public readiness']]
t = Table([[P(a, 'CB' if i == 0 else 'C') for a in row] for i, row in enumerate(rows)], colWidths=[30*mm, 35*mm, 101*mm], repeatRows=1)
t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#D9EEF5')),('GRID',(0,0),(-1,-1),.35,colors.HexColor('#B0C9D4')),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),5),('RIGHTPADDING',(0,0),(-1,-1),5),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5)]))
story += [t, P('สิ่งที่ทำเสร็จแล้ว', 'H')]
for x in ['Phase 0: public Git boundary, reference inspector, SHA-256 inventory, policy tests และ CI', 'Phase 1: assembly metadata, evidence catalog, versioned contracts และ strict validation', 'Phase 2: Unity 6 URP, Training Ground, movement, combat, quest, loot, reward และ checksummed save/load', 'Phase 3: TCP LAN server, session, lobby, host migration, sync, authoritative combat/rewards, reconnect และ persistence', 'Phase 4 foundation: roster 12 ตัว, chapter/map data, portals, spawns, NPCs, editor validation และ prefab pipeline']:
    story.append(P('• ' + x))
story += [P('การใช้ asset ต้นฉบับ', 'H2'), P('กู้คืนโปรเจกต์ Unity 3.5 จากไฟล์ติดตั้งส่วนตัวด้วย AssetRipper และนำเข้า dependency ตาม GUID ครบทั้ง 12 ฉากตัวละคร รวม mesh, texture, material, animation และ prefab จากต้นฉบับ', 'B'), P('shader รุ่นเก่าถูกแปลงเป็น URP Lit โดยคง texture/UV ไว้ และ Cat ได้เพิ่ม accessory default hair เข้ากับกระดูก Head ตาม rotation จาก logic เดิม', 'B'), PageBreak(), P('หลักฐานการตรวจสอบล่าสุด', 'H'), P('Unity 6 โหลดฉากตัวละครต้นฉบับทั้ง 12 ฉากได้ และ validation ของ prefab หลังแปลง material ผ่านครบทุกตัว', 'B')]
data = [['ตัวละคร', 'Renderers', 'Meshes', 'Materials', 'ผล'], ['Wolf','213','7','61','ผ่าน'],['Bison','139','12','43','ผ่าน'],['Panda','166','23','61','ผ่าน'],['Whale','122','13','47','ผ่าน'],['Cat','183','11','63','ผ่าน'],['Chameleon','145','9','63','ผ่าน'],['Rabbit','160','14','62','ผ่าน'],['Mole','166','33','79','ผ่าน'],['Monkey','138','25','65','ผ่าน'],['Penguin','124','16','55','ผ่าน'],['Sheep','139','22','49','ผ่าน'],['Bat','107','8','55','ผ่าน']]
t2 = Table([[P(a, 'CB' if i == 0 else 'C') for a in row] for i, row in enumerate(data)], colWidths=[35*mm, 32*mm, 27*mm, 34*mm, 24*mm], repeatRows=1)
t2.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#D9EEF5')),('GRID',(0,0),(-1,-1),.35,colors.HexColor('#B0C9D4')),('ALIGN',(1,1),(-1,-1),'CENTER'),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),4),('RIGHTPADDING',(0,0),(-1,-1),4),('TOPPADDING',(0,0),(-1,-1),4),('BOTTOMPADDING',(0,0),(-1,-1),4)]))
story += [t2, P('การทดสอบที่ผ่าน', 'H2'), P('• Unity EditMode tests ผ่าน<br/>• original character prefab validation ผ่านครบ 12 ตัว และมี texture/shader รองรับ<br/>• Windows x64 development build สร้างและเปิดทดสอบได้<br/>• LAN server/client integration tests เดิมยังผ่าน', 'B'), P('ข้อจำกัด', 'H2'), P('asset ต้นฉบับอยู่ใน client/Assets/TwelveTails/LegacyPrivate และถูก ignore โดย Git เครื่องอื่นต้องมีไฟล์ต้นฉบับ private และรัน import ก่อนจึงจะแสดงผลเหมือนกัน ส่วน decompiled scripts เก็บเพื่ออ้างอิงแต่ไม่ compile เพราะ API UnityScript เดิมไม่เข้ากับ Unity 6', 'B'), PageBreak(), P('งานที่เหลือต้องทำต่อ', 'H')]
for title, desc in [('1. Production assets ของ Phase 4', 'นำเข้าแมพและมอนสเตอร์ต้นฉบับ พร้อม shader/material, VFX และ LOD'), ('2. Character assembly ครบชุด', 'เพิ่ม default weapon, armor, accessory, boot, trinket, pet และ skin variants'), ('3. Animation และ gameplay กับโมเดลจริง', 'จับคู่ idle/run/attack/death, socket, hitbox และ animation events'), ('4. Visual comparison', 'เทียบกล้อง แสง สี scale orientation และระยะมองกับ offline reference'), ('5. Phase 5 online alpha', 'TLS, secrets, metrics, backups/restore, launcher checks, moderation, load และ hostile-request tests'), ('6. Phase 6 public readiness', 'provenance/license, secret scan, performance, accessibility, compatibility, deployment และ rollback')]:
    story += [P(title, 'H2'), P(desc, 'B')]
story += [P('ลำดับถัดไปที่แนะนำ', 'H'), P('นำเข้าแมพและมอนสเตอร์ต้นฉบับต่อ แล้วทำ equipment/skin composition ให้ครบ จากนั้นทำ visual comparison และ animation pass ก่อนเริ่ม Phase 5', 'B'), Spacer(1, 10), HRFlowable(width='100%', thickness=.7, color=colors.HexColor('#B0C9D4')), Spacer(1, 5), P('อ้างอิง docs/ROADMAP.md และ commit ล่าสุด ณ วันที่ 11 กันยายน 2026', 'C')]
SimpleDocTemplate(str(out), pagesize=A4, rightMargin=18*mm, leftMargin=18*mm, topMargin=17*mm, bottomMargin=20*mm, title='12 Tails Development Status').build(story, onFirstPage=footer, onLaterPages=footer)
print(out)
