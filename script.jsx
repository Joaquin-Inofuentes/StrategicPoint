
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


    var group_Arbol1 = doc.layerSets.add();
    group_Arbol1.name = "Arbol 1";
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 1/Arbol 1_standardSurface1_BaseMap.png");
        layer.name = "BaseMap";
        layer.move(group_Arbol1, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 1/Arbol 1_standardSurface1_MaskMap.png");
        layer.name = "MaskMap";
        layer.move(group_Arbol1, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 1/Arbol 1_standardSurface1_Normal.png");
        layer.name = "Normal";
        layer.move(group_Arbol1, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    var group_Arbol3 = doc.layerSets.add();
    group_Arbol3.name = "Arbol 3";
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 3/Arbol 3_standardSurface1_BaseMap.png");
        layer.name = "BaseMap";
        layer.move(group_Arbol3, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 3/Arbol 3_standardSurface1_MaskMap.png");
        layer.name = "MaskMap";
        layer.move(group_Arbol3, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Arbol 3/Arbol 3_standardSurface1_Normal.png");
        layer.name = "Normal";
        layer.move(group_Arbol3, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    var group_Barricada = doc.layerSets.add();
    group_Barricada.name = "Barricada";
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barricada/Barricada_standardSurface1_BaseMap.png");
        layer.name = "BaseMap";
        layer.move(group_Barricada, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barricada/Barricada_standardSurface1_MaskMap.png");
        layer.name = "MaskMap";
        layer.move(group_Barricada, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barricada/Barricada_standardSurface1_Normal.png");
        layer.name = "Normal";
        layer.move(group_Barricada, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    var group_Barril = doc.layerSets.add();
    group_Barril.name = "Barril";
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barril/Barril 1_standardSurface1_BaseMap.png");
        layer.name = "BaseMap";
        layer.move(group_Barril, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barril/Barril 1_standardSurface1_MaskMap.png");
        layer.name = "MaskMap";
        layer.move(group_Barril, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Barril/Barril 1_standardSurface1_Normal.png");
        layer.name = "Normal";
        layer.move(group_Barril, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    var group_Soldado = doc.layerSets.add();
    group_Soldado.name = "Soldado";
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Soldado/lego_Material_BaseMap.png");
        layer.name = "BaseMap";
        layer.move(group_Soldado, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Soldado/lego_Material_MaskMap.png");
        layer.name = "MaskMap";
        layer.move(group_Soldado, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    try {
        var layer = placeLinkedSmartObject("C:/_Proyectos privados/DV_C6_StrategicPoint/My project/Assets/ARTS/SP_Arte/Soldado/lego_Material_Normal.png");
        layer.name = "Normal";
        layer.move(group_Soldado, ElementPlacement.INSIDE);
    } catch(e) {
        // ignore errors for missing files
    }
    
    return "SUCCESS";
} catch (e) {
    return "ERROR: " + e.toString();
}
