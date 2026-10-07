using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;

namespace SP.Presentation
{
    public enum TipoAccion
    {
        Curar,
        Revivir,
        Demoler,          // plantar una carga (Demoler / Carga)
        Operar,           // panel de un puesto
        Hackear,          // computadora del centro de datos
        Reparar,          // WP9b
        RecogerMunicion,
        Recargar,
    }

    // Bug #067: animacion para TODA accion de un soldado (curar, revivir, plantar carga, operar un panel, hackear, reparar,
    // recoger municion, recargar). Generaliza CurandoAnimacion: los soldados no tienen clips para esto, asi que se arma por
    // CODIGO sobre el esqueleto humanoide en LateUpdate (despues del Animator), con el mismo IK de dos huesos que ya usan el
    // cuchillo y la granada (BrazoIk). Hay un componente por soldado que se agrega solo la primera vez.
    //
    // Dos formas de usarla:
    //   * Tick(actor, objetivo, tipo, dt) cada frame mientras la accion sigue viva (caduca sola a los 0.25 s sin Tick); asi la
    //     enganchan AccionesEnCurso.Reportar (todas las acciones largas ya se reportan ahi) y no hay que acordarse de cerrarla.
    //   * Iniciar(actor, tipo, objetivo, duracion) para las acciones cortas con duracion conocida (recoger, recargar).
    // Terminar(actor) la corta ya. Activa(actor) dice si hay una accion animandose.
    //
    // Reglas: si el soldado se mueve a mas de 0.5 m/s el IK se desactiva (los soldados de la IA se mueven); el soldado que maneja el
    // jugador no se gira (la camara sale de su rotacion); la postura agachada solo la maneja esta clase si CurandoAnimacion no la
    // esta animando ya, y se restaura la previa al terminar. Los soldados sin rig humano (los cubos de la suite headless) no hacen
    // nada visible: la logica de la accion no depende de esto.
    public static class AnimacionDeAccion
    {
        public const float Caducidad = 0.25f;   // segundos sin Tick para dar la accion por terminada

        // Traduce el verbo que reporta AccionesEnCurso al tipo de animacion (null = esa accion no tiene animacion propia).
        public static TipoAccion? TipoDeVerbo(string verboEs)
        {
            switch (verboEs)
            {
                case "REVIVIENDO": return TipoAccion.Revivir;
                case "CURANDO":
                case "USANDO BOTIQUIN": return TipoAccion.Curar;
                case "DETONANDO": return TipoAccion.Demoler;
                case "DESACTIVANDO": return TipoAccion.Operar;
                case "OPERANDO LA RADIO": return TipoAccion.Operar;
                case "HACKEANDO": return TipoAccion.Hackear;
                case "REPARANDO": return TipoAccion.Reparar;
                default: return null;
            }
        }

        static AnimacionDeAccionSoldado De(Soldier actor, bool crear)
        {
            if (actor == null || !Application.isPlaying) return null;
            var c = actor.GetComponent<AnimacionDeAccionSoldado>();
            if (c == null && crear) c = actor.gameObject.AddComponent<AnimacionDeAccionSoldado>();
            return c;
        }

        public static void Tick(Soldier actor, Vector3 objetivo, TipoAccion tipo, float dt)
        {
            var c = De(actor, true);
            if (c != null) c.Mantener(tipo, objetivo, Caducidad);
        }

        public static void Iniciar(Soldier actor, TipoAccion tipo, Vector3 objetivo, float duracion)
        {
            var c = De(actor, true);
            if (c != null) c.Mantener(tipo, objetivo, duracion);
        }

        // #132: la recarga se anima con el PROGRESO REAL de la recarga del arma (0..1), no con un reloj propio: el arma corre en tiempo de
        // simulacion (que puede ir mas lento o mas rapido que el tiempo real) y el brazo tiene que llegar al arma cuando el arma queda lista.
        public static void ProgresoDeRecarga(Soldier actor, float progreso01, Vector3 objetivo)
        {
            var c = De(actor, true);
            if (c == null) return;
            c.Mantener(TipoAccion.Recargar, objetivo, 0.3f);
            c.ProgresoExterno = Mathf.Clamp01(progreso01);
        }

        public static void Terminar(Soldier actor)
        {
            var c = De(actor, false);
            if (c != null) c.Cortar();
        }

        public static bool Activa(Soldier actor)
        {
            var c = De(actor, false);
            return c != null && c.Activa;
        }

        public static TipoAccion? TipoActivo(Soldier actor)
        {
            var c = De(actor, false);
            return c != null && c.Activa ? c.Tipo : (TipoAccion?)null;
        }

        // Para la suite: la animacion tiene rig humano donde actuar y cuanto se separaron las manos de la pose del Animator.
        public static bool TieneRig(Soldier actor) { var c = De(actor, false); return c != null && c.TieneRig; }
        public static float DesplazamientoDeManos(Soldier actor) { var c = De(actor, false); return c != null ? c.DesplazamientoActual : 0f; }
        public static float DesplazamientoMaximoDeManos(Soldier actor) { var c = De(actor, false); return c != null ? c.DesplazamientoMaximo : 0f; }
        public static void ReiniciarMedicion(Soldier actor) { var c = De(actor, false); if (c != null) c.DesplazamientoMaximo = 0f; }
        public static bool AgachadaPropia(Soldier actor) { var c = De(actor, false); return c != null && c.AgachadaPropia; }
        public static string Prop(Soldier actor) { var c = De(actor, false); return c != null ? c.NombreDelProp : null; }
    }

    // El componente que anima: uno por soldado, se agrega solo.
    public class AnimacionDeAccionSoldado : MonoBehaviour
    {
        const float EntradaSegundos = 0.2f;
        const float VelocidadMaxima = 0.5f;   // m/s: mas rapido que esto el IK se apaga

        Soldier soldado;
        Animator anim;
        Transform pecho, brazoD, antebrazoD, manoD, brazoI, antebrazoI, manoI, pieI, pieD;
        bool resuelto, rigOk;
        readonly OcultadorDeArma arma = new OcultadorDeArma();

        TipoAccion tipo;
        Vector3 objetivo;
        float hasta = -1f;       // realtimeSinceStartup hasta el que la accion sigue viva
        float t;                 // segundos desde que empezo (tiempo de juego)
        float peso;              // 0..1 mezcla con la pose del Animator (sube al entrar, baja al salir)
        bool activa;             // todavia hay algo que restaurar
        Vector3 posPrevia;
        bool tienePosPrevia;
        float velocidad;

        bool agachadaPropia, agachadoPrevio;
        GameObject prop;
        Transform ledProp;
        Vector3 idleD, idleI, ikD, ikI;

        public bool Activa => activa && Time.realtimeSinceStartup < hasta;
        public TipoAccion Tipo => tipo;
        public float ProgresoExterno { get; set; } = -1f;   // 0..1 si lo marca quien dirige la accion (recarga); -1 = por tiempo
        public bool TieneRig => resuelto && rigOk;
        public float DesplazamientoActual { get; private set; }
        public float DesplazamientoMaximo { get; set; }
        public bool AgachadaPropia => agachadaPropia;
        public string NombreDelProp => prop != null ? prop.name : null;

        bool Resolver()
        {
            if (resuelto) return rigOk;
            resuelto = true;
            anim = GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman) return rigOk = false;
            brazoD = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            antebrazoD = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            manoD = anim.GetBoneTransform(HumanBodyBones.RightHand);
            brazoI = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            antebrazoI = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            manoI = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            pieD = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            pieI = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            pecho = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            rigOk = brazoD != null && antebrazoD != null && manoD != null && brazoI != null && antebrazoI != null && manoI != null && pecho != null;
            return rigOk;
        }

        void Awake() { soldado = GetComponent<Soldier>(); }

        public void Mantener(TipoAccion nuevo, Vector3 destino, float segundos)
        {
            if (soldado == null) soldado = GetComponent<Soldier>();
            // Las acciones cortas de bajo valor (recargar, recoger) no pisan una accion larga en curso.
            bool corta = nuevo == TipoAccion.Recargar || nuevo == TipoAccion.RecogerMunicion;
            if (activa && Activa && corta && tipo != nuevo && !(tipo == TipoAccion.Recargar || tipo == TipoAccion.RecogerMunicion)) return;
            if (!activa || tipo != nuevo)
            {
                if (activa) Restaurar();
                tipo = nuevo;
                t = 0f;
                peso = 0f;
                ProgresoExterno = -1f;
                DesplazamientoMaximo = 0f;
                activa = true;
            }
            objetivo = destino;
            hasta = Time.realtimeSinceStartup + segundos;
        }

        public void Cortar()
        {
            hasta = -1f;
        }

        static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

        void LateUpdate()
        {
            if (!activa) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool viva = Time.realtimeSinceStartup < hasta;
            if (soldado == null || soldado.Health == null || !soldado.Health.IsAlive) viva = false;

            // Velocidad del soldado: si camina de verdad no se fuerza el brazo.
            var pos = transform.position;
            if (tienePosPrevia) velocidad = Mathf.Lerp(velocidad, (pos - posPrevia).magnitude / dt, Mathf.Clamp01(dt * 25f));
            posPrevia = pos; tienePosPrevia = true;
            bool quieto = velocidad <= VelocidadMaxima;

            // El jugador que revive con [E] mantenido: la animacion espera al retardo de 0.15 s de PlayerInputDriver (CurandoAnimacion
            // arranca ahi), asi un toque suelto de [E] frente a un caido no mueve los brazos ni agacha.
            bool delJugador = soldado != null && soldado.Brain != null && soldado.Brain.IsPossessedByPlayer;
            bool esperando = tipo == TipoAccion.Revivir && delJugador && !CurandoAnimacion.Animando(soldado);
            float meta = viva && quieto && !esperando ? 1f : 0f;
            // Sube en 0.2 s; si el soldado echa a caminar el IK se suelta casi de golpe (0.07 s) para no arrastrar los brazos en marcha.
            peso = Mathf.MoveTowards(peso, meta, dt / (meta > peso ? EntradaSegundos : (!quieto && viva ? 0.07f : EntradaSegundos)));
            if (viva && !esperando) t += dt;

            if (!viva && peso <= 0f) { Restaurar(); return; }

            bool rig = Resolver();
            Postura(viva && !esperando, dt);
            if (!rig) return;

            float w = BrazoIk.Suave(peso);
            if (w <= 0.001f) return;

            // Pose del Animator ANTES de tocar nada: referencia para medir cuanto se separan las manos del idle.
            idleD = transform.InverseTransformPoint(manoD.position);
            idleI = transform.InverseTransformPoint(manoI.position);

            Animar(w);

            ikD = transform.InverseTransformPoint(manoD.position);
            ikI = transform.InverseTransformPoint(manoI.position);
            DesplazamientoActual = Mathf.Max(Vector3.Distance(ikD, idleD), Vector3.Distance(ikI, idleI));
            if (DesplazamientoActual > DesplazamientoMaximo) DesplazamientoMaximo = DesplazamientoActual;
        }

        // Postura (agacharse, mirar al objetivo) y prop; independiente de que el soldado tenga rig.
        void Postura(bool viva, float dt)
        {
            bool delJugador = soldado != null && soldado.Brain != null && soldado.Brain.IsPossessedByPlayer;
            bool curando = CurandoAnimacion.Animando(soldado);

            bool quiereAgachar = viva && (tipo == TipoAccion.Revivir || tipo == TipoAccion.Reparar) && !curando;
            if (soldado != null && soldado.Motor != null)
            {
                if (quiereAgachar)
                {
                    if (!agachadaPropia) { agachadoPrevio = soldado.Motor.IsCrouching; agachadaPropia = true; }
                    soldado.Motor.SetCrouching(true);
                }
                else if (agachadaPropia)
                {
                    soldado.Motor.SetCrouching(agachadoPrevio);
                    agachadaPropia = false;
                }
            }

            // Mirar al objetivo (solo la IA: al soldado manejado se le gira con el mouse).
            bool girar = viva && !delJugador && !curando && tipo != TipoAccion.Recargar && tipo != TipoAccion.RecogerMunicion;
            if (girar)
            {
                var hacia = Plano(objetivo - transform.position);
                if (hacia.sqrMagnitude > 0.09f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(hacia), Mathf.Clamp01(dt * 8f));
            }
        }

        void Restaurar()
        {
            activa = false;
            arma.Restaurar();
            QuitarProp();
            if (agachadaPropia && soldado != null && soldado.Motor != null) soldado.Motor.SetCrouching(agachadoPrevio);
            agachadaPropia = false;
            DesplazamientoActual = 0f;
        }

        void OnDisable()
        {
            if (activa) Restaurar();
        }

        // ------------------------------------------------------------------ poses
        static readonly Vector3 HintD = new Vector3(0.9f, -0.5f, -0.3f);
        static readonly Vector3 HintI = new Vector3(-0.9f, -0.5f, -0.3f);

        // Resuelve un brazo para que la mano llegue a un punto del MUNDO (el IK lo recorta al alcance del brazo).
        static void Mano(Transform raiz, Transform brazo, Transform antebrazo, Transform mano, Vector3 puntoMundo, Vector3 hint, float w)
        {
            float alcance = Vector3.Distance(brazo.position, antebrazo.position) + Vector3.Distance(antebrazo.position, mano.position);
            if (alcance < 0.05f) return;
            var v = puntoMundo - brazo.position;
            var local = new Vector3(Vector3.Dot(v, raiz.right), Vector3.Dot(v, raiz.up), Vector3.Dot(v, raiz.forward)) / alcance;
            BrazoIk.Resolver(raiz, brazo, antebrazo, mano, local, hint, w);
        }

        float SueloY()
        {
            if (pieD != null && pieI != null) return Mathf.Min(pieD.position.y, pieI.position.y);
            return transform.position.y - 0.8f;
        }

        void Inclinar(float grados, float w)
        {
            if (pecho != null && Mathf.Abs(grados) > 0.01f)
                pecho.rotation = Quaternion.AngleAxis(grados * w, transform.right) * pecho.rotation;
        }

        void Animar(float w)
        {
            var fwd = Plano(transform.forward).normalized;
            var der = Plano(transform.right).normalized;
            float suelo = SueloY();
            float tau = Mathf.PI * 2f;

            switch (tipo)
            {
                case TipoAccion.Curar:
                    // Lo anima CurandoAnimacion (agachado + botiquin). Aca solo se acerca la mano al paciente si nadie lo hace.
                    if (!CurandoAnimacion.Animando(soldado)) ManosAlPunto(objetivo + Vector3.up * 0.1f, 0.12f, w);
                    break;

                case TipoAccion.Revivir:
                {
                    // Manos al pecho del caido, compresiones a 2 Hz: las manos bajan y suben y el torso acompana.
                    arma.Ocultar(manoD, transform);
                    float c = 0.5f + 0.5f * Mathf.Sin(t * tau * 2f);
                    var pt = objetivo + Vector3.up * (0.12f - 0.10f * c);
                    Inclinar(24f + 8f * c, w);
                    ManosAlPunto(pt, 0.08f, w);
                    break;
                }

                case TipoAccion.Demoler:
                {
                    // Manos al piso, plantando la carga delante de los pies.
                    arma.Ocultar(manoD, transform);
                    float vaiven = 0.02f * Mathf.Sin(t * tau * 1.5f);
                    var centro = transform.position + fwd * 0.5f; centro.y = suelo + 0.1f + vaiven;
                    Inclinar(38f, w);
                    ManosAlPunto(centro, 0.13f, w);
                    PonerProp("Prop_CargaRoja", CrearCarga, centro + Vector3.up * 0.04f);
                    if (ledProp != null) ledProp.gameObject.SetActive(Mathf.Sin(t * tau * 3f) > -0.2f);
                    break;
                }

                case TipoAccion.Operar:
                case TipoAccion.Hackear:
                {
                    // De pie, manos sobre la mesa: tecleo de 6 Hz con 3 cm de amplitud.
                    arma.Ocultar(manoD, transform);
                    var baseP = pecho.position + fwd * 0.42f + Vector3.down * 0.32f;
                    float tD = 0.03f * Mathf.Sin(t * tau * 6f);
                    float tI = 0.03f * Mathf.Sin(t * tau * 6f + Mathf.PI);
                    Inclinar(8f, w);
                    Mano(transform, brazoD, antebrazoD, manoD, baseP + der * 0.16f + Vector3.up * tD, HintD, w);
                    Mano(transform, brazoI, antebrazoI, manoI, baseP - der * 0.16f + Vector3.up * tI, HintI, w);
                    break;
                }

                case TipoAccion.Reparar:
                {
                    // Martilleo de 1.5 Hz con una llave inglesa en la derecha; la izquierda sostiene la pieza.
                    arma.Ocultar(manoD, transform);
                    var pt = objetivo; pt.y = Mathf.Max(suelo + 0.25f, Mathf.Min(objetivo.y, suelo + 0.9f));
                    float golpe = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * tau * 1.5f)), 2f);   // sube y baja el martillo
                    Inclinar(18f, w);
                    Mano(transform, brazoD, antebrazoD, manoD, pt + Vector3.up * (0.04f + 0.14f * golpe), HintD, w);
                    Mano(transform, brazoI, antebrazoI, manoI, pt - der * 0.12f, HintI, w);
                    PonerProp("Prop_LlaveInglesa", CrearLlave, Vector3.zero, manoD);
                    break;
                }

                case TipoAccion.RecogerMunicion:
                {
                    // Agacharse 0.6 s: el torso se inclina y la mano va al piso y vuelve.
                    float k = Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI);
                    Inclinar(42f * k, w);
                    var pt = transform.position + fwd * 0.4f; pt.y = suelo + 0.12f;
                    Mano(transform, brazoD, antebrazoD, manoD, Vector3.Lerp(manoD.position, pt, k), HintD, w * k);
                    break;
                }

                case TipoAccion.Recargar:
                {
                    // #132: dura toda la recarga (u = progreso del arma). 0-25 %: la izquierda baja a la cadera por el cargador; 25-60 %: lo
                    // lleva al arma; 60-80 %: lo encaja (golpecito); 80-100 %: tira del cerrojo (la mano recorre el arma hacia atras y vuelve).
                    float u = ProgresoExterno >= 0f ? ProgresoExterno : Mathf.Clamp01(t / 0.4f) * 0.55f;   // sin progreso externo (acciones cortas): baja por el cargador y lo lleva al arma, y ahi se queda
                    var cadera = pecho.position + Vector3.down * 0.55f - der * 0.18f + fwd * 0.12f;
                    var arma0 = pecho.position + Vector3.down * 0.22f + fwd * 0.5f - der * 0.05f;
                    Vector3 pt;
                    if (u < 0.25f) pt = Vector3.Lerp(manoI.position, cadera, BrazoIk.Suave(u / 0.25f));
                    else if (u < 0.6f) pt = Vector3.Lerp(cadera, arma0, BrazoIk.Suave((u - 0.25f) / 0.35f));
                    else if (u < 0.8f) pt = arma0 + Vector3.up * (0.05f * Mathf.Sin((u - 0.6f) / 0.2f * tau * 1.5f)) * (1f - (u - 0.6f) / 0.2f);
                    else
                    {
                        float k = (u - 0.8f) / 0.2f;   // cerrojo: atras (primera mitad) y adelante
                        float atras = k < 0.5f ? BrazoIk.Suave(k * 2f) : BrazoIk.Suave((1f - k) * 2f);
                        pt = arma0 - fwd * (0.22f * atras) + Vector3.up * 0.04f;
                    }
                    Inclinar(6f, w);
                    Mano(transform, brazoI, antebrazoI, manoI, pt, HintI, w);
                    if (u < 0.6f) PonerProp("Prop_Cargador", CrearCargador, Vector3.zero, manoI); else QuitarProp();
                    break;
                }
            }
        }

        // Las dos manos a un punto del mundo, abiertas "separacion" metros a cada lado (de la derecha del soldado).
        void ManosAlPunto(Vector3 punto, float separacion, float w)
        {
            var der = Plano(transform.right).normalized;
            Mano(transform, brazoD, antebrazoD, manoD, punto + der * separacion, HintD, w);
            Mano(transform, brazoI, antebrazoI, manoI, punto - der * separacion, HintI, w);
        }

        // ------------------------------------------------------------------ props
        static Material matRojo, matGris, matMetal, matLed, matCargador;
        static Material Mat(ref Material m, Color c) { if (m == null) m = DiamondGizmo.NuevoMaterial(c); return m; }

        static GameObject Cubo(Transform padre, Vector3 escala, Vector3 posLocal, Material mat, string nombre)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(padre, false);
            go.transform.localScale = escala;
            go.transform.localPosition = posLocal;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        GameObject CrearCarga()
        {
            var raiz = new GameObject("Prop_CargaRoja");
            Cubo(raiz.transform, new Vector3(0.34f, 0.16f, 0.22f), Vector3.zero, Mat(ref matRojo, new Color(0.85f, 0.08f, 0.05f)), "Caja");
            var led = Cubo(raiz.transform, new Vector3(0.05f, 0.03f, 0.05f), new Vector3(0.1f, 0.095f, 0f), Mat(ref matLed, new Color(1f, 0.95f, 0.3f)), "Led");
            ledProp = led.transform;
            return raiz;
        }

        GameObject CrearLlave()
        {
            var raiz = new GameObject("Prop_LlaveInglesa");
            Cubo(raiz.transform, new Vector3(0.035f, 0.035f, 0.30f), new Vector3(0f, 0f, 0.12f), Mat(ref matMetal, new Color(0.62f, 0.64f, 0.68f)), "Mango");
            Cubo(raiz.transform, new Vector3(0.11f, 0.035f, 0.06f), new Vector3(0f, 0f, 0.28f), matMetal, "Cabeza");
            return raiz;
        }

        GameObject CrearCargador()
        {
            var raiz = new GameObject("Prop_Cargador");
            Cubo(raiz.transform, new Vector3(0.05f, 0.14f, 0.04f), Vector3.zero, Mat(ref matCargador, new Color(0.12f, 0.12f, 0.14f)), "Cuerpo");
            Cubo(raiz.transform, new Vector3(0.045f, 0.03f, 0.035f), new Vector3(0f, 0.085f, 0f), Mat(ref matGris, new Color(0.8f, 0.65f, 0.2f)), "Balas");
            return raiz;
        }

        // El prop vive mientras dura la accion. Con "mano" va pegado a esa mano (orientado con el soldado); sin mano se coloca en el mundo.
        void PonerProp(string nombre, System.Func<GameObject> crear, Vector3 mundo, Transform mano = null)
        {
            if (prop != null && prop.name != nombre) QuitarProp();
            if (prop == null)
            {
                prop = crear();
                prop.name = nombre;
                if (mano != null) prop.transform.SetParent(mano, false);
            }
            if (mano != null)
            {
                prop.transform.localPosition = Vector3.zero;
                prop.transform.rotation = transform.rotation;
            }
            else
            {
                prop.transform.position = mundo;
                prop.transform.rotation = Quaternion.LookRotation(Plano(transform.forward).normalized);
            }
        }

        void QuitarProp()
        {
            if (prop != null) Destroy(prop);
            prop = null; ledProp = null;
        }
    }
}
