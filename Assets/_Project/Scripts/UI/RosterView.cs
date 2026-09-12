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
        [SerializeField] RosterRowView rowPrefab;

        // Para el Editor-tool que arma esto una sola vez al construir la
        // escena (mismo patron que SetPool/SetTuning en WeaponHolder).
        public void SetRowPrefab(RosterRowView prefab) => rowPrefab = prefab;

        void OnEnable() => Rebuild();

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
            var driver = FindAnyObjectByType<PlayerInputDriver>();
            var squad = driver != null ? driver.Squad : null;
            if (squad == null) return;

            int index = 1;
            foreach (var soldier in squad)
            {
                if (soldier == null) continue;
                var row = Instantiate(rowPrefab, transform);
                row.Bind(soldier, index);
                index++;
            }
        }
    }
}
