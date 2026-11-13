using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // Checks de los tres enemigos nuevos del 2026-11-13: pesado mas grande y violeta (950a), escudero (951a), perro (952a) y su ladrido (953a).
    // Fotos en Assets/Validacion~/v12_*.
    public static partial class ChecksBugs065
    {
        static Soldier ClonDeEnemigo(Vector3 pos, Vector3 haciaDir)
        {
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.GetComponent<EnemigoPesado>() == null && a.GetComponent<EnemigoEscudero>() == null && a.GetComponent<PerroEnemigo>() == null && a.name.StartsWith("Mansion_Enemigo_")) { molde = a; break; }
            if (molde == null) foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado")) { molde = a; break; }
            if (molde == null) return null;
            haciaDir.y = 0f; if (haciaDir.sqrMagnitude < 0.01f) haciaDir = Vector3.forward;
            var go = Object.Instantiate(molde.gameObject, pos, Quaternion.LookRotation(haciaDir.normalized), molde.transform.parent);
            go.SetActive(true);
            var r = go.GetComponent<Soldier>(); if (r != null) SP.Core.ApoyoEnElPiso.Apoyar(r.transform);
            return r;
        }

        // Los enemigos de la mansion aparecen por oleadas: se adelanta el reloj para que haya al menos uno que sirva de molde.
        static IEnumerable ConMolde()
        {
            var d = OperacionDirector.Instancia;
            if (d != null) typeof(OperacionDirector).GetField("relojMansion", NoPublico).SetValue(d, 100000f);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 12f)
            {
                foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.name.StartsWith("Mansion_Enemigo_") && a.GetComponent<EnemigoPesado>() == null) yield break;
                yield return null;
            }
        }

        // Aliados lejos (fuera de la vista de los enemigos) para que los modelos posen sin pelear ni lanzar cohetes.
        static void AlejarAliados(Vector3 desde)
        {
            int k = 0;
            foreach (var a in ActorRegistry.All)
                if (a != null && a.Team == TeamId.Player && a.gameObject.activeInHierarchy)
                {
                    var q = PuntoEnMalla(desde + Quaternion.Euler(0f, 120f * (k++ % 3), 0f) * Vector3.forward * 110f);
                    a.transform.position = q; SP.Core.ApoyoEnElPiso.Apoyar(a.transform);
                }
        }

        static Vector3 PuntoEnMalla(Vector3 p) => UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 6f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p;

        // Foto con el tiempo congelado (los actores quedan en su pose) y de vuelta al ritmo normal.
        static IEnumerable FotoCongelada(string archivo, Vector3 pos, Vector3 mira, float fov = 45f, int w = 1600, int h = 900)
        {
            float previo = Time.timeScale; Time.timeScale = 0f;
            var apagados = new List<Behaviour>(); var apagadosR = new List<Renderer>();
            // sin marcadores de pantalla (rombos de bando, barras de vida, etiquetas): la foto es del modelo
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) if (c != null && c.enabled && c.renderMode != RenderMode.ScreenSpaceOverlay) { c.enabled = false; apagados.Add(c); }
            var cara = typeof(DiamondGizmo).GetField("caraCompartida", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as Mesh;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude)) { if (mf == null) continue; var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue; if ((cara != null && mf.sharedMesh == cara) || mf.name == "AlertIndicator" || mf.name.Contains("Marker")) { mr.enabled = false; apagadosR.Add(mr); } }
            foreach (var x in FotoCamara(archivo, pos, mira, fov, w, h)) yield return x;
            foreach (var c in apagados) if (c != null) c.enabled = true;
            foreach (var r in apagadosR) if (r != null) r.enabled = true;
            Time.timeScale = previo;
        }

        // Primer punto de camara con linea libre hacia el centro (para que ni el aliado ni una pared tapen al perro).
        static Vector3 CamaraDespejada(Vector3 centro, params Vector3[] candidatos)
        {
            foreach (var c in candidatos) if (!Physics.CheckSphere(c, 0.6f, ~0, QueryTriggerInteraction.Ignore) && !Physics.Linecast(c, centro, ~0, QueryTriggerInteraction.Ignore)) return c;   // ni dentro de una pared ni con algo en medio
            foreach (var c in candidatos) if (!Physics.Linecast(c, centro, ~0, QueryTriggerInteraction.Ignore)) return c;
            return candidatos[0];
        }

        static float HueDe(Color c) { Color.RGBToHSV(c, out float h, out _, out _); return h; }

        // ---------------------------------------------------------------- 950a pesado: mas grande, mas lento, retumba y color violeta
        static IEnumerator Bug950a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            foreach (var x in ConMolde()) yield return x;
            var yo = Poseido();
            var baseP = PuntoEnMalla(yo.transform.position + yo.transform.forward * 14f);
            var fw = -yo.transform.forward; var der = Vector3.Cross(Vector3.up, fw).normalized; var adelanteFoto = fw;
            var normal = ClonDeEnemigo(baseP + der * -1.6f, fw);
            var pesado = ClonDeEnemigo(baseP + der * 0.4f, fw);
            if (normal == null || pesado == null) { Fin("FALLO sin molde"); yield break; }
            var p = EnemigoPesado.Aplicar(pesado);
            sb.Append($"[tras Aplicar: {pesado.transform.localScale:0.00} p!=null {p != null} nombre {pesado.name}] ");
            yield return null; sb.Append($"[1 cuadro: {pesado.transform.localScale:0.00}] ");
            // un aliado al lado para comparar los tres colores (aliado azul, enemigo rojo, pesado violeta)
            Soldier aliado = null; foreach (var a in ActorRegistry.All) if (a != null && a != yo && a.Team == TeamId.Player && a.Role != RoleType.Civilian && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy) { aliado = a; break; }
            if (aliado != null) { aliado.transform.position = baseP + der * 2.4f; aliado.transform.rotation = Quaternion.LookRotation(fw); }
            foreach (var x in Esperar(1.2f)) yield return x;
            if (normal.Brain != null) normal.Brain.Anulado = true; if (pesado.Brain != null) pesado.Brain.Anulado = true;
            normal.Motor.SetCrouching(false); pesado.Motor.SetCrouching(false);

            sb.Append($"[escalas pesado {pesado.transform.localScale:0.00} normal {normal.transform.localScale:0.00} molde-clon {normal.name}] ");
            { var smrD = pesado.GetComponentInChildren<SkinnedMeshRenderer>(true); var cadena = new StringBuilder(); var tt = smrD != null ? smrD.transform : null; var cap = pesado.GetComponent<CapsuleCollider>(); while (tt != null && tt != pesado.transform.parent) { cadena.Append($"{tt.name}(p {tt.localPosition:0.00} e {tt.localScale:0.00}) < "); tt = tt.parent; } sb.Append($"[jerarquia SMR: {cadena}; capsula {(cap != null ? cap.center.ToString("0.00") + " h" + cap.height.ToString("0.00") + " r" + cap.radius.ToString("0.00") : "-")}] "); }
            float ratio = pesado.transform.localScale.x / normal.transform.localScale.x, ratioY = pesado.transform.localScale.y / normal.transform.localScale.y;
            W8Ok(ref ok, ratio >= 1.18f && ratio <= 1.4f, sb, $"mas ancho x{ratio:0.00} y mas alto x{ratioY:0.00} que un enemigo comun (un poquito mas grande)");
            var smr = pesado.GetComponentInChildren<SkinnedMeshRenderer>(true); var smrN = normal.GetComponentInChildren<SkinnedMeshRenderer>(true);
            W8Ok(ref ok, smr != null && smr.sharedMaterial != null && smr.sharedMaterial.name == "MAT_Trimsheet_Pesado", sb, $"usa su propia paleta ({(smr != null && smr.sharedMaterial != null ? smr.sharedMaterial.name : "-")})");
            // el color: la celda del uniforme (8) es violeta; el enemigo comun es rojo y el aliado azul
            if (smr != null && smr.sharedMaterial != null && smr.sharedMaterial.GetTexture("_BaseMap") is Texture2D tx && tx.isReadable)
            {
                int lado = tx.width / 8; var c = tx.GetPixel((12 % 8) * lado + lado / 2, (12 / 8) * lado + lado / 2); float hue = HueDe(c);
                W8Ok(ref ok, hue > 0.72f && hue < 0.85f, sb, $"el uniforme es violeta (matiz {hue * 360f:0} grados; rojo=0, azul=220)");
            }
            else W8Ok(ref ok, false, sb, "no se pudo leer la textura del pesado");
            // pies en el piso: el escalado en altura no lo hunde ni lo levanta
            Physics.SyncTransforms();
            float pisoP = Physics.Raycast(pesado.transform.position + Vector3.up * 3f, Vector3.down, out var hp, 8f, ~0, QueryTriggerInteraction.Ignore) && !hp.collider.transform.IsChildOf(pesado.transform) ? hp.point.y : pesado.transform.position.y - 0.8f;
            float pisoN = Physics.Raycast(normal.transform.position + Vector3.up * 3f, Vector3.down, out var hn, 8f, ~0, QueryTriggerInteraction.Ignore) && !hn.collider.transform.IsChildOf(normal.transform) ? hn.point.y : normal.transform.position.y - 0.8f;
            var colP = pesado.GetComponent<Collider>(); var colN = normal.GetComponent<Collider>();
            float piesP = colP != null ? colP.bounds.min.y - pisoP : 9f, piesN = colN != null ? colN.bounds.min.y - pisoN : 9f;
            W8Ok(ref ok, Mathf.Abs(piesP - piesN) < 0.12f, sb, $"el collider del pesado apoya en el piso igual que el de un soldado normal (pesado {piesP:+0.00;-0.00} m, normal {piesN:+0.00;-0.00} m)");
            float altoP = smr != null ? smr.bounds.size.y : 0f, altoN = smrN != null ? smrN.bounds.size.y : 1f;
            sb.Append($"[alto {altoP:0.00} m contra {altoN:0.00} m] ");
            W8Ok(ref ok, altoP > altoN * 1.05f, sb, "es mas alto que un enemigo comun");
            // mas pesado: camina mas lento y cada paso retumba
            float vN = normal.Motor.MoveSpeed, vP = pesado.Motor.MoveSpeed;
            W8Ok(ref ok, vP < vN * 0.8f, sb, $"camina mas lento ({vP:0.0} m/s contra {vN:0.0} m/s)");
            int pasos0 = p.PasosPesados; var origen = pesado.transform.position;
            for (int i = 0; i < 90; i++) { pesado.Motor.Move(pesado.transform.forward, 0.05f); int cuadro = Time.frameCount; float t0 = Time.realtimeSinceStartup; while (Time.frameCount == cuadro && Time.realtimeSinceStartup - t0 < 0.5f) yield return null; }   // un paso por cuadro real (en batch el check puede ir mas rapido que el juego)
            W8Ok(ref ok, p.PasosPesados > pasos0, sb, $"cada paso retumba ({p.PasosPesados - pasos0} pasos en {(pesado.transform.position - origen).magnitude:0.0} m)");
            var clip = GenericSfx.Get(SfxKind.PasoPesado);
            W8Ok(ref ok, clip != null && clip.length > 0.3f, sb, $"el paso suena (clip de {(clip != null ? clip.length : 0f):0.00} s)");
            W8Ok(ref ok, pesado.Health.MaxHealth == (normal.Health.MaxHealth) * EnemigoPesado.MultiplicadorDeVida, sb, $"vida {pesado.Health.MaxHealth} (x{EnemigoPesado.MultiplicadorDeVida})");

            // fotos: los tres juntos, de frente, de perfil, de espalda y primer plano (con el cerebro vivo un momento para que tomen su pose de pie)
            pesado.transform.position = baseP + der * 0.4f; normal.transform.position = baseP + der * -1.6f;
            if (normal.Brain != null) normal.Brain.Anulado = false; if (pesado.Brain != null) pesado.Brain.Anulado = false;
            AlejarAliados(baseP);
            foreach (var x in Esperar(3.0f)) yield return x;   // se disipan los efectos de los disparos anteriores
            if (normal.Brain != null) normal.Brain.Anulado = true; if (pesado.Brain != null) pesado.Brain.Anulado = true;
            pesado.transform.position = baseP + der * 0.4f; normal.transform.position = baseP + der * -1.6f; SP.Core.ApoyoEnElPiso.Apoyar(pesado.transform); SP.Core.ApoyoEnElPiso.Apoyar(normal.transform);
            var centro = pesado.transform.position + Vector3.up * 1.0f; var adelante = fw;
            foreach (var x in FotoCongelada("v12_pesado_comparacion.jpg", baseP - adelante * 4.6f + Vector3.up * 1.5f + der * 0.4f, baseP + Vector3.up * 1.0f + der * 0.4f, 52f, 1920, 1080)) yield return x;
            foreach (var x in FotoCongelada("v12_pesado_frente.jpg", centro + adelante * 3.4f + Vector3.up * 0.3f, centro + Vector3.up * 0.1f, 42f)) yield return x;
            foreach (var x in FotoCongelada("v12_pesado_perfil.jpg", centro + der * 3.8f + Vector3.up * 0.2f, centro, 42f)) yield return x;
            foreach (var x in FotoCongelada("v12_pesado_espalda.jpg", centro - adelante * 3.6f + der * 1.2f + Vector3.up * 0.4f, centro, 42f)) yield return x;
            foreach (var x in FotoCongelada("v12_pesado_detalle.jpg", centro - adelante * 2.4f + der * 0.8f + Vector3.up * 0.5f, centro + Vector3.up * 0.35f, 36f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 951a escudero: el escudo frena lo de frente, lo de atras o el costado le pega; gira lento; dispara con pistola
        static IEnumerator Bug951a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            foreach (var x in ConMolde()) yield return x;
            var yo = Poseido();
            var baseP = PuntoEnMalla(yo.transform.position + yo.transform.forward * 14f);
            var fw = -yo.transform.forward; var der = Vector3.Cross(Vector3.up, fw).normalized;
            var e = ClonDeEnemigo(baseP, fw); var normal = ClonDeEnemigo(baseP + der * 6f, fw);
            if (e == null || normal == null) { Fin("FALLO sin molde"); yield break; }
            int vidaBase = e.Health.MaxHealth;
            var esc = EnemigoEscudero.Aplicar(e);
            foreach (var x in Esperar(1.2f)) yield return x;
            if (e.Brain != null) e.Brain.Anulado = true; if (normal.Brain != null) normal.Brain.Anulado = true;
            e.Motor.SetCrouching(false); normal.Motor.SetCrouching(false);
            W8Ok(ref ok, esc != null && e.name.StartsWith("Escudero") && esc.Escudo != null, sb, "se creo el escudero con su escudo");
            W8Ok(ref ok, e.Weapon != null && e.Weapon.CurrentWeaponKind == WeaponKind.Pistol && e.Weapon.Loadout.Count == 1, sb, $"dispara con una pistola ({(e.Weapon != null ? e.Weapon.CurrentWeaponKind.ToString() : "-")})");
            W8Ok(ref ok, e.Health.MaxHealth == vidaBase * EnemigoEscudero.MultiplicadorDeVida, sb, $"vida {vidaBase} -> {e.Health.MaxHealth}");
            var smr = e.GetComponentInChildren<SkinnedMeshRenderer>(true);
            W8Ok(ref ok, smr != null && smr.sharedMaterial != null && smr.sharedMaterial.name == "MAT_Trimsheet_Escudero", sb, $"paleta propia grafito y plata ({(smr != null && smr.sharedMaterial != null ? smr.sharedMaterial.name : "-")})");

            // escudo: tiro de frente (blanco a 10 m delante) -> 0 de dano; de costado a 90 grados, de espalda -> dano
            var frente = e.transform.forward; int vida0 = e.Health.Current;
            yo.transform.position = e.transform.position + frente * 10f;
            e.Health.TakeDamage(30, yo.Id);
            W8Ok(ref ok, e.Health.Current == vida0 && esc.Bloqueados == 1, sb, $"un tiro de frente lo frena el escudo (vida {vida0} -> {e.Health.Current}, bloqueados {esc.Bloqueados})");
            foreach (var x in FotoCongelada("v12_escudero_bloqueo.jpg", e.transform.position + frente * 3.4f + Vector3.Cross(Vector3.up, frente) * 1.6f + Vector3.up * 0.7f, e.transform.position + Vector3.up * 0.8f, 45f)) yield return x;
            yo.transform.position = e.transform.position + Vector3.Cross(Vector3.up, frente) * 10f;   // costado (90 grados)
            e.Health.TakeDamage(30, yo.Id);
            W8Ok(ref ok, e.Health.Current < vida0 && esc.Pasaron == 1, sb, $"un tiro de costado le pega (vida {vida0} -> {e.Health.Current})");
            yo.transform.position = e.transform.position - frente * 10f;   // espalda
            e.Health.TakeDamage(30, yo.Id);
            int vLado = e.Health.Current; W8Ok(ref ok, vLado < vida0 && esc.Pasaron == 2 && esc.Bloqueados == 1, sb, $"un tiro por la espalda le pega (vida {vLado})");
            yo.transform.position = e.transform.position + frente * 10f + Vector3.Cross(Vector3.up, frente) * 8f;   // ~39 grados: dentro del escudo
            int v1 = e.Health.Current; e.Health.TakeDamage(30, yo.Id);
            W8Ok(ref ok, e.Health.Current == v1, sb, "a 39 grados de la frente tambien lo cubre el escudo");
            e.Health.TakeDamage(30, -1);
            W8Ok(ref ok, e.Health.Current < v1, sb, "una explosion o un vehiculo (origen desconocido) si pasa");

            // giro lento: gira hacia atras durante 0,5 s; el soldado comun gira mucho mas
            var objetivo = e.transform.position - frente * 8f; var objN = normal.transform.position - normal.transform.forward * 8f;
            float yawE0 = e.transform.eulerAngles.y, yawN0 = normal.transform.eulerAngles.y;
            e.Motor.LookTowards(objetivo, 0.5f); normal.Motor.LookTowards(objN, 0.5f); yield return null;   // medio segundo de giro de una sola vez
            float giroE = Mathf.Abs(Mathf.DeltaAngle(yawE0, e.transform.eulerAngles.y)), giroN = Mathf.Abs(Mathf.DeltaAngle(yawN0, normal.transform.eulerAngles.y));
            W8Ok(ref ok, giroE < giroN * 0.6f && giroE > 1f, sb, $"demora en girar: {giroE:0} grados contra {giroN:0} del soldado comun en el mismo tiempo (factor de giro {e.Motor.FactorDeGiro:0.00})");
            e.Health.Initialize(e.Id, e.Health.MaxHealth); e.Health.Invulnerable = true;   // los aliados lo matarian antes de medir el flanqueo
            // vuelve a mirar de frente para la prueba de fuego
            e.transform.rotation = Quaternion.LookRotation(fw);
            // dispara detras del escudo: con el cerebro encendido y el blanco delante, gasta municion
            if (e.Brain != null) { e.Brain.Anulado = false; }
            yo.transform.position = PuntoEnMalla(e.transform.position + fw * 9f);
            int ammo0 = e.Weapon.CurrentAmmo; float t0 = Time.realtimeSinceStartup; float tDisparo = -1f;
            while (Time.realtimeSinceStartup - t0 < 14f) { yield return null; if (e.Weapon.CurrentAmmo < ammo0 && tDisparo < 0f) { tDisparo = Time.realtimeSinceStartup - t0; break; } }
            W8Ok(ref ok, tDisparo >= 0f, sb, $"dispara su pistola al ver al blanco (primer tiro a los {tDisparo:0.0} s)");
            // sorprendido por la espalda: se mide cuanto tarda en encarar y disparar
            e.Weapon.RestaurarMunicion(e.Weapon.MagazineSize);
            Vector3 detras = PuntoEnMalla(e.transform.position - e.transform.forward * 7f);
            for (int k = 0; k < 12; k++)
            {
                var cand = PuntoEnMalla(e.transform.position + Quaternion.Euler(0f, 180f + (k % 2 == 0 ? 1 : -1) * (k / 2) * 12f, 0f) * e.transform.forward * 7f);
                if (!Physics.Linecast(e.transform.position + Vector3.up * 0.8f, cand + Vector3.up * 0.8f, ~0, QueryTriggerInteraction.Ignore)) { detras = cand; break; }
            }
            yo.transform.position = detras;
            ammo0 = e.Weapon.CurrentAmmo; t0 = Time.realtimeSinceStartup; float tFlanco = -1f;
            while (Time.realtimeSinceStartup - t0 < 16f) { yield return null; if (e.Weapon.CurrentAmmo < ammo0) { tFlanco = Time.realtimeSinceStartup - t0; break; } }
            W8Ok(ref ok, tFlanco > 0.9f && tFlanco < 16f, sb, $"flanqueado por la espalda tarda {(tFlanco < 0f ? ">16" : tFlanco.ToString("0.0"))} s en responder (tiempo de girar; estado {(e.Brain != null ? e.Brain.State.ToString() : "-")}, angulo al blanco {Vector3.Angle(e.transform.forward, yo.transform.position - e.transform.position):0})");
            // fotos del escudero: de frente, perfil y de espalda, congelado
            e.Brain.Anulado = false; if (normal.Brain != null) normal.Brain.Anulado = false; e.Health.Invulnerable = false;
            e.transform.position = baseP; e.transform.rotation = Quaternion.LookRotation(fw); normal.transform.position = baseP + der * 3.2f; normal.transform.rotation = Quaternion.LookRotation(fw);
            AlejarAliados(baseP);   // los blancos lejos: no pelean, solo toman su pose de pie
            foreach (var x in Esperar(3.0f)) yield return x;   // se disipan los efectos de los disparos anteriores
            e.Brain.Anulado = true; if (normal.Brain != null) normal.Brain.Anulado = true;
            e.transform.position = baseP; e.transform.rotation = Quaternion.LookRotation(fw); normal.transform.position = baseP + der * 3.2f; SP.Core.ApoyoEnElPiso.Apoyar(e.transform); SP.Core.ApoyoEnElPiso.Apoyar(normal.transform);
            var c = e.transform.position + Vector3.up * 1.0f;
            foreach (var x in FotoCongelada("v12_escudero_frente.jpg", c + fw * 2.8f + Vector3.up * 0.1f, c, 45f)) yield return x;
            foreach (var x in FotoCongelada("v12_escudero_perfil.jpg", c + der * 3.6f, c, 45f)) yield return x;
            foreach (var x in FotoCongelada("v12_escudero_espalda.jpg", c - fw * 2.8f + der * 0.9f + Vector3.up * 0.4f, c, 45f)) yield return x;
            foreach (var x in FotoCongelada("v12_escudero_comparacion.jpg", baseP + fw * 5.5f + Vector3.up * 1.5f + der * 1.5f, baseP + der * 1.5f + Vector3.up * 1.0f, 50f, 1920, 1080)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 952a perro: merodea caminando, ladra, carga a gran velocidad, salta y muerde; muere y cae
        static IEnumerator Bug952a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            foreach (var x in ConMolde()) yield return x;
            var yo = Poseido();
            // el perro empieza lejos (mas de 38 m de cualquier aliado) para ver primero su caminata
            var lejos = PuntoEnMalla(yo.transform.position + new Vector3(0f, 0f, 70f));
            var s = ClonDeEnemigo(lejos, -yo.transform.forward);
            if (s == null) { Fin("FALLO sin molde"); yield break; }
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a != s && a.GetComponent<PerroEnemigo>() == null) a.gameObject.SetActive(false);   // sin otros enemigos que lo tapen
            var perro = PerroEnemigo.Aplicar(s);
            s.Health.Invulnerable = true;   // los aliados lo matarian a tiros antes de poder medirlo
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, perro != null && s.name.StartsWith("Perro") && perro.Modelo != null, sb, "se creo el perro con su modelo");
            int piezas = perro.Modelo != null ? perro.Modelo.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
            W8Ok(ref ok, piezas >= 40, sb, $"el modelo tiene {piezas} piezas (cuerpo, cabeza con mandibula y dientes, orejas, cola, 4 patas articuladas, collar con puas)");
            W8Ok(ref ok, s.Health.MaxHealth == PerroEnemigo.Vida, sb, $"vida {s.Health.MaxHealth}");
            var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
            W8Ok(ref ok, smr == null || !smr.enabled, sb, "el cuerpo humano esta oculto");
            var col = s.GetComponent<Collider>(); float altoCol = col != null ? col.bounds.size.y : 9f;
            W8Ok(ref ok, altoCol < 1.2f, sb, $"su collider es de perro: {altoCol:0.00} m de alto");
            W8Ok(ref ok, HitboxCabeza.De(s) == null, sb, "sin cabeza humana invisible (no hay headshot fantasma sobre su lomo)");

            // caminata: merodea a paso lento y las patas se mueven
            var p0 = s.transform.position; Vector3 pAnt = p0; float camino = 0f; float tIni = Time.realtimeSinceStartup; float maxMarcha = 0f, sumaV = 0f; int nV = 0; bool fotoCamina = false; int pasos0 = perro.Pasos;
            while (Time.realtimeSinceStartup - tIni < 9f)
            {
                yield return null; camino += Vector3.Distance(new Vector3(pAnt.x, 0f, pAnt.z), new Vector3(s.transform.position.x, 0f, s.transform.position.z)); pAnt = s.transform.position; if (perro.VelocidadActual < 6f) { sumaV += perro.VelocidadActual; nV++; maxMarcha = Mathf.Max(maxMarcha, perro.VelocidadActual); }
                if (!fotoCamina && Time.realtimeSinceStartup - tIni > 3.5f && perro.VelocidadActual > 0.5f) { fotoCamina = true; var q = s.transform.position; var d = perro.Modelo.position + Vector3.up * 0.5f; foreach (var x in FotoCongelada("v12_perro_camina.jpg", CamaraDespejada(d, d + s.transform.right * 3.6f + Vector3.up * 0.7f, d - s.transform.right * 3.6f + Vector3.up * 0.7f, d + s.transform.right * 5f + s.transform.forward * 2f + Vector3.up * 1.2f, d - s.transform.right * 5f + s.transform.forward * 2f + Vector3.up * 1.2f, d - s.transform.forward * 4.5f + Vector3.up * 1.4f, d + s.transform.forward * 4.5f + Vector3.up * 1.4f, d + Vector3.up * 5f + s.transform.right * 2f), d, 40f)) yield return x; }
            }
            var cab = perro.Modelo.Find("Cuerpo/Cabeza");
            if (cab != null)
            {
                var hp = cab.position; var fr = s.transform.forward; var lt = Vector3.Cross(Vector3.up, fr).normalized;
                foreach (var x in FotoCongelada("v12_perro_cabeza.jpg", CamaraDespejada(hp, hp + fr * 1.5f + lt * 0.5f + Vector3.up * 0.2f, hp + lt * 1.5f + Vector3.up * 0.15f), hp, 38f)) yield return x;
                foreach (var x in FotoCongelada("v12_perro_cuerpo.jpg", CamaraDespejada(hp, hp - fr * 0.5f + lt * 2.6f + Vector3.up * 0.1f, hp - fr * 0.5f - lt * 2.6f + Vector3.up * 0.1f), hp - fr * 0.4f - Vector3.up * 0.35f, 42f)) yield return x;
            }
            W8Ok(ref ok, perro.Estado == PerroEnemigo.Fase.Merodea, sb, $"sin blancos a la vista merodea ({perro.Estado})");
            float medMarcha = nV > 0 ? sumaV / nV : 0f; W8Ok(ref ok, medMarcha > 0.5f && medMarcha < 2.6f, sb, $"camina a paso lento (promedio {medMarcha:0.0} m/s; velocidad del motor {s.Motor.MoveSpeed:0.0}, recorrio {camino:0.0} m en 9 s, pedidos al motor {perro.MetrosPedidos:0.0} m)");
            W8Ok(ref ok, perro.Pasos > pasos0, sb, $"suena cada pisada de pata ({perro.Pasos - pasos0} pasos)");

            // la carga: aparece a 32 m del jugador
            Time.timeScale = 1f;
            // punto de partida a 28-36 m del aliado MAS CERCANO (dentro de su vision), con camino completo y sin paredes de por medio
            Vector3 inicio = PuntoEnMalla(yo.transform.position + yo.transform.forward * 30f); var dirA = yo.transform.forward; bool hallado = false;
            foreach (float radio in new[] { 30f, 36f, 26f, 42f, 50f })
            {
                for (int k = 0; k < 16 && !hallado; k++)
                {
                    var d16 = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward; var cand = PuntoEnMalla(yo.transform.position + d16 * radio);
                    Soldier cerca = null; float md = 1e9f;
                    foreach (var a2 in ActorRegistry.All) if (a2 != null && a2.Team == TeamId.Player && a2.Health != null && a2.Health.IsAlive && a2.gameObject.activeInHierarchy) { float dd2 = Vector3.Distance(a2.transform.position, cand); if (dd2 < md) { md = dd2; cerca = a2; } }
                    if (cerca == null || md < 26f || md > 36f) continue;
                    var tr = new UnityEngine.AI.NavMeshPath(); bool hay = UnityEngine.AI.NavMesh.CalculatePath(cand, cerca.transform.position, UnityEngine.AI.NavMesh.AllAreas, tr) && tr.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                    if (hay && !Physics.Linecast(cand + Vector3.up * 0.5f, cerca.transform.position + Vector3.up * 0.5f, ~0, QueryTriggerInteraction.Ignore)) { inicio = cand; dirA = d16; hallado = true; }
                }
                if (hallado) break;
            }
            sb.Append($"[salida a {(inicio - yo.transform.position).magnitude:0} m] ");
            s.transform.position = inicio; s.transform.rotation = Quaternion.LookRotation(-dirA); SP.Core.ApoyoEnElPiso.Apoyar(s.transform);
            var ladridos0 = perro.Ladridos; bool fAlerta = false, fGalope = false, fSalto = false, fMuerde = false; bool vioAlerta = false, vioSalto = false, vioMuerde = false;
            float t0 = Time.realtimeSinceStartup; float tCarga = -1f, tSalto = -1f, vMax = 0f; Vector3 posSalto0 = Vector3.zero, posSaltoApice = Vector3.zero; float alturaMax = 0f; float pisoSalto = 0f;
            var cam = new Vector3();
            while (Time.realtimeSinceStartup - t0 < 30f && !(vioMuerde && fMuerde))
            {
                yield return null;
                if (perro.Estado == PerroEnemigo.Fase.Carga) vMax = Mathf.Max(vMax, perro.VelocidadActual);   // la velocidad de la carrera por el piso (el salto se mide aparte)
                var d = perro.Modelo.position + Vector3.up * 0.5f; var lado = Vector3.Cross(Vector3.up, s.transform.forward).normalized;
                if (perro.Estado == PerroEnemigo.Fase.Alerta) { vioAlerta = true; if (!fAlerta && Time.realtimeSinceStartup - t0 > 0f && perro.Ladridos > ladridos0) { fAlerta = true; sb.Append($"[foto ladra: vivo {s.Health.IsAlive}, rotZ {perro.Modelo.localEulerAngles.z:0}, estado {perro.Estado}, y {s.transform.position.y:0.0}] "); foreach (var x in FotoCongelada("v12_perro_ladra.jpg", CamaraDespejada(d, d + s.transform.forward * 2.8f + lado * 1.4f + Vector3.up * 0.6f, d + lado * 3f + Vector3.up * 0.8f, d - lado * 3f + Vector3.up * 0.8f), d + Vector3.up * 0.3f, 40f)) yield return x; } }
                if (perro.Estado == PerroEnemigo.Fase.Carga && perro.VelocidadActual > 8f) { if (tCarga < 0f) tCarga = Time.realtimeSinceStartup - t0; if (!fGalope) { fGalope = true; foreach (var x in FotoCongelada("v12_perro_galopa.jpg", CamaraDespejada(d, d + lado * 4.2f + Vector3.up * 0.9f, d - lado * 4.2f + Vector3.up * 0.9f, d + s.transform.forward * 4f + lado * 2f + Vector3.up * 0.8f), d, 45f)) yield return x; } }
                if (perro.Estado == PerroEnemigo.Fase.Salto)
                {
                    if (!vioSalto) { vioSalto = true; tSalto = Time.realtimeSinceStartup - t0; posSalto0 = s.transform.position; pisoSalto = posSalto0.y; }
                    alturaMax = Mathf.Max(alturaMax, s.transform.position.y - pisoSalto);
                    if (!fSalto && perro.ProgresoDelSalto > 0.45f) { fSalto = true; posSaltoApice = s.transform.position; foreach (var x in FotoCongelada("v12_perro_salta.jpg", CamaraDespejada(d, d + lado * 4.2f + Vector3.up * 0.4f, d - lado * 4.2f + Vector3.up * 0.4f), d, 45f)) yield return x; }
                }
                if (perro.Estado == PerroEnemigo.Fase.Muerde) { vioMuerde = true; if (!fMuerde && perro.Mordidas >= 1) { fMuerde = true; foreach (var x in FotoCongelada("v12_perro_muerde.jpg", CamaraDespejada(d, d + lado * 3.4f + Vector3.up * 1.0f, d - lado * 3.4f + Vector3.up * 1.0f, d - s.transform.forward * 3.2f + lado * 1.5f + Vector3.up * 1.2f), d + Vector3.up * 0.2f, 45f)) yield return x; } }
            }
            sb.Append($"[fin: {perro.Estado}, blanco {(perro.Blanco != null ? perro.Blanco.name : "-")} a {(perro.Blanco != null ? Vector3.Distance(perro.Blanco.transform.position, s.transform.position) : -1f):0.0} m] ");
            W8Ok(ref ok, vioAlerta, sb, "al ver al blanco se pone en alerta");
            W8Ok(ref ok, perro.Ladridos - ladridos0 >= 2, sb, $"ladra ({perro.Ladridos - ladridos0} ladridos en la persecucion)");
            W8Ok(ref ok, vMax >= 9f, sb, $"carga a gran velocidad (pico {vMax:0.0} m/s; un soldado corre a {(yo.Motor.MoveSpeed * SoldierMotor.FactorDeCarrera):0.0} m/s)");
            W8Ok(ref ok, vioSalto && perro.Saltos >= 1, sb, $"salta sobre el blanco ({perro.Saltos} saltos, en el segundo {tSalto:0.0})");
            W8Ok(ref ok, alturaMax > 0.7f, sb, $"el salto es un arco de {alturaMax:0.00} m de altura");
            W8Ok(ref ok, perro.Mordidas >= 1, sb, $"muerde ({perro.Mordidas} mordidas)");
            W8Ok(ref ok, yo.Health.Current == yo.Health.MaxHealth, sb, "con el modo dios el jugador no pierde vida");
            // sin modo dios la mordida duele
            ModoDios.Poner(false);
            var victima = perro.Blanco != null ? perro.Blanco : yo;
            int vida0 = victima.Health.Current; float tm = Time.realtimeSinceStartup; int morD0 = perro.Mordidas;
            while (Time.realtimeSinceStartup - tm < 5f && perro.Mordidas < morD0 + 2) yield return null;
            W8Ok(ref ok, victima.Health.Current < vida0, sb, $"sin modo dios la mordida le saca vida al aliado {victima.name} ({vida0} -> {victima.Health.Current})");
            ModoDios.Poner(true);
            // muerte
            s.Health.Invulnerable = false;
            s.Health.TakeDamage(9999, yo.Id);
            foreach (var x in Esperar(0.9f)) yield return x;
            W8Ok(ref ok, perro.Estado == PerroEnemigo.Fase.Muerto, sb, "al morir cae de costado");
            var dm = perro.Modelo.position + Vector3.up * 0.2f;
            foreach (var x in FotoCongelada("v12_perro_muerto.jpg", dm + Vector3.right * 3f + Vector3.up * 1.2f, dm, 45f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 953a el ladrido: forma de onda, tono y espectro (sale un WAV y un espectrograma)
        static void EscribirWav(string ruta, float[] d, int sr)
        {
            using (var bw = new BinaryWriter(File.Create(ruta)))
            {
                int n = d.Length;
                bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + n * 2); bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
                bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(n * 2);
                for (int i = 0; i < n; i++) bw.Write((short)(Mathf.Clamp(d[i], -1f, 1f) * 32000f));
            }
        }

        static IEnumerator Bug953a()
        {
            var sb = new StringBuilder(); bool ok = true;
            var dir = Path.Combine(Application.dataPath, "Validacion~"); Directory.CreateDirectory(dir);
            string[] nombres = { "Ladrido", "LadridoCorto", "GruniidoDePerro", "PasoDePerro", "PasoPesado", "ImpactoEscudo" };
            SfxKind[] tipos = { SfxKind.Ladrido, SfxKind.LadridoCorto, SfxKind.GruniidoDePerro, SfxKind.PasoDePerro, SfxKind.PasoPesado, SfxKind.ImpactoEscudo };
            for (int k = 0; k < tipos.Length; k++)
            {
                var clip = GenericSfx.Get(tipos[k]);
                if (clip == null) { W8Ok(ref ok, false, sb, nombres[k] + " no existe"); continue; }
                var d = new float[clip.samples]; clip.GetData(d, 0);
                float pico = 0f, rms = 0f; foreach (var v in d) { pico = Mathf.Max(pico, Mathf.Abs(v)); rms += v * v; }
                rms = Mathf.Sqrt(rms / d.Length);
                EscribirWav(Path.Combine(dir, "v12_" + nombres[k] + ".wav").Replace('\\', '/'), d, clip.frequency);
                sb.Append($"[{nombres[k]}: {clip.length:0.00} s, pico {pico:0.00}, rms {rms:0.000}] ");
                W8Ok(ref ok, pico > 0.5f && rms > 0.01f && !float.IsNaN(rms), sb, nombres[k] + " tiene senal util");
            }
            // tono del ladrido: autocorrelacion sobre 40 ms del primer ladrido
            var l = GenericSfx.Get(SfxKind.LadridoCorto); var dd = new float[l.samples]; l.GetData(dd, 0);
            int sr = l.frequency, ini = (int)(0.06f * sr), len = (int)(0.04f * sr); float mejor = 0f; int lagM = 0;
            for (int lag = sr / 1100; lag < sr / 150; lag++) { float c = 0f; for (int i = 0; i < len; i++) c += dd[ini + i] * dd[ini + i + lag]; if (c > mejor) { mejor = c; lagM = lag; } }
            float f0 = lagM > 0 ? (float)sr / lagM : 0f;
            W8Ok(ref ok, f0 > 280f && f0 < 900f, sb, $"el tono del ladrido ronda {f0:0} Hz (un perro mediano ladra entre 300 y 900 Hz)");
            yield return null;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
