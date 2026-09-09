
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

    var group_Arbol1 = doc.layerSets.add();
    group_Arbol1.name = "Arbol 1";
    setLayerColor(group_Arbol1, "greenColor");
    
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 1/Arbol 1_standardSurface1_BaseMap.png");
                    layer.name = "BaseMap";
                    layer.move(group_Arbol1, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 1/Arbol 1_standardSurface1_MaskMap.png");
                    layer.name = "MaskMap";
                    layer.move(group_Arbol1, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 1/Arbol 1_standardSurface1_Normal.png");
                    layer.name = "Normal";
                    layer.move(group_Arbol1, ElementPlacement.INSIDE);
                } catch(e) { }
                
        try {
            var baseMapLayer = group_Arbol1.layers.getByName("BaseMap");
            doc.activeLayer = baseMapLayer;
            var colorLayer = createSolidColorLayer(110, 101, 55, "Color Trimsheet");
            colorLayer.blendMode = BlendMode.MULTIPLY;
            colorLayer.grouped = true; 
        } catch(e) { }
        
    var group_Arbol3 = doc.layerSets.add();
    group_Arbol3.name = "Arbol 3";
    setLayerColor(group_Arbol3, "greenColor");
    
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 3/Arbol 3_standardSurface1_BaseMap.png");
                    layer.name = "BaseMap";
                    layer.move(group_Arbol3, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 3/Arbol 3_standardSurface1_MaskMap.png");
                    layer.name = "MaskMap";
                    layer.move(group_Arbol3, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 3/Arbol 3_standardSurface1_Normal.png");
                    layer.name = "Normal";
                    layer.move(group_Arbol3, ElementPlacement.INSIDE);
                } catch(e) { }
                
        try {
            var baseMapLayer = group_Arbol3.layers.getByName("BaseMap");
            doc.activeLayer = baseMapLayer;
            var colorLayer = createSolidColorLayer(144, 134, 73, "Color Trimsheet");
            colorLayer.blendMode = BlendMode.MULTIPLY;
            colorLayer.grouped = true; 
        } catch(e) { }
        
    var group_Barricada = doc.layerSets.add();
    group_Barricada.name = "Barricada";
    setLayerColor(group_Barricada, "orange");
    
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barricada/Barricada_standardSurface1_BaseMap.png");
                    layer.name = "BaseMap";
                    layer.move(group_Barricada, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barricada/Barricada_standardSurface1_MaskMap.png");
                    layer.name = "MaskMap";
                    layer.move(group_Barricada, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barricada/Barricada_standardSurface1_Normal.png");
                    layer.name = "Normal";
                    layer.move(group_Barricada, ElementPlacement.INSIDE);
                } catch(e) { }
                
        try {
            var baseMapLayer = group_Barricada.layers.getByName("BaseMap");
            doc.activeLayer = baseMapLayer;
            var colorLayer = createSolidColorLayer(123, 133, 120, "Color Trimsheet");
            colorLayer.blendMode = BlendMode.MULTIPLY;
            colorLayer.grouped = true; 
        } catch(e) { }
        
    var group_Barril = doc.layerSets.add();
    group_Barril.name = "Barril";
    setLayerColor(group_Barril, "violet");
    
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barril/Barril 1_standardSurface1_BaseMap.png");
                    layer.name = "BaseMap";
                    layer.move(group_Barril, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barril/Barril 1_standardSurface1_MaskMap.png");
                    layer.name = "MaskMap";
                    layer.move(group_Barril, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barril/Barril 1_standardSurface1_Normal.png");
                    layer.name = "Normal";
                    layer.move(group_Barril, ElementPlacement.INSIDE);
                } catch(e) { }
                
        try {
            var baseMapLayer = group_Barril.layers.getByName("BaseMap");
            doc.activeLayer = baseMapLayer;
            var colorLayer = createSolidColorLayer(95, 76, 46, "Color Trimsheet");
            colorLayer.blendMode = BlendMode.MULTIPLY;
            colorLayer.grouped = true; 
        } catch(e) { }
        
    var group_Soldado = doc.layerSets.add();
    group_Soldado.name = "Soldado";
    setLayerColor(group_Soldado, "blue");
    
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Soldado/lego_Material_BaseMap.png");
                    layer.name = "BaseMap";
                    layer.move(group_Soldado, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Soldado/lego_Material_MaskMap.png");
                    layer.name = "MaskMap";
                    layer.move(group_Soldado, ElementPlacement.INSIDE);
                } catch(e) { }
                
                try {
                    var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Soldado/lego_Material_Normal.png");
                    layer.name = "Normal";
                    layer.move(group_Soldado, ElementPlacement.INSIDE);
                } catch(e) { }
                
        try {
            var baseMapLayer = group_Soldado.layers.getByName("BaseMap");
            doc.activeLayer = baseMapLayer;
            var colorLayer = createSolidColorLayer(144, 134, 73, "Color Trimsheet");
            colorLayer.blendMode = BlendMode.MULTIPLY;
            colorLayer.grouped = true; 
        } catch(e) { }
        
    return "SUCCESS_COMBINED";
} catch (e) {
    return "ERROR: " + e.toString();
}
