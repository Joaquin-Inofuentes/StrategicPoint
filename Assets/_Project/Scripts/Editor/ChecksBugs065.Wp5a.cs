using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP5a: interaccion con [E]. #083/#084 rol requerido y cartel, #088 revivir con animacion y bloqueo, #089 sonido y anillos al revivir,
    // #092 el agachado no se entierra. Hay que estar en Play sobre SC_Operacion (cada check arranca el objetivo que necesita).
    // Validar cada uno en un Play fresco: la suite encadenada arrastra estado (la escuadra puede quedar en el helicoptero).
    public static partial class ChecksBugs065
    {
        static Soldier Escuadrista(RoleType rol)
        {
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Player && s.Role == rol && s.Health != null && s.gameObject.activeInHierarchy) return s;
            return null;
        }

        // Pone al soldado a 'dist' m del punto, del lado del que viene (sin tocar la altura: se apoya en el piso).
        static void Junto(Soldier s, Vector3 punto, float dist, Vector3? desde = null)
        {
            var dir = (desde ?? s.transform.position) - punto; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            dir.Normalize();
            var p = punto + dir * dist;
            s.transform.position = new Vector3(p.x, s.transform.position.y, p.z);
            s.transform.rotation = Quaternion.LookRotation(-dir);
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
        }

        static void Poseer(Soldier s)
        {
            var d = PlayerInputDriver.Activo;
            if (d != null && d.Brain != null && d.Brain.Current != s) d.Brain.Possess(s);
        }

        static bool SonoUnClipDe(string carpeta, int desdeIndice)
        {
            var nombres = new HashSet<string>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/Sfx/" + carpeta)) nombres.Add(c.name);
            nombres.Add(carpeta);
            for (int i = Mathf.Max(0, desdeIndice); i < AudioDirector.Historial.Count; i++)
                if (nombres.Contains(AudioDirector.Historial[i])) return true;
            return false;
        }

        // ---------------------------------------------------------------- #083 / #084 rol requerido
        static IEnumerator Bug083()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var kes = Escuadrista(RoleType.Flanker);
            CargaExplosiva panel = null;   // WP9a: el unico objeto con rol exclusivo es la carga del puesto (solo el ASALTO)
            foreach (var t in UnityEngine.Object.FindObjectsByType<CargaExplosiva>(FindObjectsSortMode.None))
                if (t.Habilitado && !t.EstaPlantada && (panel == null || t.transform.position.x < panel.transform.position.x)) panel = t;
            if (vega == null || kes == null || panel == null) { ModoDios.Poner(diosAntes); Fin($"FALLO faltan piezas: vega={(vega != null)} kes={(kes != null)} carga={(panel != null)}"); yield break; }
            try
            {
                // A) Flanqueador frente a la carga del puesto: apretar [E] da el cartel y el clic, y la carga no avanza.
                Poseer(kes);
                yield return null;
                Junto(kes, panel.transform.position, 2f);
                Junto(vega, panel.transform.position, 6f, vega.transform.position);
                RolRequerido.Reiniciar();
                AudioDirector.Historial.Clear();
                int n0 = AudioDirector.Historial.Count;
                OperacionTerminal.PruebaToqueE = true;
                for (int i = 0; i < 4; i++) yield return null;
                string texto = RolRequerido.UltimoTexto ?? "(ninguno)";
                string central = AvisoCentral.TextoActual ?? "(oculto)";
                bool cartel = texto.StartsWith("DEBES SER ASALTO PARA COLOCAR LA CARGA", StringComparison.Ordinal) && texto.Contains("[Q] ENVIAR A " + RolRequerido.NombreCorto(vega));
                bool visible = central == texto;
                bool clic = SonoUnClipDe("EmptyClick", n0);
                bool quieto = panel.Progreso01 <= 0.0001f;
                if (!cartel || !visible || !clic || !quieto) ok = false;
                sb.Append($"A) Kes frente a la carga: cartel='{texto}'{(cartel ? "" : "(MAL)")} visible={visible} clic={clic} progreso={panel.Progreso01:0.00}{(quieto ? "" : "(MAL)")}; ");
                foreach (var x in CapturarPantalla("v2_084_cartel_rol.png")) yield return x;

                // B) Enfriamiento de 1.5 s: otro toque enseguida no repite; pasado el tiempo, si.
                int c0 = RolRequerido.Cantidad;
                OperacionTerminal.PruebaToqueE = true; yield return null; yield return null;
                bool throttle = RolRequerido.Cantidad == c0;
                foreach (var x in Esperar(RolRequerido.Enfriamiento + 0.2f)) yield return x;
                OperacionTerminal.PruebaToqueE = true; yield return null; yield return null;
                bool repite = RolRequerido.Cantidad == c0 + 1;
                if (!throttle || !repite) ok = false;
                sb.Append($"B) enfriamiento: no repite al instante={throttle}, repite a los 1.7 s={repite}; ");

                // C) La pista de la mira: el Flanqueador lee "DEBES SER ASALTO", el Asalto "MANTENE [E]".
                string pistaKes = PistaDeLaMira(panel);
                Poseer(vega); yield return null;
                Junto(vega, panel.transform.position, 2f);
                string pistaVega = PistaDeLaMira(panel);
                bool pistas = pistaKes.StartsWith("DEBES SER ASALTO", StringComparison.Ordinal) && pistaVega.Contains("MANTENÉ [E]");
                if (!pistas) ok = false;
                sb.Append($"C) pista Vega='{pistaVega}' pista Kes='{pistaKes}'{(pistas ? "" : "(MAL)")}; ");

                // D) Vega (Asalto) manteniendo [E]: la carga avanza y la accion queda reportada en AccionesEnCurso.Vigentes.
                OperacionTerminal.PruebaMantenerE = true;
                foreach (var x in Esperar(0.7f)) yield return x;
                var lista = new List<AccionesEnCurso.Accion>();
                AccionesEnCurso.Vigentes(lista);
                bool reporta = false; foreach (var a in lista) if (a.Actor == vega && a.VerboEs == "DETONANDO" && a.Progreso01 > 0f) reporta = true;
                bool avanza = panel.Progreso01 > 0.05f;
                OperacionTerminal.PruebaMantenerE = false;
                if (!reporta || !avanza) ok = false;
                sb.Append($"D) Vega mantiene [E]: reporta DETONANDO={reporta} progreso={panel.Progreso01:0.00}{(avanza ? "" : "(MAL)")}; ");

                // E) Demolicion: indestructible (Operacion) = "ESTO NO SE PUEDE DEMOLER"; destructible + rol equivocado = "DEBES SER ASALTO".
                ObstacleMarker indes = null, sacos = null;
                foreach (var m in UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsSortMode.None))
                {
                    if (!m.gameObject.activeInHierarchy || m.IsCollapsed || m.GetComponent<Collider>() == null) continue;
                    if (m.MaxHealth >= Demolicion.VidaIndestructible && indes == null) indes = m;
                    else if (m.MaxHealth < 1000 && sacos == null) sacos = m;
                }
                if (indes == null || sacos == null) { ok = false; sb.Append($"E) faltan obstaculos: indestructible={(indes != null)} destructible={(sacos != null)}; "); }
                else
                {
                    bool noDemol = !Demolicion.EsDemolible(indes, out string mot) && mot == Demolicion.MotivoIndestructible;
                    bool siDemol = Demolicion.EsDemolible(sacos, out _);
                    // Vega (Asalto) apunta al indestructible, a 3 m: aviso del motivo.
                    Poseer(vega); yield return null;
                    var rayo = new Ray(vega.transform.position, vega.transform.forward);
                    Junto(vega, indes.GetComponent<Collider>().bounds.center, indes.GetComponent<Collider>().bounds.extents.magnitude + 2f);
                    var aimI = new SP.Player.AimResult { Type = AimTargetType.Obstacle, HitTransform = indes.transform, Point = indes.transform.position };
                    string avisoI = Demolicion.AvisarSiNoSePuede(vega, aimI, rayo, true);
                    // Kes (Flanqueador) apunta a un saco: cartel de rol.
                    foreach (var x in Esperar(1.3f)) yield return x;
                    Poseer(kes); yield return null;
                    Junto(kes, sacos.GetComponent<Collider>().bounds.center, sacos.GetComponent<Collider>().bounds.extents.magnitude + 1.5f);
                    RolRequerido.Reiniciar();
                    var aimS = new SP.Player.AimResult { Type = AimTargetType.Obstacle, HitTransform = sacos.transform, Point = sacos.transform.position };
                    string avisoS = Demolicion.AvisarSiNoSePuede(kes, aimS, new Ray(kes.transform.position, kes.transform.forward), true);
                    bool okE = noDemol && siDemol && avisoI == Demolicion.MotivoIndestructible && avisoS != null && avisoS.StartsWith("DEBES SER ASALTO PARA DEMOLER", StringComparison.Ordinal);
                    if (!okE) ok = false;
                    sb.Append($"E) {indes.name} (vida {indes.MaxHealth}) EsDemolible={!noDemol} aviso='{avisoI}'; {sacos.name} (vida {sacos.MaxHealth}) EsDemolible={siDemol} aviso Kes='{avisoS}'{(okE ? "" : "(MAL)")}; ");
                }
            }
            finally
            {
                OperacionTerminal.PruebaMantenerE = false; OperacionTerminal.PruebaToqueE = false;
                Poseer(vega); ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // #084: el cartel nombra el rol REAL que hace falta (ASALTO, FLANQUEADOR, MEDICO) y sugiere a quien mandar con [Q] solo si hay alguien vivo con ese rol.
        static string Bug084()
        {
            var sb = new StringBuilder(); bool ok = true;
            foreach (var (rol, nombre) in new[] { (RoleType.Assault, "ASALTO"), (RoleType.Flanker, "FLANQUEADOR"), (RoleType.Medic, "MÉDICO") })
            {
                string t = RolRequerido.Texto(rol, "INTERACTUAR CON ESTO", null);
                bool bien = t.StartsWith("DEBES SER " + nombre + " PARA INTERACTUAR CON ESTO", StringComparison.Ordinal);
                if (!bien) ok = false;
                sb.Append($"{rol}: '{t}'{(bien ? "" : "(MAL)")}; ");
            }
            // Sin ningun aliado vivo de ese rol no hay sugerencia de [Q] (rol francotirador: la escuadra no lo tiene).
            string sin = RolRequerido.Texto(RoleType.Sniper, "ESTO", null);
            bool sinSug = sin == "DEBES SER FRANCOTIRADOR PARA ESTO";
            if (!sinSug) ok = false;
            sb.Append($"sin aliado: '{sin}'{(sinSug ? "" : "(MAL)")}");
            return (ok ? "OK " : "FALLO ") + sb;
        }

        static string PistaDeLaMira(Component panel)
        {
            var d = PlayerInputDriver.Activo;
            if (d == null) return "(sin driver)";
            var m = typeof(PlayerInputDriver).GetMethod("ActualizarPromptContextual", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            m.Invoke(d, new object[] { new SP.Player.AimResult { Type = AimTargetType.Interactuar, HitTransform = panel.transform, Point = panel.transform.position } });
            var v = UnityEngine.Object.FindFirstObjectByType<InteractHintView>(FindObjectsInactive.Include);
            if (v == null) return "(sin vista)";
            var campo = typeof(InteractHintView).GetField("texto", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var t = campo != null ? campo.GetValue(v) as UnityEngine.UI.Text : null;
            return t != null && t.gameObject.activeSelf ? t.text : "(oculto)";
        }

        // ---------------------------------------------------------------- teclado inyectado (tecla E, W...)
        static void Teclas(params Key[] teclas)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            InputSystem.QueueStateEvent(kb, new KeyboardState(teclas));
        }

        // ---------------------------------------------------------------- #088 revivir: animacion y bloqueo
        static IEnumerator Bug088()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (Keyboard.current == null) { Fin("FALLO no hay Keyboard.current"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var doc = Escuadrista(RoleType.Medic);
            var drv = PlayerInputDriver.Activo;
            if (vega == null || doc == null || drv == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (vega/doc/driver)"); yield break; }
            Key kE = KeyBindings.Get(KeyBindings.Interactuar);
            try
            {
                Poseer(vega); yield return null;
                var mando = drv.Brain.Current;
                // Doc cae a 1.5 m de Vega (sin terminar nada: solo la baja).
                Junto(doc, mando.transform.position, 1.5f, doc.transform.position);
                for (int i = 0; i < 3; i++) yield return null;
                ComandosDeDepuracion.Matar(doc.Id);
                for (int i = 0; i < 5; i++) yield return null;
                if (doc.Health.IsAlive) { Fin("FALLO Doc no cayo"); yield break; }

                // Control: con W solo, el poseido SI camina (si no, la prueba de bloqueo no probaria nada).
                var p0 = mando.transform.position;
                Teclas(Key.W);
                foreach (var x in Esperar(0.5f)) yield return x;
                Teclas();
                float control = Vector3.Distance(p0, mando.transform.position);
                bool controlOk = control > 0.5f;
                if (!controlOk) ok = false;
                sb.Append($"control W solo: avanzo {control:0.00} m{(controlOk ? "" : " (MAL: W no llega, la prueba no vale)")}; ");
                for (int i = 0; i < 10; i++) yield return null;
                Junto(doc, mando.transform.position, 1.5f, doc.transform.position);   // Doc vuelve a quedar al lado
                for (int i = 0; i < 3; i++) yield return null;

                // Mantener [E] + W durante 3.5 s: el poseido no se mueve mas de 5 cm, esta agachado, con la animacion de atender y no dispara.
                var posIni = mando.transform.position;
                KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 0f);
                Teclas(kE, Key.W);
                float maxDesvio = 0f; bool agachadoSiempre = true; int cuadros = 0; bool anim = false, bloqueo = false; float t0 = Time.realtimeSinceStartup; bool capturado = false;
                while (Time.realtimeSinceStartup - t0 < 3.5f)
                {
                    yield return null;
                    float t = Time.realtimeSinceStartup - t0;
                    KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, t);   // el estado del teclado inyectado no marca wasPressed: se fija el inicio a mano
                    if (t > 0.4f)
                    {
                        cuadros++;
                        maxDesvio = Mathf.Max(maxDesvio, Vector3.Distance(posIni, mando.transform.position));
                        if (!mando.Motor.IsCrouching) agachadoSiempre = false;
                        anim |= CurandoAnimacion.Animando(mando);
                        bloqueo |= drv.ReviviendoActivo;
                    }
                    if (t > 2.0f && !capturado) { capturado = true; foreach (var x in CapturarPantalla("v2_088_reviviendo.png")) yield return x; }
                }
                bool sigueCaido = !doc.Health.IsAlive;
                bool moviendo = maxDesvio > 0.05f;
                if (moviendo || !agachadoSiempre || !anim || !bloqueo || !sigueCaido) ok = false;
                sb.Append($"3.5 s con [E]+W: desvio max {maxDesvio:0.000} m{(moviendo ? " (MAL)" : "")}, agachado siempre={agachadoSiempre}, animacion de atender={anim}, bloqueo activo={bloqueo}, Doc sigue caido a los 3.5 s={sigueCaido} ({cuadros} cuadros); ");

                // Soltar [E]: se cancela (sin revivir), se levanta y la animacion desaparece.
                Teclas();
                foreach (var x in Esperar(0.5f)) yield return x;
                bool cancelado = !drv.ReviviendoActivo && !drv.AnimandoRevivir && !CurandoAnimacion.Animando(mando) && !mando.Motor.IsCrouching && !doc.Health.IsAlive;
                if (!cancelado) ok = false;
                sb.Append($"soltar [E]: cancela y se levanta={cancelado} (bloqueo={drv.ReviviendoActivo} anim={CurandoAnimacion.Animando(mando)} agachado={mando.Motor.IsCrouching}); ");

                // Completo: 5.4 s manteniendo [E]: Doc revive, el poseido se levanta y recupera el control.
                Junto(doc, mando.transform.position, 1.5f, doc.transform.position);
                for (int i = 0; i < 3; i++) yield return null;
                var posRev = mando.transform.position;
                KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 0f);
                Teclas(kE, Key.W);
                t0 = Time.realtimeSinceStartup; float desvio2 = 0f; bool revivio = false; float tRevivio = -1f;
                while (Time.realtimeSinceStartup - t0 < 6.5f)
                {
                    yield return null;
                    float t = Time.realtimeSinceStartup - t0;
                    KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, t);
                    if (!revivio) desvio2 = Mathf.Max(desvio2, Vector3.Distance(posRev, mando.transform.position));
                    if (!revivio && doc.Health.IsAlive) { revivio = true; tRevivio = t; }
                    if (revivio && t > tRevivio + 0.1f) break;
                }
                Teclas();
                foreach (var x in Esperar(0.4f)) yield return x;
                bool libre = !drv.ReviviendoActivo && !CurandoAnimacion.Animando(mando) && !mando.Motor.IsCrouching;
                bool tiempo = revivio && tRevivio >= 4.8f && tRevivio <= 6.2f;
                bool sinMover = desvio2 <= 0.05f;
                if (!revivio || !tiempo || !sinMover || !libre) ok = false;
                sb.Append($"revivir completo: revivio={revivio} a los {tRevivio:0.0} s{(tiempo ? "" : " (MAL: se esperaban ~5 s)")}, desvio {desvio2:0.000} m{(sinMover ? "" : " (MAL)")}, control devuelto={libre}; ");
            }
            finally
            {
                Teclas();
                if (doc != null && !doc.Health.IsAlive) Reanimacion.Ejecutar(doc);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #089 sonido y anillos al revivir
        static float Goertzel(float[] datos, int canales, int desde, int hasta, float hz, int sr)
        {
            double w = 2.0 * Math.PI * hz / sr, coef = 2.0 * Math.Cos(w), s1 = 0, s2 = 0;
            for (int i = desde; i < hasta; i++)
            {
                double s = datos[i * canales] + coef * s1 - s2; s2 = s1; s1 = s;
            }
            return (float)Math.Sqrt(s1 * s1 + s2 * s2 - coef * s1 * s2) / Mathf.Max(1, hasta - desde);
        }

        static IEnumerator Bug089()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var doc = Escuadrista(RoleType.Medic); var vega = Escuadrista(RoleType.Assault);
            if (doc == null || vega == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (doc/vega)"); yield break; }
            float escalaPrev = Time.timeScale;
            try
            {
                // 1) El clip: largo, pico y contenido (golpe grave 55 Hz al inicio, acorde Do-Mi-Sol despues).
                var clip = GenericSfx.Get(SfxKind.Revive);
                var datos = new float[clip.samples * clip.channels];
                clip.GetData(datos, 0);
                float pico = 0f; bool nan = false; foreach (var v in datos) { if (float.IsNaN(v)) nan = true; pico = Mathf.Max(pico, Mathf.Abs(v)); }
                int sr = clip.frequency, ch = clip.channels;
                float grave = Goertzel(datos, ch, 0, (int)(0.4f * sr), 55f, sr), agudoInicio = Goertzel(datos, ch, 0, (int)(0.4f * sr), 2500f, sr);
                int a = (int)(0.5f * sr), b = (int)(1.4f * sr);
                float eC = Goertzel(datos, ch, a, b, 523.25f, sr), eE = Goertzel(datos, ch, a, b, 659.25f, sr), eG = Goertzel(datos, ch, a, b, 783.99f, sr), fondo = Goertzel(datos, ch, a, b, 410f, sr);
                bool clipOk = clip.name == "Revive" && clip.length > 1.5f && clip.length < 2.2f && pico > 0.7f && pico <= 0.85f && !nan
                    && grave > agudoInicio * 3f && eC > fondo * 4f && eE > fondo * 4f && eG > fondo * 4f;
                if (!clipOk) ok = false;
                sb.Append($"clip '{clip.name}' {clip.length:0.00} s pico {pico:0.00}; grave55={grave:0.0000} vs 2.5k={agudoInicio:0.0000}; acorde Do/Mi/Sol={eC:0.0000}/{eE:0.0000}/{eG:0.0000} vs fondo {fondo:0.0000}{(clipOk ? "" : " (MAL)")}; ");

                // 2) El revivido: Doc cae y se lo revive por el camino unico. "Revive" suena exactamente 1 vez y salen 3 anillos.
                Junto(doc, vega.transform.position, 3f, doc.transform.position);
                for (int i = 0; i < 3; i++) yield return null;
                ComandosDeDepuracion.Matar(doc.Id);
                for (int i = 0; i < 5; i++) yield return null;
                foreach (var x in Esperar(0.2f)) yield return x;
                AudioDirector.Historial.Clear();
                int cant0 = FeedbackDeRevivir.Cantidad;
                Time.timeScale = 0.25f;   // camara lenta para ver y medir los anillos
                bool revivio = Reanimacion.Ejecutar(doc);
                yield return null;
                int anillosYa = ImpactFx.ReviveRingsActive;
                int revives = 0; foreach (var n in AudioDirector.Historial) if (n == "Revive") revives++;
                // La voz que suena es 3D.
                bool tresD = false; float blend = -1f;
                foreach (var src in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (src.isPlaying && src.clip != null && src.clip.name == "Revive") { blend = src.spatialBlend; tresD = src.spatialBlend > 0.9f; }
                // 3) Geometria de los anillos a mitad de camino: planos en el piso (+0.05), radio entre 0.4 y 3.0, alfa decreciente.
                var radios = new List<float>(); var alfas = new List<float>(); var alturas = new List<float>();
                int lineas = 0;
                float tM = Time.realtimeSinceStartup;
                // Tiempo de juego transcurrido ~ realtime*0.25: se espera hasta ~0.55 s de juego (2.2 s reales).
                while (Time.realtimeSinceStartup - tM < 2.2f) yield return null;
                var raiz = GameObject.Find("ReviveRingPool");
                if (raiz != null)
                    foreach (var lr in raiz.GetComponentsInChildren<LineRenderer>(false))
                    {
                        if (!lr.enabled) continue;
                        lineas++;
                        var c0 = lr.GetPosition(0); var c1 = lr.GetPosition(lr.positionCount / 2);
                        var centro = (c0 + c1) * 0.5f;
                        radios.Add(Vector3.Distance(c0, c1) * 0.5f); alturas.Add(centro.y); alfas.Add(lr.startColor.a);
                    }
                foreach (var x in CapturarPantalla("v2_089_anillos.png")) yield return x;
                float suelo = FloorY(doc);
                bool geom = lineas >= 2;
                foreach (var h in alturas) if (Mathf.Abs(h - (suelo + FeedbackDeRevivir.AlturaSobreElPiso)) > 0.03f) geom = false;
                foreach (var rr in radios) if (rr < 0.4f || rr > 3.05f) geom = false;
                foreach (var al in alfas) if (al <= 0f || al >= 0.9f) geom = false;
                Time.timeScale = escalaPrev;
                // 4) A los pocos segundos reales no queda ninguno activo.
                foreach (var x in Esperar(1.0f)) yield return x;
                int anillosFin = ImpactFx.ReviveRingsActive;
                bool anillosOk = anillosYa == FeedbackDeRevivir.Anillos && anillosFin == 0;
                bool sonidoOk = revivio && revives == 1 && tresD && FeedbackDeRevivir.Cantidad == cant0 + 1;
                if (!sonidoOk || !anillosOk || !geom) ok = false;
                sb.Append($"revivir: Revive sono {revives} vez/veces (pide 1), voz 3D={tresD} (spatialBlend {blend:0.00}){(sonidoOk ? "" : " (MAL)")}; anillos al revivir={anillosYa} (pide {FeedbackDeRevivir.Anillos}), a mitad {lineas} visibles con radios [{string.Join(", ", radios.ConvertAll(q => q.ToString("0.00")))}] alturas sobre el piso [{string.Join(", ", alturas.ConvertAll(q => (q - suelo).ToString("0.00")))}] alfas [{string.Join(", ", alfas.ConvertAll(q => q.ToString("0.00")))}]{(geom ? "" : " (MAL)")}, activos 1 s despues (1.3 s de vida) ={anillosFin}{(anillosOk ? "" : " (MAL)")}; ");

                // 5) Nadie mas toca SfxKind.Revive: el sonido sale SOLO de FeedbackDeRevivir (un unico camino, sin duplicados).
                var otros = new List<string>();
                foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Scripts"), "*.cs", SearchOption.AllDirectories))
                {
                    string n = Path.GetFileName(f);
                    if (n == "FeedbackDeRevivir.cs" || n == "GenericSfx.cs" || n.StartsWith("ChecksBugs065") || n.StartsWith("HeadlessTestRunner")) continue;   // la suite headless solo comprueba que el clip no sea mudo
                    if (File.ReadAllText(f).Contains("SfxKind.Revive")) otros.Add(n);
                }
                bool unico = otros.Count == 0;
                if (!unico) ok = false;
                sb.Append($"otros archivos con SfxKind.Revive: {(unico ? "ninguno" : string.Join(",", otros) + " (MAL)")}; ");
            }
            finally
            {
                Time.timeScale = escalaPrev;
                if (doc != null && !doc.Health.IsAlive) Reanimacion.Ejecutar(doc);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Camina hacia adelante 1.5 s y mide la malla minima en cada cuadro (salteando 0.4 s del arranque del paso).
        // res = { minimo, maximo, cuadros }.
        static IEnumerable CaminarYMedir(Soldier s, SoldierAnimatorDriver drv, float[] res, string[] detalle)
        {
            float min = float.MaxValue, max = float.MinValue; int n = 0; string peor = ""; float peorAbs = 0f;
            var dir = s.transform.forward;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.5f)
            {
                s.Motor.Move(dir, Time.deltaTime);
                yield return null;
                float v = MallaMinima(s, out _);
                if (Time.realtimeSinceStartup - t0 < 0.4f) continue;
                n++;
                min = Mathf.Min(min, v); max = Mathf.Max(max, v);
                float lejos = Mathf.Max(Mathf.Abs(v + 0.02f), Mathf.Abs(v - 0.04f));
                if (lejos > peorAbs) { peorAbs = lejos; peor = $"(peor: malla {v:+0.000;-0.000} huesos {drv.AlturaMinimaDeApoyo:+0.000;-0.000} desfase {drv.DesfaseDelModelo:+0.000;-0.000})"; }
            }
            res[0] = min; res[1] = max; res[2] = n; detalle[0] = peor;
        }

        static float FloorY(Soldier s)
        {
            var p = s.transform.position;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.3f, Vector3.down, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<Soldier>() == null) return h.point.y;
            return p.y - SoldierMotor.PivoteSobrePiso;
        }

        // ---------------------------------------------------------------- #092 el agachado no se entierra
        // Punto mas bajo de la malla visible (BakeMesh con la pose de este frame) respecto del piso real.
        static float MallaMinima(Soldier s, out float suelo)
        {
            suelo = FloorY(s);
            var smr = s.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null) return float.NaN;
            var m = new Mesh(); smr.BakeMesh(m, true);
            float min = 999f; var vs = m.vertices; var tr = smr.transform;
            for (int i = 0; i < vs.Length; i++) { float y = tr.TransformPoint(vs[i]).y; if (y < min) min = y; }
            UnityEngine.Object.Destroy(m);
            return min - suelo;
        }

        static IEnumerator Bug092()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var kes = Escuadrista(RoleType.Flanker);
            if (kes == null) { ModoDios.Poner(diosAntes); Fin("FALLO sin Kes"); yield break; }
            var brain = kes.GetComponent<SP.Ai.AiBrain>();
            var drvAnim = kes.GetComponentInChildren<SoldierAnimatorDriver>();
            const float Lo = -0.02f, Hi = 0.04f;
            try
            {
                // 0) El clip horneado existe y el controlador lo usa (se miden los dos lados: archivo y runtime).
                var horneado = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animation/Marcha/idle_crouching_aiming.anim");
                float rootTy = float.NaN;
                if (horneado != null)
                    foreach (var bnd in UnityEditor.AnimationUtility.GetCurveBindings(horneado))
                        if (bnd.propertyName == "RootT.y") { var cv = UnityEditor.AnimationUtility.GetEditorCurve(horneado, bnd); float sm = 0; foreach (var k in cv.keys) sm += k.value; rootTy = sm / cv.length; }
                bool clipOk = horneado != null && Mathf.Abs(rootTy - 0.4274f) < 0.001f;
                string enControlador = "?";
                var anim = kes.GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                    foreach (var c in anim.runtimeAnimatorController.animationClips) if (c.name.Replace('_', ' ') == "idle crouching aiming") enControlador = UnityEditor.AssetDatabase.GetAssetPath(c);
                bool ctrlOk = enControlador.EndsWith("idle_crouching_aiming.anim", StringComparison.Ordinal);
                if (!clipOk || !ctrlOk) ok = false;
                sb.Append($"clip horneado RootT.y={rootTy:0.0000}{(clipOk ? "" : "(MAL)")}, el controlador usa '{enControlador}'{(ctrlOk ? "" : "(MAL)")}; ");

                // 1) Kes de pie y agachado (cerebro apagado para que no cambie la postura).
                if (brain != null) brain.enabled = false;
                kes.Motor.SetCrouching(false);
                foreach (var x in Esperar(1.2f)) yield return x;
                float dePie = MallaMinima(kes, out float suelo);
                bool pieOk = dePie >= Lo && dePie <= Hi;
                kes.Motor.SetCrouching(true);
                foreach (var x in Esperar(1.2f)) yield return x;
                float agachado = MallaMinima(kes, out _);
                bool agaOk = agachado >= Lo && agachado <= Hi;
                if (!pieOk || !agaOk) ok = false;
                sb.Append($"Kes de pie malla minima {dePie:+0.000;-0.000}{(pieOk ? "" : "(MAL)")} (desfase {drvAnim.DesfaseDelModelo:+0.000;-0.000}); agachado {agachado:+0.000;-0.000}{(agaOk ? "" : "(MAL)")} (desfase {drvAnim.DesfaseDelModelo:+0.000;-0.000}) [rango {Lo:0.00}..{Hi:0.00}]; ");

                // 2) Caminando agachado y de pie (la cadera del ciclo cambia con cada paso): 1.5 s hacia adelante, medido en cada cuadro.
                foreach (var postura in new[] { true, false })
                {
                    kes.Motor.SetCrouching(postura);
                    foreach (var x in Esperar(0.6f)) yield return x;
                    var res = new float[3]; var detalle = new string[1];
                    foreach (var x in CaminarYMedir(kes, drvAnim, res, detalle)) yield return x;
                    bool caminOk = res[0] >= Lo - 0.02f && res[1] <= Hi + 0.07f;   // enterrarse es lo grave; flotar unos cm entre dos pasos se tolera
                    if (!caminOk) ok = false;
                    sb.Append($"{(postura ? "agachado" : "de pie")} caminando: malla minima entre {res[0]:+0.000;-0.000} y {res[1]:+0.000;-0.000} en {res[2]:0} cuadros (tolerancia {Lo - 0.02f:0.00}..{Hi + 0.07f:0.00}){(caminOk ? "" : "(MAL)")} {detalle[0]}; ");
                }
                kes.Motor.SetCrouching(false);
                if (brain != null) brain.enabled = true;

                // 3) Tres enemigos atrincherados (objetivo 2, puesto 1): agachados, en cobertura, medidos con la malla real.
                OperacionPrueba.Arrancar(2, 1);
                foreach (var x in Esperar(2.5f)) yield return x;
                var medidos = new List<string>(); int buenos = 0, total = 0;
                foreach (var e in ActorRegistry.All)
                {
                    if (e == null || e.Team != TeamId.Enemy || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy || !e.Motor.IsCrouching) continue;
                    float v = MallaMinima(e, out _);
                    total++;
                    if (v >= Lo && v <= Hi) buenos++;
                    medidos.Add($"{e.name}:{v:+0.000;-0.000}");
                    if (total >= 3) break;
                }
                bool atrOk = total >= 3 && buenos == total;
                if (!atrOk) ok = false;
                sb.Append($"enemigos atrincherados agachados medidos {total} (pide 3): [{string.Join(", ", medidos)}] dentro de rango {buenos}/{total}{(atrOk ? "" : "(MAL)")}; ");
            }
            finally
            {
                if (brain != null) brain.enabled = true;
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
