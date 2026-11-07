using UnityEngine;

namespace SP.Presentation
{
    // Particulas y "feeling" de los disparos de la minigun del enemigo pesado (pedido 2026-11-07: "que tenga particulas sus disparos y mejora el feeling").
    // Por cada tiro: fogonazo grande naranja, chispas en abanico, un casquillo de laton que cae y, de a ratos, una columna de humo caliente de los canones.
    // Si el jugador esta cerca, la camara recibe un empujon chico en sentido opuesto al tirador (limitado por cuadro para que sea un temblor y no un golpe).
    public static class MinigunFx
    {
        public static int Disparos { get; private set; }
        static int cuadroDelEmpujon = -1; static int tirosPorEmpujon;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Disparos = 0; cuadroDelEmpujon = -1; tirosPorEmpujon = 0; }

        public static void Disparo(Vector3 boca, Vector3 dir)
        {
            if (!Application.isPlaying) return;
            Disparos++;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            var cam = SP.Core.CamaraPrincipal.Actual;
            float d2 = cam != null ? (cam.transform.position - boca).sqrMagnitude : 0f;
            if (cam != null && d2 > 90f * 90f) return;   // lejos: solo el sonido

            ImpactFx.SpawnMuzzleBlastLite(boca, dir, 1.35f, new Color(1f, 0.55f, 0.2f, 1f));
            var lateral = Vector3.Cross(dir, Vector3.up).normalized;
            for (int i = 0; i < 4; i++)
            {
                var v = dir * Random.Range(7f, 14f) + lateral * Random.Range(-4f, 4f) + Vector3.up * Random.Range(-1.5f, 3.5f);
                SpriteFx.Lanzar("spark_01", boca + dir * 0.35f, new Color(1f, Random.Range(0.6f, 0.85f), 0.25f, 1f), 0.22f, 0.04f, Random.Range(0.12f, 0.24f), 0f, v, 2.5f, 9);
            }
            // casquillo de laton: sale de costado y cae
            SpriteFx.Lanzar("spark_01", boca - dir * 0.3f + lateral * 0.1f, new Color(0.95f, 0.75f, 0.28f, 1f), 0.09f, 0.07f, 0.5f, Random.Range(-540f, 540f),
                lateral * Random.Range(2f, 3.5f) + Vector3.up * Random.Range(1.5f, 2.5f) + Vector3.down * 0.5f, 0.2f, 8);
            if (Disparos % 6 == 0)
                SpriteFx.Lanzar("smoke_04", boca + dir * 0.9f, new Color(0.55f, 0.53f, 0.5f, 0.45f), 0.5f, 2.2f, 1.1f, Random.Range(-30f, 30f), dir * 2.5f + Vector3.up * 1.4f, 1.2f, 6);

            // empujon de camara: solo a menos de 35 m
            var rig = SP.CameraSystem.CameraRig.Instance;
            if (rig != null && cam != null && d2 < 35f * 35f)
            {
                if (cuadroDelEmpujon != Time.frameCount) { cuadroDelEmpujon = Time.frameCount; tirosPorEmpujon = 0; }
                if (tirosPorEmpujon++ == 0) rig.KickDirectional(cam.transform.position - boca, Mathf.Lerp(0.03f, 0.008f, Mathf.Sqrt(d2) / 35f));
            }
        }
    }
}
