using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Ronda 13, T3 (decisiones D5): el dano propio / enemigo es un coeficiente global y la tarjeta DAÑO vive DENTRO de Configuraciones.
    //   Bug972a  coeficientes por via (k = 2 y k = 0,5): bala, explosion a soldado, a vehiculo, torre, enemigo a aliado, vehiculos; RestaurarVida(0) mata,
    //            DestruirPorGuion destruye con Propio 0,1, la herida de guion del tutorial quita exactamente lo pedido; los PlayerPrefs quedan intactos.
    //   Bug973a  seccion DAÑO: fuera == 0 en 3 resoluciones x 2 escalas de interfaz x 2 idiomas; un slider cambia AjustesDeDano; Restablecer vuelve a 1 / 0,5.
    public static partial class ChecksBugs065
    {
        // Cuanto baja la vida de 'h' con 'golpe' (la vida se pone en 100000 antes, para que nada muera).
        static int R13Baja(Health h, System.Action golpe)
        {
            h.Initialize(h.ActorId, 100000);
            int v0 = h.Current; golpe();
            return v0 - h.Current;
        }

        // Compara una via con k = 2 y con k = 0,5: la baja con 2 debe ser ~4 veces la de 0,5 (+-4 por redondeo) y las dos > 0.
        static bool R13ViaProporcional(ref bool ok, StringBuilder sb, string via, float esperadoAlto, float esperadoBajo, System.Func<int> medirConK)
        {
            AjustesDeDano.ForzarParaPrueba(2f, 2f); int alta = medirConK();
            AjustesDeDano.ForzarParaPrueba(0.5f, 0.5f); int baja = medirConK();
            bool bien = baja > 0 && System.Math.Abs(alta - 4 * baja) <= 4 && (esperadoAlto <= 0f || System.Math.Abs(alta - esperadoAlto) <= 2) && (esperadoBajo <= 0f || System.Math.Abs(baja - esperadoBajo) <= 2);
            return W8Ok(ref ok, bien, sb, $"{via}: k=2 -> {alta}, k=0,5 -> {baja}");
        }

        static IEnumerator Bug972a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; bool difActivaAntes = Dificultad.Activa;
            float prefP = PlayerPrefs.GetFloat("ajuste_dano_propio", -123f), prefE = PlayerPrefs.GetFloat("ajuste_dano_enemigo", -123f);
            ModoDios.Poner(false); Dificultad.Activa = false;
            var creados = new List<GameObject>();
            try
            {
                var yo = Poseido();
                if (yo == null) { Fin("FALLO no hay poseido"); yield break; }
                var enemigo = R13EnemigoAnteElCuchillo(yo, 6f, true);
                var aliado = Escuadrista(RoleType.Assault); if (aliado == yo) aliado = Escuadrista(RoleType.Medic);
                if (enemigo == null || aliado == null) { Fin("FALLO faltan piezas (enemigo/aliado)"); yield break; }
                creados.Add(enemigo.gameObject);
                foreach (var x in Esperar(0.3f)) yield return x;
                enemigo.Brain.Anulado = true;
                int idYo = yo.Id, idEnemigo = enemigo.Id;

                // bala: tu bando le pega a un enemigo (coeficiente PROPIO) y un enemigo le pega a tu bando (ENEMIGO); exacto: 100 x k
                R13ViaProporcional(ref ok, sb, "bala propia -> enemigo", 200f, 50f, () => R13Baja(enemigo.Health, () => enemigo.Health.TakeDamage(100, idYo)));
                R13ViaProporcional(ref ok, sb, "bala enemiga -> aliado", 200f, 50f, () => R13Baja(aliado.Health, () => aliado.Health.TakeDamage(100, idEnemigo)));
                // explosion (un soldado a pocos metros): misma cola de dano, el radio la modula pero la proporcion se conserva
                R13ViaProporcional(ref ok, sb, "explosion propia -> soldado", 0f, 0f, () => R13Baja(enemigo.Health, () => Projectile.ExplodeAt(enemigo.transform.position + Vector3.up * 0.8f, 4f, 100, idYo, TeamId.Player)));

                // vehiculos: un blindado cualquiera de la escena (el del jugador recibe del enemigo; uno enemigo, de tu bando)
                Vehicle propio = null, ajeno = null;
                foreach (var v in Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
                {
                    if (v == null || v.Health == null || !v.Health.IsAlive || v.IsDestroyed) continue;
                    if (v.Bando == TeamId.Player && propio == null) propio = v; else if (v.Bando == TeamId.Enemy && ajeno == null) ajeno = v;
                }
                if (propio != null)
                {
                    var vp = propio; float pisoAntes = vp.PisoDeVida01; var ajusteAntes = vp.AjusteDeDano; vp.PisoDeVida01 = 0f; vp.AjusteDeDano = null;
                    R13ViaProporcional(ref ok, sb, "enemigo -> vehiculo propio", 0f, 0f, () => R13Baja(vp.Health, () => vp.TakeDamage(100, idEnemigo)));
                    vp.PisoDeVida01 = pisoAntes; vp.AjusteDeDano = ajusteAntes; vp.Health.Initialize(vp.Health.ActorId, vp.Health.MaxHealth);
                }
                else sb.Append("(sin vehiculo propio en la escena: via omitida) ");
                if (ajeno != null)
                {
                    var va = ajeno; float pisoAntes = va.PisoDeVida01; var ajusteAntes = va.AjusteDeDano; va.PisoDeVida01 = 0f; va.AjusteDeDano = null;
                    R13ViaProporcional(ref ok, sb, "cohete propio directo -> vehiculo enemigo", 0f, 0f, () => R13Baja(va.Health, () => va.TakeDamage(100, idYo)));
                    R13ViaProporcional(ref ok, sb, "explosion propia -> vehiculo enemigo", 0f, 0f, () => R13Baja(va.Health, () => Projectile.ExplodeAt(va.transform.position + Vector3.up * 1f, 5f, 100, idYo, TeamId.Player)));
                    va.PisoDeVida01 = pisoAntes; va.AjusteDeDano = ajusteAntes; va.Health.Initialize(va.Health.ActorId, va.Health.MaxHealth);
                }
                else sb.Append("(sin vehiculo enemigo en la escena: via omitida) ");

                // torre destruible (D5): el coeficiente propio vale tambien contra ella; luminarias, barriles y coberturas no
                TorreDestruible torre = null; foreach (var t in TorreDestruible.Todas) if (t != null && !t.EstaCaida && t.Marca != null && t.Marca.CurrentHealth > 0) { torre = t; break; }
                if (torre != null)
                {
                    int Medir()
                    {
                        int v0 = torre.Marca.CurrentHealth;
                        Projectile.ExplodeAt(torre.transform.position + Vector3.up * 1.5f, 4f, 40, idYo, TeamId.Player);
                        return v0 - torre.Marca.CurrentHealth;
                    }
                    AjustesDeDano.ForzarParaPrueba(0.5f, 0.5f); int baja = Medir();
                    AjustesDeDano.ForzarParaPrueba(2f, 2f); int alta = Medir();
                    W8Ok(ref ok, baja > 0 && torre.Marca.CurrentHealth > 0 && System.Math.Abs(alta - 4 * baja) <= 6, sb, $"torre destruible: k=0,5 -> {baja}, k=2 -> {alta}");
                }
                else sb.Append("(sin torre destruible en esta escena: via omitida) ");

                // alcance: RestaurarVida(0) mata, DestruirPorGuion destruye con Propio 0,1, la herida de guion es exacta
                AjustesDeDano.ForzarParaPrueba(AjustesDeDano.PropioPorDefecto, AjustesDeDano.EnemigoPorDefecto);
                var otro = R13EnemigoAnteElCuchillo(yo, 8f, true); creados.Add(otro.gameObject);
                foreach (var x in Esperar(0.3f)) yield return x;
                otro.Health.RestaurarVida(0);
                W8Ok(ref ok, !otro.Health.IsAlive, sb, $"RestaurarVida(0) mata con los coeficientes por defecto (vida {otro.Health.Current})");
                AjustesDeDano.ForzarParaPrueba(0.1f, 0.1f);
                var victimaV = ajeno != null ? ajeno : propio;
                if (victimaV != null)
                {
                    victimaV.Health.Invulnerable = false; victimaV.PisoDeVida01 = 0f; victimaV.AjusteDeDano = null;
                    victimaV.DestruirPorGuion();
                    W8Ok(ref ok, victimaV.IsDestroyed && !victimaV.Health.IsAlive, sb, "DestruirPorGuion destruye al vehiculo con Propio 0,1");
                }
                else sb.Append("(sin vehiculo: DestruirPorGuion omitido) ");
                aliado.Health.Initialize(aliado.Id, 1000); int a0 = aliado.Health.Current;
                aliado.Health.AplicarDanoDeGuion(40);
                W8Ok(ref ok, a0 - aliado.Health.Current == 40, sb, $"la herida de guion del tutorial quita exactamente 40 con Enemigo 0,1 ({a0} -> {aliado.Health.Current})");
                W8Ok(ref ok, AjustesDeDano.Multiplicador(AjustesDeDano.IdGuion, aliado.Health) == 1f, sb, "el id de guion no se escala");
            }
            finally
            {
                AjustesDeDano.Liberar(); Dificultad.Activa = difActivaAntes; ModoDios.Poner(diosAntes);
                foreach (var g in creados) if (g != null) Object.Destroy(g);
            }
            W8Ok(ref ok, Mathf.Approximately(PlayerPrefs.GetFloat("ajuste_dano_propio", -123f), prefP) && Mathf.Approximately(PlayerPrefs.GetFloat("ajuste_dano_enemigo", -123f), prefE), sb, "los PlayerPrefs de dano quedaron intactos");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug973a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.0f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var pc = Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
            var sp = pc != null ? pc.transform.Find("SettingsPanel") : null;
            if (sp == null) { Fin("FALLO no hay SettingsPanel"); yield break; }
            var idioma = Loc.Actual; float escala = SP.UI.AjustesDeJuego.Escala;
            float prefP = PlayerPrefs.GetFloat("ajuste_dano_propio", -123f), prefE = PlayerPrefs.GetFloat("ajuste_dano_enemigo", -123f);
            bool estabaActivo = sp.gameObject.activeSelf;
            try
            {
                sp.gameObject.SetActive(true);
                SP.UI.PanelAjustesExtra.Preparar(sp.gameObject);
                var layout = sp.GetComponent<SP.UI.LayoutDeAjustes>();
                W8Ok(ref ok, layout != null && sp.Find("Sec_Dano") != null && sp.Find("SecSep_Dano") != null && sp.Find("Daño propio_Slider") != null && sp.Find("Daño enemigo_Slider") != null
                    && sp.Find("Daño propio_Chip") != null && sp.Find("RestablecerDano") != null, sb, "existe la seccion DAÑO con sus filas y el boton de restablecer");
                W8Ok(ref ok, sp.Find("AjusteDeDano") == null, sb, "ya no hay tarjeta suelta 'AjusteDeDano'");
                // resoluciones 1280x720, 1600x900 y 1920x1080 (16:9: el canvas es de 960x540 y se escala), x escala de interfaz 100 % y 125 % x es/en
                int total = 0, malos = 0; var primeros = new List<string>();
                var resol = new[] { new Vector2(1280, 720), new Vector2(1600, 900), new Vector2(1920, 1080) };
                foreach (var esc in new[] { 1f, 1.25f })
                    foreach (var lang in new[] { Idioma.Es, Idioma.En })
                    {
                        SP.UI.AjustesDeJuego.PonerEscala(esc);
                        if (Loc.Actual != lang) Loc.Poner(lang);
                        yield return null;
                        foreach (var r in resol)
                        {
                            var area = new Vector2(r.x, r.y) / (r.y / 540f) / esc;   // canvas escalado por altura y por la escala de interfaz
                            layout.Aplicar(area); total++;
                            Canvas.ForceUpdateCanvases();
                            var med = SP.UI.LayoutDeAjustes.MedirFuera(sp.gameObject, area);
                            if (med.fuera != 0) { malos++; if (primeros.Count < 6) primeros.Add($"{r} esc {esc} {lang}: fuera={med.fuera} peor={med.peor:0}%"); }
                        }
                    }
                W8Ok(ref ok, malos == 0, sb, $"fuera == 0 en {total} combinaciones ({malos} malas) {string.Join(" | ", primeros)}");
                Loc.Poner(Idioma.Es); SP.UI.AjustesDeJuego.PonerEscala(1f); layout.Aplicar();
                var real = SP.UI.LayoutDeAjustes.Diagnosticar(sp.gameObject, layout.AreaDelCanvas());
                W8Ok(ref ok, real.Count == 0, sb, $"Diagnosticar del canvas real sin defectos ({(real.Count > 0 ? real[0] : "-")})");

                // slider -> valor; numero editable; restablecer
                var sP = sp.Find("Daño propio_Slider").GetComponent<Slider>(); var sE = sp.Find("Daño enemigo_Slider").GetComponent<Slider>();
                var chip = sp.Find("Daño propio_Chip").GetComponent<InputField>();
                AjustesDeDano.ForzarParaPrueba(1f, 0.5f);   // no se escribe en PlayerPrefs durante la prueba
                sP.value = 1.8f; sE.value = 0.3f;
                W8Ok(ref ok, Mathf.Abs(AjustesDeDano.Propio - 1.8f) < 0.011f && Mathf.Abs(AjustesDeDano.Enemigo - 0.3f) < 0.011f, sb, $"arrastrar los sliders cambia AjustesDeDano ({AjustesDeDano.Propio:0.00} / {AjustesDeDano.Enemigo:0.00})");
                chip.text = "2,50"; chip.onEndEdit.Invoke("2,50");
                W8Ok(ref ok, Mathf.Abs(AjustesDeDano.Propio - 2.5f) < 0.011f && Mathf.Abs(sP.value - 2.5f) < 0.011f, sb, $"el numero editable (el chip) cambia el valor y el slider ({AjustesDeDano.Propio:0.00})");
                sp.Find("RestablecerDano").GetComponent<Button>().onClick.Invoke();
                W8Ok(ref ok, Mathf.Approximately(AjustesDeDano.Propio, 1f) && Mathf.Approximately(AjustesDeDano.Enemigo, 0.5f) && Mathf.Approximately(sP.value, 1f) && Mathf.Approximately(sE.value, 0.5f), sb, "Restablecer vuelve a 1,00 / 0,50");
                W8Ok(ref ok, Mathf.Approximately(PlayerPrefs.GetFloat("ajuste_dano_propio", -123f), prefP) && Mathf.Approximately(PlayerPrefs.GetFloat("ajuste_dano_enemigo", -123f), prefE), sb, "los PlayerPrefs de dano del usuario quedaron intactos");
            }
            finally
            {
                AjustesDeDano.Liberar();
                Loc.Poner(idioma); SP.UI.AjustesDeJuego.PonerEscala(escala);
                sp.gameObject.SetActive(estabaActivo);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
