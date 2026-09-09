import os
import colorsys
import math
from PIL import Image

def get_trimsheet_colors():
    img_path = r"C:\_Proyectos privados\DV_C6_StrategicPoint\My project\Assets\ARTS\SP_Arte\Trimsheet.png"
    img = Image.open(img_path).convert("RGB")
    w, h = img.size
    cols, rows = 6, 2
    cw, ch = w / cols, h / rows
    colors = []
    for r in range(rows):
        for c in range(cols):
            x = int((c + 0.5) * cw)
            y = int((r + 0.5) * ch)
            colors.append(img.getpixel((x, y)))
            
    def darken_color(rgb, factor=0.6, sat_factor=0.7):
        r, g, b = [x / 255.0 for x in rgb]
        h, l, s = colorsys.rgb_to_hls(r, g, b)
        l = max(0, l * factor)
        s = max(0, s * sat_factor)
        r, g, b = colorsys.hls_to_rgb(h, l, s)
        return (int(r * 255), int(g * 255), int(b * 255))

    somber_colors = [darken_color(c) for c in colors]
    extra_colors = [
        darken_color(somber_colors[0], 0.7),
        darken_color(somber_colors[4], 0.7),
        darken_color(somber_colors[7], 0.7),
        (25, 25, 28)
    ]
    all_colors = somber_colors + extra_colors
    
    def color_sort_key(rgb):
        r, g, b = [x / 255.0 for x in rgb]
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        return (int(h * 8), v)
        
    all_colors.sort(key=color_sort_key)
    return all_colors

def color_distance(c1, c2):
    return math.sqrt(sum((a - b) ** 2 for a, b in zip(c1, c2)))

def get_average_color(image_path):
    img = Image.open(image_path).convert("RGB")
    img.thumbnail((16, 16))
    pixels = list(img.getdata())
    r = sum(p[0] for p in pixels) // len(pixels)
    g = sum(p[1] for p in pixels) // len(pixels)
    b = sum(p[2] for p in pixels) // len(pixels)
    return (r, g, b)

def get_closest_color(target, colors):
    return min(colors, key=lambda c: color_distance(target, c))

base_dir = r"C:\_Proyectos privados\DV_C6_StrategicPoint\My project\Assets\ARTS\SP_Arte"
models = [
    {"name": "Arbol 1", "label": "greenColor"},
    {"name": "Arbol 3", "label": "greenColor"},
    {"name": "Barricada", "label": "orange"},
    {"name": "Barril", "label": "violet"},
    {"name": "Soldado", "label": "blue"}
]

trimsheet_colors = get_trimsheet_colors()

jsx_code = """
try {
    var doc = app.activeDocument;
    
    function setLayerColor(layer, colorName) {
        var desc = new ActionDescriptor();
        var ref = new ActionReference();
        ref.putIdentifier(charIDToTypeID('Lyr '), layer.id);
        desc.putReference(charIDToTypeID('null'), ref);
        var desc2 = new ActionDescriptor();
        desc2.putEnumerated(charIDToTypeID('Clr '), charIDToTypeID('Clr '), stringIDToTypeID(colorName));
        desc.putObject(charIDToTypeID('T   '), charIDToTypeID('Lyr '), desc2);
        try { executeAction(charIDToTypeID('setd'), desc, DialogModes.NO); } catch(e) {}
    }

    function placeLinkedSmartObject(filePath) {
        var idPlc = charIDToTypeID( "Plc " );
        var desc = new ActionDescriptor();
        var idnull = charIDToTypeID( "null" );
        desc.putPath( idnull, new File( filePath ) );
        var idFTcs = charIDToTypeID( "FTcs" );
        var idQCSt = charIDToTypeID( "QCSt" );
        var idQcsa = charIDToTypeID( "Qcsa" );
        desc.putEnumerated( idFTcs, idQCSt, idQcsa );
        var idLnkd = charIDToTypeID( "Lnkd" );
        desc.putBoolean( idLnkd, true );
        executeAction( idPlc, desc, DialogModes.NO );
        return doc.activeLayer;
    }

    function createSolidColorLayer(r, g, b, layerName) {
        var idMk = charIDToTypeID( "Mk  " );
        var desc = new ActionDescriptor();
        var idnull = charIDToTypeID( "null" );
        var ref = new ActionReference();
        var idcontentLayer = stringIDToTypeID( "contentLayer" );
        ref.putClass( idcontentLayer );
        desc.putReference( idnull, ref );
        var idUsng = charIDToTypeID( "Usng" );
        var desc2 = new ActionDescriptor();
        var idNm = charIDToTypeID( "Nm  " );
        desc2.putString( idNm, layerName );
        var idType = charIDToTypeID( "T   " );
        var desc3 = new ActionDescriptor();
        var idClr = charIDToTypeID( "Clr " );
        var desc4 = new ActionDescriptor();
        desc4.putDouble( charIDToTypeID( "Rd  " ), r );
        desc4.putDouble( charIDToTypeID( "Grn " ), g );
        desc4.putDouble( charIDToTypeID( "Bl  " ), b );
        desc3.putObject( idClr, charIDToTypeID( "RGBC" ), desc4 );
        var idsolidColorLayer = stringIDToTypeID( "solidColorLayer" );
        desc2.putObject( idType, idsolidColorLayer, desc3 );
        desc.putObject( idUsng, idcontentLayer, desc2 );
        executeAction( idMk, desc, DialogModes.NO );
        return doc.activeLayer;
    }
"""

for model in models:
    model_name = model["name"]
    model_dir = os.path.join(base_dir, model_name)
    
    # Create group
    jsx_code += f"""
    var group_{model_name.replace(' ', '')} = doc.layerSets.add();
    group_{model_name.replace(' ', '')}.name = "{model_name}";
    setLayerColor(group_{model_name.replace(' ', '')}, "{model['label']}");
    """
    
    basemap_color = None
    if os.path.isdir(model_dir):
        for f in os.listdir(model_dir):
            if f.endswith(".png") and ("BaseMap" in f or "MaskMap" in f or "Normal" in f):
                t = "BaseMap" if "BaseMap" in f else "MaskMap" if "MaskMap" in f else "Normal"
                path = os.path.join(model_dir, f).replace("\\", "/")
                
                jsx_code += f"""
                try {{
                    var layer = placeLinkedSmartObject("{path}");
                    layer.name = "{t}";
                    layer.move(group_{model_name.replace(' ', '')}, ElementPlacement.INSIDE);
                }} catch(e) {{ }}
                """
                
                if t == "BaseMap":
                    avg = get_average_color(os.path.join(model_dir, f))
                    basemap_color = get_closest_color(avg, trimsheet_colors)

    if basemap_color:
        r, g, b = basemap_color
        jsx_code += f"""
        try {{
            var baseMapLayer = group_{model_name.replace(' ', '')}.layers.getByName("BaseMap");
            doc.activeLayer = baseMapLayer;
            var colorLayer = createSolidColorLayer({r}, {g}, {b}, "Color Trimsheet");
            colorLayer.blendMode = BlendMode.MULTIPLY;
            colorLayer.grouped = true; 
        }} catch(e) {{ }}
        """

jsx_code += """
    return "SUCCESS_COMBINED";
} catch (e) {
    return "ERROR: " + e.toString();
}
"""

with open("script_combined.jsx", "w", encoding="utf-8") as f:
    f.write(jsx_code)

print("Generated script_combined.jsx")
