"""
Generate app.ico - draws each size individually for pixel-perfect results.
Requires: pip install pillow
"""
from PIL import Image, ImageDraw

def draw_256(size=256):
    img = Image.new("RGBA", (size, size), (0,0,0,0))
    d = ImageDraw.Draw(img)
    fd, fl, lk = (75,110,195,255), (120,155,230,255), (26,26,46,255)

    # Folder back
    d.rounded_rectangle([10,72,246,242], radius=14, fill=fd)
    # Tab
    d.polygon([(10,72),(10,50),(20,38),(104,38),(118,52),(132,72)], fill=fd)
    # Folder face
    d.rounded_rectangle([10,100,246,242], radius=12, fill=fl)
    # Shine
    d.rounded_rectangle([10,100,246,122], radius=12, fill=(145,175,235,80))

    # Shackle outer
    d.arc([90,84,166,140], 180, 0, fill=lk, width=18)
    d.rectangle([90,112,108,142], fill=lk)
    d.rectangle([148,112,166,142], fill=lk)
    # Lock body
    d.rounded_rectangle([88,138,168,210], radius=10, fill=lk)
    # Shackle inner cutout
    d.arc([98,92,158,148], 180, 0, fill=fl, width=9)
    d.rectangle([98,120,107,142], fill=fl)
    d.rectangle([149,120,158,142], fill=fl)
    # Keyhole
    d.ellipse([117,155,139,177], fill=fd)
    d.rounded_rectangle([124,166,132,184], radius=3, fill=fd)
    return img

def draw_64(size=64):
    img = Image.new("RGBA", (size, size), (0,0,0,0))
    d = ImageDraw.Draw(img)
    fd, fl, lk = (75,110,195,255), (120,155,230,255), (26,26,46,255)

    d.rounded_rectangle([2,18,62,61], radius=4, fill=fd)
    d.polygon([(2,18),(2,12),(6,9),(26,9),(30,13),(34,18)], fill=fd)
    d.rounded_rectangle([2,25,62,61], radius=4, fill=fl)

    d.arc([22,20,42,36], 180, 0, fill=lk, width=5)
    d.rectangle([22,28,27,35], fill=lk)
    d.rectangle([37,28,42,35], fill=lk)
    d.rounded_rectangle([21,34,43,52], radius=3, fill=lk)
    d.arc([25,22,39,38], 180, 0, fill=fl, width=3)
    d.rectangle([25,30,28,35], fill=fl)
    d.rectangle([36,30,39,35], fill=fl)
    d.ellipse([28,38,36,46], fill=fd)
    d.rectangle([30,42,34,48], fill=fd)
    return img

def draw_48(size=48):
    img = Image.new("RGBA", (size, size), (0,0,0,0))
    d = ImageDraw.Draw(img)
    fd, fl, lk = (75,110,195,255), (120,155,230,255), (26,26,46,255)

    d.rounded_rectangle([2,14,46,46], radius=3, fill=fd)
    d.polygon([(2,14),(2,9),(5,7),(20,7),(23,10),(26,14)], fill=fd)
    d.rounded_rectangle([2,19,46,46], radius=3, fill=fl)

    d.arc([16,15,32,27], 180, 0, fill=lk, width=4)
    d.rectangle([16,21,20,26], fill=lk)
    d.rectangle([28,21,32,26], fill=lk)
    d.rounded_rectangle([15,25,33,39], radius=3, fill=lk)
    d.arc([19,17,29,29], 180, 0, fill=fl, width=2)
    d.rectangle([19,23,21,26], fill=fl)
    d.rectangle([27,23,29,26], fill=fl)
    d.ellipse([21,29,27,35], fill=fd)
    d.rectangle([23,32,25,37], fill=fd)
    return img

def draw_32(size=32):
    img = Image.new("RGBA", (size, size), (0,0,0,0))
    d = ImageDraw.Draw(img)
    fd, fl, lk = (75,110,195,255), (120,155,230,255), (26,26,46,255)

    d.rounded_rectangle([1,9,31,31], radius=2, fill=fd)
    d.polygon([(1,9),(1,6),(4,4),(13,4),(16,7),(18,9)], fill=fd)
    d.rounded_rectangle([1,13,31,31], radius=2, fill=fl)

    d.arc([10,10,22,18], 180, 0, fill=lk, width=3)
    d.rectangle([10,14,13,17], fill=lk)
    d.rectangle([19,14,22,17], fill=lk)
    d.rounded_rectangle([9,16,23,26], radius=2, fill=lk)
    d.arc([12,11,20,19], 180, 0, fill=fl, width=2)
    d.rectangle([12,15,14,17], fill=fl)
    d.rectangle([18,15,20,17], fill=fl)
    d.ellipse([14,19,18,23], fill=fd)
    d.rectangle([15,21,17,25], fill=fd)
    return img

def draw_16(size=16):
    img = Image.new("RGBA", (size, size), (0,0,0,0))
    d = ImageDraw.Draw(img)
    fd, fl, lk = (75,110,195,255), (120,155,230,255), (26,26,46,255)

    d.rounded_rectangle([1,5,15,15], radius=1, fill=fd)
    d.polygon([(1,5),(1,3),(3,2),(7,2),(9,4),(10,5)], fill=fd)
    d.rounded_rectangle([1,7,15,15], radius=1, fill=fl)

    d.arc([5,5,11,9], 180, 0, fill=lk, width=2)
    d.rectangle([5,7,7,9], fill=lk)
    d.rectangle([9,7,11,9], fill=lk)
    d.rounded_rectangle([4,8,12,14], radius=1, fill=lk)
    d.ellipse([7,9,9,12], fill=fd)
    return img

# Also generate 128 by downscaling from 256
img256 = draw_256(256)
img128 = img256.resize((128,128), Image.LANCZOS)
img64  = draw_64()
img48  = draw_48()
img32  = draw_32()
img16  = draw_16()

for sz, img in [(256,img256),(128,img128),(64,img64),(48,img48),(32,img32),(16,img16)]:
    print(f"Ready {sz}x{sz}")

ico_path = r"C:\Users\sglomb\source\NTFSPermissionReporter\app.ico"

img256.save(
    ico_path,
    format="ICO",
    sizes=[(256,256),(128,128),(64,64),(48,48),(32,32),(16,16)],
    append_images=[img128, img64, img48, img32, img16]
)

# Verify sizes
with Image.open(ico_path) as f:
    print(f"\nEmbedded: {sorted(f.info.get('sizes',[]), reverse=True)}")
print(f"Saved: {ico_path}")
