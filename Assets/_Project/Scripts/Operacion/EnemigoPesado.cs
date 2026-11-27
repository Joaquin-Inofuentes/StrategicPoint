using System.Collections;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // Pedido 2026-11-07: "nuevo enemigo pesado y gordo con una minigun, 4 veces mas vida; ve al enemigo, se estaciona 2 segundos y empieza a disparar de
    // manera frenetica; recarga pesada; su minigun suena distinto, mas fuerte y retumbante". Mismo modelo y trimsheet del asalto, ensanchado (geometria
    // editada por escala: ancho x1,35, alto igual para no tocar el pivote); la minigun (WeaponKind.Minigun) trae la cadencia, el cargador de 160 y la
    // recarga de 4,2 s. El comportamiento es una maquina de tres estados encima del AiBrain.
    public sealed class EnemigoPesado : MonoBehaviour
    {
        // Bugs #385/#389 (2026-11-08): "que tenga 3 veces la vida que tiene ahora" -> de x4 a x12.
        public const int MultiplicadorDeVida = 12;
        public const float SegundosDeEstacionamiento = 2f, SegundosSinVerParaRelajarse = 3f;
        // 2026-11-13: "mas pesado, otro color muy distinto y un poquito mas grande" y despues "el pesado doble de tamano": el modelo mide el DOBLE de un enemigo
        // comun en las tres dimensiones (Ensanche), camina mas lento y cada paso retumba. AnchoDeCuerpo es lo que se ensancha la raiz (y el collider).
        public static readonly Vector3 Ensanche = new Vector3(2f, 2f, 2f);
        public const float FactorDeVelocidad = 0.72f, MetrosPorPaso = 2.2f, AnchoDeCuerpo = 1.5f;
        // R17 (pedido 8): al romperse la mascara el pesado entra en FURIA: estaciona menos, se relaja mas tarde y camina mas rapido (la cadencia y los cohetes no cambian).
        public const float SegundosDeEstacionamientoFuria = 1.2f, SegundosSinVerParaRelajarseFuria = 6f, FactorDeVelocidadFuria = 0.85f;
        public enum Fase { Patrulla, Estacionando, Frenesi }

        public static int Creados { get; private set; }
        public Fase Estado { get; private set; } = Fase.Patrulla;
        // Time.time al entrar a cada fase (para medirlo desde afuera)
        public float InicioDeEstacionar { get; private set; } = -1f;
        public float InicioDeFrenesi { get; private set; } = -1f;
        public int VidaMaxima { get; private set; }
        // Ronda 13 (T1): el cuchillo lo mata en 2 cuchilladas dentro de VentanaEntreCuchilladas (la 1.a lo deja a media vida).
        public const float VentanaEntreCuchilladas = 4f;
        public int GolpesDeCuchillo;
        public float UltimoGolpeDeCuchillo = -999f;
        public bool AnimacionPropia { get; private set; }
        Soldier soldado; AiBrain cerebro;
        // R17: los tiempos de la maquina de estados se acumulan con dt (Tick), no con Time.time: la suite headless los puede simular.
        float enEstado, sinVer;
        public bool EnFuria { get; private set; }
        public bool MascaraPuesta { get; private set; }
        public int PiezasDeMascaraSueltas { get; private set; }
        public float EstacionamientoActual => EnFuria ? SegundosDeEstacionamientoFuria : SegundosDeEstacionamiento;
        public float SinVerParaRelajarse => EnFuria ? SegundosSinVerParaRelajarseFuria : SegundosSinVerParaRelajarse;
        readonly System.Collections.Generic.List<Transform> piezasDeMascara = new System.Collections.Generic.List<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            Creados = 0;
            // R19 (R2): estaticos nuevos
            relojGlobal = 0f; cuadroDelReloj = -1; ultimoAnuncio = -999f; ultimoPasoConSacudida = -999f;
            Anuncios = 0; SacudidasDePaso = 0; SacudidasDeMuerte = 0; PolvosDeMuerte = 0; GolpesDeMuerte = 0; HumosDeRecarga = 0; CartelesDeRecarga = 0;
        }

        // ================================================================ R19 (punto 2, "iterar en enemigo grande")
        // a. legibilidad: marcador de alerta y barra de vida SOBRE la cabeza (antes a 1,4 m: dentro del pecho del modelo x2).
        public const float MargenSobreLaCabeza = 0.45f, SeparacionDeLaBarra = 0.35f, TamanoDelMarcador = 0.30f;
        // b. telegrafo completo: en los ultimos SegundosDeParpadeo del estacionamiento el laser parpadea a HzDeParpadeo (alfa 1 / AlfaBajaDelParpadeo).
        public const float SegundosDeParpadeo = 0.5f, HzDeParpadeo = 8f, AlfaBajaDelParpadeo = 0.25f;
        // c. ventana de castigo: mientras recarga recibe FactorDeDanoRecargando (se aplica antes que Dificultad/AjustesDeDano, Health.cs:182).
        public const float FactorDeDanoRecargando = 1.5f, SegundosEntreHumos = 0.5f;
        // d. presencia: un anuncio cada SegundosEntreAnuncios entre todos los pesados; pasos que sacuden la camara si el poseido esta cerca.
        public const float SegundosEntreAnuncios = 45f, SegundosAnuncio = 3.5f, MetrosDeSacudidaDePaso = 14f, SacudidaDePaso = 0.04f, SegundosEntrePasosConSacudida = 0.45f;
        public const string TextoDelAnuncio = "¡ENEMIGO PESADO! · 2 TIROS A LA CABEZA O ATACALO CUANDO RECARGA";
        // e. muerte: sacudida si el jugador esta cerca; a los SegundosHastaElPiso el golpe contra el piso y polvo grande.
        public const float MetrosDeSacudidaDeMuerte = 25f, SacudidaDeMuerte = 0.18f, SegundosHastaElPiso = 0.7f, EscalaDelPolvoDeMuerte = 2.5f;

        static float relojGlobal; static int cuadroDelReloj = -1;
        static float ultimoAnuncio = -999f, ultimoPasoConSacudida = -999f;
        public static int Anuncios { get; private set; }
        public static int SacudidasDePaso { get; private set; }
        public static int SacudidasDeMuerte { get; private set; }
        public static int PolvosDeMuerte { get; private set; }
        public static int GolpesDeMuerte { get; private set; }
        public static int HumosDeRecarga { get; private set; }
        public static int CartelesDeRecarga { get; private set; }

        // Reloj compartido por todos los pesados (dt, R3). En Play avanza una sola vez por cuadro aunque haya varios pesados; fuera de Play (suite
        // headless) avanza con cada Tick.
        static void AvanzarRelojGlobal(float dt)
        {
            if (Application.isPlaying) { if (Time.frameCount == cuadroDelReloj) return; cuadroDelReloj = Time.frameCount; }
            relojGlobal += dt;
        }
        public static float RelojGlobal => relojGlobal;
        // Solo para los checks (994c): que un anuncio anterior de la partida no tape el que se mide.
        public static void ReiniciarEnfriamientos() { ultimoAnuncio = -999f; ultimoPasoConSacudida = -999f; }

        // Convierte a un soldado enemigo recien creado en pesado. Hay que llamarlo apenas se instancia (antes de su primer Start).
        public static EnemigoPesado Aplicar(Soldier s)
        {
            if (s == null || s.Team != TeamId.Enemy) return null;
            var ya = s.GetComponent<EnemigoPesado>(); if (ya != null) return ya;   // idempotente: un clon de un pesado ya lo es
            s.gameObject.name = "Pesado_" + (++Creados);
            // La raiz solo se ensancha un poco (AnchoDeCuerpo, para que entre por las puertas): el motor fija el pivote a 0,8 m del piso sin mirar la escala, asi
            // que escalar la raiz en Y hundiria el collider. El tamano doble se le da al modelo visual, estirandolo desde los pies para que sigan en el piso, y
            // al collider (alto y centro) para que los disparos le peguen en todo el cuerpo.
            s.transform.localScale = Vector3.Scale(s.transform.localScale, new Vector3(AnchoDeCuerpo, 1f, AnchoDeCuerpo));
            foreach (var r in s.GetComponents<SP.Presentation.CubeFxReactor>()) r.EscalaBase = s.transform.localScale;   // que el reactor de impactos no la devuelva a 1
            var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                var visual = smr.transform.parent != null && smr.transform.parent != s.transform ? smr.transform.parent : smr.transform;
                float piesAntes = smr.bounds.min.y, vy = visual.position.y;
                visual.localScale = new Vector3(visual.localScale.x * Ensanche.x / AnchoDeCuerpo, visual.localScale.y * Ensanche.y, visual.localScale.z * Ensanche.z / AnchoDeCuerpo);
                float piesDespues = vy + (piesAntes - vy) * Ensanche.y;
                visual.position -= Vector3.up * (piesDespues - piesAntes);   // los pies vuelven adonde estaban
            }
            var caja = s.GetComponent<BoxCollider>();
            if (caja != null)
            {
                float pies = caja.center.y - caja.size.y * 0.5f;   // la base de la caja no se mueve: crece hacia arriba
                var tam = caja.size; tam.y *= Ensanche.y; caja.size = tam;
                var cen = caja.center; cen.y = pies + tam.y * 0.5f; caja.center = cen;
            }
            return s.gameObject.AddComponent<EnemigoPesado>();
        }

        IEnumerator Start()
        {
            soldado = GetComponent<Soldier>(); cerebro = soldado != null ? soldado.Brain : null;
            yield return null; yield return null;   // despues de los Start de Soldier/AiBrain/SoldierLook, que pisan vida y arsenal
            if (soldado == null || soldado.Health == null) yield break;
            VidaMaxima = Mathf.Max(1, soldado.Health.MaxHealth) * MultiplicadorDeVida;
            soldado.Health.Initialize(soldado.Id, VidaMaxima);
            if (soldado.Motor != null) soldado.Motor.FactorDeClase = FactorDeVelocidad;
            soldado.Health.CapasDeCabeza = 1;   // R17 (pedido 5): el 1.er headshot rompe la mascara, el 2.o mata
            soldado.Health.CabezaExpuesta -= RomperMascara; soldado.Health.CabezaExpuesta += RomperMascara;
            if (cerebro != null) cerebro.PuestoFijoPorRol = true;   // R17: ReaccionAlDano no lo saca de su maquina (solo le cambia el blanco)
            ArmarMascara();
            SubirMarcadores();
            ultimaPos = transform.position;
            var arma = soldado.Weapon;
            if (arma != null && !arma.Loadout.Contains(WeaponKind.Minigun)) { arma.Loadout.Insert(0, WeaponKind.Minigun); arma.EquipFromLoadout(0); }
            // Bug #389: en movimiento dispara rafaga continua (minigun); detenido, obuses explosivos como los tanques (el lanzacohetes: explosion de 5 m).
            if (arma != null && !arma.Loadout.Contains(WeaponKind.Rocket)) arma.Loadout.Add(WeaponKind.Rocket);
            // animacion propia (Mixamo "Gunplay": disparo sostenido): AC_Pesado es un override de AC_Soldado con el clip de disparo cambiado
            anim = GetComponentInChildren<Animator>(true);
            var ac = SP.Core.RecursosCache.Cargar<RuntimeAnimatorController>("Soldados/AC_Pesado");
            if (anim != null && ac != null) { anim.runtimeAnimatorController = ac; AnimacionPropia = true; for (int i = 0; i < anim.layerCount; i++) if (anim.GetLayerName(i) == "Recarga") capaRecarga = i; }
            GameLog.Line($"[Pesado] {name}: vida {VidaMaxima}, minigun, ensanche {Ensanche.x:0.00}");
            // R19: la caja del modelo cambia con la pose (los primeros cuadros dan la de reposo, mas baja): se recoloca otra vez ya asentado
            yield return new WaitForSeconds(1f);
            SubirMarcadores();
        }

        Animator anim; int capaRecarga = -1; float pesoRecarga; bool recargandoAntes;
        public bool Recargando { get; private set; }
        void ManejarRecarga()
        {
            if (anim == null || capaRecarga < 0 || soldado == null || soldado.Weapon == null) return;
            bool recarga = soldado.Weapon.IsReloading && soldado.Health != null && soldado.Health.IsAlive;
            if (recarga && !recargandoAntes) anim.Play("Recarga", capaRecarga, 0f);   // la recarga siempre arranca desde el primer cuadro
            recargandoAntes = recarga;
            pesoRecarga = Mathf.MoveTowards(pesoRecarga, recarga ? 1f : 0f, Time.deltaTime * 4f);
            anim.SetLayerWeight(capaRecarga, pesoRecarga);
            Recargando = pesoRecarga > 0.5f;
        }

        // Arma segun el movimiento: en marcha la minigun, detenido (estacionando o en frenesi) el lanzacohetes.
        public WeaponKind ArmaEsperada => Estado == Fase.Frenesi ? WeaponKind.Rocket : WeaponKind.Minigun;
        void ElegirArma()
        {
            var arma = soldado != null ? soldado.Weapon : null;
            if (arma == null || VidaMaxima <= 0) return;   // VidaMaxima > 0 = Start ya corrio (arsenal armado)
            var quiero = ArmaEsperada;
            if (arma.CurrentWeaponKind == quiero || arma.IsReloading) return;
            int i = arma.Loadout.IndexOf(quiero);
            if (i >= 0) arma.EquipFromLoadout(i);
        }

        Vector3 ultimaPos; float recorrido;
        public int PasosPesados { get; private set; }
        // Cada MetrosPorPaso recorridos retumba el suelo: un golpe grave bajo y corto (el impacto pesado con el tono bajado).
        void PasosQueRetumban()
        {
            var p = transform.position; var d = p - ultimaPos; d.y = 0f; ultimaPos = p;
            if (soldado == null || soldado.Health == null || !soldado.Health.IsAlive || d.magnitude > 2f) return;
            recorrido += d.magnitude;
            if (recorrido < MetrosPorPaso) return;
            recorrido = 0f; PasosPesados++;
            SP.Presentation.AudioDirector.PlayAt(SP.Presentation.SfxKind.PasoPesado, p, 0.5f, 0.6f);
            SacudirPorPaso(p);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            PasosQueRetumban();
            ManejarRecarga();
            ElegirArma();
            VentanaDeCastigo(dt);
            Muerte(dt);
            Tick(dt);
        }

        // ---------------------------------------------------------------- R19 a: marcador y barra de vida sobre la cabeza
        public float AlturaDeLosMarcadores { get; private set; }
        void SubirMarcadores()
        {
            if (soldado == null) return;
            float pivote = transform.position.y, tope = pivote + 2f;
            var h = HitboxCabeza.De(soldado);
            if (h != null) tope = Mathf.Max(tope, h.Centro.y + MargenSobreLaCabeza);
            var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null) tope = Mathf.Max(tope, smr.bounds.max.y + 0.30f);   // margen para la variacion de la caja con la animacion (medido: ~0,1 m)   // por si la caja del modelo pasa a la cabeza (brazos, arma)
            AlturaDeLosMarcadores = tope - pivote;
            var marca = GetComponent<EnemyAlertIndicatorView>();
            if (marca != null) marca.Recolocar(AlturaDeLosMarcadores, TamanoDelMarcador);
            var barra = GetComponentInChildren<HealthBarView>(true);
            if (barra != null)
            {
                var bp = barra.transform.position; bp.y = tope + SeparacionDeLaBarra;
                barra.transform.position = bp;
            }
        }

        // ---------------------------------------------------------------- R19 c: ventana de castigo durante la recarga
        public bool EnVentanaDeCastigo { get; private set; }
        public int VentanasDeCastigo { get; private set; }
        float relojDeHumo;
        void VentanaDeCastigo(float dt)
        {
            if (soldado == null || soldado.Health == null) return;
            bool recarga = soldado.Health.IsAlive && soldado.Weapon != null && soldado.Weapon.IsReloading;
            if (recarga && !EnVentanaDeCastigo)
            {
                EnVentanaDeCastigo = true; VentanasDeCastigo++; relojDeHumo = 0f;
                soldado.Health.FactorDeDano = FactorDeDanoRecargando;
                var cabeza = transform.position + Vector3.up * Mathf.Max(2f, AlturaDeLosMarcadores);
                // Feedback ya no dibuja texto en el mundo (pulido de feedback): queda en el log y el contador; lo visible es el latido ambar del marcador y el humo.
                Feedback.Visual("RECARGA", cabeza, Feedback.Info, aviso: false, pulso: false);
                CartelesDeRecarga++;
                var marca = GetComponent<EnemyAlertIndicatorView>();
                if (marca != null) marca.Pulsar(EnemyAlertIndicatorView.ColorDeRecarga);
            }
            else if (!recarga && EnVentanaDeCastigo)
            {
                EnVentanaDeCastigo = false;
                if (soldado.Health.FactorDeDano == FactorDeDanoRecargando) soldado.Health.FactorDeDano = 1f;   // solo deshace lo que puso
            }
            if (!EnVentanaDeCastigo) return;
            relojDeHumo -= dt;
            if (relojDeHumo > 0f) return;
            relojDeHumo = SegundosEntreHumos;
            var arma = soldado.Weapon;
            var boca = arma != null && arma.Muzzle != null ? arma.Muzzle.position : transform.position + Vector3.up * 2.2f + transform.forward * 0.8f;
            if (Application.isPlaying)
                SpriteFx.Lanzar("smoke_02", boca, new Color(0.55f, 0.55f, 0.55f, 0.25f), 0.3f, 1.1f, 0.9f, Random.Range(-30f, 30f), Vector3.up * 0.5f, 1.2f, 1);
            HumosDeRecarga++;
        }

        // ---------------------------------------------------------------- R19 d: anuncio y pasos que sacuden
        static Vector3? PosicionDelPoseido()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            var yo = d != null && d.Brain != null ? d.Brain.Current : null;
            return yo != null ? yo.transform.position : (Vector3?)null;
        }

        void Anunciar()
        {
            if (relojGlobal - ultimoAnuncio < SegundosEntreAnuncios) return;
            ultimoAnuncio = relojGlobal; Anuncios++;
            SP.UI.AlertQueue.Push(TextoDelAnuncio, SP.UI.AlertPriority.Alta, SegundosAnuncio);
        }

        void SacudirPorPaso(Vector3 p)
        {
            var yo = PosicionDelPoseido(); if (!yo.HasValue) return;
            float d = Vector3.Distance(yo.Value, p);
            if (d >= MetrosDeSacudidaDePaso || relojGlobal - ultimoPasoConSacudida < SegundosEntrePasosConSacudida) return;
            ultimoPasoConSacudida = relojGlobal; SacudidasDePaso++;
            var rig = SP.CameraSystem.CameraRig.Instance;
            if (rig != null) rig.KickDirectional(Vector3.down, SacudidaDePaso * (1f - d / MetrosDeSacudidaDePaso));
        }

        // ---------------------------------------------------------------- R19 e: la muerte pesa
        bool muertoVisto; float desdeLaMuerte = -1f;
        public bool GolpeDeMuerteDado { get; private set; }
        void Muerte(float dt)
        {
            if (soldado == null || soldado.Health == null || VidaMaxima <= 0) return;
            if (soldado.Health.IsAlive)
            {
                // revivido (o reinicializado): se rearma para una proxima muerte
                if (muertoVisto) { muertoVisto = false; desdeLaMuerte = -1f; GolpeDeMuerteDado = false; }
                return;
            }
            if (!muertoVisto)
            {
                muertoVisto = true; desdeLaMuerte = 0f;
                if (EnVentanaDeCastigo) { EnVentanaDeCastigo = false; if (soldado.Health.FactorDeDano == FactorDeDanoRecargando) soldado.Health.FactorDeDano = 1f; }
                var yo = PosicionDelPoseido();
                if (yo.HasValue && Vector3.Distance(yo.Value, transform.position) < MetrosDeSacudidaDeMuerte)
                {
                    SacudidasDeMuerte++;
                    var rig = SP.CameraSystem.CameraRig.Instance;
                    if (rig != null) rig.KickDirectional(Vector3.down, SacudidaDeMuerte);
                }
                return;
            }
            if (GolpeDeMuerteDado) return;
            desdeLaMuerte += dt;
            if (desdeLaMuerte < SegundosHastaElPiso) return;
            GolpeDeMuerteDado = true; GolpesDeMuerte++;
            var piso = transform.position + Vector3.up * 0.05f;
            AudioDirector.PlayAt(SfxKind.PasoPesado, piso, 0.9f, 1f, PerfilEspacial.Para(SfxKind.PasoPesado), 0.4f);
            if (Application.isPlaying)
            {
                float e = EscalaDelPolvoDeMuerte;
                for (int i = 0; i < 3; i++)
                {
                    var lado = new Vector3(Mathf.Sin(i * 2.1f), 0f, Mathf.Cos(i * 2.1f)) * 0.6f;
                    SpriteFx.Lanzar("smoke_02", piso + lado, new Color(0.62f, 0.58f, 0.52f, 0.35f), 0.35f * e, 1.5f * e * 0.6f, 1.0f, Random.Range(-30f, 30f), Vector3.up * 0.3f + lado * 0.4f, 1.0f, 1);
                }
            }
            PolvosDeMuerte++;
        }

        // Maquina de estados con dt (la suite headless la simula). InicioDeEstacionar / InicioDeFrenesi quedan con Time.time solo para medirlos desde afuera.
        public void Tick(float dt)
        {
            AvanzarRelojGlobal(dt);   // R19: antes del corte por muerte, asi el reloj de anuncios/sacudidas avanza tambien en la suite headless
            if (cerebro == null || soldado == null || soldado.Health == null || !soldado.Health.IsAlive) { if (laser != null) laser.enabled = false; return; }
            var objetivo = cerebro.CurrentTarget;
            // R17: "ve" ahora exige LINEA DE TIRO (antes bastaba con tener blanco: se plantaba ~8 s tirando contra una pared).
            bool ve = objetivo != null && objetivo.Health != null && objetivo.Health.IsAlive && cerebro.TieneLineaDeTiro(objetivo);
            enEstado += dt;
            if (ve) sinVer = 0f; else sinVer += dt;
            switch (Estado)
            {
                case Fase.Patrulla:
                    if (ve) { Estado = Fase.Estacionando; enEstado = 0f; sinVer = 0f; InicioDeEstacionar = Time.time; InicioDeFrenesi = -1f; cerebro.Quieto = true; Anunciar(); }   // lo ve: frena en seco
                    break;
                case Fase.Estacionando:
                    if (enEstado >= EstacionamientoActual) { Estado = Fase.Frenesi; enEstado = 0f; InicioDeFrenesi = Time.time; }   // despues abre fuego sostenido
                    else if (sinVer > SinVerParaRelajarse) { Estado = Fase.Patrulla; enEstado = 0f; cerebro.Quieto = false; }
                    break;
                case Fase.Frenesi:
                    cerebro.Quieto = true;
                    if (sinVer > SinVerParaRelajarse) { Estado = Fase.Patrulla; enEstado = 0f; cerebro.Quieto = false; }
                    break;
            }
            Telegrafar(Estado == Fase.Estacionando && ve, objetivo);
        }


        // ---------------------------------------------------------------- R17 (pedido 8): telegrafo. Mientras estaciona apuntandole a alguien (antes del fuego sostenido)
        // sale un laser rojo que se engrosa hasta que abre fuego, mas un tic de aviso al empezar. Asi el jugador lo lee y puede romperle la mascara o ponerse a cubierto.
        static Material matLaser;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarLaser() { matLaser = null; }
        LineRenderer laser; bool avisoSonoro;
        public bool TelegrafoActivo => laser != null && laser.enabled;
        public float AnchoDelTelegrafo => laser != null ? laser.widthMultiplier : 0f;
        // R19 b: parpadeo de los ultimos SegundosDeParpadeo (alfa por instancia con el color de vertices del LineRenderer: el material sigue compartido, R4)
        public float AlfaDelTelegrafo { get; private set; } = 1f;
        public int CambiosDeParpadeo { get; private set; }
        bool parpadeoBajo;
        // Pura (suite): alfa del laser cuando faltan 'falta' segundos para abrir fuego.
        public static float AlfaDeParpadeo(float falta)
        {
            if (falta > SegundosDeParpadeo) return 1f;
            int fase = Mathf.FloorToInt(Mathf.Max(0f, SegundosDeParpadeo - falta) * HzDeParpadeo * 2f);
            return (fase & 1) == 1 ? AlfaBajaDelParpadeo : 1f;
        }

        void Telegrafar(bool encender, Soldier objetivo)
        {
            if (!encender || objetivo == null)
            {
                if (laser != null) laser.enabled = false;
                avisoSonoro = false;
                parpadeoBajo = false; AlfaDelTelegrafo = 1f;
                return;
            }
            if (laser == null)
            {
                if (matLaser == null) matLaser = SP.Presentation.ParticleMaterialFactory.CreateTransparent(new Color(1f, 0.1f, 0.08f, 0.9f));
                var go = new GameObject("Telegrafo_Laser");
                go.transform.SetParent(transform, false);
                laser = go.AddComponent<LineRenderer>();
                laser.useWorldSpace = true; laser.positionCount = 2; laser.sharedMaterial = matLaser;
                laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; laser.receiveShadows = false;
                laser.numCapVertices = 2;
            }
            laser.enabled = true;
            float k = Mathf.Clamp01(enEstado / Mathf.Max(0.01f, EstacionamientoActual));
            laser.widthMultiplier = Mathf.Lerp(0.03f, 0.11f, k);
            float alfa = AlfaDeParpadeo(EstacionamientoActual - enEstado);
            bool bajo = alfa < 0.99f;
            if (bajo != parpadeoBajo) { parpadeoBajo = bajo; CambiosDeParpadeo++; }
            if (!Mathf.Approximately(alfa, AlfaDelTelegrafo) || laser.startColor.a != alfa)
            {
                AlfaDelTelegrafo = alfa;
                var c = new Color(1f, 1f, 1f, alfa); laser.startColor = c; laser.endColor = c;
            }
            laser.SetPosition(0, transform.position + Vector3.up * 1.7f);
            laser.SetPosition(1, objetivo.transform.position + Vector3.up * 1.2f);
            if (!avisoSonoro) { avisoSonoro = true; AudioDirector.PlayAt(SfxKind.BombTick, transform.position, 0.8f, 0.7f); }
        }
        // ---------------------------------------------------------------- R17 (pedido 5): mascara con dos etapas de headshot
        static Material matMascara, matVisor;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarMateriales() { matMascara = null; matVisor = null; }

        static Material MaterialDe(Color c, Color? emision)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MAT_MascaraPesado", hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 0.6f); m.SetFloat("_Smoothness", 0.55f);
            if (emision.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emision.Value); }
            return m;
        }

        // Tres piezas (placa, visor, filtro) pegadas a la cara, hijas del hueso Head: acompanan la animacion. Sin collider: la caja de la cabeza no cambia.
        void ArmarMascara()
        {
            if (MascaraPuesta || soldado == null) return;
            var h = HitboxCabeza.De(soldado);
            if (h == null || h.transform.parent == null) return;
            var hueso = h.transform.parent;
            if (matMascara == null) matMascara = MaterialDe(new Color(0.12f, 0.12f, 0.14f), null);
            if (matVisor == null) matVisor = MaterialDe(new Color(0.35f, 0.04f, 0.03f), new Color(1.6f, 0.15f, 0.08f));
            var centro = h.Centro; var adelante = transform.forward; adelante.y = 0f; adelante = adelante.normalized;
            var rot = Quaternion.LookRotation(adelante, Vector3.up);
            Pieza(hueso, "Mascara_Placa", centro + adelante * 0.22f, rot, new Vector3(0.50f, 0.44f, 0.07f), matMascara);
            Pieza(hueso, "Mascara_Visor", centro + adelante * 0.26f + Vector3.up * 0.07f, rot, new Vector3(0.38f, 0.08f, 0.04f), matVisor);
            Pieza(hueso, "Mascara_Filtro", centro + adelante * 0.30f - Vector3.up * 0.12f, rot, new Vector3(0.18f, 0.16f, 0.12f), matMascara);
            MascaraPuesta = piezasDeMascara.Count > 0;
        }

        void Pieza(Transform padre, string n, Vector3 posMundo, Quaternion rotMundo, Vector3 tamMundo, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.layer = 2;   // Ignore Raycast: no estorba a las balas ni a la mira
            g.transform.SetPositionAndRotation(posMundo, rotMundo);
            g.transform.localScale = tamMundo;
            g.transform.SetParent(padre, true);   // conserva la pose y el tamano en mundo
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            piezasDeMascara.Add(g.transform);
        }

        // Primer headshot: la mascara sale despedida (chispas y golpe de metal); el pesado queda aturdido (vuelve a estacionar) y entra en furia.
        void RomperMascara()
        {
            EnFuria = true;
            if (piezasDeMascara.Count == 0) return;
            MascaraPuesta = false;
            var hb = soldado != null ? HitboxCabeza.De(soldado) : null;
            var cara = hb != null ? hb.Centro : transform.position + Vector3.up * 3f;
            var dir = transform.forward; dir.y = 0f;
            var tirador = soldado != null && soldado.Health != null ? ActorRegistry.FindById(soldado.Health.LastAttackerId) : null;
            if (tirador != null) { dir = tirador.transform.position - transform.position; dir.y = 0f; }
            dir = dir.normalized;
            foreach (var t in piezasDeMascara)
            {
                if (t == null) continue;
                t.SetParent(null, true);
                t.gameObject.layer = Fragmentador.LayerSinRayos;
                var caja = t.gameObject.AddComponent<BoxCollider>();
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = 1f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) Physics.IgnoreCollision(caja, c);
                rb.AddForce(dir * Random.Range(2.5f, 4f) + Vector3.up * Random.Range(1.5f, 2.5f) + Random.insideUnitSphere * 0.8f, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
                Destroy(t.gameObject, 8f);
                PiezasDeMascaraSueltas++;
            }
            piezasDeMascara.Clear();
            ChispasDeMetal.Spawn(cara, (dir + Vector3.up * 0.2f).normalized, 18);
            AudioDirector.PlayAt(SfxKind.ImpactMetal, cara, 1f, 0.75f);
            // aturdimiento: si estaba disparando vuelve a estacionar (no abre fuego hasta cumplir EstacionamientoActual, ya corto por la furia)
            if (Estado == Fase.Frenesi) { Estado = Fase.Estacionando; enEstado = 0f; InicioDeEstacionar = Time.time; InicioDeFrenesi = -1f; if (cerebro != null) cerebro.Quieto = true; }
            if (soldado != null && soldado.Motor != null) soldado.Motor.FactorDeClase = FactorDeVelocidadFuria;
            GameLog.Line($"[Pesado] {name}: MASCARA ROTA, furia (estaciona {SegundosDeEstacionamientoFuria:0.0} s)");
        }

        void OnDestroy()
        {
            if (soldado != null && soldado.Health != null) soldado.Health.CabezaExpuesta -= RomperMascara;
        }
    }
}
