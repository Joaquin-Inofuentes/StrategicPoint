using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Item 93: SC_TestLevel, SC_Menu y SC_Tutorial se regeneran por codigo (la suite y los builders las reescriben),
    // asi que una edicion hecha a mano se perdia sin aviso. Antes de que Unity guarde CUALQUIER escena .unity ya
    // existente en disco, se deja una copia en Library/RespaldoEscenas (fuera de git, por maquina) y se conservan las
    // ultimas 8 de cada escena. Recuperar una edicion perdida: menu Strategic Point > Escenas > Abrir carpeta de respaldos.
    public class RespaldoDeEscenas : AssetModificationProcessor
    {
        const int Conservar = 8;
        public static string Carpeta => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "RespaldoEscenas"));

        static string[] OnWillSaveAssets(string[] rutas)
        {
            foreach (var ruta in rutas)
                if (ruta.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) Respaldar(ruta);
            return rutas;
        }

        // Devuelve la ruta de la copia (o null si no habia nada que copiar o fallo; nunca interrumpe el guardado).
        public static string Respaldar(string rutaAsset)
        {
            try
            {
                var origen = Path.GetFullPath(Path.Combine(Application.dataPath, "..", rutaAsset));
                if (!File.Exists(origen)) return null;
                Directory.CreateDirectory(Carpeta);
                var nombre = Path.GetFileNameWithoutExtension(origen);
                var destino = Path.Combine(Carpeta, nombre + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
                File.Copy(origen, destino, true);
                var viejos = Directory.GetFiles(Carpeta, nombre + "_*.unity").OrderByDescending(f => f).Skip(Conservar);
                foreach (var v in viejos) File.Delete(v);
                return destino;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RespaldoDeEscenas: no se pudo respaldar " + rutaAsset + ": " + e.Message);
                return null;
            }
        }

        [MenuItem("Strategic Point/Escenas/Abrir carpeta de respaldos")]
        static void AbrirCarpeta()
        {
            Directory.CreateDirectory(Carpeta);
            EditorUtility.RevealInFinder(Carpeta);
        }
    }
}
