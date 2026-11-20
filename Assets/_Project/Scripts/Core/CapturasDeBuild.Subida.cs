using System.Collections;
using UnityEngine;

namespace SP.Core
{
    // Prueba REAL de la subida al Sheet desde la build: abre el dialogo [U], confirma con una nota de prueba y espera la respuesta de Apps Script.
    // Sube UNA fila al Sheet (la nota dice que es de prueba). Uso: -capturas <dir> -tomas subida_real
    public sealed partial class CapturasDeBuild
    {
        public static readonly string[] IdsSubida = { "subida_real" };

        IEnumerator SubidaReal()
        {
            yield return Espera(3f);
            Dato("dato subida.Habilitada=" + SubidaDeBug.Habilitada + " SubeAlSheet=" + SubidaDeBug.SubeAlSheet + " Disponible=" + SubidaDeBug.Disponible
                 + " isEditor=" + Application.isEditor + " batch=" + Application.isBatchMode);
            ReporteDeBug.Abrir();
            yield return Espera(1.5f);
            Dato("dato subida.dialogo_abierto=" + ReporteDeBug.Abierto);
            int n = ReporteDeBug.Confirmar("PRUEBA AUTOMATICA DE SUBIDA DESDE LA BUILD (se puede borrar)");
            Dato("dato subida.numero=" + n);
            float limite = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < limite && (SubidaDeBug.Enviando > 0 || string.IsNullOrEmpty(SubidaDeBug.UltimoResultado))) yield return null;
            Dato("dato subida.resultado=" + SubidaDeBug.UltimoResultado + " fila=" + SubidaDeBug.UltimaFila);
        }
    }
}
