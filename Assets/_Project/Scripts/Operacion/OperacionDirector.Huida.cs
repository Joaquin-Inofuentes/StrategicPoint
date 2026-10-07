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
            tanque.PisoDeVida01 = PisoDeVidaDelTanque;

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
            var libres = new[] { VehicleSeatRole.Passenger1, VehicleSeatRole.Passenger2 };
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
                }
            }
            PotenciarCanon();
            InstalarMetralletaConIA();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            Aviso("A BORDO · TRAYECTO DE " + Mathf.RoundToInt(segundosDeTrayecto) + " s\nDESTRUI A LOS AUTOS CON EL CANON");
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
            t.FijarLimitesDeElevacion(32f, 15f);   // WP9b-2: el Halcon orbita a 18..24 m de altura (hasta ~28 grados desde la torreta)
        }

        float LargoDeLaRuta()
        {
            float l = 0f;
            var prev = tanque.transform.position;
            foreach (var t in rutaDelTanque) { l += Plano(t.position, prev).magnitude; prev = t.position; }
            return Mathf.Max(50f, l);
        }

        void EmitirOrdenDeRuta()
        {
            var vb = tanque.GetComponent<VehicleBrain>();
            if (vb == null || indiceRuta >= rutaDelTanque.Length) return;
            vb.IssueMoveOrder(rutaDelTanque[indiceRuta].position);
        }

        public const float PisoDeVidaDelTanque = 0.2f;
        public const float ProporcionDeAutosPorDelante = 0.3f;
        public const float AparicionMin = 1f, AparicionMax = 2f;

        // Bug #073: 4 camionetas a la vez (5 en Dificil: la dificultad escala con CantidadDeOleadas, tope +1).
        public int AutosSimultaneosEfectivos
        {
            get
            {
                float k = Mathf.Max(1f, Dificultad.Datos(Dificultad.Actual).CantidadDeOleadas);
                return Mathf.Clamp(Mathf.RoundToInt(autosSimultaneos * k), autosSimultaneos, autosSimultaneos + 1);
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

        void TickTrayecto(float dt)
        {
            var vb = tanque.GetComponent<VehicleBrain>();
            // El tanque no se detiene: cuando esta cerca del punto actual, ya se pide el siguiente.
            if (indiceRuta < rutaDelTanque.Length)
            {
                float d = Plano(rutaDelTanque[indiceRuta].position, tanque.transform.position).magnitude;
                if (d < 7f && indiceRuta < rutaDelTanque.Length - 1) { indiceRuta++; EmitirOrdenDeRuta(); }
                else if (vb != null && !vb.HasOrder && indiceRuta < rutaDelTanque.Length - 1) { indiceRuta++; EmitirOrdenDeRuta(); }
                else if (vb != null && !vb.HasOrder) EmitirOrdenDeRuta();
            }

            // El tanque aguanta hasta el final del trayecto (el guion lo destruye). Bug #073: solo tiene un PISO del 20 % de la vida
            // (Vehicle.PisoDeVida01, WP9b: antes era una cura "a mano" cada frame): si el dano lo pasa, se lo deja justo ahi.

            if (Reloj >= segundosDeTrayecto) { IniciarCinematicaFinal(); return; }
            MantenerAutos();
            TickCarreraExtras();
            int rest = Mathf.CeilToInt(TrayectoRestante);
            if (hud != null)
            {
                hud.Objetivo(TituloObjetivo(4, "HUIR EN EL TANQUE"), $"Canon: camionetas, cornisas con cohetes y el HALCON · derribados: {AutosDestruidos} · el tanque sigue su camino", Reloj / segundosDeTrayecto, new Color(0.4f, 1f, 0.5f));
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
            a.offsetDeseado = new Vector3(lat, 0f, atras);
            if (porDelante) a.tacticaInicial = OperacionAuto.Tactica.Adelantar;
            // Arranca a la marcha del tanque (antes salia desde cero y tardaba varios segundos en alcanzar velocidad).
            var motorTanque = tanque.GetComponent<VehicleMotor>();
            a.FijarVelocidadInicial(motorTanque != null ? Mathf.Abs(motorTanque.CurrentSpeed) * 0.95f : 0f);
            if (porDelante) AutosPorDelante++;
            autos.Add(a);
        }

        public int AutosPorDelante { get; private set; }

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
