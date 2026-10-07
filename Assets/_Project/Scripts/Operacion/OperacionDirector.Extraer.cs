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
    // Fase 6 (EXTRAER): subida al helicoptero, cinematica de salida, fundido y victoria.
    public partial class OperacionDirector
    {
        // ---- 6. extraccion ----
        void TickExtraer()
        {
            if (SubiendoAlHeli) return;
            if (CoberturaActiva) { TickCoberturaDeExtraer(Time.deltaTime); return; }   // #127: estacionario cubriendo hasta que todos esten en la zona
            var yo = Poseido();
            float d = yo != null ? Plano(yo.transform.position, heli.position).magnitude : 999f;
            if (hud != null) hud.Objetivo(TituloObjetivo(6, "SUBIR AL HELICOPTERO"), $"El helicoptero (con metralleta) aterrizo · {Mathf.RoundToInt(d)} m · acercate y apreta [E]", 0.9f, new Color(0.35f, 1f, 0.5f));
            if (d <= 11f && yo != null && yo.Health.IsAlive)
            {
                if (hud != null) hud.Prompt("[E] SUBIR AL HELICOPTERO");
                if (OperacionTerminal.EToque()) SubirAlHeli();
            }
        }

        public void SubirAlHeli()
        {
            if (SubiendoAlHeli) return;
            if (CoberturaActiva) PosarElHeliYa();   // #127: subir con el helicoptero en el aire no existe; se lo posa antes (atajo de pruebas)
            SubiendoAlHeli = true;
            QuitarFlechaHeli();
            MusicDirector.PrecargarVictoria();   // se sintetiza en un hilo: queda listo mucho antes del despegue (5,2 s)
            StartCoroutine(RutinaDeSubida());
        }

        IEnumerator RutinaDeSubida()
        {
            var driver = PlayerInputDriver.Activo;
            if (driver != null)
            {
                OrderService.ManejadoAMano = null;
                driver.enabled = false;
                if (driver.Brain != null && driver.Brain.Current != null && driver.Brain.Current.Brain != null)
                    driver.Brain.Current.Brain.IsPossessedByPlayer = false;
            }
            var cam = CamaraPrincipal.Actual != null ? CamaraPrincipal.Actual : (driver != null && driver.Rig != null ? driver.Rig.Cam : null);
            if (driver != null && driver.Rig != null) driver.Rig.enabled = false;
            if (hud != null) hud.Aviso("SUBIENDO AL HELICOPTERO...", 3f);
            if (ScriptHeli != null) { ScriptHeli.Alerta(true); ScriptHeli.DisparaCobertura = true; }

            var puerta = heli.position + heli.right * 1.6f;
            var pasajeros = PrepararEmbarque();
            float proximaOrden = 0f;
            float tEmbarque = 0f; bool avisoDeEspera = false;   // #128: el despegue espera a TODOS (escuadra + milicianos) hasta EsperaMaximaDeEmbarque

            // Pedido: "mejora la animacion del final cuando me subo, que suene mejor". Cada soldado que sube suena (asiento +
            // golpe de puerta, un poco mas agudo cada uno), al despegar hay un barrido de camara con el rotor a fondo y al
            // terminar una fanfarria de logro + objetivo cumplido.
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.BoardAll), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1f, "BoardAll");
            float t = 0f;
            bool despego = false;
            int subidos = 0;
            var posHeli = heli.position;
            var rumboSalida = heli.forward; rumboSalida.y = 0f; rumboSalida = rumboSalida.sqrMagnitude > 0.01f ? rumboSalida.normalized : Vector3.forward;
            bool tomaFinal = false, ganado = false;
            float tras = 0f;
            float proximaRafagaEnemiga = 0f;
            // Bug #051 / #077: linea de tiempo (14,0 s en total):
            //   0    - 5,2  suben los soldados (cada uno aparece sentado adentro del helicoptero)
            //   5,2  - 7,4  despegue vertical (acelera hacia arriba, sin avanzar) hasta ~9 m
            //   7,4  - 9,4  baja la nariz y acelera hacia adelante trepando; la camara lo sigue desde atras; los enemigos le tiran
            //               trazadoras y las dos ametralladoras de las puertas contestan
            //   9,4  - 13,4 TOMA FINAL: la camara salta al piso (1,2 m), detras y entre tres enemigos que giran y le disparan al
            //               helicoptero que se achica (de ~60 a 250 m). Sin fundido a negro.
            //   13,4 -      aparece la pantalla de resultado (fondo translucido, camara lenta x0,35) sobre esa misma toma
            const float DuracionTomaFinal = 4f, InicioDespegue = 5.2f, InicioAvance = 7.4f, InicioTomaFinal = 9.4f;
            const float DuracionSalida = InicioTomaFinal + DuracionTomaFinal;
            while (true)
            {
                float dt = Time.deltaTime;
                tEmbarque += dt;
                // #128: antes de despegar se espera a los que todavia caminan hacia la puerta (el reloj del embarque se detiene en el instante previo).
                bool retener = !despego && pasajeros.Count > 0 && tEmbarque < EsperaMaximaDeEmbarque;
                if (!retener) t += dt;
                else
                {
                    t = Mathf.Min(t + dt, InicioDespegue - 0.02f);
                    if (t >= InicioDespegue - 0.03f && !avisoDeEspera) { avisoDeEspera = true; if (hud != null) hud.Aviso($"ESPERANDO A {pasajeros.Count} ALIADO(S) QUE LLEGAN AL HELICOPTERO...", 3f); }
                }
                // Bug #064: nada los distrae (curar, cubrirse, volver con el lider): cada segundo se les repite la orden de subir.
                if (t < InicioDespegue && Time.time >= proximaOrden)
                {
                    proximaOrden = Time.time + 1f;
                    foreach (var p in pasajeros) OrdenDeSubir(p, puerta);
                }
                if (t > 2.2f)
                    for (int i = pasajeros.Count - 1; i >= 0; i--)
                    {
                        var p = pasajeros[i];
                        if (p == null) { pasajeros.RemoveAt(i); continue; }
                        var dd = puerta - p.transform.position; dd.y = 0f;
                        // Bug #076 / #128: sube el que llego a la puerta; pasados los 5 s tambien el que esta a < 25 m (se lo sube igual: no hay camino
                        // que lo frene mas); el que viene de mas lejos (un miliciano en su sector) sigue caminando hasta el tope de espera.
                        if (dd.magnitude < 1.8f || (t > 5f && dd.magnitude < DistanciaParaSubirDeUna))
                        {
                            // Bug #076: el soldado real se apaga y aparece una copia visual sentada adentro del helicoptero.
                            if (ScriptHeli != null && ScriptHeli.Tripulacion != null) ScriptHeli.Tripulacion.SentarCopia(p);
                            p.gameObject.SetActive(false); pasajeros.RemoveAt(i);
                            // Bug #095: el asiento sonaba dos veces (3D en la puerta + una copia 2D). Queda solo el 3D, mas agudo
                            // con cada soldado que sube.
                            AudioDirector.PlayAt(SfxKind.SeatChange, puerta, 0.9f, 0.8f, PerfilEspacial.Base, 1f + 0.06f * subidos);
                            subidos++;
                        }
                    }
                if (t > InicioDespegue)
                {
                    if (!despego)
                    {
                        GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.CameraSwoosh), 0.8f * AudioDirector.GainFor(SfxChannel.Sfx), 0.8f, "CameraSwoosh");
                        // Bug #093: himno de victoria y salvataje, con fade de entrada largo (6 s; la musica de combate baja a 0 en 4 s).
                        MusicDirector.TocarVictoria();
                        if (hud != null) hud.Aviso(pasajeros.Count > 0 ? "¡DESPEGAMOS! HAY QUIEN NO LLEGO" : "¡DESPEGANDO! EXTRACCION EXITOSA", 3f);
                        SubidosAlHeli = subidos;
                    }
                    despego = true;
                    if (ScriptHeli != null) ScriptHeli.Volando = true;
                    heli.position = PosicionDeSalida(posHeli, rumboSalida, t - InicioDespegue, InicioAvance - InicioDespegue, out float pitch);
                    heli.rotation = Quaternion.Euler(pitch, Quaternion.LookRotation(rumboSalida).eulerAngles.y, 0f);
                    if (t > InicioAvance - 0.8f && t < DuracionSalida + 8f && Time.time >= proximaRafagaEnemiga) proximaRafagaEnemiga = Time.time + TirarleAlHeli();
                }
                if (cam != null)
                {
                    if (t < InicioTomaFinal)
                    {
                        var mira = heli.position + Vector3.up * 1.5f;
                        Vector3 pos;
                        if (!despego) pos = posHeli + new Vector3(-9f, 4.5f, -12f);
                        else pos = heli.position - rumboSalida * 15f + Vector3.Cross(Vector3.up, rumboSalida) * 7f + Vector3.up * 3.5f;
                        cam.transform.position = Vector3.Lerp(cam.transform.position, pos, Mathf.Clamp01(Time.deltaTime * (despego ? 3f : 4f)));
                        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation((mira - cam.transform.position).normalized), Mathf.Clamp01(Time.deltaTime * 5f));
                    }
                    else
                    {
                        // Bug #077: toma final desde el piso. Corte seco a la nueva camara y de ahi en mas solo gira hacia el helicoptero.
                        if (!tomaFinal) { tomaFinal = true; PrepararTomaFinal(cam, posHeli, rumboSalida); }
                        TickTomaFinal(cam, dt);
                    }
                }
                // El rotor se apaga de a poco mientras se aleja (antes seguia sonando a todo volumen en la pantalla de victoria).
                if (ScriptHeli != null) ScriptHeli.VolumenExtra = 1f - Mathf.Clamp01((t - (DuracionSalida - 2.6f)) / 2.4f);

                if (!ganado && t >= DuracionSalida)
                {
                    ganado = true;
                    if (ScriptHeli != null) ScriptHeli.ApagarSonido();
                    TerminarEmbarque();
                    Ganar();
                    GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.ObjetivoCumplido), 0.8f * AudioDirector.GainFor(SfxChannel.Sfx), 1f, "ObjetivoCumplido");
                    GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Logro), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1.12f, "Logro");
                }
                // La toma sigue (en camara lenta, bajo la pantalla de resultado) hasta que se va el helicoptero o pasan 45 s reales.
                if (ganado)
                {
                    tras += Time.unscaledDeltaTime;
                    if (tras > 45f) break;
                }
                yield return null;
            }
            TerminarTomaFinal();
        }

        // ---- Bug #064: subida al helicoptero ----
        // "Se cayeron unos aliados. Quiero que cuando suban los aliados ignoren todo lo que esten haciendo y le den prioridad a
        // subir". Iban caminando a la puerta en modo pasivo (no contestaban el fuego) con 20+ enemigos encima, y el medico
        // ademas cortaba la subida para ir a curar: dos cayeron en el camino. Desde que se aprieta [E]: la escuadra es
        // invulnerable, se cancelan las curaciones/reanimaciones automaticas, los caidos se levantan (los suben heridos) y
        // todos van derecho a la puerta.
        public const float EsperaMaximaDeEmbarque = 60f, DistanciaParaSubirDeUna = 25f;
        public int HeridosRescatados { get; private set; }
        bool atencionPrevia = true, calmaPrevia = true, embarcando;

        List<Soldier> PrepararEmbarque()
        {
            embarcando = true;
            atencionPrevia = PedidoDeCuracion.AtencionAutomatica;
            calmaPrevia = PedidoDeCuracion.ReanimarEnCalma;
            PedidoDeCuracion.Cancelar();
            PedidoDeCuracion.LimpiarCola();
            PedidoDeCuracion.AtencionAutomatica = false;
            PedidoDeCuracion.ReanimarEnCalma = false;
            HeridosRescatados = 0;
            var pasajeros = new List<Soldier>();
            foreach (var s in AliadosAEvacuar(false))   // #128: escuadra + milicianos
            {
                if (!s.Health.IsAlive)
                {
                    if (!SP.Player.Reanimacion.Ejecutar(s, 0.35f)) continue;
                    HeridosRescatados++;
                }
                s.Health.Invulnerable = true;
                pasajeros.Add(s);
            }
            SP.Mision.EstadisticasDeMision.HeridosRescatados = HeridosRescatados;
            if (HeridosRescatados > 0) GameLog.Line($"[Operacion] Subida al helicoptero: {HeridosRescatados} caido(s) levantado(s) para subir");
            return pasajeros;
        }

        static void OrdenDeSubir(Soldier s, Vector3 puerta)
        {
            if (s == null || !s.gameObject.activeInHierarchy || s.Brain == null) return;
            s.Brain.IsPossessedByPlayer = false;
            s.Brain.Quieto = false;
            s.Brain.Pasivo = true;
            s.Brain.Atrincherado = false;
            if (s.Motor != null) s.Motor.SetCrouching(false);
            var lado = new Vector3(((s.Id * 37) % 7 - 3) * 0.3f, 0f, ((s.Id * 53) % 5 - 2) * 0.3f);
            s.Brain.IssueMoveOrder(puerta + lado);
        }

        void TerminarEmbarque()
        {
            if (!embarcando) return;
            embarcando = false;
            PedidoDeCuracion.AtencionAutomatica = atencionPrevia;
            PedidoDeCuracion.ReanimarEnCalma = calmaPrevia;
            foreach (var s in AliadosAEvacuar(false)) if (s.Health != null) s.Health.Invulnerable = false;
            SP.Mision.EstadisticasDeMision.SupervivientesForzados = SubidosAlHeli;
        }

        // Despegue: primero vertical (acelera hacia arriba sin avanzar), despues baja la nariz y acelera hacia adelante
        // trepando, como un helicoptero real (antes subia y avanzaba con una curva cubica a la vez desde el piso).
        static Vector3 PosicionDeSalida(Vector3 origen, Vector3 rumbo, float k, float vertical, out float pitch)
        {
            const float altura = 9f;
            if (k < vertical)
            {
                float x = k / vertical;
                pitch = Mathf.Lerp(0f, 3f, x);
                return origen + Vector3.up * (altura * x * x * (3f - 2f * x));
            }
            float a = k - vertical;
            const float aceleracion = 8.5f, trepada = 4.2f;   // m/s² hacia adelante (bug #077: 6 -> 8,5 para que la toma final lo vea irse de ~55 a 250 m), m/s de ascenso
            pitch = Mathf.Lerp(3f, 13f, Mathf.Clamp01(a / 1.6f));
            return origen + Vector3.up * (altura + trepada * a + 0.4f * a * a) + rumbo * (0.5f * aceleracion * a * a);
        }

        // Los enemigos que quedan le tiran al helicoptero mientras se va (trazadoras: no le hacen nada, es la cinematica).
        float TirarleAlHeli()
        {
            if (heli == null) return 1f;
            var pool = ProjectilePool.Activo;
            if (pool == null) return 1f;
            int disparos = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (disparos >= 6) break;
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if ((s.transform.position - heli.position).sqrMagnitude > 140f * 140f) continue;
                if (tiradoresFinales.Contains(s)) continue;   // los de la toma final disparan con su propia rutina (con fogonazo)
                var boca = s.transform.position + Vector3.up * 1.4f;
                var dir = (heli.position + Vector3.up * 1f - boca).normalized;
                dir = (dir + UnityEngine.Random.insideUnitSphere * 0.06f).normalized;
                pool.Spawn(boca, dir, s.Id, TeamId.Enemy, 0, new Color(1f, 0.4f, 0.2f));
                AudioDirector.PlayAt(SfxKind.Shoot, boca, 0.3f);
                disparos++;
            }
            return disparos > 0 ? UnityEngine.Random.Range(0.12f, 0.25f) : 0.5f;
        }

        Image fundido;
        public static int SubidosAlHeli { get; private set; }
        public bool FundidoActivo => fundido != null && fundido.color.a > 0.02f;

        void QuitarFundido()
        {
            if (fundido == null) return;
            var c = fundido.canvas != null ? fundido.canvas.gameObject : fundido.gameObject;
            fundido = null;
            Destroy(c);
        }

        void OnDisable() { QuitarFundido(); QuitarFlechaHeli(); ApagarCoberturaDeExtraer(); }

        Image CrearFundido()
        {
            var go = new GameObject("OperacionFundido", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 900;
            var im = new GameObject("Negro", typeof(RectTransform), typeof(Image));
            im.transform.SetParent(go.transform, false);
            var r = im.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var img = im.GetComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = false;
            return img;
        }

        void Ganar()
        {
            EntrarFase(FaseOperacion.Victoria);
            // Bug #051: nada del helicoptero sigue sonando debajo de la pantalla de victoria (que corre en timeScale 0).
            if (ScriptHeli != null) { ScriptHeli.ApagarSonido(); ScriptHeli.DisparaCobertura = false; }
            ApagarCoberturaDeExtraer();
            GameLog.Line("[Operacion] VICTORIA");
            PartidaGuardada.Borrar();   // P10 (#120): misión cumplida, ya no hay nada que continuar
            var outcome = GameOutcomeController.Activo;
            if (outcome != null) outcome.ShowVictory(false, AlfaDeFondoDeVictoria);   // bug #077: se ve la toma final detras, en camara lenta
        }
    }
}
