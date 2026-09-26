using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Vehicles
{
    // AMETRALLADORA FIJA: los emplazamientos del mapa (P_Env_Emplazamiento_MG, en las torres y
    // los bunkers) dejan de ser decorado. Un soldado puede ocuparlos: se queda plantado detras
    // del arma, gira solo dentro de un arco de +-70 grados y dispara una cinta de 100 balas
    // con la ametralladora montada. No se mueve mientras la usa. Al salir recupera su arma.
    //
    // Es una torreta FIJA: no se desplaza ni se lleva. Se usa apuntandole (radial > TORRETA FIJA
    // o [E] estando cerca).
    [DisallowMultipleComponent]
    public class TorretaFija : MonoBehaviour
    {
        public const float AlcanceDeUso = 4.5f;     // distancia maxima para ocuparla con [E]
        public const float ArcoDeGiro = 70f;        // grados a cada lado del frente
        public const float DistanciaAlArma = 0.7f;  // el soldado se para detras del arma
        public const float AlturaDelPivote = 0.8f;  // el pivote del soldado esta a esta altura sobre sus pies
        public const int DanoPorBala = 14;
        public const float Cadencia = 0.085f;
        public const int Cinta = 100;
        public const float SegundosDeRecarga = 3.2f;
        public const float FactorPrecision = 0.35f;   // multiplica SpreadDegEfectivo: mas preciso que a mano

        static readonly List<TorretaFija> todas = new List<TorretaFija>();
        public static IReadOnlyList<TorretaFija> Todas => todas;

        public Soldier Ocupante { get; private set; }
        public bool Libre => Ocupante == null;
        public Vector3 Puesto { get; private set; }
        public float YawCentro { get; private set; }

        int armaPreviaIndice;
        Vector3 posicionPrevia;

        // BUG REAL reportado: "montas la torreta y la torreta no se mueve, se deberia rotar". El
        // codigo de arriba (Ocupar/AcotarGiro) siempre giro al SOLDADO, nunca al mesh del arma --
        // el emplazamiento (P_Env_Emplazamiento_MG) es una sola pieza con un BoxCollider SOLIDO en
        // la raiz (el parapeto de sacos con el que choca todo el mundo); girar transform ENTERO
        // giraria tambien ese collider y dejaria el parapeto desalineado del arte del nivel en
        // cuanto alguien la use. No se puede resolver moviendo el mesh a un hijo pivote nuevo:
        // ese mesh (GRP_Env_Emplazamiento) es parte de la estructura de un Prefab Instance conectado
        // y Unity ignora en silencio el SetParent que lo saca de ahi (verificado en vivo). En cambio
        // se gira cada hijo visual EN SU LUGAR (pos/rot local original + delta de yaw), que da el
        // mismo resultado visual que orbitar alrededor de un pivote sin tocar la estructura del prefab.
        readonly List<Transform> hijosVisuales = new List<Transform>();
        readonly List<Vector3> posLocalOriginal = new List<Vector3>();
        readonly List<Quaternion> rotLocalOriginal = new List<Quaternion>();
        float yawNeutro;

        void GirarVisual(float yaw)
        {
            var delta = Quaternion.Euler(0f, Mathf.DeltaAngle(yawNeutro, yaw), 0f);
            for (int i = 0; i < hijosVisuales.Count; i++)
            {
                var c = hijosVisuales[i];
                if (c == null) continue;
                c.localPosition = delta * posLocalOriginal[i];
                c.localRotation = delta * rotLocalOriginal[i];
            }
        }

        // Aviso para el tutorial, la interfaz y las pruebas.
        public static event System.Action<TorretaFija, Soldier> Ocupada;
        public static event System.Action<TorretaFija, Soldier> Liberada;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todas.Clear(); Ocupada = null; Liberada = null; }

        void OnEnable() { Registrar(); }
        void OnDestroy() { todas.Remove(this); }
        void Registrar() { if (!todas.Contains(this)) todas.Add(this); }
        void OnDisable()
        {
            todas.Remove(this);
            if (Ocupante != null) Liberar();
        }

        public static TorretaFija De(Soldier s)
        {
            if (s == null) return null;
            foreach (var t in todas) if (t != null && t.Ocupante == s) return t;
            return null;
        }

        public static TorretaFija MasCercana(Vector3 p, float radio)
        {
            TorretaFija mejor = null; float d2 = radio * radio;
            foreach (var t in todas)
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                // Distancia en el plano: se puede usar una torreta de una torre parado en su base.
                var v = t.transform.position - p; v.y = 0f;
                float d = v.sqrMagnitude;
                if (d <= d2) { d2 = d; mejor = t; }
            }
            return mejor;
        }

        // Cuenta el punto donde se para el tirador segun de donde llega: mirando hacia el arma.
        public bool Ocupar(Soldier s, out string motivo)
        {
            motivo = null;
            if (s == null || s.Health == null || !s.Health.IsAlive) { motivo = "NO SE PUEDE"; return false; }
            if (Ocupante != null && Ocupante != s) { motivo = "LA TORRETA YA ESTA OCUPADA"; return false; }
            if (Ocupante == s) return true;
            var previa = De(s);
            if (previa != null) previa.Liberar();

            Vector3 haciaArma = transform.position - s.transform.position; haciaArma.y = 0f;
            if (haciaArma.sqrMagnitude < 0.01f) haciaArma = s.transform.forward;
            haciaArma.Normalize();
            YawCentro = Mathf.Atan2(haciaArma.x, haciaArma.z) * Mathf.Rad2Deg;

            posicionPrevia = s.transform.position;
            // El soldado se para SOBRE el emplazamiento (techo de la torre, parapeto del bunker).
            float y = transform.position.y + AlturaDelPivote;
            Puesto = new Vector3(transform.position.x, y, transform.position.z) - new Vector3(haciaArma.x, 0f, haciaArma.z) * DistanciaAlArma;
            Ocupante = s;
            s.transform.position = Puesto;
            s.transform.rotation = Quaternion.Euler(0f, YawCentro, 0f);
            s.Motor.SetCrouching(false);
            s.Motor.SetRunning(false);

            var arma = s.Weapon;
            armaPreviaIndice = arma.CurrentLoadoutIndex;
            arma.EquipWeapon(WeaponKind.Smg, DanoPorBala, Cadencia, new Color(1f, 0.85f, 0.3f));
            arma.ConfigurarCargador(Cinta, SegundosDeRecarga);
            // BALANCE: montada sobre un pivote fijo, no sobre el cuerpo de pie -- mucho
            // mas estable que la misma arma disparada a mano. 0.35 = 65% menos dispersion
            // efectiva mientras dura la ocupacion.
            arma.MultiplicadorTorretaFija = FactorPrecision;
            GameLog.Line($"{s.DisplayName} ocupa la ametralladora fija");
            Ocupada?.Invoke(this, s);
            return true;
        }

        public void Liberar()
        {
            var s = Ocupante;
            if (s == null) return;
            Ocupante = null;
            if (s.Weapon != null)
            {
                s.Weapon.MultiplicadorTorretaFija = 1f;
                s.Weapon.EquipFromLoadout(armaPreviaIndice);
            }
            // Baja de la torre: vuelve donde estaba parado antes de subir (si sigue vivo).
            if (s.Health != null && s.Health.IsAlive) s.transform.position = posicionPrevia;
            GameLog.Line($"{s.DisplayName} deja la ametralladora fija");
            Liberada?.Invoke(this, s);
        }

        // El giro esta limitado al arco: se llama despues de cada giro del mouse.
        public void AcotarGiro(Soldier s)
        {
            if (s == null || s != Ocupante) return;
            float yaw = s.transform.eulerAngles.y;
            float d = Mathf.DeltaAngle(YawCentro, yaw);
            if (Mathf.Abs(d) > ArcoDeGiro)
                s.transform.rotation = Quaternion.Euler(0f, YawCentro + Mathf.Sign(d) * ArcoDeGiro, 0f);
        }

        // Fraccion 0..1 dentro del arco (0 = centro, 1 = tope): para el aviso de "tope de giro".
        public float FraccionDeArco(Soldier s)
        {
            if (s == null) return 0f;
            return Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(YawCentro, s.transform.eulerAngles.y)) / ArcoDeGiro);
        }

        void LateUpdate()
        {
            var s = Ocupante;
            if (s == null)
            {
                // Sin nadie a bordo el arma vuelve a mirar hacia donde la puso el nivel: no queda
                // apuntando para siempre hacia donde disparo el ultimo que la uso.
                GirarVisual(yawNeutro);
                return;
            }
            // Muerto, o sacado del puesto por una orden (RTS): se libera solo.
            if (s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy
                || (s.transform.position - Puesto).sqrMagnitude > 2.25f)
            {
                Liberar();
                return;
            }
            var p = s.transform.position;
            if ((p - Puesto).sqrMagnitude > 0.0001f) s.transform.position = Puesto;

            // El mesh sigue la punteria YA acotada al arco (PlayerInputDriver llama AcotarGiro en
            // Update, que corre siempre antes que este LateUpdate).
            GirarVisual(s.transform.eulerAngles.y);
        }

        // ---- Instalacion sobre el arte del mapa -------------------------------------------------

        public const string NombreDelArte = "P_Env_Emplazamiento_MG";

        // Convierte en torretas jugables los emplazamientos de nivel superior de la escena.
        public static int InstalarEnEscena()
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t == null || t.name != NombreDelArte) continue;
                if (t.parent == null || t.parent.name != "ArteBloque") continue;   // los anidados son piezas del mismo
                if (t.GetComponent<TorretaFija>() != null) continue;
                Instalar(t.gameObject);
                n++;
            }
            return n;
        }

        public static TorretaFija Instalar(GameObject go)
        {
            var torreta = go.GetComponent<TorretaFija>();
            if (torreta != null) return torreta;
            torreta = go.AddComponent<TorretaFija>();
            torreta.Registrar();   // en Edit mode (la suite) OnEnable no corre al agregar el componente

            // Zona de apuntado: una caja (trigger, no estorba al caminar) del tamano del modelo.
            var rends = go.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(go.transform.position + Vector3.up * 0.5f, Vector3.one);
            if (rends.Length > 0)
            {
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            }
            var caja = go.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            caja.center = go.transform.InverseTransformPoint(b.center);
            var e = go.transform.lossyScale;
            caja.size = new Vector3(b.size.x / Mathf.Max(0.01f, Mathf.Abs(e.x)) + 0.4f, b.size.y / Mathf.Max(0.01f, Mathf.Abs(e.y)) + 0.4f, b.size.z / Mathf.Max(0.01f, Mathf.Abs(e.z)) + 0.4f);

            // Todos los hijos existentes (el mesh, donde vive el prefab anidado del arte) se guardan
            // como "hijos visuales": se giran en su lugar con la punteria (ver GirarVisual) sin tocar
            // el root, que es donde viven los dos colliders de arriba (el solido del parapeto y esta
            // caja de apuntado).
            torreta.yawNeutro = go.transform.eulerAngles.y;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var c = go.transform.GetChild(i);
                torreta.hijosVisuales.Add(c);
                torreta.posLocalOriginal.Add(c.localPosition);
                torreta.rotLocalOriginal.Add(c.localRotation);
            }
            if (Application.isPlaying)
            {
                var rombo = InteractableDiamond.Agregar(go.transform, Vector3.up * 2f, DiamondGizmo.ColorTorreta);
                rombo.Condicion = () => torreta.Ocupante == null;
            }
            return torreta;
        }
    }
}
