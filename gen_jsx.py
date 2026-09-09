import os
import json

base_dir = r"C:\_Proyectos privados\DV_C6_StrategicPoint\My project\Assets\ARTS\SP_Arte"
models = ["Arbol 1", "Arbol 3", "Barricada", "Barril", "Soldado"]

def get_maps_for_model(model_name):
    model_dir = os.path.join(base_dir, model_name)
    maps = []
    if os.path.isdir(model_dir):
        for f in os.listdir(model_dir):
            if f.endswith(".png") and ("BaseMap" in f or "MaskMap" in f or "Normal" in f):
                maps.append({
                    "path": os.path.join(model_dir, f).replace("\\", "/"),
                    "name": f.replace(".png", ""),
                    "type": "BaseMap" if "BaseMap" in f else "MaskMap" if "MaskMap" in f else "Normal"
                })
    return maps

jsx_code = """
try {
    var doc = app.activeDocument;
    
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

"""

for model in models:
    maps = get_maps_for_model(model)
    if not maps: continue
    
    jsx_code += f"""
    var group_{model.replace(' ', '')} = doc.layerSets.add();
    group_{model.replace(' ', '')}.name = "{model}";
    """
    
    for m in maps:
        jsx_code += f"""
    try {{
        var layer = placeLinkedSmartObject("{m['path']}");
        layer.name = "{m['type']}";
        layer.move(group_{model.replace(' ', '')}, ElementPlacement.INSIDE);
    }} catch(e) {{
        // ignore errors for missing files
    }}
    """

jsx_code += """
    return "SUCCESS";
} catch (e) {
    return "ERROR: " + e.toString();
}
"""

with open("script.jsx", "w", encoding="utf-8") as f:
    f.write(jsx_code)

print("Generated script.jsx")
