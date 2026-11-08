using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // Fase 4 (HUIR): subir al tanque, trayecto de 60 s con camionetas perseguidoras, llegada en calma y destruccion del tanque.
    public partial class OperacionDirector
    {
        // ---- 4. huida en tanque ----
        // TickHuir (las subfases Acercamiento / Jefe / Reparacion / Abordaje) vive en OperacionDirector.Jefe.cs (WP9b).

        void TickAntesDelTanque()
        {
            var yo = Poseido();
            float d = yo != null ? Plano(yo.transform.position, tanque.transform.position).magnitude : 999f;
            if (hud != null) hud.Objetivo(TituloObjetivo(4, "HUIR EN EL TANQUE"), $"Subi al tanque reparado · {Mathf.RoundToInt(d)} m · el tanque avanza solo: vos manejas el CANON", 0f, new Color(0.4f, 1f, 0.5f));
            if (d <= 6.5f && yo != null && yo.Health.IsAlive)
            {
                if (hud != null) hud.Prompt("[E] SUBIR AL TANQUE (VAS DE ARTILLERO: SOLO EL CANON)");
                if (OperacionTerminal.EToque()) Embarcar();
            }
            else if (yo != null && yo.Health.IsAlive && hud != null)
            {
                string pista = PistaDeLanzacohetes(yo);
                if (pista != null) hud.Prompt(pista);
            }
        }

        // Bug #055: "que aqui haya un cartel que diga use la tecla 3 para usar el lanzacohetes contra los vehiculos". Mientras
        // haya camionetas enemigas a la vista (a menos de 160 m) se indica la tecla del lanzacohetes del soldado poseido, o
        // quien lo lleva si el poseido no tiene. Con el lanzacohetes ya en la mano no hace falta repetirlo.
        public const float DistanciaPistaLanzacohetes = 160f;
        public string PistaDeLanzacohetes(Soldier yo)
        {
            bool hayVehiculo = false;
            foreach (var v in SP.Core.WorldSystemsRegistry.Vehicles)
                if (v != null && v.isActiveAndEnabled && !v.IsDestroyed && v.Bando == TeamId.Enemy && Plano(v.transform.position, yo.transform.position).magnitude < DistanciaPistaLanzacohetes) { hayVehiculo = true; break; }
            if (!hayVehiculo || yo.Weapon == null) return null;
            int slot = yo.Weapon.Loadout.IndexOf(WeaponKind.Rocket);
            if (slot >= 0)
                return yo.Weapon.CurrentWeaponKind == WeaponKind.Rocket ? null : $"VEHICULOS ENEMIGOS · TECLA [{slot + 1}] = LANZACOHETES: UN COHETE LOS DESTRUYE";
            foreach (var s in Escuadra())
                if (s != yo && s.Weapon != null && s.Weapon.Loadout.Contains(WeaponKind.Rocket))
                    return $"VEHICULOS ENEMIGOS · EL {s.ClassName} ({s.DisplayName}) TIENE LANZACOHETES: POSEELO Y USA LA TECLA [{s.Weapon.Loadout.IndexOf(WeaponKind.Rocket) + 1}]";
            return null;
        }

        System.Collections.IEnumerator PonerDeArtillero(PlayerInputDriver driver, Soldier yo)
        {
            for (int i = 0; i < 120 && tanque.IsMountAnimating(yo); i++) yield return null;
            for (int intento = 0; intento < 5 && driver.CurrentSeat != VehicleSeatRole.Gunner; intento++)
            {
                driver.SwitchSeat(VehicleSeatRole.Gunner);
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Sube a la escuadra: un aliado maneja, el resto de pasajeros, y el soldado poseido queda de artillero.
        public void Embarcar()
        {
            if (EnTanque) return;
            var driver = PlayerInputDriver.Activo;
            var yo = Poseido();
            if (driver == null || yo == null) return;
            // WP9b: si todavia no se derribo al jefe ni se lo reparo (checks viejos, restauraciones), se da por hecho.
            if (Subfase < (int)SubfaseHuida.Abordaje) SaltearJefeYReparacion();
            QuitarRestosDelPatio();
            // PUNTO DE CONTROL al subir: si el tanque cae (o la escuadra muere dentro), "reintentar" vuelve justo aca, a pie y junto al tanque.
            // Se guarda ANTES de montar: el guardado exige que el soldado poseido este vivo y a pie.
            PartidaGuardada.AutoGuardar("subir al tanque");
            // Sin piso de vida: el tanque se puede DESTRUIR en la carrera. A cambio el dano es parejo (ver CurvaDeDanoDelTanque) y no
            // "mucho al principio y casi nada despues" como con el piso del 20 %.
            tanque.PisoDeVida01 = 0f;
            tanque.AjusteDeDano = CurvaDeDanoDelTanque();

            var otros = Escuadra();
            otros.Remove(yo);
            // Maneja Kes (la Flanqueadora); si el jugador ES Kes, maneja Doc (el Medico) y si no hay, el primero que quede.
            Soldier conductor = null;
            foreach (var s in otros) if (s.Role == RoleType.Flanker) { conductor = s; break; }
            if (conductor == null) foreach (var s in otros) if (s.Role == RoleType.Medic) { conductor = s; break; }
            if (conductor == null && otros.Count > 0) conductor = otros[0];
            if (conductor != null)
            {
                otros.Remove(conductor);
                tanque.Mount(conductor, VehicleSeatRole.Driver, true);
            }
            var libres = new[] { VehicleSeatRole.Passenger2, VehicleSeatRole.Passenger1 };   // bug #322: sin metralleta, el Passenger1 es el ultimo recurso
            for (int i = 0; i < otros.Count && i < libres.Length; i++) tanque.Mount(otros[i], libres[i], true);

            driver.Vehicle = tanque;   // el driver trabaja sobre "su" vehiculo (campo serializado en la escena)
            driver.EnterVehicle(tanque);
            // El jugador maneja solo el canon: asiento de artillero. SwitchSeat se rechaza mientras dura la animacion de subida,
            // asi que se pide en cuanto termina.
            StartCoroutine(PonerDeArtillero(driver, yo));

            EnTanque = true;
            EntrarSubfase((int)SubfaseHuida.Carrera);
            IniciarCarrera();
            Reloj = 0f;
            MaximoDeAutosVivos = 0;
            indiceRuta = 0;
            proximoAuto = 2.5f;
            destrabeDesde = 0f; destrabePos = tanque.transform.position; destrabeIntentos = 0; DestrabadasDelTanque = 0; RescatesDelTanque = 0;
            // Velocidad del tanque tal que la ruta entera dure ~el trayecto (el motor es [SerializeField] privado).
            var motor = tanque.GetComponent<VehicleMotor>();
            if (motor != null)
            {
                var f = typeof(VehicleMotor).GetField("maxSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null)
                {
                    if (velocidadOriginal < 0f) velocidadOriginal = (float)f.GetValue(motor);
                    // Ruta del doble de largo (~870 m) recorrida en ~52 s: ~17 m/s, 2,4 veces la marcha de antes (7 m/s). Mas aceleracion
                    // para que arranque y salga de cada curva con empuje en vez de "flotar" hasta la velocidad final.
                    f.SetValue(motor, LargoDeLaRuta() / (segundosDeTrayecto * 0.97f));
                    var fa = typeof(VehicleMotor).GetField("acceleration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fa != null) fa.SetValue(motor, 16f);
                    motor.VariarRitmo = true;   // pedido 2026-11-07: la marcha varia entre normal y rapida
                }
            }
            PotenciarCanon();
            tanque.QuitarMetralleta(true);
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            Aviso("A BORDO · TRAYECTO DE " + Mathf.RoundToInt(segundosDeTrayecto) + " s\nDESTRUI A LOS AUTOS CON EL CANON");
            StartCoroutine(DespejarRutaPorLotes());   // #373: por lotes, sin tiron al subir
            EmitirOrdenDeRuta();
        }

        // Pedido: "mas poder a los ataques e impactos de tanques: lo que manejas es un tanque, los enemigos manejan camionetas". El obus
        // pega 3 veces mas fuerte, la explosion cubre casi el doble de radio y el cañon pesa mas (recarga de ~1,1 s en vez de 0,5 s).
        public const int DanoDelCanon = 135;
        public const float RadioDelCanon = 5.5f;
        public const float RecargaDelCanon = 1.1f;

        void PotenciarCanon()
        {
            var t = tanque != null ? tanque.TorretaCanon : null;
            if (t == null) return;
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(TurretWeapon).GetField("damage", bf)?.SetValue(t, DanoDelCanon);
            typeof(TurretWeapon).GetField("explosionRadius", bf)?.SetValue(t, RadioDelCanon);
            typeof(TurretWeapon).GetField("fireCooldown", bf)?.SetValue(t, RecargaDelCanon);
            // Bug #047: el artillero apunta al suelo cerca del tanque: el tubo necesita bajar mas que los 10° de antes.
            t.FijarLimitesDeElevacion(75f, 45f);   // bug #217: el Halcon enemigo se podia perder por arriba de los 32 grados; ahora el cañon sube hasta 75   // WP9b-2: el Halcon orbita a 18..24 m de altura (hasta ~28 grados desde la torreta)
        }

        float LargoDeLaRuta()
        {
            float l = 0f;
            var prev = tanque.transform.position;
            foreach (var t in rutaDelTanque) { l += Plano(t.position, prev).magnitude; prev = t.position; }
            return Mathf.Max(50f, l);
        }

        // Pedido 2026-10-21: "el tanque al iniciar recorrido se traba: no se tiene que obstaculizar con objetos que no sean destruibles".
        // Medido: el pulido del nivel dejo props sin vida (poste caido, auto quemado, neumaticos) sobre la ruta y el casco quedaba
        // clavado (hasta 38 s). Antes de arrancar se barre el corredor del tanque y se les apaga la colision; lo destruible (barricadas,
        // autos abandonados) se deja: el tanque lo aplasta (Atropello). Murallas y edificios (altos o enormes) no se tocan.
        public const float AnchoDelCorredorDelTanque = 1.7f, AlturaMaximaDeUnPropDespejable = 4f, TamanoMaximoDeUnPropDespejable = 12f;
        public int PropsDespejadosDeLaRuta { get; private set; }

        // Apaga tambien lo VISIBLE del prop: el propio objeto del collider y, si su padre inmediato es chico (el prop completo, p. ej. auto + collider hijo), el padre.
        static void OcultarPropDespejado(Collider c)
        {
            var t = c.transform;
            var p = t.parent;
            if (p != null && p.childCount <= 6)
            {
                var rs = p.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    if (Mathf.Max(b.size.x, b.size.z) <= TamanoMaximoDeUnPropDespejable && b.max.y <= AlturaMaximaDeUnPropDespejable) t = p;
                }
            }
            t.gameObject.SetActive(false);
        }

        public int DespejarRutaDelTanque()
        {
            PropsDespejadosDeLaRuta = 0;
            if (tanque == null || rutaDelTanque == null) return 0;
            var propios = tanque.GetComponentsInChildren<Collider>(true);
            var vistos = new HashSet<Collider>();
            var prev = tanque.transform.position;
            foreach (var t in rutaDelTanque)
            {
                if (t == null) continue;
                DespejarTramo(prev, t.position, propios, vistos);
                prev = t.position;
            }
            SesionLog.Evento($"HUIDA: ruta del tanque despejada ({PropsDespejadosDeLaRuta} props sin vida sin colision)");
            return PropsDespejadosDeLaRuta;
        }

        // Bug #373 ("se trabo mucho al entrar"): barrer toda la ruta (~870 m, cientos de OverlapBox) de golpe en el frame de subir al tanque era un
        // tiron justo al arrancar. Por lotes: un tramo por cuadro, empezando por el primero (el tanque tarda ~0,7 s en recorrer cada tramo).
        public bool RutaDespejadaPorLotes { get; private set; }
        System.Collections.IEnumerator DespejarRutaPorLotes()
        {
            PropsDespejadosDeLaRuta = 0; RutaDespejadaPorLotes = false;
            if (tanque == null || rutaDelTanque == null) yield break;
            var propios = tanque.GetComponentsInChildren<Collider>(true);
            var vistos = new HashSet<Collider>();
            var prev = tanque.transform.position;
            foreach (var t in rutaDelTanque)
            {
                if (t == null) continue;
                DespejarTramo(prev, t.position, propios, vistos);
                prev = t.position;
                yield return null;
            }
            RutaDespejadaPorLotes = true;
            SesionLog.Evento($"HUIDA: ruta del tanque despejada por lotes ({PropsDespejadosDeLaRuta} props sin vida sin colision)");
        }

        void DespejarTramo(Vector3 prev, Vector3 hasta, Collider[] propios, HashSet<Collider> vistos)
        {
            var dir = hasta - prev; dir.y = 0f;
            float largo = dir.magnitude;
            if (largo <= 0.1f) return;
            dir /= largo;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            for (float d = 0f; d <= largo + 1f; d += 2f)
            {
                var centro = prev + dir * Mathf.Min(d, largo) + Vector3.up * 1.4f;
                foreach (var c in Physics.OverlapBox(centro, new Vector3(AnchoDelCorredorDelTanque, 1.1f, 1.4f), rot))
                {
                    if (c == null || c.isTrigger || !c.enabled || !vistos.Add(c) || c is TerrainCollider) continue;
                    if (System.Array.IndexOf(propios, c) >= 0) continue;
                    if (c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Vehicle>() != null) continue;
                    var marca = c.GetComponentInParent<ObstacleMarker>();
                    if (marca != null && marca.EsDestruible) continue;
                    var b = c.bounds;
                    if (b.max.y > AlturaMaximaDeUnPropDespejable || Mathf.Max(b.size.x, b.size.z) > TamanoMaximoDeUnPropDespejable) continue;
                    c.enabled = false;
                    OcultarPropDespejado(c);   // bug #316: "elimina los elementos que estan en el camino": no basta quitarles la colision
                    PropsDespejadosDeLaRuta++;
                }
            }
        }

        void EmitirOrdenDeRuta()
        {
            var vb = tanque.GetComponent<VehicleBrain>();
            if (vb == null || indiceRuta >= rutaDelTanque.Length) return;
            vb.IssueMoveOrder(rutaDelTanque[indiceRuta].position);
        }

        public const float PisoDeVidaDelTanque = 0.2f;
        public const float ProporcionDeAutosPorDelante = 0.3f;
        public const int AutosSimultaneosBase = 2;   // constante: el campo serializado autosSimultaneos de SC_Operacion quedo con el 4 viejo
        public const float AparicionMin = 3f, AparicionMax = 5f;   // Bug #172: antes 1-2 s (se destruian 30 autos en una huida)

        // Bug #073/#172: 2 camionetas a la vez (3 en Dificil: la dificultad escala con CantidadDeOleadas, tope +1).
        public int AutosSimultaneosEfectivos
        {
            get
            {
                float k = Mathf.Max(1f, Dificultad.Datos(Dificultad.Actual).CantidadDeOleadas);
                return Mathf.Clamp(Mathf.RoundToInt(AutosSimultaneosBase * k), AutosSimultaneosBase, AutosSimultaneosBase + 1);
            }
        }

        public int AutosVivosAhora { get { int n = 0; foreach (var a in autos) if (a != null && !a.Muerto) n++; return n; } }
        public int MaximoDeAutosVivos { get; private set; }

        // Bug #069: la metralleta del tanque (asiento Passenger1) la opera la IA cuando ahi va un aliado. Idempotente.
        public TurretAI InstalarMetralletaConIA()
        {
            var mg = tanque != null ? tanque.TorretaMetralleta : null;
            if (mg == null) return null;
            var ai = mg.GetComponent<TurretAI>();
            if (ai == null) ai = mg.gameObject.AddComponent<TurretAI>();
            ai.ConfigurarAsiento(VehicleSeatRole.Passenger1, 48f);
            return ai;
        }

        // Pedido 2026-10-21 (otra vez): "el tanque se sigue trabando al comienzo". Ademas de limpiar la ruta antes de arrancar, un destrabador
        // general: si con orden activa el tanque no avanza ni 1,5 m en 1,2 s, se le libera lo que tiene delante (cadaveres, restos de vehiculos,
        // props y barricadas: se les apaga la colision, el tanque los aplasta) y, si a la 2.a vez sigue clavado, se lo adelanta 7 m por la ruta.
        public const float SegundosParaDestrabar = 1.2f, AvanceMinimoParaNoEstarTrabado = 1.5f, SaltoDeRescate = 7f;
        public const float FinDeRutaRadio = 7f;
        public bool RutaTerminadaAntesDeTiempo { get; private set; }
        public int DestrabadasDelTanque { get; private set; }
        public int RescatesDelTanque { get; private set; }
        float destrabeDesde; Vector3 destrabePos; int destrabeIntentos;

        void DestrabarTanque(float dt)
        {
            if (tanque == null || Reloj < 1f) return;
            destrabeDesde += dt;
            if (destrabeDesde < SegundosParaDestrabar) return;
            var pos = tanque.transform.position;
            bool quieto = Plano(pos, destrabePos).magnitude < AvanceMinimoParaNoEstarTrabado;
            destrabeDesde = 0f; destrabePos = pos;
            if (!quieto) { destrabeIntentos = 0; return; }
            destrabeIntentos++;
            var rb = tanque.GetComponent<Rigidbody>();
            var objetivo = indiceRuta < rutaDelTanque.Length ? rutaDelTanque[indiceRuta].position : pos + tanque.transform.forward * 10f;
            var hacia = objetivo - pos; hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.01f) hacia = tanque.transform.forward;
            hacia.Normalize();
            if (destrabeIntentos == 1)
            {
                int n = 0;
                var propios = tanque.GetComponentsInChildren<Collider>(true);
                var rot = Quaternion.LookRotation(hacia, Vector3.up);
                foreach (var c in Physics.OverlapBox(pos + hacia * 3f + Vector3.up * 1.4f, new Vector3(2.4f, 1.6f, 4.5f), rot))
                {
                    if (c == null || c.isTrigger || !c.enabled || c is TerrainCollider || System.Array.IndexOf(propios, c) >= 0) continue;
                    var otro = c.GetComponentInParent<Vehicle>();
                    if (otro == tanque || (otro != null && !otro.IsDestroyed)) continue;   // vehiculos vivos (camionetas) no pierden su collider
                    var sol = c.GetComponentInParent<Soldier>();
                    if (sol != null && sol.Team == TeamId.Player && sol.Health.IsAlive) continue;   // la escuadra del jugador no se toca
                    var b = c.bounds;
                    if (b.max.y > 9f || Mathf.Max(b.size.x, b.size.z) > 20f) continue;   // murallas y edificios no
                    c.enabled = false; n++;
                }
                DestrabadasDelTanque++;
                SesionLog.Evento($"HUIDA: tanque trabado en {pos}: se liberaron {n} colisiones de adelante");
                var vb = tanque.GetComponent<VehicleBrain>(); if (vb != null) vb.IssueMoveOrder(objetivo);
            }
            else if (destrabeIntentos == 2)
            {
                // Bugs #374/#376 (2026-11-08: "se me snapea al obstaculo, corrigelo... quiero que no snapee o quita esos obstaculos"): el salto de 7 m
                // se veia como un tiron. Antes de saltar se prueba algo suave: se libera un corredor mas ancho y largo (props sin vida) y se le da un
                // empujon hacia adelante por velocidad (sin teletransportar).
                int n = 0;
                var propios = tanque.GetComponentsInChildren<Collider>(true);
                var rot = Quaternion.LookRotation(hacia, Vector3.up);
                foreach (var c in Physics.OverlapBox(pos + hacia * 5f + Vector3.up * 1.4f, new Vector3(3.6f, 2.2f, 8f), rot))
                {
                    if (c == null || c.isTrigger || !c.enabled || c is TerrainCollider || System.Array.IndexOf(propios, c) >= 0) continue;
                    var otro = c.GetComponentInParent<Vehicle>();
                    if (otro != null && !otro.IsDestroyed) continue;
                    var sol = c.GetComponentInParent<Soldier>();
                    if (sol != null && sol.Team == TeamId.Player && sol.Health.IsAlive) continue;
                    var b = c.bounds;
                    if (b.max.y > 9f || Mathf.Max(b.size.x, b.size.z) > 20f) continue;
                    c.enabled = false; OcultarPropDespejado(c); n++;
                }
                if (rb != null) rb.linearVelocity = hacia * 7f;
                SesionLog.Evento($"HUIDA: tanque trabado en {pos}: corredor ancho ({n} colisiones) y empujon suave");
            }
            else
            {
                var nueva = pos + hacia * SaltoDeRescate;
                if (rb != null) { rb.position = nueva; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                tanque.transform.position = nueva;
                RescatesDelTanque++; destrabeIntentos = 0;
                SesionLog.Evento($"HUIDA: tanque rescatado en {pos}: adelantado {SaltoDeRescate} m por la ruta");
            }
        }

        // Balance del dano al tanque (valores razonados sobre las fuentes: camioneta 7/bala, cohete 60-80, metralla 6, embestida 24):
        //  - factor segun la vida restante: 0,55 hasta el 60 %, baja lineal a 0,35 en el 15 % y ahi queda (nunca 0, el tanque cae);
        //  - tope por golpe del 7 % de la vida maxima (un cohete de 60 pasa a 18);
        //  - presupuesto en ventana de 1 s: 1,6 % de la vida maxima por segundo con el tanque sano y 1,2 % debajo del 50 %;
        //  - los primeros 6 s de la carrera, todo a la mitad (el jugador todavia se esta acomodando).
        // Con fuego continuo ininterrumpido el tanque dura ~65 s (el trayecto); con presion normal llega al final con 30-50 % de vida.
        // Bug #009 (build, "me deberian hacer el doble de dano"): el factor, el tope por golpe y el presupuesto por segundo se duplican (x2).
        public const float MultiplicadorDeDanoDelTanque = 4f;   // bug #347 ("deberian quitarme mucha mas vida"): era x2; llegando al final de la ruta sin repararlo el tanque queda casi destruido
        System.Func<int, int> CurvaDeDanoDelTanque()
        {
            float ventanaInicio = -10f, gastado = 0f;
            return dano =>
            {
                if (tanque == null || tanque.Health == null) return dano;
                float max = tanque.Health.MaxHealth, v = Mathf.Clamp01(tanque.Health.Current / Mathf.Max(1f, max));
                float f = v >= 0.60f ? 0.55f : Mathf.Lerp(0.35f, 0.55f, Mathf.InverseLerp(0.15f, 0.60f, v));
                if (Reloj < 6f) f *= 0.5f;
                f *= MultiplicadorDeDanoDelTanque;
                float d = Mathf.Min(dano * f, max * 0.07f * MultiplicadorDeDanoDelTanque);
                float ahora = Time.time;
                if (ahora - ventanaInicio >= 1f) { ventanaInicio = ahora; gastado = 0f; }
                float tope = max * (v > 0.5f ? 0.016f : 0.012f) * MultiplicadorDeDanoDelTanque;   // con 1600 de vida: ~51 HP/s sano, ~38 HP/s herido
                d = Mathf.Min(d, Mathf.Max(1f, tope - gastado));
                gastado += d;
                return Mathf.Max(1, Mathf.RoundToInt(d));
            };
        }

        void TickTrayecto(float dt)
        {
            var vb = tanque.GetComponent<VehicleBrain>();
            // Sin piso de vida el tanque puede caer: se pierde y el reintento vuelve al punto de control de cuando se subio.
            if (tanque != null && tanque.IsDestroyed && Subfase == (int)SubfaseHuida.Carrera) { Perder("EL TANQUE FUE DESTRUIDO"); return; }
            // El tanque no se detiene: cuando esta cerca del punto actual, ya se pide el siguiente.
            if (indiceRuta < rutaDelTanque.Length)
            {
                float d = Plano(rutaDelTanque[indiceRuta].position, tanque.transform.position).magnitude;
                if (d < 11f && indiceRuta < rutaDelTanque.Length - 1) { indiceRuta++; EmitirOrdenDeRuta(); }
                else if (vb != null && !vb.HasOrder && indiceRuta < rutaDelTanque.Length - 1) { indiceRuta++; EmitirOrdenDeRuta(); }
                else if (vb != null && !vb.HasOrder) EmitirOrdenDeRuta();
            }

            // Bug #379 ("se bugeo no avanza en esta parte"): en el ULTIMO punto de la ruta el tanque ya no tiene a donde ir y el destrabador lo
            // "rescataba" una y otra vez contra el muro de la ciudad hasta que se cumplia el reloj. Al llegar al final se cierra el viaje.
            if (rutaDelTanque.Length > 0 && indiceRuta >= rutaDelTanque.Length - 1 && Reloj > 5f
                && Plano(rutaDelTanque[rutaDelTanque.Length - 1].position, tanque.transform.position).magnitude < FinDeRutaRadio)
            {
                RutaTerminadaAntesDeTiempo = true;
                IniciarCinematicaFinal(); return;
            }
            DestrabarTanque(dt);

            // El tanque aguanta hasta el final del trayecto (el guion lo destruye). Bug #073: solo tiene un PISO del 20 % de la vida
            // (Vehicle.PisoDeVida01, WP9b: antes era una cura "a mano" cada frame): si el dano lo pasa, se lo deja justo ahi.

            if (Reloj >= segundosDeTrayecto) { IniciarCinematicaFinal(); return; }
            MantenerAutos();
            TickCarreraExtras();
            int rest = Mathf.CeilToInt(TrayectoRestante);
            if (hud != null)
            {
                hud.Objetivo(TituloObjetivo(4, "HUIR EN EL TANQUE"), $"Canon: camionetas y cornisas con cohetes · derribados: {AutosDestruidos} · el tanque sigue su camino", Reloj / segundosDeTrayecto, new Color(0.4f, 1f, 0.5f));
                hud.Timer("TRAYECTO", rest, rest <= 8 ? new Color(1f, 0.4f, 0.3f) : Color.white);
            }
        }

        // ---- Bug #060: llegada en calma ----
        // "El cambio no debe ser tan brusco: 3 segundos de margen de seguridad en que sigue la ruta en calma hasta que te bajas,
        // y un feedback sonoro de que termino el viaje y debes seguir a pie". Al cumplirse el trayecto: los perseguidores
        // abandonan (frenan y quedan atras, sin disparar), el tanque frena suave durante SegundosDeLlegada, la escuadra se baja
        // con una campana de "fin del viaje" y recien SegundosHastaElAtaque despues un cohete enemigo destruye el tanque vacio.
        public const float SegundosDeLlegada = 3f, SegundosHastaElAtaque = 2.6f;
        float llegadaDesde = -1f, velocidadDeViaje;
        public bool EnLlegada => llegadaDesde >= 0f;
        public bool BajaronDelTanque { get; private set; }

        void TickLlegada()
        {
            var motor = tanque.GetComponent<VehicleMotor>();
            var campoVel = typeof(VehicleMotor).GetField("maxSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (motor != null) motor.VariarRitmo = false;
            if (llegadaDesde < 0f)
            {
                llegadaDesde = Reloj;
                BajaronDelTanque = false;
                velocidadDeViaje = motor != null && campoVel != null ? (float)campoVel.GetValue(motor) : 0f;
                foreach (var a in autos) if (a != null && !a.Muerto) a.Retirarse();
                if (hud != null) hud.OcultarTimer();
                Aviso("LLEGANDO A LA CIUDAD\nEL TANQUE FRENA: PREPARATE PARA BAJAR");
                AudioDirector.PlayUi2D(SfxKind.RadialConfirm, 0.6f, 0.9f);
            }
            float t = Reloj - llegadaDesde;
            // Frenada suave hasta detenerse al final del margen.
            if (motor != null && campoVel != null) campoVel.SetValue(motor, Mathf.Lerp(velocidadDeViaje, 0.5f, Mathf.SmoothStep(0f, 1f, t / SegundosDeLlegada)));
            if (hud != null)
                hud.Objetivo(TituloObjetivo(4, "HUIR EN EL TANQUE"), BajaronDelTanque ? "Fin del viaje · segui a pie hasta la plaza de la ciudad" : "Llegando a la ciudad · el tanque frena en la entrada", 1f, new Color(0.4f, 1f, 0.5f));
            if (t >= SegundosDeLlegada && !BajaronDelTanque) BajarDelTanque();
            if (t >= SegundosDeLlegada + SegundosHastaElAtaque) DestruirTanque();
        }

        void BajarDelTanque()
        {
            BajaronDelTanque = true;
            var vb = tanque.GetComponent<VehicleBrain>();
            if (vb != null) vb.Stop();
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.CurrentSeat.HasValue && drv.Vehicle == tanque) drv.ExitVehicle();
            foreach (var o in new List<Soldier>(tanque.Occupants)) tanque.Dismount(o);
            // El panel de tripulacion (CO/CA/ME) y el cartel de asiento quedaban prendidos a pie.
            if (drv != null && drv.VehicleStatus != null) { drv.VehicleStatus.UpdateFrom(null, null); drv.VehicleStatus.gameObject.SetActive(false); }
            var yo = Poseido();
            if (yo != null) foreach (var s in Escuadra()) if (s != yo && s.Brain != null) OrderService.IssueFollowOrder(s, yo, default, 2f, true);
            AudioDirector.PlayUi2D(SfxKind.ObjetivoCumplido, 0.85f, 1f);
            Aviso("FIN DEL VIAJE\nSEGUI A PIE HASTA LA PLAZA");
            GameLog.Line("[Operacion] Fin del viaje en tanque: la escuadra baja");
        }

        void MantenerAutos()
        {
            autos.RemoveAll(a => a == null);
            int vivos = 0;
            foreach (var a in autos) if (!a.Muerto) { vivos++; }
            if (vivos < AutosSimultaneosEfectivos && Reloj >= proximoAuto && autoPlantilla != null)
            {
                proximoAuto = Reloj + UnityEngine.Random.Range(AparicionMin, AparicionMax);
                CrearAuto();
                vivos++;
            }
            if (vivos > MaximoDeAutosVivos) MaximoDeAutosVivos = vivos;
            // Los muertos ya sumaron: se cuentan una sola vez al detectar la muerte.
            foreach (var a in autos) if (a.Muerto && !contados.Contains(a)) { contados.Add(a); AutosDestruidos++; AlertQueue.Push("AUTO ENEMIGO DESTRUIDO · " + AutosDestruidos, AlertPriority.Media, 1.6f); }
        }

        readonly HashSet<OperacionAuto> contados = new HashSet<OperacionAuto>();

        void CrearAuto()
        {
            var t = tanque.transform;
            var frente = t.forward; frente.y = 0f; frente.Normalize();
            var lado = new Vector3(frente.z, 0f, -frente.x);
            float signo = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float lat = signo * UnityEngine.Random.Range(4.5f, 7.5f);
            // Bug #073: parte de las camionetas nace POR DELANTE del tanque (tactica Adelantar) en vez de siempre por detras; las que
            // vienen de atras nacen mas cerca (30-42 m) porque ahora solo son un poco mas rapidas que el tanque.
            bool porDelante = UnityEngine.Random.value < ProporcionDeAutosPorDelante;
            float atras = -UnityEngine.Random.Range(11f, 20f);
            var pos = porDelante
                ? t.position + frente * UnityEngine.Random.Range(40f, 55f) + lado * lat
                : t.position - frente * UnityEngine.Random.Range(30f, 42f) + lado * lat;
            pos.y = 0.6f;
            var go = InstanciarCamioneta(pos, Quaternion.LookRotation(frente, Vector3.up));
            go.name = "Auto_Perseguidor_" + (++AutosCreados);
            var a = go.GetComponent<OperacionAuto>();
            a.objetivo = tanque.transform;
            // #378: de la tercera en adelante, una de cada cuatro sale como TANQUE ENEMIGO (rojo, 3x vida, obus y rafaga mas fuerte).
            if (AutosCreados >= 3 && UnityEngine.Random.value < ProporcionDeTanquesEnemigos) a.ConvertirEnTanque();
            a.offsetDeseado = new Vector3(lat, 0f, atras);
            if (porDelante) a.tacticaInicial = OperacionAuto.Tactica.Adelantar;
            // Arranca a la marcha del tanque (antes salia desde cero y tardaba varios segundos en alcanzar velocidad).
            var motorTanque = tanque.GetComponent<VehicleMotor>();
            a.FijarVelocidadInicial(motorTanque != null ? Mathf.Abs(motorTanque.CurrentSpeed) * 0.95f : 0f);
            if (porDelante) AutosPorDelante++;
            autos.Add(a);
        }

        public int AutosPorDelante { get; private set; }
        public const float ProporcionDeTanquesEnemigos = 0.25f;

        void DestruirTanque()
        {
            // Los autos que quedan ya se retiraron (TickLlegada): se van sin explosion; solo explota alguno si quedo a la vista.
            var camara = SP.Core.CamaraPrincipal.Actual;
            foreach (var a in autos)
            {
                if (a == null || a.Muerto) continue;
                bool cerca = camara != null && Vector3.Distance(camara.transform.position, a.transform.position) < 60f;
                if (cerca && !a.Retirado) ImpactFx.SpawnExplosion(a.transform.position + Vector3.up, 3f);
                Destroy(a.gameObject);
            }
            autos.Clear();
            llegadaDesde = -1f;
            tanque.DestruirPorGuion();   // WP9b: el piso del 20 % y el modo dios no lo salvan del guion
            if (!tanque.IsDestroyed)
            {
                // Modo dios u otra proteccion: se fuerza igual.
                foreach (var o in new List<Soldier>(tanque.Occupants)) tanque.Dismount(o);
                tanque.FinalExplosion();
            }
            EnTanque = false;
            if (hud != null) hud.OcultarTimer();
            // El HUD de tripulacion y el cartel de asiento quedaban prendidos tras la explosion.
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.VehicleStatus != null) { drv.VehicleStatus.UpdateFrom(null, null); drv.VehicleStatus.gameObject.SetActive(false); }
            EntrarFase(FaseOperacion.Resistir);
        }
    }
}
