using UnityEngine;
using SP.Player;

namespace SP.UI
{
    // Arma el roster instanciando UN prefab de fila (RosterRowView) por
    // soldado del equipo del jugador -- pedido explicito: "un prefab para
    // cada caso". Antes las filas vivian pre-armadas a mano en la escena
    // (Row_Soldado_1_Vega, etc.) y SelectedSoldierUI las reencontraba por
    // nombre; ahora la fuente de verdad es el squad real de
    // PlayerInputDriver y cada fila es duena de su soldado desde que nace.
    public class RosterView : MonoBehaviour
    {
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static RosterView Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        [SerializeField] RosterRowView rowPrefab;

        // Para el Editor-tool que arma esto una sola vez al construir la
        // escena (mismo patron que SetPool/SetTuning en WeaponHolder).
        public void SetRowPrefab(RosterRowView prefab) => rowPrefab = prefab;

        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable()
        {
            RegistrarActivo();
            Rebuild();
        }

        // Publico ademas de disparado por OnEnable: la posesion inicial de
        // partida (PlayerInputDriver.Start -> Brain.Possess(Squad[0])) NO
        // publica PossessionChangedEvent (mismo caso ya documentado en
        // PossessedMarkerView.SetInitial), y el orden real de OnEnable de
        // "Roster" contra el Start() del driver no esta garantizado. En vez
        // de que cada fila adivine la posesion inicial leyendo el brain en
        // su propio Bind (fragil si corre antes de tiempo), quien SI sabe
        // con certeza que la posesion inicial ya paso (PlayerInputDriver)
        // vuelve a llamar esto para asegurar el estado correcto sin
        // importar el orden real de ejecucion.
        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            if (rowPrefab == null) return;
            var driver = PlayerInputDriver.Activo;
            var squad = driver != null ? driver.Squad : null;

            int index = 1;
            if (squad != null)
                foreach (var soldier in squad)
                {
                    if (soldier == null) continue;
                    var row = Instantiate(rowPrefab, transform);
                    row.Bind(soldier, index);
                    index++;
                }

            // Pedido explicito: "al rescatar al civil que aparezca como otro
            // mas... quiero ver su vida e icono abajo a la izquierda porque
            // es nuevo". El civil no se suma al Squad real (evitando que
            // ordenes de formacion/posesion lo traten como un soldado de
            // combate mas -- no tiene arma), pero una vez rescatado SI se le
            // arma su propia fila de roster, igual que cualquier aliado.
            var m = SP.Mision.MisionDirector.Instancia;
            var civil = m != null ? m.Civil : null;
            if (civil != null && m.CivilRescatado && civil.Health != null && civil.Health.IsAlive && civil.gameObject.activeInHierarchy)
            {
                var row = Instantiate(rowPrefab, transform);
                row.Bind(civil, index);
            }
        }
    }
}
