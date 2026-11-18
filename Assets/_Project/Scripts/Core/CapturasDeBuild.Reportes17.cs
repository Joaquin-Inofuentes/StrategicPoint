using System.Collections;
using UnityEngine;

namespace SP.Core
{
    // R14: recrea la camara exacta de los 31 reportes del 17/11 (F2 del tester) para tener el ANTES y el DESPUES de cada nota.
    // Pose = camPos / camRot / fov que quedaron en el JSON del reporte (Docs/RONDA_14/reportes_17_11). Solo geometria estatica: la camara es libre.
    public sealed partial class CapturasDeBuild
    {
        // { numero, x, y, z, rotX, rotY, fov }
        static readonly float[][] PosesReportes17 = new[]
        {
            new[] { 1f, -14.47f, 3.83f, -31.28f, 22.05f, 92.98f, 60f },   // Huir
            new[] { 2f, 51.99f, 3.42f, -25.78f, 15.75f, 78.43f, 60f },   // Huir
            new[] { 3f, 45.63f, 3.59f, -31.33f, 18.3f, 30.63f, 60f },   // Huir
            new[] { 4f, 9.78f, 3.26f, 17.08f, 13.5f, 44.23f, 60f },   // Huir
            new[] { 5f, -11.01f, 3.53f, 16.53f, 17.4f, 333.82f, 60f },   // Huir
            new[] { 6f, -13.2f, 3.98f, 19.39f, 24.31f, 2.67f, 60f },   // Huir
            new[] { 7f, -151.14f, 2.05f, 274.84f, 3.01f, 115.16f, 18.7f },   // Huir
            new[] { 8f, 90.57f, 2.06f, 370.15f, 4.97f, 257.72f, 18.7f },   // Huir
            new[] { 9f, 257.96f, 2.19f, 224.37f, 346.95f, 80.55f, 15.8f },   // Huir
            new[] { 10f, 257.71f, 2.48f, 231.55f, 2.1f, 99.48f, 60f },   // Resistir
            new[] { 11f, 246.16f, 2.33f, 229.65f, 0f, 90f, 60f },   // Resistir
            new[] { 12f, 280.55f, 3.04f, 225.74f, 10.2f, 81.3f, 60f },   // Resistir
            new[] { 13f, 331.34f, 3.71f, 230.22f, 20.12f, 41.09f, 60f },   // Resistir
            new[] { 14f, 330.18f, 3.36f, 229.57f, 14.85f, 37.2f, 60f },   // Resistir
            new[] { 15f, 329.48f, 3.73f, 231.63f, 20.55f, 307.95f, 60f },   // Resistir
            new[] { 16f, 324.36f, 8.88f, 266.64f, 0.75f, 142.05f, 60f },   // Resistir
            new[] { 17f, 325.98f, 15.46f, 259.15f, 1.8f, 320.85f, 60f },   // Resistir
            new[] { 18f, 304.19f, 16.91f, 270.87f, 23.21f, 298.35f, 60f },   // Resistir
            new[] { 19f, 7.92f, 4.02f, -359.33f, 25.05f, 308.55f, 60f },   // Infiltrar
            new[] { 20f, 3.28f, 2.72f, -364.75f, 5.55f, 192.15f, 60f },   // Infiltrar
            new[] { 21f, -1.24f, 3.04f, -346.83f, 10.2f, 4.5f, 60f },   // Infiltrar
            new[] { 22f, 19.05f, 1.29f, -255.28f, 357.23f, 322.69f, 32.2f },   // Infiltrar
            new[] { 23f, 1.69f, 5.47f, -226.33f, 51.66f, 335.45f, 60f },   // Infiltrar
            new[] { 24f, -13.82f, 3.05f, -180.98f, 10.43f, 161.24f, 60f },   // Puestos
            new[] { 25f, -15.74f, 3.02f, -185.41f, 9.96f, 107.9f, 60f },   // Puestos
            new[] { 26f, 8f, 7.3f, -182.5f, 13.27f, 327.39f, 50f },   // CentroDeDatos
            new[] { 27f, 202.92f, 12f, 62.26f, 17.98f, 354.08f, 60f },   // CentroDeDatos
            new[] { 28f, -122.65f, 14.16f, 168.96f, 28.34f, 39.58f, 60f },   // CentroDeDatos
            new[] { 29f, -175.34f, 12f, 138.58f, 28.34f, 39.58f, 60f },   // CentroDeDatos
            new[] { 30f, 296.86f, 97.97f, 186.78f, 62.69f, 48.42f, 60f },   // CentroDeDatos
            new[] { 31f, 303.8f, 13.43f, 229.78f, 15f, 97.53f, 60f },   // CentroDeDatos

        };

        public static string[] IdsReportes17()
        {
            var ids = new string[PosesReportes17.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = "reporte_" + ((int)PosesReportes17[i][0]).ToString("00");
            return ids;
        }

        IEnumerator Reportes17()
        {
            Saltar(4); yield return Espera(3f);
            foreach (var p in PosesReportes17)
            {
                var pos = new Vector3(p[1], p[2], p[3]);
                var fwd = Quaternion.Euler(p[4], p[5], 0f) * Vector3.forward;
                yield return Foto("reporte_" + ((int)p[0]).ToString("00"), pos, pos + fwd * 25f, p[6], false);
            }
        }
    }
}
