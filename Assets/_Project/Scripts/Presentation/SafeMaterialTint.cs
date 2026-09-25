using UnityEngine;

namespace SP.Presentation
{
    // Reaplica un SafeMaterial.Create(color) a los Renderer hijos cada vez que el objeto se
    // habilita. Existe para contenido de escena PERMANENTE (creado una vez por un builder de
    // Editor y guardado en el .unity, no un FX efimero): SafeMaterial usa HideAndDontSave a
    // proposito (ver SafeMaterial.cs) asi que ese Material puntual NUNCA sobrevive el viaje a
    // disco -- sin este componente, el Renderer queda con sharedMaterial nulo (magenta/roto) en
    // cuanto se guarda y se vuelve a abrir la escena.
    //
    // BUG REAL que esto corrige (reportado: "Perimetro_Cerros" y "Waypoint_1" con material roto /
    // null reference): un Awake() comun alcanza para el primer frame despues de crear el objeto Y
    // para cuando se entra a Play, pero el Editor NO llama Awake/OnEnable de un MonoBehaviour
    // comun al simplemente REABRIR una escena guardada en Edit Mode (sin ExecuteAlways). Por eso
    // se veia roto en el Editor aunque se autorreparaba al apretar Play. [ExecuteAlways] hace que
    // OnEnable corra tambien en Edit Mode, en cuanto la escena carga.
    [ExecuteAlways]
    public class SafeMaterialTint : MonoBehaviour
    {
        [SerializeField] Color color = Color.white;

        public void SetColor(Color c)
        {
            color = c;
            Reaplicar();
        }

        void OnEnable() => Reaplicar();

        void Reaplicar()
        {
            var mat = SafeMaterial.Create(color);
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        }
    }
}
