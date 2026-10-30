using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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
    // Fase 3 (CENTRO DE DATOS): hackeo de 30 s con oleadas y camionetas de asalto. Activar()/InstanciarCamioneta() tambien los usa la huida.
    public partial class OperacionDirector
    {
        // ---- 3. centro de datos ----
        // Pedido de pulido: "enemigos mas secuenciales, mas dispersos y mas enemigos" en el hackeo. Al entrar a la fase, las 3 oleadas del
        // blockout (16 soldados, 3 entradas) se rearman en tandas de 3 que salen cada 2,5 s, con 14 refuerzos clonados y repartidos por
        // 5 entradas (sur, patio oeste, patio este y las dos esquinas del sur), asi nunca llegan todos juntos ni por el mismo lado.
        bool oleadasReforzadas;
        public const int SoldadosPorTanda = 3, RefuerzosDelCentro = 7;   // #273: la mitad de enemigos que antes (30 -> 15)
        public const float SegundosEntreTandas = 2.5f;

        void ReforzarOleadasDelCentro()
        {
            if (oleadasReforzadas || oleadasDelCentro == null || oleadasDelCentro.Length == 0) return;
            oleadasReforzadas = true;
            var todos = new List<Soldier>();
            Transform destino = null;
            foreach (var w in oleadasDelCentro)
            {
                if (w == null || w.soldados == null) continue;
                if (destino == null) destino = w.destino;
                foreach (var so in w.soldados) if (so != null) todos.Add(so);
            }
            if (todos.Count == 0) return;
            var plantilla = todos[0];
            // #273: "baja a la mitad los enemigos": se descarta uno de cada dos de los originales (los refuerzos ya son la mitad).
            for (int i = todos.Count - 1; i >= 1; i -= 2)
            {
                var sobra = todos[i];
                todos.RemoveAt(i);
                if (sobra != null) Destroy(sobra.gameObject);
            }

            // Refuerzos: clones inactivos de un soldado de reserva, repartidos por las 5 entradas.
            var rnd = new System.Random(20261018);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int k = 0; k < RefuerzosDelCentro; k++)
            {
                Vector3 p; float yaw;
                switch (k % 5)
                {
                    case 0: p = new Vector3(R(-42f, 42f), 0.8f, R(-114f, -102f)); yaw = 0f; break;     // entrada sur, abierta de lado a lado
                    case 1: p = new Vector3(R(-60f, -52f), 0.8f, R(-88f, -44f)); yaw = 90f; break;     // patio oeste
                    case 2: p = new Vector3(R(52f, 60f), 0.8f, R(-88f, -44f)); yaw = 270f; break;      // patio este
                    case 3: p = new Vector3(R(-56f, -44f), 0.8f, R(-100f, -92f)); yaw = 45f; break;    // esquina suroeste
                    default: p = new Vector3(R(44f, 56f), 0.8f, R(-100f, -92f)); yaw = -45f; break;    // esquina sureste
                }
                if (NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) p = hit.position + Vector3.up * 0.2f;
                var clon = Instantiate(plantilla, p, Quaternion.Euler(0f, yaw, 0f), plantilla.transform.parent);
                clon.name = "Reserva_CD_Refuerzo_" + (k + 1);
                clon.gameObject.SetActive(false);
                todos.Add(clon);
            }

            // Se alternan los soldados de cada entrada para que cada tanda mezcle lados, y se arman tandas de a 3 cada 2,5 s.
            var mezclados = new List<Soldier>(todos.Count);
            var cola = new List<Soldier>(todos);
            cola.Sort((a, b) => (Mathf.Atan2(a.transform.position.z + 70f, a.transform.position.x)).CompareTo(Mathf.Atan2(b.transform.position.z + 70f, b.transform.position.x)));
            for (int i = 0, j = cola.Count - 1; i <= j; i++, j--)
            {
                mezclados.Add(cola[i]);
                if (i != j) mezclados.Add(cola[j]);
            }
            var nuevas = new List<OleadaDeReserva>();
            for (int i = 0; i < mezclados.Count; i += SoldadosPorTanda)
            {
                int n = Mathf.Min(SoldadosPorTanda, mezclados.Count - i);
                var grupo = new Soldier[n];
                for (int k = 0; k < n; k++) grupo[k] = mezclados[i + k];
                nuevas.Add(new OleadaDeReserva { segundo = (i / SoldadosPorTanda) * SegundosEntreTandas, aviso = null, soldados = grupo, destino = destino });
            }
            oleadasDelCentro = nuevas.ToArray();
            GameLog.Line($"[Operacion] Hackeo: {todos.Count} soldados en {nuevas.Count} tandas de {SoldadosPorTanda} cada {SegundosEntreTandas} s");
        }

        // #273: "que aparezcan mas de manera aleatoria segun la vida promedio de todos: a menos vida promedio menos enemigos y a mas vida mas
        // enemigos". Cada tanda sale con un tamano que depende de la vida media de la escuadra (muertos cuentan 0) y de un azar suave;
        // los soldados que sobran no salen nunca. Con la escuadra entera y sana pueden salir tandas de hasta 4.
        public const float FactorMinimoPorVida = 0.34f, FactorMaximoPorVida = 1.5f;
        readonly System.Random azarDeTandas = new System.Random();
        public float VidaPromedioDeLaEscuadra()
        {
            float suma = 0f; int n = 0;
            foreach (var a in ActorRegistry.All)
            {
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || (!a.gameObject.activeInHierarchy && a.Health.IsAlive)) continue;
                suma += a.Health.IsAlive ? (float)a.Health.Current / Mathf.Max(1, a.Health.MaxHealth) : 0f; n++;
            }
            return n > 0 ? suma / n : 1f;
        }

        public int UltimaTandaAjustada { get; private set; }
        void AjustarTandaALaVidaDeLaEscuadra(OleadaDeReserva o)
        {
            if (o == null || o.soldados == null || o.soldados.Length == 0) return;
            float vida = VidaPromedioDeLaEscuadra();
            float factor = Mathf.Lerp(FactorMinimoPorVida, FactorMaximoPorVida, Mathf.Clamp01(vida)) * (0.75f + (float)azarDeTandas.NextDouble() * 0.5f);
            int salen = Mathf.Clamp(Mathf.RoundToInt(o.soldados.Length * factor), 1, o.soldados.Length + 1);
            if (salen > o.soldados.Length) salen = o.soldados.Length;
            for (int i = salen; i < o.soldados.Length; i++) { if (o.soldados[i] != null) Destroy(o.soldados[i].gameObject); o.soldados[i] = null; }
            UltimaTandaAjustada = salen;
            GameLog.Line($"[Operacion] Hackeo: tanda de {salen}/{o.soldados.Length} (vida media de la escuadra {vida:0.00})");
        }

        void TickCentro()
        {
            ReforzarOleadasDelCentro();
            if (defensaDelCentro == null) defensaDelCentro = DefensaDelCentro.Preparar(computadora.transform, halcon);   // #272: torretas aliadas y helicopteros de asalto
            if (computadora.Operador != null && hackInicio < 0f) hackInicio = Time.time;
            if (hackInicio >= 0f && defensaDelCentro != null) defensaDelCentro.Comenzar();
            if (hackInicio >= 0f)
            {
                float t = Time.time - hackInicio;
                for (int i = 0; i < oleadasDelCentro.Length; i++)
                {
                    if (oleadasLanzadas.Contains(i) || t < oleadasDelCentro[i].segundo) continue;
                    oleadasLanzadas.Add(i);
                    AjustarTandaALaVidaDeLaEscuadra(oleadasDelCentro[i]);
                    Activar(oleadasDelCentro[i], computadora.transform.position);
                }
                for (int i = 0; segundosDeCamionetasDelCentro != null && i < segundosDeCamionetasDelCentro.Length; i++)
                {
                    if (tandasDeCamionetas.Contains(i) || t < segundosDeCamionetasDelCentro[i]) continue;
                    tandasDeCamionetas.Add(i);
                    LanzarCamionetasDelCentro();
                }
            }
            if (hud != null)
            {
                string det = computadora.Operador != null
                    ? $"{computadora.Operador.DisplayName} esta hackeando · {Mathf.CeilToInt((1f - computadora.Progreso01) * computadora.duracion)} s · oleadas {oleadasLanzadas.Count}/{oleadasDelCentro.Length}"
                      + (CamionetasDelCentroVivas > 0 ? $" · ¡{CamionetasDelCentroVivas} vehiculos! cohetes o granadas" : "")
                    : "Entra al centro de datos y apreta [E] en la computadora";
                hud.Objetivo(TituloObjetivo(3, "CENTRO DE DATOS"), det, computadora.Progreso01, new Color(0.35f, 0.8f, 1f));
                if (computadora.Operador != null) hud.Timer("DESCARGANDO DATOS", Mathf.CeilToInt((1f - computadora.Progreso01) * computadora.duracion), new Color(0.5f, 0.9f, 1f));
                else hud.OcultarTimer();
            }
            if (computadora.Completo)
            {
                if (hud != null) hud.OcultarTimer();
                if (defensaDelCentro != null) { defensaDelCentro.Terminar(); defensaDelCentro = null; }
                AbrirSalidaNorte(true);   // bug #291: la terminal explota y la salida norte se abre sola
                EntrarFase(FaseOperacion.Huir);
            }
        }

        // Bug #291 ("al hackear esto deberia explotar y tambien abrir la puerta"): al completarse el hackeo la computadora explota (efecto, humo, sonido,
        // pantalla apagada) y 1,2 s despues la salida norte (Muro_Salida_Norte) se vuela sola con el mismo camino de Demoler (escombros, malla de
        // navegacion, coberturas). Antes el ASALTO tenia que demolerla a mano. Idempotente; sin explosion (restaurar partida, atajo de prueba) cae al instante.
        bool salidaAbierta; float salidaAbiertaT = -99f;
        public bool SalidaNorteAbierta => salidaAbierta;
        public float SalidaNorteAbiertaT => salidaAbiertaT;
        public const float SegundosAntesDeLaToma = 2.5f;
        // Para las pruebas: lo que hace el hackeo al completarse (con o sin la explosion de la terminal).
        public void AbrirSalidaNortePorPrueba(bool conExplosion) => AbrirSalidaNorte(conExplosion);

        void AbrirSalidaNorte(bool conExplosion)
        {
            if (salidaAbierta) return;
            salidaAbierta = true;
            if (!MuroDeSalidaEnPie()) return;
            salidaAbiertaT = Time.time;
            var pc = computadora != null ? computadora.transform.position : muroDeSalida.transform.position;
            if (conExplosion) StartCoroutine(VoladuraDeLaTerminal(pc));
            else muroDeSalida.Demoler(pc, 6f);
        }

        IEnumerator VoladuraDeLaTerminal(Vector3 pc)
        {
            var centro = pc + Vector3.up * 1.4f;
            ImpactFx.SpawnExplosion(centro, 3.2f, false);
            AudioDirector.PlayAt(SfxKind.Explosion, pc, 0.8f);
            if (computadora != null)
            {
                if (computadora.luz != null) computadora.luz.material.color = Color.black;
                if (computadora.indicador != null) computadora.indicador.gameObject.SetActive(false);
                // Bug #315: las pantallas del arte nuevo de la terminal tambien se apagan, y la superior se inclina.
                var arteTerminal = computadora.transform.parent != null ? computadora.transform.parent.Find("ArteTerminal") : null;
                if (arteTerminal != null)
                    foreach (var r in arteTerminal.GetComponentsInChildren<Renderer>(true))
                        if (r.name.StartsWith("Pantalla_"))
                        {
                            r.material.color = Color.black;
                            if (r.material.HasProperty("_EmissionColor")) r.material.SetColor("_EmissionColor", Color.black);
                            if (r.name == "Pantalla_Superior") r.transform.localRotation *= Quaternion.Euler(0f, 0f, 12f);
                        }
            }
            for (int i = 0; i < 6; i++)
                SpriteFx.Lanzar("smoke_02", centro + UnityEngine.Random.insideUnitSphere * 0.6f, new Color(0.35f, 0.34f, 0.33f, 0.6f), 0.6f, 2.6f, UnityEngine.Random.Range(1.6f, 2.6f), UnityEngine.Random.Range(-30f, 30f), Vector3.up * UnityEngine.Random.Range(1.2f, 2.4f), 1.5f, 1);
            yield return new WaitForSeconds(1.2f);
            if (muroDeSalida != null && !muroDeSalida.IsCollapsed) muroDeSalida.Demoler(pc, 6f);
        }

        DefensaDelCentro defensaDelCentro;
        public DefensaDelCentro DefensaDelCentroActual => defensaDelCentro;

        // ---- Bug #043: camionetas de asalto contra el centro de datos ----
        readonly HashSet<int> tandasDeCamionetas = new HashSet<int>();
        readonly List<OperacionAuto> camionetasDelCentro = new List<OperacionAuto>();
        public int CamionetasDelCentroCreadas { get; private set; }
        public int CamionetasDelCentroVivas { get { int n = 0; foreach (var a in camionetasDelCentro) if (a != null && !a.Muerto) n++; return n; } }

        // Bug #047: "la mitad de tamaño los enemigos": las camionetas eran mas grandes que el propio tanque (5,6 m de largo
        // contra 3,6). Se instancian a escala 0,5 y se apoyan en el piso (el pivote de la plantilla queda a 0,6 m de altura,
        // que a mitad de escala dejaba las ruedas flotando).
        public const float EscalaDeCamioneta = 0.5f;

        GameObject InstanciarCamioneta(Vector3 pos, Quaternion rot)
        {
            var go = Instantiate(autoPlantilla, pos, rot);
            go.transform.localScale = autoPlantilla.transform.localScale * EscalaDeCamioneta;
            go.SetActive(true);   // antes de medir: un renderer inactivo devuelve una caja vacia
            SP.Presentation.MinimapIcon.AsegurarParaVehiculo(go.GetComponent<SP.Vehicles.Vehicle>());   // bug #302: la camioneta enemiga se ve en el minimapa
            bool hay = false; var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            if (hay) { var p = go.transform.position; p.y -= b.min.y; go.transform.position = p; }
            return go;
        }

        // Bug #070: las paradas de las camionetas caian encima de los sacos de arena (Camioneta_Asalto_3 quedaba DENTRO de Saco_Salida_1). Cada
        // parada (y cada salida) se valida con una caja del tamano de la camioneta contra los solidos; si choca se corre de a 1 m a un
        // costado (hasta 6 m) hasta encontrar lugar. soloLado != 0 corre solo hacia ese lado (la salida, que se abre del tanque).
        public static readonly Vector3 MitadDeCajaDeCamioneta = new Vector3(1.2f, 1.0f, 2.9f);
        static readonly Collider[] bufferParada = new Collider[24];

        public static bool LugarLibreParaCamioneta(Vector3 centro, Quaternion rot, Transform ignorar = null)
        {
            var c = new Vector3(centro.x, 1.3f, centro.z);
            int n = Physics.OverlapBoxNonAlloc(c, MitadDeCajaDeCamioneta, bufferParada, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = bufferParada[i];
                if (col == null || !NavService.BlocksMovement(col)) continue;
                if (ignorar != null && col.transform.IsChildOf(ignorar)) continue;
                // Una carcasa que ya se esta yendo no estorba.
                var otra = col.GetComponentInParent<OperacionAuto>();
                if (otra != null && otra.Muerto) continue;
                // El piso (Ground) es un solido enorme: lo que importa es lo que sobresale, y la caja arranca a 0,3 m.
                return false;
            }
            return true;
        }

        public static Vector3 BuscarLugarParaCamioneta(Vector3 deseado, Quaternion rot, float soloLado = 0f, Transform ignorar = null)
        {
            if (LugarLibreParaCamioneta(deseado, rot, ignorar)) return deseado;
            for (int paso = 1; paso <= 6; paso++)
            {
                for (int k = 0; k < 2; k++)
                {
                    float signo = soloLado != 0f ? soloLado : (k == 0 ? 1f : -1f);
                    if (soloLado != 0f && k == 1) break;
                    var p = deseado + Vector3.right * (signo * paso);
                    if (LugarLibreParaCamioneta(p, rot, ignorar)) return p;
                }
            }
            return deseado;
        }

        public int ParadasCorridas { get; private set; }

        // Bug #184 ("quita los 4 vehiculos previos, no suman"): las camionetas de asalto del centro de datos que siguen en pie al empezar la huida
        // se retiran: la fase de huida la juegan los autos de la oleada y el blindado, no esos restos.
        public int RetirarCamionetasDelCentro()
        {
            int n = 0;
            foreach (var a in camionetasDelCentro)
            {
                if (a == null) continue;
                Destroy(a.gameObject);
                n++;
            }
            camionetasDelCentro.Clear();
            return n;
        }

        void LanzarCamionetasDelCentro()
        {
            if (autoPlantilla == null || computadora == null) return;
            var c = computadora.transform.position;
            AlertQueue.Push("¡VEHICULOS ENEMIGOS POR LA RUTA NORTE! COHETES Y GRANADAS", AlertPriority.Alta, 3.5f);
            if (hud != null) hud.Aviso("¡VEHICULOS ENEMIGOS!\nUSA COHETES O GRANADAS", 3f);
            for (int k = 0; k < camionetasPorTanda; k++)
            {
                float lado = (k % 2 == 0 ? -1f : 1f) * (2.2f + 1.5f * (k / 2));
                var rotSur = Quaternion.LookRotation(Vector3.back, Vector3.up);
                var salida = new Vector3(c.x + lado, 0.6f, c.z + 70f + k * 9f);      // patio del tanque, al norte
                // ~25 m al norte de la computadora; cada tanda frena mas atras y mas abierta (antes la segunda se encimaba a la primera).
                int tanda = tandasDeCamionetas.Count - 1;
                var parada = new Vector3(c.x + lado * (1.6f + 1.4f * tanda), 0.6f, c.z + 26f + k * 3f + tanda * 9f);
                // Bug #070: ni la salida (se encimaba con el tanque) ni la parada (con los sacos) pueden quedar dentro de un solido.
                var salidaLibre = BuscarLugarParaCamioneta(salida, rotSur, Mathf.Sign(lado));
                var paradaLibre = BuscarLugarParaCamioneta(parada, rotSur);
                if ((salidaLibre - salida).sqrMagnitude > 0.01f || (paradaLibre - parada).sqrMagnitude > 0.01f) ParadasCorridas++;
                salida = salidaLibre; parada = paradaLibre;
                var go = InstanciarCamioneta(salida, rotSur);
                go.name = "Camioneta_Asalto_" + (++CamionetasDelCentroCreadas);
                var a = go.GetComponent<OperacionAuto>();
                a.ConfigurarAsalto(parada);
                camionetasDelCentro.Add(a);
            }
            GameLog.Line($"[Operacion] Camionetas de asalto contra el centro de datos: {camionetasPorTanda}");
        }

        void Activar(OleadaDeReserva o, Vector3 destino)
        {
            if (o == null || o.soldados == null) return;
            // Antes salia dos veces a la vez (cartel grande + caja de alertas): un solo cartel.
            if (hud != null && !string.IsNullOrEmpty(o.aviso)) hud.Aviso(o.aviso, 2.5f);
            else if (!string.IsNullOrEmpty(o.aviso)) AlertQueue.Push(o.aviso, AlertPriority.Alta, 3f);
            var meta = o.destino != null ? o.destino.position : destino;
            CazaDeEnemigos.Marcar(o.soldados);   // bugs #20/#21: la oleada sale a CAZAR a la escuadra, no a patrullar un punto fijo
            foreach (var s in o.soldados)
            {
                if (s == null) continue;
                EntradaDeReserva.Marcar(s.transform.position);
                s.gameObject.SetActive(true);
                ApoyoEnElPiso.Apoyar(s.transform);
                if (s.Brain != null)
                {
                    s.Brain.ReactivarNavegacion();
                    var jitter = new Vector3(UnityEngine.Random.Range(-4f, 4f), 0f, UnityEngine.Random.Range(-4f, 4f));
                    s.Brain.SetPatrolRoute(new[] { meta + jitter, meta - jitter });
                }
            }
        }
    }
}
