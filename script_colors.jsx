
try {
    var doc = app.activeDocument;
    
    function setLayerColor(layer, colorName) {
        var desc = new ActionDescriptor();
        var ref = new ActionReference();
        ref.putIdentifier(charIDToTypeID('Lyr '), layer.id);
        desc.putReference(charIDToTypeID('null'), ref);
        var desc2 = new ActionDescriptor();
        desc2.putEnumerated(charIDToTypeID('Clr '), charIDToTypeID('Clr '), charIDToTypeID(colorName));
        desc.putObject(charIDToTypeID('T   '), charIDToTypeID('Lyr '), desc2);
        try { executeAction(charIDToTypeID('setd'), desc, DialogModes.NO); } catch(e) {}
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
    
    function groupLayers(layerSet, colorName) {
        setLayerColor(layerSet, colorName);
    }

    try {
        var group = doc.layerSets.getByName("Arbol 1");
        groupLayers(group, "green");
        
        var baseMapLayer = group.artLayers.getByName("BaseMap");
        doc.activeLayer = baseMapLayer;
        
        // Create solid color layer
        var colorLayer = createSolidColorLayer(110, 101, 55, "Trimsheet Color (Multiply)");
        colorLayer.blendMode = BlendMode.MULTIPLY; // Blend mode to apply color to the channel
        colorLayer.grouped = true; // Clip to BaseMap
        
    } catch(e) {
        // Ignore if group or basemap doesn't exist
    }
    
    try {
        var group = doc.layerSets.getByName("Arbol 3");
        groupLayers(group, "green");
        
        var baseMapLayer = group.artLayers.getByName("BaseMap");
        doc.activeLayer = baseMapLayer;
        
        // Create solid color layer
        var colorLayer = createSolidColorLayer(144, 134, 73, "Trimsheet Color (Multiply)");
        colorLayer.blendMode = BlendMode.MULTIPLY; // Blend mode to apply color to the channel
        colorLayer.grouped = true; // Clip to BaseMap
        
    } catch(e) {
        // Ignore if group or basemap doesn't exist
    }
    
    try {
        var group = doc.layerSets.getByName("Barricada");
        groupLayers(group, "orange");
        
        var baseMapLayer = group.artLayers.getByName("BaseMap");
        doc.activeLayer = baseMapLayer;
        
        // Create solid color layer
        var colorLayer = createSolidColorLayer(123, 133, 120, "Trimsheet Color (Multiply)");
        colorLayer.blendMode = BlendMode.MULTIPLY; // Blend mode to apply color to the channel
        colorLayer.grouped = true; // Clip to BaseMap
        
    } catch(e) {
        // Ignore if group or basemap doesn't exist
    }
    
    try {
        var group = doc.layerSets.getByName("Barril");
        groupLayers(group, "violet");
        
        var baseMapLayer = group.artLayers.getByName("BaseMap");
        doc.activeLayer = baseMapLayer;
        
        // Create solid color layer
        var colorLayer = createSolidColorLayer(95, 76, 46, "Trimsheet Color (Multiply)");
        colorLayer.blendMode = BlendMode.MULTIPLY; // Blend mode to apply color to the channel
        colorLayer.grouped = true; // Clip to BaseMap
        
    } catch(e) {
        // Ignore if group or basemap doesn't exist
    }
    
    try {
        var group = doc.layerSets.getByName("Soldado");
        groupLayers(group, "blue");
        
        var baseMapLayer = group.artLayers.getByName("BaseMap");
        doc.activeLayer = baseMapLayer;
        
        // Create solid color layer
        var colorLayer = createSolidColorLayer(144, 134, 73, "Trimsheet Color (Multiply)");
        colorLayer.blendMode = BlendMode.MULTIPLY; // Blend mode to apply color to the channel
        colorLayer.grouped = true; // Clip to BaseMap
        
    } catch(e) {
        // Ignore if group or basemap doesn't exist
    }
    
    return "SUCCESS_COLOR_APPLIED";
} catch (e) {
    return "ERROR: " + e.toString();
}
