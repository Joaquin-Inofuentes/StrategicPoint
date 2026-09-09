import os
import colorsys
from PIL import Image

def get_colors():
    img_path = r"C:\_Proyectos privados\DV_C6_StrategicPoint\My project\Assets\ARTS\SP_Arte\Trimsheet.png"
    img = Image.open(img_path).convert("RGB")
    w, h = img.size
    
    cols = 6
    rows = 2
    cw = w / cols
    ch = h / rows
    
    colors = []
    for r in range(rows):
        for c in range(cols):
            x = int((c + 0.5) * cw)
            y = int((r + 0.5) * ch)
            colors.append(img.getpixel((x, y)))
            
    return colors

def darken_color(rgb, factor=0.6, sat_factor=0.7):
    # rgb are 0-255
    r, g, b = [x / 255.0 for x in rgb]
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    
    # Make somber: reduce lightness and saturation
    l = max(0, l * factor)
    s = max(0, s * sat_factor)
    
    r, g, b = colorsys.hls_to_rgb(h, l, s)
    return (int(r * 255), int(g * 255), int(b * 255))

def color_sort_key(rgb):
    r, g, b = [x / 255.0 for x in rgb]
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    
    # Sort by hue primarily, then brightness
    # group hues into buckets to keep similar colors together
    return (int(h * 8), v)

def main():
    colors = get_colors()
    
    # Darken colors to make them more somber
    somber_colors = [darken_color(c) for c in colors]
    
    # We need 16 colors for a 4x4 grid (to have square textures in a 1024x1024 map)
    # Let's generate 4 more by further darkening some or adding gray/black variations.
    extra_colors = [
        darken_color(somber_colors[0], 0.7),
        darken_color(somber_colors[4], 0.7), # dark green darker
        darken_color(somber_colors[7], 0.7), # another variation
        (25, 25, 28) # very dark somber gray
    ]
    all_colors = somber_colors + extra_colors
    
    # Sort by hue
    all_colors.sort(key=color_sort_key)
    
    # Create 1024x1024 image
    new_img = Image.new("RGB", (1024, 1024))
    pixels = new_img.load()
    
    cell_size = 256
    for idx, color in enumerate(all_colors):
        row = idx // 4
        col = idx % 4
        for y in range(cell_size):
            for x in range(cell_size):
                pixels[col * cell_size + x, row * cell_size + y] = color
                
    out_path = r"C:\_Proyectos privados\DV_C6_StrategicPoint\My project\Assets\ARTS\SP_Arte\Trimsheet_Nuevo.png"
    new_img.save(out_path)
    print(f"Saved {out_path}")

if __name__ == '__main__':
    main()
