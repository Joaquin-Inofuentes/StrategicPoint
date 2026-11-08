/* =====================================================================================================
 * SEGUIMIENTO DE BUGS (2026-11-08) - capa sobre el receptor original. Ver Docs/PLAN_columnas_bugs_opus.md §4.
 *
 * Esta parte se agrega AL FINAL de Codigo.gs. En el script original se hicieron dos cambios de nombre:
 *     function doPost(e)  ->  function doPostBase_(e)     (el receptor de siempre: foto + fila nueva)
 *     function doGet(e)   ->  function doGetBase_(e)      (ping / verify)
 * y CONFIG.SPREADSHEET_ID quedo fijado al Sheet "StrategicPoint_Correcciones".
 *
 * Todo se escribe POR NOMBRE de columna. Las columnas A-F (Fecha, Hora, Descripcion, Imagen, Comentarios, Estado_JSON)
 * no se mueven porque el receptor base las usa por posicion; las de seguimiento se insertan despues de Estado_JSON.
 *
 * Una vez: ejecutar migrarColumnas() (usa los permisos que el script ya tenia: Drive y Sheets). Despues, nueva version de la implementacion.
 * onEdit(e) es un activador SIMPLE: no hay que instalar nada. Locale del Sheet es es-AR: las formulas de validacion y de formato
 * condicional van con ';' (el script prueba con ',' y reintenta con ';').
 * ===================================================================================================== */

const SEG = {
  S: ['Bug_id', 'Estado', 'Prioridad', 'Clase', 'Area', 'Duplicado_de', 'Commit_fix', 'Fecha_corregido', 'Verificado_por', 'Notas_dev', 'Clasif_auto', 'Bugs_cercanos'],
  ESTADOS: ['Nuevo', 'En análisis', 'Corregido', 'Verificado', 'Reabierto', 'No reproducible', 'Duplicado', 'Descartado'],
  PRIORIDADES: ['P0', 'P1', 'P2', 'P3'],
  CLASES: ['Defecto', 'Pedido', 'Balance', 'Pregunta'],
  AREAS: ['IA aliados', 'IA enemigos', 'Vehiculos/Helis', 'Colision/Nav', 'UI/Minimapa', 'Luces/Visual', 'Audio/FX', 'Balance', 'Mision/Guion', 'Rendimiento', 'Otro'],
  NUMERICAS: ['Subfase', 'Desplaz_10s_m', 'FPS_5s', 'Frame_ms_max_5s'],   // el cliente las manda como texto; en el Sheet tienen que ser numeros
  ACTUALIZABLES: ['Estado', 'Commit_fix', 'Notas_dev', 'Prioridad', 'Clase', 'Area', 'Duplicado_de'],
  RADIO_CERCANOS_M: 15,
  FILAS_CERCANOS: 200,
  MAX_CERCANOS: 5,
  COMMIT_RE: /^[0-9a-f]{7,40}( [0-9a-f]{7,40})*$/
};

/* ------------------------------------------------ endpoints ------------------------------------------------ */

function doPost(e) {
  let body;
  try { body = JSON.parse((e && e.postData && e.postData.contents) || '{}'); }
  catch (err) { return json_({ ok: false, error: 'JSON inválido' }); }
  let evento = e;
  try {
    checkToken_(body.token);
    if (body.accion === 'actualizar') return json_(accionActualizar_(body));
    if (body.datos && typeof body.datos === 'object') {
      numerosComoNumeros_(body.datos);
      const id = body.datos.Bug_id;
      if (id) {
        const r = upsertExistente_(body);
        if (r) return json_(r);
      }
      evento = { postData: { contents: JSON.stringify(body) } };
    }
  } catch (err) {
    return json_({ ok: false, error: String(err && err.message ? err.message : err) });
  }
  const salida = doPostBase_(evento);
  try {
    const r = JSON.parse(salida.getContent());
    if (r && r.ok && r.fila) posInsercion_(r.fila);
  } catch (err) { console.warn('posInsercion_ falló: ' + err); }
  return salida;
}

function doGet(e) {
  const p = (e && e.parameter) || {};
  if (p.accion !== 'listar' && p.action !== 'listar') return doGetBase_(e);
  try {
    checkToken_(p.token);
    return json_(accionListar_(p));
  } catch (err) {
    return json_({ ok: false, error: String(err && err.message ? err.message : err) });
  }
}

/* ------------------------------------------------ utilidades ------------------------------------------------ */

function encabezados_(sh) {
  const n = Math.max(sh.getLastColumn(), 1);
  return sh.getRange(1, 1, 1, n).getValues()[0].map(function (h) { return String(h).trim(); });
}

function col_(headers, nombre) { return headers.indexOf(nombre) + 1; }   // 0 si no existe

function letra_(n) {
  let s = '';
  while (n > 0) { const m = (n - 1) % 26; s = String.fromCharCode(65 + m) + s; n = Math.floor((n - 1) / 26); }
  return s;
}

function buscarFila_(sh, headers, nombreCol, valor) {
  const c = col_(headers, nombreCol);
  const n = sh.getLastRow() - 1;
  if (!c || n < 1) return 0;
  const vals = sh.getRange(2, c, n, 1).getValues();
  const buscado = String(valor).trim();
  for (let i = vals.length - 1; i >= 0; i--) if (String(vals[i][0]).trim() === buscado) return i + 2;
  return 0;
}

function numerosComoNumeros_(datos) {
  SEG.NUMERICAS.forEach(function (k) {
    const v = datos[k];
    if (typeof v === 'string' && /^-?\d+(\.\d+)?$/.test(v.trim())) datos[k] = parseFloat(v);
  });
}

function sinTildes_(s) {
  return String(s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
}

// Misma heuristica que ContextoDeBug.Clasificar (C#), para las filas viejas.
function clasificar_(nota) {
  const t = sinTildes_(nota);
  const atasco = /snap|trab|atasc|atrap|no avanza|no pasa|bugue|bugeo|atravies|collider/.test(t);
  const balance = /\bvida\b|dano|velocidad|doble|mitad|veces|cadencia/.test(t);
  const pedido = /\bquiero\b|\bfalta|\bquita|deberia|\bagrega|\bpon\b|que haya|mejor|sea /.test(t);
  const pregunta = t.indexOf('?') >= 0 && !atasco;
  const clase = atasco ? 'Defecto' : balance ? 'Balance' : pregunta ? 'Pregunta' : pedido ? 'Pedido' : 'Defecto';
  const area = atasco ? 'Colision/Nav'
    : /helic|vehicul|tanque|camioneta/.test(t) ? 'Vehiculos/Helis'
    : /minimapa|rombo|\bui\b|barra|cartel|hud/.test(t) ? 'UI/Minimapa'
    : /luz|luces|brillo|ventana|sombra|fuente|bandera|textura/.test(t) ? 'Luces/Visual'
    : /sonido|particula|explosion|efecto|humo/.test(t) ? 'Audio/FX'
    : /aliado|miliciano/.test(t) ? 'IA aliados'
    : /enemigo|francotirador|flanco|pesado/.test(t) ? 'IA enemigos'
    : balance ? 'Balance' : /lag|fps|traba mucho|lento/.test(t) ? 'Rendimiento' : 'Otro';
  const prio = /no avanza|se trabo|no pasa|bugeo|bugue/.test(t) ? 'P0'
    : clase === 'Defecto' ? 'P1' : (clase === 'Balance' || area === 'Luces/Visual') ? 'P2' : 'P3';
  return clase + '·' + area + '·' + prio;
}

function hoy_() { return new Date(); }

function emailActual_() {
  try { return Session.getActiveUser().getEmail() || Session.getEffectiveUser().getEmail() || ''; }
  catch (err) { return ''; }
}

function validarValor_(clave, valor) {
  const listas = { Estado: SEG.ESTADOS, Prioridad: SEG.PRIORIDADES, Clase: SEG.CLASES, Area: SEG.AREAS };
  if (listas[clave] && valor !== '' && listas[clave].indexOf(valor) < 0)
    throw new Error(clave + ' inválido: "' + valor + '" (permitidos: ' + listas[clave].join(', ') + ')');
  if (clave === 'Commit_fix' && valor !== '' && !SEG.COMMIT_RE.test(String(valor)))
    throw new Error('Commit_fix inválido: usar sha de 7 a 40 hex, separados por espacio');
  if (clave === 'Duplicado_de' && valor !== '' && !/^\d+$/.test(String(valor)))
    throw new Error('Duplicado_de debe ser el número de bug');
}

/* ------------------------------------------------ upsert por Bug_id ------------------------------------------------ */

// Si el Bug_id ya esta en la hoja (reintento de subida): actualiza SOLO las columnas automaticas, sin tocar el bloque de seguimiento ni la foto.
function upsertExistente_(body) {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) throw new Error('servidor ocupado, reintentá');
  try {
    const sh = getHoja_();
    const headers = encabezados_(sh);
    const fila = buscarFila_(sh, headers, 'Bug_id', body.datos.Bug_id);
    if (!fila) return null;
    Object.keys(body.datos).forEach(function (k) {
      if (SEG.S.indexOf(k) >= 0 && k !== 'Clasif_auto') return;   // el seguimiento es de la persona
      let idx = headers.indexOf(k);
      if (idx < 0) {
        idx = headers.length;
        headers.push(k);
        sh.getRange(1, idx + 1).setValue(k).setFontWeight('bold');
      }
      sh.getRange(fila, idx + 1).setValue(valorCelda_(body.datos[k]));
    });
    if (body.descripcion) sh.getRange(fila, 3).setValue(textoSeguro_(String(body.descripcion).slice(0, CONFIG.MAX_DESC)));
    SpreadsheetApp.flush();
    return { ok: true, fila: fila, actualizada: true };
  } finally { lock.releaseLock(); }
}

/* ------------------------------------------------ despues de insertar ------------------------------------------------ */

function posInsercion_(fila) {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) { console.warn('posInsercion_: sin lock'); return; }
  try {
    const sh = getHoja_();
    const headers = encabezados_(sh);
    const leer = function (n) { const c = col_(headers, n); return c ? sh.getRange(fila, c).getValue() : ''; };
    const poner = function (n, v) { const c = col_(headers, n); if (c) sh.getRange(fila, c).setValue(v); };
    if (!String(leer('Estado')).trim()) poner('Estado', 'Nuevo');
    const cl = String(leer('Clasif_auto')).split('·');
    if (cl.length === 3) {
      if (!String(leer('Clase')).trim()) poner('Clase', cl[0]);
      if (!String(leer('Area')).trim()) poner('Area', cl[1]);
      if (!String(leer('Prioridad')).trim()) poner('Prioridad', cl[2]);
    }
    if (col_(headers, 'Bugs_cercanos')) poner('Bugs_cercanos', calcularCercanos_(sh, headers, fila));
    SpreadsheetApp.flush();
  } finally { lock.releaseLock(); }
}

function posicion_(c, i) {
  const x = c.Jugador_X ? parseFloat(c.Jugador_X[i]) : NaN;
  const z = c.Jugador_Z ? parseFloat(c.Jugador_Z[i]) : NaN;
  if (isFinite(x) && isFinite(z) && String(c.Jugador_X[i]) !== '') return [x, z];
  if (c.Camara) {
    const m = /\(\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)/.exec(String(c.Camara[i] || ''));
    if (m) return [parseFloat(m[1]), parseFloat(m[3])];
  }
  const cx = c.Camara_X ? parseFloat(c.Camara_X[i]) : NaN;
  const cz = c.Camara_Z ? parseFloat(c.Camara_Z[i]) : NaN;
  return isFinite(cx) && isFinite(cz) ? [cx, cz] : null;
}

// Misma escena y fase, a <= 15 m en el plano, entre los ultimos 200 bugs: "#384(8m) #376(12m)".
function calcularCercanos_(sh, headers, fila) {
  const desde = Math.max(2, fila - SEG.FILAS_CERCANOS);
  const n = fila - desde + 1;
  const c = {};
  ['Bug', 'Escena', 'Fase', 'Jugador_X', 'Jugador_Z', 'Camara', 'Camara_X', 'Camara_Z'].forEach(function (nombre) {
    const k = col_(headers, nombre);
    if (k) c[nombre] = sh.getRange(desde, k, n, 1).getValues().map(function (r) { return r[0]; });
  });
  if (!c.Escena || !c.Fase) return '';
  const yo = n - 1;
  const pYo = posicion_(c, yo);
  if (!pYo) return '';
  const cerca = [];
  for (let i = 0; i < yo; i++) {
    if (c.Escena[i] !== c.Escena[yo] || c.Fase[i] !== c.Fase[yo]) continue;
    const p = posicion_(c, i);
    if (!p) continue;
    const d = Math.sqrt(Math.pow(p[0] - pYo[0], 2) + Math.pow(p[1] - pYo[1], 2));
    if (d <= SEG.RADIO_CERCANOS_M) cerca.push({ bug: c.Bug ? c.Bug[i] : '?', d: d });
  }
  cerca.sort(function (a, b) { return a.d - b.d; });
  return cerca.slice(0, SEG.MAX_CERCANOS).map(function (o) { return '#' + o.bug + '(' + Math.round(o.d) + 'm)'; }).join(' ');
}

/* ------------------------------------------------ reglas de estado (onEdit y accion actualizar) ------------------------------------------------ */

function aplicarReglas_(sh, headers, fila, estado, quien) {
  const celda = function (n) { const c = col_(headers, n); return c ? sh.getRange(fila, c) : null; };
  const vacia = function (n) { const r = celda(n); return r && String(r.getValue()).trim() === ''; };
  const ponerFecha = function (n) {
    const r = celda(n);
    if (r && String(r.getValue()).trim() === '') r.setNumberFormat('dd/MM/yyyy').setValue(hoy_());
  };
  if (estado === 'Corregido') {
    ponerFecha('Fecha_corregido');
    if (vacia('Commit_fix')) celda('Commit_fix').setNote('Falta el commit del arreglo');
  } else if (estado === 'Verificado') {
    if (vacia('Verificado_por')) celda('Verificado_por').setValue([quien || emailActual_(), Utilities.formatDate(hoy_(), CONFIG.TZ, 'dd/MM/yyyy')].filter(Boolean).join(' '));
    ponerFecha('Fecha_corregido');
  } else if (estado === 'Duplicado') {
    if (vacia('Duplicado_de')) celda('Duplicado_de').setNote('Indicá de qué bug es duplicado');
  } else if (estado === 'Reabierto') {
    const r = celda('Notas_dev');
    if (r) {
      const marca = '[reabierto ' + Utilities.formatDate(hoy_(), CONFIG.TZ, 'dd/MM/yyyy') + '] ';
      const actual = String(r.getValue());
      if (actual.indexOf('[reabierto ') !== 0) r.setValue(marca + actual);
    }
  }
}

// onEdit SIMPLE (no hace falta instalarlo ni ampliar los permisos del script): sólo toca celdas de la hoja que se está editando.
// Un activador simple no puede abrir otras hojas por id ni pedir el mail con certeza, por eso usa e.source / e.user.
function onEdit(e) {
  try {
    const r = e && e.range;
    if (!r || r.getRow() < 2 || r.getNumRows() !== 1 || r.getNumColumns() !== 1) return;
    const sh = r.getSheet();
    if (sh.getIndex() !== 1) return;   // el receptor escribe siempre en la primera pestaña
    const headers = encabezados_(sh);
    if (r.getColumn() !== col_(headers, 'Estado')) return;
    let quien = '';
    try { quien = (e.user && e.user.getEmail && e.user.getEmail()) || ''; } catch (err2) { quien = ''; }
    aplicarReglas_(sh, headers, r.getRow(), String(r.getValue()), quien);
  } catch (err) { console.warn('onEdit: ' + err); }
}

/* ------------------------------------------------ acciones ------------------------------------------------ */

function accionActualizar_(b) {
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) throw new Error('servidor ocupado, reintentá');
  try {
    const sh = getHoja_();
    const headers = encabezados_(sh);
    let fila = 0;
    if (b.bug_id) fila = buscarFila_(sh, headers, 'Bug_id', b.bug_id);
    if (!fila && b.bug) fila = buscarFila_(sh, headers, 'Bug', b.bug);
    if (!fila) throw new Error('bug no encontrado');
    SEG.ACTUALIZABLES.forEach(function (k) { if (b[k] !== undefined) validarValor_(k, b[k]); });
    SEG.ACTUALIZABLES.forEach(function (k) {
      if (b[k] === undefined) return;
      const c = col_(headers, k);
      if (!c) throw new Error('falta la columna ' + k + ' (ejecutar migrarColumnas)');
      sh.getRange(fila, c).setValue(b[k]);
    });
    if (b.Estado !== undefined) aplicarReglas_(sh, headers, fila, b.Estado);
    SpreadsheetApp.flush();
    return { ok: true, fila: fila };
  } finally { lock.releaseLock(); }
}

// GET ?accion=listar&estado=Nuevo,Reabierto&token=...  -> la cola de trabajo del agente.
function accionListar_(p) {
  const sh = getHoja_();
  const headers = encabezados_(sh);
  const n = sh.getLastRow() - 1;
  if (n < 1) return { ok: true, bugs: [] };
  const pedidos = p.estado ? String(p.estado).split(',').map(function (s) { return s.trim(); }) : null;
  const nombres = ['Bug_id', 'Bug', 'Comentarios', 'Clase', 'Prioridad', 'Estado', 'Estado_JSON'];
  const datos = {};
  nombres.forEach(function (k) {
    const c = col_(headers, k);
    datos[k] = c ? sh.getRange(2, c, n, 1).getDisplayValues().map(function (r) { return r[0]; }) : null;
  });
  const bugs = [];
  for (let i = 0; i < n; i++) {
    if (pedidos && pedidos.indexOf(datos.Estado ? datos.Estado[i] : '') < 0) continue;
    const o = {};
    nombres.forEach(function (k) { o[k] = datos[k] ? datos[k][i] : ''; });
    bugs.push(o);
  }
  return { ok: true, bugs: bugs };
}

/* ------------------------------------------------ migracion (se corre a mano, una vez) ------------------------------------------------ */

function migrarColumnas() {
  const sh = getHoja_();
  const ss = sh.getParent();
  const t0 = Date.now();

  // 1) Copia de seguridad (archivo aparte en la misma carpeta; el receptor usa CONFIG.SPREADSHEET_ID, asi que no se confunde).
  const nombreCopia = ss.getName() + '_backup_2026-11-08';
  const carpeta = DriveApp.getFolderById(CONFIG.FOLDER_PRINCIPAL_ID);
  if (!carpeta.getFilesByName(nombreCopia).hasNext()) DriveApp.getFileById(ss.getId()).makeCopy(nombreCopia, carpeta);

  // 2) Columnas del bloque de seguimiento que falten: se insertan despues de Estado_JSON.
  let headers = encabezados_(sh);
  const faltan = SEG.S.filter(function (k) { return headers.indexOf(k) < 0; });
  if (faltan.length) {
    const despuesDe = col_(headers, 'Estado_JSON') || 5;
    sh.insertColumnsAfter(despuesDe, faltan.length);
    sh.getRange(1, despuesDe + 1, 1, faltan.length).setValues([faltan]).setFontWeight('bold');
    headers = encabezados_(sh);
  }

  // 3) Relleno de las filas viejas (sólo celdas vacías).
  const n = sh.getLastRow() - 1;
  if (n >= 1) {
    const leerCol = function (nombre) { const c = col_(headers, nombre); return c ? sh.getRange(2, c, n, 1).getValues().map(function (r) { return r[0]; }) : null; };
    const guardarCol = function (nombre, arr) { const c = col_(headers, nombre); if (c) sh.getRange(2, c, n, 1).setValues(arr.map(function (v) { return [v]; })); };
    const bug = leerCol('Bug'), nota = leerCol('Comentarios');
    const id = leerCol('Bug_id'), est = leerCol('Estado'), cla = leerCol('Clasif_auto');
    const clase = leerCol('Clase'), area = leerCol('Area'), prio = leerCol('Prioridad');
    for (let i = 0; i < n; i++) {
      if (!String(id[i]).trim() && String(bug[i]).trim()) id[i] = 'EX-?-' + String(bug[i]).trim();
      if (!String(est[i]).trim()) est[i] = 'Nuevo';
      if (!String(cla[i]).trim()) cla[i] = clasificar_(nota ? nota[i] : '');
      const partes = String(cla[i]).split('·');
      if (partes.length === 3) {
        if (!String(clase[i]).trim()) clase[i] = partes[0];
        if (!String(area[i]).trim()) area[i] = partes[1];
        if (!String(prio[i]).trim()) prio[i] = partes[2];
      }
    }
    guardarCol('Bug_id', id); guardarCol('Estado', est); guardarCol('Clasif_auto', cla);
    guardarCol('Clase', clase); guardarCol('Area', area); guardarCol('Prioridad', prio);
  }

  // 4) Validaciones, formato y vista.
  aplicarValidaciones_(sh, headers);
  aplicarFormato_(sh, headers);
  console.log('Migración lista en ' + Math.round((Date.now() - t0) / 1000) + ' s; columnas nuevas: ' + (faltan.join(', ') || 'ninguna') + '; filas: ' + n);
}

function aplicarValidaciones_(sh, headers) {
  const filas = Math.max(sh.getMaxRows() - 1, 1);
  const lista = function (nombre, valores) {
    const c = col_(headers, nombre);
    if (!c) return;
    sh.getRange(2, c, filas, 1).setDataValidation(SpreadsheetApp.newDataValidation().requireValueInList(valores, true).setAllowInvalid(false).build());
  };
  lista('Estado', SEG.ESTADOS); lista('Prioridad', SEG.PRIORIDADES); lista('Clase', SEG.CLASES); lista('Area', SEG.AREAS);
  const cBug = col_(headers, 'Bug'), cDup = col_(headers, 'Duplicado_de'), cCom = col_(headers, 'Commit_fix'), cFec = col_(headers, 'Fecha_corregido');
  // Las formulas de validacion se interpretan con el idioma del Sheet (es-AR usa ';'): se prueba con ',' y, si falla, con ';'.
  const conFormula = function (col, formulaComa, ayuda) {
    if (!col) return;
    const rango = sh.getRange(2, col, filas, 1);
    rango.clearDataValidations();
    const variantes = [formulaComa, formulaComa.replace(/,/g, ';')];
    for (let i = 0; i < variantes.length; i++) {
      try {
        rango.setDataValidation(SpreadsheetApp.newDataValidation().requireFormulaSatisfied(variantes[i]).setAllowInvalid(false).setHelpText(ayuda).build());
        SpreadsheetApp.flush();   // una regla invalida falla recien aca o en la proxima operacion: que falle dentro del try
        sh.getRange(1, 1).getValue();
        return;
      } catch (err) { console.warn('validación col ' + col + ' variante ' + i + ': ' + err); rango.clearDataValidations(); }
    }
    console.warn('col ' + col + ': sin validación (la acción actualizar sí valida)');
  };
  // Duplicado_de: sin formulas, contra el rango de la columna Bug (sin lista desplegable de 400 numeros).
  if (cDup && cBug) {
    const r = sh.getRange(2, cDup, filas, 1);
    r.clearDataValidations();
    r.setDataValidation(SpreadsheetApp.newDataValidation().requireValueInRange(sh.getRange(2, cBug, filas, 1), false)
      .setAllowInvalid(false).setHelpText('Número de un bug que exista en la columna Bug').build());
  }
  if (cCom) {
    const l = letra_(cCom);
    conFormula(cCom, '=OR(' + l + '2="",LEN(' + l + '2)>=7)', 'sha del commit (7 a 40 hex); varios separados por espacio');
  }
  if (cFec) sh.getRange(2, cFec, filas, 1).setNumberFormat('dd/MM/yyyy').setDataValidation(SpreadsheetApp.newDataValidation().requireDate().setAllowInvalid(false).build());
}

function aplicarFormato_(sh, headers, puntoYComa) {
  const ultima = sh.getLastColumn();
  const filas = Math.max(sh.getMaxRows() - 1, 1);
  const todo = sh.getRange(2, 1, filas, ultima);
  const reglas = [];
  const L = function (n) { const c = col_(headers, n); return c ? '$' + letra_(c) + '2' : null; };
  const adaptar = function (f) { return puntoYComa ? f.replace(/,/g, ';') : f; };   // el Sheet puede esperar el idioma local
  const filaCompleta = function (cond, fondo, texto, tachado) {
    const b = SpreadsheetApp.newConditionalFormatRule().whenFormulaSatisfied(adaptar(cond)).setBackground(fondo);
    if (texto) b.setFontColor(texto);
    if (tachado) b.setStrikethrough(true);
    reglas.push(b.setRanges([todo]).build());
  };
  const est = L('Estado');
  if (est) {
    filaCompleta('=' + est + '="En análisis"', '#ddebf7');
    filaCompleta('=' + est + '="Corregido"', '#e2f0d9');
    filaCompleta('=' + est + '="Verificado"', '#c6e0b4');
    filaCompleta('=' + est + '="Reabierto"', '#f8cbad');
    filaCompleta('=OR(' + est + '="No reproducible",' + est + '="Descartado")', '#eeeeee', '#999999');
    filaCompleta('=' + est + '="Duplicado"', '#eeeeee', '#999999', true);
  }
  const celdas = function (nombre, formula, fondo, texto, negrita) {
    const c = col_(headers, nombre);
    if (!c) return;
    const r = sh.getRange(2, c, filas, 1);
    const b = SpreadsheetApp.newConditionalFormatRule().whenFormulaSatisfied(adaptar(formula));
    if (fondo) b.setBackground(fondo);
    if (texto) b.setFontColor(texto);
    if (negrita) b.setBold(true);
    reglas.push(b.setRanges([r]).build());
  };
  const pri = L('Prioridad');
  if (pri) { celdas('Prioridad', '=' + pri + '="P0"', '#f4b6b6', '#9c0006', true); celdas('Prioridad', '=' + pri + '="P1"', '#fce4d6', '#9c5700'); }
  const fps = L('FPS_5s');
  if (fps) { celdas('FPS_5s', '=AND(ISNUMBER(' + fps + '),' + fps + '<20)', '#f4b6b6'); celdas('FPS_5s', '=AND(ISNUMBER(' + fps + '),' + fps + '<30)', '#fce4d6'); }
  const fr = L('Frame_ms_max_5s');
  if (fr) { celdas('Frame_ms_max_5s', '=AND(ISNUMBER(' + fr + '),' + fr + '>100)', '#f4b6b6'); celdas('Frame_ms_max_5s', '=AND(ISNUMBER(' + fr + '),' + fr + '>50)', '#fce4d6'); }
  const er = L('Errores_log');
  if (er) celdas('Errores_log', '=AND(' + er + '<>"",' + er + '<>0,' + er + '<>"0")', null, '#c00000');
  const ec = L('Estado_control');
  if (ec) celdas('Estado_control', '=REGEXMATCH(' + ec + '&"","Sin_control|Sin_soldado|Muerto|Cinematica")', '#fff2cc');
  const de = L('Desplaz_10s_m'), cl = L('Clase');
  if (de && cl) celdas('Desplaz_10s_m', '=AND(ISNUMBER(' + de + '),' + de + '<0.5,' + cl + '="Defecto")', '#fbe5e5');
  try { sh.setConditionalFormatRules(reglas); }
  catch (err) {
    if (puntoYComa) throw err;
    console.warn('formato condicional con ",": ' + err + '; se reintenta con ";"');
    return aplicarFormato_(sh, headers, true);
  }

  sh.setFrozenRows(1);
  sh.setFrozenColumns(Math.max(col_(headers, 'Comentarios'), 1));
  if (!sh.getFilter()) sh.getRange(1, 1, Math.max(sh.getLastRow(), 2), ultima).createFilter();
}

/* ------------------------------------------------ pruebas ------------------------------------------------ */

// Borra las filas de prueba (Bug_id que empieza con TEST-) y manda a la papelera su foto.
function borrarFilasDePrueba() {
  const sh = getHoja_();
  const headers = encabezados_(sh);
  const c = col_(headers, 'Bug_id');
  if (!c) return;
  for (let f = sh.getLastRow(); f >= 2; f--) {
    if (String(sh.getRange(f, c).getValue()).indexOf('TEST-') !== 0) continue;
    const nota = sh.getRange(f, 4).getNote();
    const m = /[-\w]{25,}/.exec(nota || '');
    if (m) { try { DriveApp.getFileById(m[0]).setTrashed(true); } catch (err) { console.warn('foto: ' + err); } }
    sh.deleteRow(f);
  }
}
