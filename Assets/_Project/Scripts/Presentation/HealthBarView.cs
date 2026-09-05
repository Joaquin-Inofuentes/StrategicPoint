using System;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;

namespace SP.Presentation
{
    // Barra de vida flotante sobre un soldado. Sube y baja con la vida y
    // siempre mira a la cámara activa (billboard) — sirve igual en FPS y RTS.
    // Se auto-inicializa buscando a su propio Soldier: no depende de que
    // alguien la configure a mano (eso no sobrevive a un domain reload).
    //
    // Ya NO tiene LateUpdate propio: la recorre WorldUiDirector en un
    // unico pase para toda la UI de mundo. Antes, con cincuenta soldados,
    // esto eran cincuenta LateUpdate, cincuenta Camera.main (busqueda por
    // tag) y cincuenta enumeradores de hijos por frame.
    public class HealthBarView : MonoBehaviour
    {
        Health health;
        Image fill;
        bool bootstrapped;

        // Mostrar TODAS las barras siempre es inviable con cincuenta
        // unidades: satura la pantalla y ademas cuesta dibujarlas. Solo se
        // muestra la del que acaba de recibir daño o curacion, que es justo
        // cuando la informacion importa, y se apaga sola despues de unos
        // segundos sin nuevos impactos.
        // El poseido NO es una excepcion: su vida ya vive en el HUD fijo
        // (UI.PlayerHealthView, que PlayerInputDriver refresca cada frame
        // mientras se esta a pie), y en primera persona su barra flotante
        // queda arriba y detras de la camara -- que vive en su EyeAnchor --
        // asi que nunca entraria en cuadro aunque se dejara prendida.
        const float VisibleSeconds = 3.5f;
        float hideAt = -1f;
        IDisposable damageSub;
        // La curacion tiene su propia suscripcion en vez de refrescar hideAt
        // desde LateUpdate: alli la barra oculta corta antes por el early
        // return, con lo que curar a alguien apagado no se veia nunca.
        IDisposable healSub;
        Soldier owner;

        // Puerta de nivel de detalle que escribe WorldUiDirector.
        //
        // Arranca en true A PROPOSITO: si el director nunca corre (Edit
        // mode, o una escena donde no esta cableado) la barra se comporta
        // exactamente como antes, solo que sin LOD. Un default en false
        // dejaria todas las barras mudas para siempre.
        //
        // El LOD se COMPONE con la regla propia de la barra, no la
        // reemplaza: nadie mas que las suscripciones de daño y curacion
        // toca hideAt. Un soldado dañado que queda fuera de encuadre sigue
        // gastando su ventana de 3.5 s y, si vuelve a entrar antes de que
        // se acabe, muestra la barra el resto del tiempo que le quedaba.
        bool lodAllowed = true;
        public bool LodAllowed => lodAllowed;
        public void SetLodAllowed(bool value) => lodAllowed = value;

        // Los hijos de la barra (BG y Fill) son fijos, pero antes se
        // recorrian con "foreach (Transform child in transform)" por barra
        // y por frame: el enumerador de Transform es una clase, asi que
        // eran cincuenta asignaciones por frame yendo directo al GC. Se
        // cachean una vez y se recorren por indice.
        Transform[] children;

        void OnEnable()
        {
            Bootstrap();
            CacheChildren();
            damageSub?.Dispose();
            damageSub = SP.Core.EventBus.Instance.Subscribe<SP.Core.DamageTakenEvent>(OnAnyDamage);
            healSub?.Dispose();
            healSub = SP.Core.EventBus.Instance.Subscribe<SP.Core.HealedEvent>(OnAnyHeal);
            WorldUiDirector.Register(this);
        }

        void OnDisable()
        {
            damageSub?.Dispose();
            healSub?.Dispose();
            WorldUiDirector.Unregister(this);
        }

        // La guarda de Application.isPlaying es la misma que tiene
        // CubeFxReactor.OnDamage: HeadlessTestRunner corre en Edit mode y
        // publica daño y curacion de verdad, y nada de esto debe encenderse
        // ahi (no hay frame que dibujar y Time.time no significa nada).
        void OnAnyDamage(SP.Core.DamageTakenEvent evt)
        {
            if (!Application.isPlaying) return;
            if (owner == null || evt.TargetId != owner.Id) return;
            hideAt = Time.time + VisibleSeconds;
        }

        void OnAnyHeal(SP.Core.HealedEvent evt)
        {
            if (!Application.isPlaying) return;
            if (owner == null || evt.TargetId != owner.Id) return;
            hideAt = Time.time + VisibleSeconds;
        }

        void Start() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;

            var soldier = GetComponentInParent<Soldier>();
            if (soldier == null || soldier.Health == null) return;

            bootstrapped = true;
            owner = soldier;
            health = soldier.Health;

            var fillTransform = transform.Find("Fill");
            fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.color = soldier.Team == TeamId.Player ? new Color(0.35f, 0.9f, 0.4f) : new Color(0.95f, 0.3f, 0.25f);
                // Tambien aca y no solo en el barrido de arranque: un
                // soldado instanciado despues (un refuerzo, un respawn)
                // trae su barra sin pasar por ese barrido, y volveria a
                // quedarse llena para siempre.
                SP.UI.SpriteBlanco.Reparar(fill);
            }
        }

        // La comparacion contra childCount cuesta un int y cubre el caso
        // de que alguien agregue un hijo despues (el array cacheado se
        // rearma solo). No hay asignacion mientras la cantidad no cambie.
        void CacheChildren()
        {
            int count = transform.childCount;
            if (children != null && children.Length == count) return;
            children = new Transform[count];
            for (int i = 0; i < count; i++) children[i] = transform.GetChild(i);
        }

        // Lo llama WorldUiDirector una vez por frame. Devuelve true si la
        // barra quedo visible, para que el director pueda contarla.
        public bool Tick()
        {
            if (!bootstrapped) Bootstrap();
            if (health == null) return false;

            CacheChildren();

            // Las dos reglas se componen con un AND: manda la ventana de
            // daño/curacion, y el LOD solo puede restarle.
            bool shouldShow = Time.time <= hideAt && lodAllowed;

            // Los hijos, no este objeto: apagarse a si mismo desde aca
            // dispararia OnDisable y mataria la suscripcion al bus (y
            // ahora tambien el alta en el director), con lo que la barra
            // no volveria a aparecer nunca mas.
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null) continue;
                if (child.gameObject.activeSelf != shouldShow) child.gameObject.SetActive(shouldShow);
            }
            if (!shouldShow) return false;

            float pct = health.MaxHealth > 0 ? (float)health.Current / health.MaxHealth : 0f;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(pct);
            return true;
        }

        // La rotacion la provee el director, que resolvio Camera.main una
        // sola vez para toda la UI de mundo en vez de una vez por barra y
        // por frame.
        public void ApplyBillboard(Quaternion cameraRotation) => transform.rotation = cameraRotation;
    }
}
