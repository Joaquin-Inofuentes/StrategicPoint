using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    public static partial class OperacionBuilder
    {
        // Bug #295 (retoque EN SITIO e idempotente de SC_Operacion, nunca reconstruye): los dos sectores de la defensa (A calle norte, B mercado oeste)
        // pasan a UN solo puesto, la plaza. Sector_A se convierte en "Sector_P" (322, 228, radio 26, omnidireccional), Sector_B desaparece con sus
        // letras y sacos, y el director queda apuntando a un solo sector. El civil, los francotiradores y las entradas por los bordes los arma el
        // director en Play: no viven en la escena.
        public static string PlazaUnicaEscenaAbierta()
        {
            var sb = new StringBuilder();
            var mando = GameObject.Find("6_Ciudad/Mando");
            if (mando == null) return "Falta 6_Ciudad/Mando";
            Transform sector = mando.transform.Find("Sector_P");
            if (sector == null) sector = mando.transform.Find("Sector_A");
            if (sector == null) return "Falta Sector_A / Sector_P";
            sector.name = "Sector_P";
            sector.position = new Vector3(322f, 0f, 228f);
            var comp = sector.GetComponent<SectorDeDefensa>();
            var so = new SerializedObject(comp);
            so.FindProperty("letra").stringValue = "P";
            so.FindProperty("nombre").stringValue = "PLAZA";
            so.FindProperty("radio").floatValue = RadioDeLaPlazaDelSector;
            so.FindProperty("omnidireccional").boolValue = true;
            var amenaza = so.FindProperty("haciaLaAmenaza"); amenaza.vector3Value = new Vector3(0f, 0f, 1f);
            so.ApplyModifiedProperties();

            int quitados = 0;
            for (int i = mando.transform.childCount - 1; i >= 0; i--)
            {
                var h = mando.transform.GetChild(i);
                string n = h.name;
                if (n == "Sector_B" || n == "Letra_A" || n == "Letra_B" || n.StartsWith("Saco_A_") || n.StartsWith("Saco_B_")) { Object.DestroyImmediate(h.gameObject); quitados++; }
            }
            sb.Append("quitados=").Append(quitados).Append(' ');

            var dir = Object.FindFirstObjectByType<OperacionDirector>();
            if (dir == null) return sb.Append("sin director").ToString();
            var dso = new SerializedObject(dir);
            var arr = dso.FindProperty("sectoresDeDefensa");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = comp;
            dso.ApplyModifiedProperties();
            sb.Append("sectores=").Append(dir.sectoresDeDefensa.Length).Append(" radio=").Append(comp.radio);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
