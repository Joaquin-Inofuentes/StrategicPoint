using UnityEngine;
using SP.Core;

namespace SP.Tutorial
{
    // Los objetos del tutorial que viven bakeados en SC_Tutorial, con referencias serializadas. Antes el tutorial y su
    // corrida automatica los buscaban con GameObject.Find("Tut_..."): un renombre rompia el tutorial en silencio.
    // Ahora las asigna el constructor de la escena (TutorialSceneBuilder) y este componente grita al arrancar si falta alguna.
    [DefaultExecutionOrder(-200)]
    public class CatalogoDelTutorial : MonoBehaviour
    {
        public static CatalogoDelTutorial Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo() => Activo = this;

        public GameObject EnemigoEstatico;
        public GameObject Pared;
        public GameObject Destruible;
        public GameObject ZonaA;
        public GameObject ZonaB;
        public GameObject Meta;

        void Awake()
        {
            RegistrarActivo();
            foreach (var campo in CamposVacios()) GameLog.Line($"[AVISO] CatalogoDelTutorial: falta '{campo}' (regenerar SC_Tutorial)");
        }
        void OnDestroy() { if (Activo == this) Activo = null; }

        // Nombres de los campos que quedaron sin asignar; la suite exige que la lista este vacia.
        public System.Collections.Generic.List<string> CamposVacios()
        {
            var vacios = new System.Collections.Generic.List<string>();
            foreach (var f in typeof(CatalogoDelTutorial).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (f.FieldType == typeof(GameObject) && (GameObject)f.GetValue(this) == null) vacios.Add(f.Name);
            return vacios;
        }
    }
}
