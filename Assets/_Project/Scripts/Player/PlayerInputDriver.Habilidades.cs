using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Presentation;
using SP.UI;

namespace SP.Player
{
    // PlayerInputDriver (parte): habilidad de clase con [Ctrl] (medico / punteria fina) y el
    // resaltado del aliado vivo mas cercano. Separado del archivo principal solo para no pasar
    // el presupuesto de lineas (ver HeadlessTestRunner.BusquedasGlobales.cs) -- sigue siendo la
    // MISMA clase parcial, con acceso directo a Brain/Squad/etc.
    public partial class PlayerInputDriver
    {
        // -----------------------------------------------------------
        // [Ctrl]: habilidad especial de la clase que se maneja.
        //   - Medico: cura a un herido o reanima a un caido (cercano o en
        //     la mira). Independiente del [E] sostenido de arriba (A4) --
        //     ese sigue andando para CUALQUIER clase; esto es la version
        //     "especialista" del medico, mas rapida, atada a otra tecla.
        //   - Asalto / Explosivos (mismo RoleType.Assault: no hay una clase
        //     "Explosivos" separada, ver Demolicion.cs) y Francotirador:
        //     mientras se sostiene [Ctrl] apuntando (click derecho), el
        //     spread del arma baja progresivamente hasta un piso, con un
        //     tope de tiempo. Soltar [Ctrl], dejar de apuntar o moverse
        //     mucho reinicia el progreso a cero.
        // -----------------------------------------------------------
        public const float TiempoDeReanimarMedico = 2.5f;   // el especialista reanima el doble de rapido que el [E] generico (TiempoDeRevivir = 5f)
        public const float CuracionMedicoPorSegundo = 25f;  // mas rapido que el enfermero de IA (PedidoDeCuracion.CuracionPorSegundo = 12)
        public const float TiempoDeEnfoqueMax = 2.5f;       // segundos sosteniendo [Ctrl]+mira hasta la precision maxima

        float medicoAccionSegundos;
        float enfoqueSostenidoSegundos;
        float curacionAcumulada;
        CirculoDeProgreso circuloEnfoque;

        static readonly Color EnfoqueFondo = new Color(0f, 0f, 0f, 0.4f);
        static readonly Color EnfoqueRelleno = new Color(0.95f, 0.85f, 0.3f);

        void ActualizarHabilidadDeClase(AimResult result, bool ctrlSostenido, bool moviendoseMucho)
        {
            if (Brain.Current == null) return;

            switch (Brain.Current.Role)
            {
                case RoleType.Medic:
                    ActualizarHabilidadMedico(result, ctrlSostenido);
                    break;
                case RoleType.Assault:
                case RoleType.Sniper:
                    ActualizarEnfoquePrecision(result, ctrlSostenido, moviendoseMucho);
                    break;
                default:
                    // Cualquier otra clase (Flanker, etc.): sin habilidad de
                    // [Ctrl] todavia -- se asegura que no quede un enfoque
                    // viejo pegado si el jugador cambio de soldado.
                    Brain.Current.Weapon.SetEnfoque(0f);
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    break;
            }
        }

        // --- Medico: curar / reanimar con [Ctrl] ---------------------
        void ActualizarHabilidadMedico(AimResult result, bool ctrlSostenido)
        {
            // Prioridad al caido, igual que el [E] generico de arriba: un
            // companero muerto importa mas que uno solo herido.
            var caido = result.Type == AimTargetType.Caido ? result.Soldier : FindNearestDownedAlly();
            if (caido != null)
            {
                if (!ctrlSostenido)
                {
                    medicoAccionSegundos = 0f;
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    return;
                }
                medicoAccionSegundos += Time.deltaTime;
                MostrarCirculoEnfoque(Mathf.Clamp01(medicoAccionSegundos / TiempoDeReanimarMedico));
                AccionesEnCurso.Reportar(Brain.Current, "REVIVIENDO", caido.transform.position, medicoAccionSegundos / TiempoDeReanimarMedico, TiempoDeReanimarMedico - medicoAccionSegundos, caido.transform);   // ronda 13 (puntos 2 y 3)
                SetInstructionText($"[E mantenido] Reanimando a {caido.DisplayName}...");
                if (medicoAccionSegundos >= TiempoDeReanimarMedico && TryRevivir(caido, true))
                {
                    medicoAccionSegundos = 0f;
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    Feedback.Accion(SfxKind.Revive, $"{caido.DisplayName} REANIMADO", caido.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.8f);
                }
                return;
            }

            // Sin caido a mano: si hay un herido (con vida pero no al
            // maximo) cerca o en la mira, se lo cura mientras se sostiene
            // [Ctrl]. En la mira tiene prioridad sobre el mas cercano --
            // apuntarle a proposito a un herido especifico entre varios.
            Soldier herido = (result.Type == AimTargetType.Ally && Herido(result.Soldier)) ? result.Soldier : FindNearestWoundedAlly();
            if (herido != null && ctrlSostenido)
            {
                medicoAccionSegundos = 0f;
                curacionAcumulada += CuracionMedicoPorSegundo * Time.deltaTime;
                int entero = Mathf.FloorToInt(curacionAcumulada);
                if (entero > 0)
                {
                    herido.Health.Heal(entero);
                    curacionAcumulada -= entero;
                }
                float frac = herido.Health.MaxHealth > 0 ? (float)herido.Health.Current / herido.Health.MaxHealth : 1f;
                MostrarCirculoEnfoque(Mathf.Clamp01(frac));
                AccionesEnCurso.Reportar(Brain.Current, "CURANDO", herido.transform.position, frac, (herido.Health.MaxHealth - herido.Health.Current) / CuracionMedicoPorSegundo, herido.transform);   // ronda 13 (puntos 2 y 3)
                SetInstructionText($"[E mantenido] Curando a {herido.DisplayName}...");
                if (herido.Health.Current >= herido.Health.MaxHealth)
                {
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    Feedback.Accion(SfxKind.HealDone, $"{herido.DisplayName} CURADO", herido.transform.position, Feedback.Ok, aviso: true, pulso: false, volumen: 0.7f);
                }
                return;
            }

            medicoAccionSegundos = 0f;
            curacionAcumulada = 0f;
            if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
        }

        // Aliado vivo pero no al maximo de vida, mas cercano -- mismo patron que FindNearestDownedAlly.
        Soldier FindNearestWoundedAlly()
        {
            if (Squad == null || Brain.Current == null) return null;
            Soldier best = null;
            float bestDist = interactRadius;
            foreach (var s in Squad)
            {
                if (!Herido(s)) continue;
                float d = Vector3.Distance(Brain.Current.transform.position, s.transform.position);
                if (d <= bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        // --- Asalto / Explosivos / Francotirador: punteria fina con [Ctrl] ---
        void ActualizarEnfoquePrecision(AimResult result, bool ctrlSostenido, bool moviendoseMucho)
        {
            // "apuntando" = el mismo ADS de siempre (click derecho sostenido, Rig.EstaConZoom).
            bool apuntando = Rig != null && Rig.EstaConZoom;
            bool activo = ctrlSostenido && apuntando && !moviendoseMucho;

            enfoqueSostenidoSegundos = activo
                ? Mathf.Min(TiempoDeEnfoqueMax, enfoqueSostenidoSegundos + Time.deltaTime)
                : 0f;

            float progreso01 = Mathf.Clamp01(enfoqueSostenidoSegundos / TiempoDeEnfoqueMax);
            Brain.Current.Weapon.SetEnfoque(progreso01);

            if (activo && progreso01 > 0.01f)
            {
                MostrarCirculoEnfoque(progreso01);
                // Ronda 13 (puntos 2 y 3): el "interactuable" del francotirador / asalto es lo que tiene en la mira.
                var puntoDeMira = result.Type != AimTargetType.None ? result.Point : Brain.Current.transform.position + Brain.Current.transform.forward * 6f;
                AccionesEnCurso.Reportar(Brain.Current, "AFINANDO PUNTERIA", puntoDeMira, progreso01, TiempoDeEnfoqueMax - enfoqueSostenidoSegundos, result.HitTransform);
                SetInstructionText(progreso01 >= 0.999f ? "[E mantenido] Punteria maxima" : "[E mantenido] Afinando la punteria...");
            }
            else if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
        }

        void MostrarCirculoEnfoque(float progreso01)
        {
            if (circuloEnfoque == null)
            {
                var canvasRoot = AimUiRef != null ? AimUiRef.transform.parent : null;
                if (canvasRoot == null) return;
                circuloEnfoque = CirculoDeProgreso.Construir(canvasRoot, 46f, EnfoqueFondo, EnfoqueRelleno);
                circuloEnfoque.gameObject.name = "CirculoEnfoque";
                var rt = (RectTransform)circuloEnfoque.transform;
                // Mismo anclaje vertical que el circulo de revivir pero del
                // otro lado del centro, para no superponerse si algun dia
                // coinciden en pantalla (dos jugadores distintos, replay, etc.).
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.24f);
                rt.anchoredPosition = Vector2.zero;
            }
            circuloEnfoque.SetVisible(true);
            circuloEnfoque.SetProgreso(progreso01);
        }

        // Resalta (aclara el color) el aliado o vehiculo al que se le esta
        // apuntando, y le devuelve su color original apenas se deja de
        // apuntarle o se apunta a otra cosa.
        // Para poseer a un aliado hay que apuntarle con precision, sin
        // ninguna pista de cual esta en rango util -- este anillo marca
        // al vivo mas cercano (excluyendo al propio poseido) para que el
        // jugador sepa a quien puede cambiar sin tener que girar la
        // camara buscando. Se recalcula por intervalo, no por frame: no
        // hace falta la precision de un frame para "quien esta mas cerca".
        const float NearestAllyRange = 15f;
        const float NearestAllyCheckInterval = 0.35f;
        float nextNearestAllyCheck;
        Soldier nearestAllyHighlighted;
        SelectionRingFx nearestAllyRing;
        static readonly Color NearestAllyRingColor = new Color(0.4f, 0.85f, 1f, 0.8f);

        void UpdateNearestAllyHighlight()
        {
            if (Squad == null || Time.time < nextNearestAllyCheck) return;
            nextNearestAllyCheck = Time.time + NearestAllyCheckInterval;

            Soldier nearest = null;
            float bestDistSqr = NearestAllyRange * NearestAllyRange;
            foreach (var s in Squad)
            {
                if (s == null || s == Brain.Current || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = (s.transform.position - Brain.Current.transform.position).sqrMagnitude;
                if (d <= bestDistSqr) { bestDistSqr = d; nearest = s; }
            }

            if (nearest == nearestAllyHighlighted) return;
            nearestAllyHighlighted = nearest;

            if (nearestAllyRing != null) { Destroy(nearestAllyRing.gameObject); nearestAllyRing = null; }
            if (nearest != null) nearestAllyRing = SelectionRingFx.Spawn(nearest.transform, NearestAllyRingColor, 0.85f);
        }

        void ClearNearestAllyHighlight()
        {
            if (nearestAllyRing != null) { Destroy(nearestAllyRing.gameObject); nearestAllyRing = null; }
            nearestAllyHighlighted = null;
        }

        void HideFpsOnlyIndicators()
        {
            CoverHologram.Ocultar();
            ClearNearestAllyHighlight();
            if (mountIndicator != null) mountIndicator.Hide();
            if (highlightedRenderer != null)
            {
                SP.Presentation.CubeFxReactor.WriteTint(highlightedRenderer, highlightedOriginalColor);
                highlightedRenderer = null;
            }
        }
    }
}
