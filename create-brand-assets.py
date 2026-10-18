# ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
from pathlib import Path
from PIL import Image, ImageDraw
root=Path(__file__).parent
svg='''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<rect width="256" height="256" rx="56" fill="#152b46"/>
<path d="M128 24 214 82 202 170 128 232 54 170 42 82Z" fill="#63d8ed"/>
<path d="M128 24 104 96 42 82Z" fill="#c9f5ff"/>
<path d="M128 24 214 82 156 96Z" fill="#8feaff"/>
<path d="m104 96 52 0 46 74-74 62Z" fill="#208bd4"/>
<path d="M42 82 104 96 128 232 54 170Z" fill="#44bfdc"/>
<path d="M156 96 214 82 202 170Z" fill="#44b2ec"/>
</svg>'''
for name in ['Setup/Assets/Themes/Kobe/Logo.svg','Setup/Assets/Themes/Kobe-Light/Logo.svg']:
    (root/name).write_text(svg,encoding='utf-8')
img=Image.new('RGBA',(256,256))
d=ImageDraw.Draw(img)
d.rounded_rectangle((0,0,255,255),radius=56,fill='#152b46')
for points,color in [([(128,24),(214,82),(202,170),(128,232),(54,170),(42,82)],'#63d8ed'),([(128,24),(104,96),(42,82)],'#c9f5ff'),([(128,24),(214,82),(156,96)],'#8feaff'),([(104,96),(156,96),(202,170),(128,232)],'#208bd4'),([(42,82),(104,96),(128,232),(54,170)],'#44bfdc'),([(156,96),(214,82),(202,170)],'#44b2ec')]:
    d.polygon(points,fill=color)
img.save(root/'Source/Components/ImageGlass.UI/Resources/DefaultLogo.png')
img.save(root/'Source/ImageGlass/icon256.ico',sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
img.save(root/'Setup/Assets/default.webp')
print('Original crystal logo assets written.')
