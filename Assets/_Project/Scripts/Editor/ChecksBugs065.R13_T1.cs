using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Ronda 13, T1 (decisiones D2 y D3): el cuchillo es LETAL y sangra.
    //   Bug970a  letalidad: soldado de espaldas 1 golpe (aun con el dano propio al minimo); pesado 2 golpes dentro de 4 s; pesado 1 golpe y 5 s despues sigue vivo;
    //            soldado tapado por un muro a 1,5 m no recibe dano; escudero: de frente bloquea (D2) y de espalda muere.
    //   Bug971a  sangre y sonido: al conectar hay mas de 30 particulas de SangreFx_* a los 0,1 s, se reprodujo KnifeBlood y hay un decal de sangre; el fallo no genera sangre.
    public static partial class ChecksBugs065
    {
        // Un enemigo comun clonado, quieto, a 'dist' metros delante del jugador y mirando en la direccion 'mirada' (+1 = misma que el jugador = de espaldas).
        static Soldier R13EnemigoAnteElCuchillo(Soldier yo, float dist, bool deEspaldas)
        {
            var fw = yo.transform.forward; fw.y = 0f; fw.Normalize();
            var p = PuntoEnMalla(yo.transform.position + fw * dist);
            p.y = yo.transform.position.y;
            var e = ClonDeEnemigo(p, deEspaldas ? fw : -fw);
            if (e == null) return null;
            if (e.Brain != null) e.Brain.Anulado = true;
            return e;
        }

        static void R13Acercar(Soldier e, Soldier yo, float dist, bool deEspaldas)
        {
            var fw = yo.transform.forward; fw.y = 0f; fw.Normalize();
            var p = yo.transform.position + fw * dist;
            e.transform.position = new Vector3(p.x, e.transform.position.y, p.z);
            e.transform.rotation = Quaternion.LookRotation(deEspaldas ? fw : -fw);
        }

        static IEnumerator Bug970a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);   // el poseido no muere mientras se prueba; el cuchillo no depende del modo dios
            AjustesDeDano.ForzarParaPrueba(0.1f, 0.1f);                // el cuchillo ignora los coeficientes: con el dano propio al minimo igual mata
            var creados = new System.Collections.Generic.List<GameObject>();
            try
            {
                var yo = Poseido();
                if (yo == null) { Fin("FALLO no hay poseido"); yield break; }
                var dir0 = yo.transform.forward;

                // A) soldado de espaldas a 1,4 m: muere de un golpe
                var a = R13EnemigoAnteElCuchillo(yo, 1.4f, true);
                if (a == null) { Fin("FALLO sin molde de enemigo"); yield break; }
                creados.Add(a.gameObject);
                foreach (var x in Esperar(0.3f)) yield return x;
                R13Acercar(a, yo, 1.4f, true);
                int vidaA = a.Health.Current;
                yo.Weapon.Tick(1f); bool pego = yo.Weapon.TryMelee();
                W8Ok(ref ok, pego && vidaA > 0 && !a.Health.IsAlive, sb, $"soldado de espaldas muere de un golpe (vida {vidaA} -> {a.Health.Current}, dano propio 0,1)");
                foreach (var x in Esperar(1.3f)) yield return x;   // la camara lenta de la baja (1 s reales)

                // B) pesado: 1.er golpe lo deja a media vida, el 2.o a los 0,6 s lo mata
                var b = R13EnemigoAnteElCuchillo(yo, 1.4f, true);
                creados.Add(b.gameObject);
                var pb = EnemigoPesado.Aplicar(b);
                foreach (var x in Esperar(0.6f)) yield return x;
                R13Acercar(b, yo, 1.6f, true);
                int maxB = b.Health.MaxHealth;
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                bool vivoTras1 = b.Health.IsAlive; int vida1 = b.Health.Current;
                foreach (var x in Esperar(0.6f)) yield return x;
                R13Acercar(b, yo, 1.6f, true);
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                W8Ok(ref ok, pb != null && vivoTras1 && vida1 <= maxB / 2 + 1 && vida1 > 0, sb, $"pesado: 1.er golpe lo deja vivo a media vida ({vida1}/{maxB})");
                W8Ok(ref ok, !b.Health.IsAlive, sb, $"pesado: 2.o golpe dentro de 4 s lo mata (vida {b.Health.Current}, golpes {(pb != null ? pb.GolpesDeCuchillo : -1)})");

                // C) pesado: 1 golpe y 5 s despues otro -> sigue vivo (la ventana de 4 s vencio)
                var c = R13EnemigoAnteElCuchillo(yo, 1.4f, true);
                creados.Add(c.gameObject);
                var pc = EnemigoPesado.Aplicar(c);
                foreach (var x in Esperar(0.6f)) yield return x;
                R13Acercar(c, yo, 1.6f, true);
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                float t0 = Time.time;
                while (Time.time - t0 < 5f) { R13Acercar(c, yo, 1.6f, true); yield return null; }
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                W8Ok(ref ok, c.Health.IsAlive && pc != null && pc.GolpesDeCuchillo == 1, sb, $"pesado con 5 s entre golpes sigue vivo (vida {c.Health.Current}/{c.Health.MaxHealth}, golpes {(pc != null ? pc.GolpesDeCuchillo : -1)})");

                // D) soldado detras de un muro a 1,5 m: tapado, sin dano
                var d = R13EnemigoAnteElCuchillo(yo, 1.5f, true);
                creados.Add(d.gameObject);
                foreach (var x in Esperar(0.3f)) yield return x;
                R13Acercar(d, yo, 1.5f, true);
                var muro = GameObject.CreatePrimitive(PrimitiveType.Cube); muro.name = "R13_MuroDePrueba"; creados.Add(muro);
                var fw = yo.transform.forward; fw.y = 0f; fw.Normalize();
                muro.transform.position = yo.transform.position + fw * 0.8f + Vector3.up * 1.5f;
                muro.transform.rotation = Quaternion.LookRotation(fw); muro.transform.localScale = new Vector3(4f, 3.5f, 0.2f);
                Physics.SyncTransforms();
                int vidaD = d.Health.Current;
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                W8Ok(ref ok, d.Health.Current == vidaD && d.Health.IsAlive, sb, $"soldado tapado por un muro no recibe dano (vida {vidaD} -> {d.Health.Current})");
                Object.Destroy(muro);

                // E) escudero: de frente el escudo bloquea (D2); de espalda muere de un golpe
                var e = R13EnemigoAnteElCuchillo(yo, 1.4f, false);
                creados.Add(e.gameObject);
                var esc = EnemigoEscudero.Aplicar(e);
                foreach (var x in Esperar(1.2f)) yield return x;
                R13Acercar(e, yo, 1.4f, false);
                int vidaE = e.Health.Current;
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                W8Ok(ref ok, esc != null && e.Health.IsAlive && e.Health.Current == vidaE, sb, $"escudero de frente: el escudo bloquea la cuchillada (vida {vidaE} -> {e.Health.Current})");
                R13Acercar(e, yo, 1.4f, true);
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                W8Ok(ref ok, !e.Health.IsAlive, sb, $"escudero de espalda muere de un golpe (vida {e.Health.Current})");
                yo.transform.forward = dir0;
            }
            finally
            {
                AjustesDeDano.Liberar(); ModoDios.Poner(diosAntes);
                foreach (var g in creados) if (g != null) Object.Destroy(g);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static bool R13ClipAudible(AudioClip clip)
        {
            if (clip == null) return false;
            var d = new float[clip.samples * clip.channels]; clip.GetData(d, 0);
            float max = 0f; foreach (var v in d) { if (float.IsNaN(v)) return false; float a = v < 0f ? -v : v; if (a > max) max = a; }
            return max > 0.05f;
        }

        static IEnumerator Bug971a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var creados = new System.Collections.Generic.List<GameObject>();
            try
            {
                var yo = Poseido();
                if (yo == null) { Fin("FALLO no hay poseido"); yield break; }
                CuchilloFx.ResetearContadores(); SangreFx.ResetearContadores(); CuchilladaLenta.ResetearContadores();

                // fallo: sin nadie al alcance, ni particulas ni sonido de sangre ni mancha
                int manchas0 = DecalPool.CountOf(DecalKind.Sangre);
                yo.Weapon.Tick(1f); yo.Weapon.TryMelee();
                foreach (var x in Esperar(0.15f)) yield return x;
                W8Ok(ref ok, SangreFx.Rafagas == 0 && SangreFx.VivasAhora == 0 && CuchilloFx.SonidosDeSangre == 0 && DecalPool.CountOf(DecalKind.Sangre) == manchas0, sb,
                    $"el fallo no genera sangre (rafagas {SangreFx.Rafagas}, vivas {SangreFx.VivasAhora}, sonidos {CuchilloFx.SonidosDeSangre})");

                // acierto
                var a = R13EnemigoAnteElCuchillo(yo, 1.4f, true);
                if (a == null) { Fin("FALLO sin molde de enemigo"); yield break; }
                creados.Add(a.gameObject);
                foreach (var x in Esperar(0.3f)) yield return x;
                R13Acercar(a, yo, 1.4f, true);
                yo.Weapon.Tick(1f); bool pego = yo.Weapon.TryMelee();
                foreach (var x in Esperar(0.1f)) yield return x;
                int vivas = SangreFx.VivasAhora;
                W8Ok(ref ok, pego && !a.Health.IsAlive, sb, "la cuchillada conecta y mata");
                W8Ok(ref ok, vivas > 30, sb, $"hay mas de 30 particulas de SangreFx_* a los 0,1 s ({vivas})");
                W8Ok(ref ok, SangreFx.Rafagas == 1 && CuchilloFx.SonidosDeSangre == 1, sb, $"una rafaga y un sonido KnifeBlood (rafagas {SangreFx.Rafagas}, sonidos {CuchilloFx.SonidosDeSangre})");
                var clip = GenericSfx.Get(SfxKind.KnifeBlood);
                W8Ok(ref ok, clip != null && clip.length > 0.1f && R13ClipAudible(clip), sb, $"KnifeBlood es un clip valido y audible ({(clip != null ? clip.length.ToString("0.00") : "null")} s)");
                W8Ok(ref ok, DecalPool.CountOf(DecalKind.Sangre) >= manchas0 + 1, sb, $"hay una mancha de sangre en el piso ({DecalPool.CountOf(DecalKind.Sangre)})");
                bool nombres = false; foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>()) if (ps != null && ps.name.StartsWith("SangreFx_")) { nombres = true; break; }
                W8Ok(ref ok, nombres, sb, "los sistemas se llaman SangreFx_<n>");
            }
            finally
            {
                ModoDios.Poner(diosAntes);
                foreach (var g in creados) if (g != null) Object.Destroy(g);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
