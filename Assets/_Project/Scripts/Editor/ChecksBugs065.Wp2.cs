using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // WP2: audio. #065 muerte 3D y mas baja, #095 disparos 3D con distorsion y nada 2D salvo musica/ambiente/UI, #072 graves y chapa,
    // #093 musica de victoria con fade largo. NO se puede ESCUCHAR desde el CLI: se verifican datos objetivos (spatialBlend, rolloff,
    // volumenes, clips horneados, historial del director, energia por banda). Hay que estar en Play sobre SC_Operacion.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- utilidades de audio
        static List<AudioSource> FuentesSonando()
        {
            var l = new List<AudioSource>();
            foreach (var a in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (a != null && a.isPlaying && a.clip != null) l.Add(a);
            return l;
        }

        static string RutaDeValidacion(string archivo)
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "Validacion");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, archivo);
        }

        static HashSet<string> NombresDeClipsDelMundo()
        {
            var h = new HashSet<string>();
            var carpetas = new List<string>(AudioImportFix.Voces); carpetas.AddRange(AudioImportFix.Mundo);
            foreach (var c in carpetas)
                foreach (var clip in Resources.LoadAll<AudioClip>("Audio/Sfx/" + c)) { h.Add(clip.name); h.Add(clip.name + "_dist"); }
            foreach (var k in new[] { SfxKind.Shoot, SfxKind.Hit, SfxKind.Death, SfxKind.VehicleHit, SfxKind.CannonBody, SfxKind.CannonCrack, SfxKind.Wounded,
                SfxKind.ImpactMetal, SfxKind.ImpactDirt, SfxKind.ImpactStone, SfxKind.BulletWhizz, SfxKind.FootstepGrass, SfxKind.FootstepConcrete, SfxKind.Explosion,
                SfxKind.GrenadePin, SfxKind.GrenadeThrow, SfxKind.GrenadeBounce, SfxKind.KnifeSwing, SfxKind.KnifeHit, SfxKind.Jump, SfxKind.Land, SfxKind.BombPlant,
                SfxKind.BombTick, SfxKind.ImpactoPesado, SfxKind.BalaImpacto, SfxKind.DestruccionVehiculo, SfxKind.AmmoPickup, SfxKind.SeatChange })
                h.Add(k.ToString());
            h.Add("CannonBody+Sub"); h.Add("Explosion+Sub");
            foreach (WeaponKind w in Enum.GetValues(typeof(WeaponKind))) { h.Add("Shot_" + w); h.Add("Shot_" + w + "_dist"); }
            return h;
        }

        static bool EsClipDeMundo(HashSet<string> mundo, string nombre)
            => mundo.Contains(nombre) || nombre.StartsWith("Reload_") || nombre.StartsWith("Draw_") || nombre.StartsWith("Dry_");

        // Nombres de GameObject 2D que NO son del mundo (musica, ambiente, stingers/feedback de interfaz; documentados en el codigo).
        static readonly HashSet<string> Blanca2D = new HashSet<string>
        {
            "Estrategia", "Lucha", "Victoria", "Tension",                      // musica
            "SonidosDeOperacion",                                              // ambiente (lazo de viento)
            "StreakTone", "CritTone", "Heartbeat",                             // tonos de interfaz del jugador
            "BoardAll", "CameraSwoosh", "ObjetivoCumplido", "Logro",           // stingers de objetivo / cinematica
        };

        static float PicoDe(float[] d) { float p = 0f; foreach (var v in d) { float a = v < 0f ? -v : v; if (a > p) p = a; } return p; }
        static float RmsDe(float[] d) { double e = 0; foreach (var v in d) e += (double)v * v; return d.Length > 0 ? (float)Math.Sqrt(e / d.Length) : 0f; }

        // Amplitud media del tono 'f' en todo el clip (Goertzel), relativa al pico del clip.
        static double Goertzel(float[] d, int sr, double f)
        {
            double co = 2.0 * Math.Cos(2.0 * Math.PI * f / sr), s0, s1 = 0, s2 = 0;
            for (int i = 0; i < d.Length; i++) { s0 = d[i] + co * s1 - s2; s2 = s1; s1 = s0; }
            double p = s1 * s1 + s2 * s2 - co * s1 * s2;
            return 2.0 * Math.Sqrt(Math.Max(0.0, p)) / Math.Max(1, d.Length);
        }

        // Graves del clip: promedio de la amplitud de los tonos 30/45/60/90/120/150 Hz respecto del pico (0 = nada, mas = mas graves).
        static float BandaGrave(float[] mono, int sr)
        {
            float pico = Mathf.Max(0.0001f, PicoDe(mono));
            double suma = 0;
            foreach (var f in new[] { 30.0, 45.0, 60.0, 90.0, 120.0, 150.0 }) suma += Goertzel(mono, sr, f);
            return (float)(suma / 6.0 / pico);
        }

        static bool MuestrasMono(AudioClip c, out float[] mono)
        {
            mono = null;
            if (c == null) return false;
            if (c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            var d = new float[c.samples * c.channels];
            if (!c.GetData(d, 0)) return false;
            if (c.channels == 1) { mono = d; return true; }
            mono = new float[c.samples];
            for (int i = 0; i < c.samples; i++) { float a = 0; for (int k = 0; k < c.channels; k++) a += d[i * c.channels + k]; mono[i] = a / c.channels; }
            return true;
        }

        // Onda en PNG (maximo por columna, espejada) para dejar una evidencia visual del clip.
        static void GuardarOnda(string archivo, float[] mono, int sr, int w = 1200, int h = 260, float[] otra = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var fondo = new Color(0.07f, 0.08f, 0.1f, 1f);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = fondo;
            void Dibujar(float[] d, Color col, float escala)
            {
                int paso = Mathf.Max(1, d.Length / w);
                for (int x = 0; x < w; x++)
                {
                    float mx = 0f;
                    int i0 = x * paso, i1 = Mathf.Min(d.Length, i0 + paso);
                    for (int i = i0; i < i1; i++) { float a = Mathf.Abs(d[i]); if (a > mx) mx = a; }
                    int alto = Mathf.Clamp(Mathf.RoundToInt(mx * escala * (h / 2f - 2)), 0, h / 2 - 1);
                    for (int y = -alto; y <= alto; y++) px[(h / 2 + y) * w + x] = col;
                }
            }
            if (otra != null) Dibujar(otra, new Color(0.45f, 0.55f, 0.75f, 1f), 1f);
            Dibujar(mono, new Color(1f, 0.6f, 0.2f, 1f), 1f);
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(RutaDeValidacion(archivo), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static Soldier BuscarEnemigoVivo(Soldier excepto = null)
        {
            foreach (var a in ActorRegistry.All)
                if (a != null && a != excepto && a.Team == TeamId.Enemy && a.Role != RoleType.Civilian && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy) return a;
            return null;
        }

        static void Teletransportar(Soldier s, Vector3 destino)
        {
            if (NavMesh.SamplePosition(destino, out var h, 6f, NavMesh.AllAreas)) destino = h.position;
            var ag = s.GetComponent<NavMeshAgent>();
            if (ag != null && ag.enabled && ag.isOnNavMesh) ag.Warp(destino); else s.transform.position = destino;
        }

        // ---------------------------------------------------------------- #065 audio de la muerte
        static IEnumerator Bug065a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo = Poseido();
            var dir = AudioDirector.Instance;
            if (yo == null || dir == null) { Fin("FALLO sin soldado poseido o sin AudioDirector"); yield break; }

            // 1) Importacion: los clips de voz estan en mono (si no, el 3D los abre a los dos lados).
            int voces = 0, sinMono = 0;
            foreach (var r in AudioImportFix.Rutas(AudioImportFix.Voces)) { voces++; if (!AudioImportFix.EsMono(r)) sinMono++; }
            if (voces == 0 || sinMono > 0) ok = false;
            sb.Append($"clips Death/Wounded={voces} sin forceToMono={sinMono}{(voces > 0 && sinMono == 0 ? "" : " (MAL)")}; ");
            var muerte = new HashSet<string>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/Sfx/Death")) muerte.Add(c.name);

            // 2) Dos bajas a ~10 m del jugador: una por el propio jugador y otra por un aliado. Se muestrea 0.6 s.
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                for (int caso = 0; caso < 2; caso++)
                {
                    bool propia = caso == 0;
                    var e = BuscarEnemigoVivo();
                    if (e == null) { ok = false; sb.Append("sin enemigo vivo; "); break; }
                    var br = e.GetComponent<SP.Ai.AiBrain>(); if (br != null) br.enabled = false;
                    Teletransportar(e, yo.transform.position + yo.transform.forward * 10f);
                    yield return null; yield return null;
                    int atacante = propia ? yo.Id : -77;
                    int hist0 = AudioDirector.TotalReproducidos;
                    e.Health.TakeDamage(e.Health.Current + 999, atacante);
                    int voces3D = 0, mal = 0, logro3D = 0, logro2D = 0, logro2DFuerte = 0;
                    float volMax = 0f, volLogro3D = 0f, volLogro2D = 0f;
                    var vistos = new HashSet<AudioSource>();
                    for (int f = 0; f < 40; f++)
                    {
                        foreach (var a in FuentesSonando())
                        {
                            string n = a.clip.name;
                            if (muerte.Contains(n) && vistos.Add(a))
                            {
                                voces3D++;
                                volMax = Mathf.Max(volMax, a.volume);
                                bool bien = a.spatialBlend >= 0.99f && a.rolloffMode == AudioRolloffMode.Logarithmic && a.volume <= 0.55f
                                            && Mathf.Approximately(a.minDistance, 3f) && Mathf.Approximately(a.maxDistance, 60f);
                                if (!bien) { mal++; sb.Append($"[voz {a.name}: blend={a.spatialBlend:0.00} rolloff={a.rolloffMode} vol={a.volume:0.00} min={a.minDistance} max={a.maxDistance}] "); }
                            }
                            if (n == "Logro")
                            {
                                if (a.spatialBlend >= 0.99f) { if (vistos.Add(a)) { logro3D++; volLogro3D = Mathf.Max(volLogro3D, a.volume); } }
                                else { if (vistos.Add(a)) { logro2D++; volLogro2D = Mathf.Max(volLogro2D, a.volume); if (a.volume > 0.2f) logro2DFuerte++; } }
                            }
                        }
                        yield return null;
                    }
                    bool casoOk = voces3D >= 1 && mal == 0 && logro3D >= 1 && volLogro3D <= 0.36f && logro2DFuerte == 0 && (propia ? logro2D >= 1 : logro2D == 0);
                    if (!casoOk) ok = false;
                    sb.Append($"baja {(propia ? "del jugador" : "ajena")}: voces de muerte 3D={voces3D} (vol max {volMax:0.00}, mal={mal}), Logro 3D={logro3D} (vol {volLogro3D:0.00}), Logro 2D={logro2D} (vol {volLogro2D:0.00}, >0.2: {logro2DFuerte}), reproducciones +{AudioDirector.TotalReproducidos - hist0}{(casoOk ? "" : " (MAL)")}; ");
                    // Que terminen de sonar los gritos y el Logro de este caso antes del siguiente (si no, se cuentan dos veces).
                    float espera0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - espera0 < 6f)
                    {
                        bool queda = false;
                        foreach (var a in FuentesSonando()) if (a.clip.name == "Logro" || muerte.Contains(a.clip.name)) { queda = true; break; }
                        if (!queda) break;
                        yield return null;
                    }
                }
            }
            finally { ModoDios.Poner(diosAntes); }
            sb.Append("(no pude ESCUCHAR: verificado por propiedades de las AudioSource del pool)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #095 todo 3D y distorsion
        static IEnumerator Bug095a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var sb = new StringBuilder(); bool ok = true;

            // 1) Disparos horneados: sufijo _dist, pico <= 0.95 y mono (la fuente 3D los posiciona).
            int armas = 0, malArma = 0;
            foreach (WeaponKind w in Enum.GetValues(typeof(WeaponKind)))
            {
                var c = GenericSfx.GetWeaponShot(w);
                armas++;
                bool bien = c != null && c.name.EndsWith("_dist", StringComparison.Ordinal) && MuestrasMono(c, out var m) && PicoDe(m) <= 0.95f && c.channels == 1;
                if (!bien) { malArma++; sb.Append($"[{w}: {(c != null ? c.name : "null")}] "); }
            }
            string rifle = GenericSfx.GetWeaponShot(WeaponKind.Rifle).name;
            if (malArma > 0) ok = false;
            sb.Append($"armas={armas} sin _dist/pico>0.95={malArma} (Rifle='{rifle}'); ");

            // 2) 20 s de combate en el objetivo 1: 4 enemigos a ~14 m tirandole a la escuadra, el jugador responde.
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo = Poseido(); var drv = PlayerInputDriver.Activo;
            if (yo == null || drv == null || drv.Brain == null) { Fin("FALLO sin soldado poseido"); yield break; }
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var mundo = NombresDeClipsDelMundo();
            var malas = new Dictionary<string, int>();
            var vistas2D = new Dictionary<string, int>();
            int muestras = 0, fuentes3D = 0, fuentes2D = 0, hist0 = AudioDirector.TotalReproducidos;
            try
            {
                var objetivos = new List<Soldier>();
                for (int i = 0; i < 4; i++)
                {
                    var e = BuscarEnemigoVivo();
                    if (e == null) break;
                    foreach (var o in objetivos) if (o == e) { e = null; break; }
                    if (e == null) break;
                    var br = e.GetComponent<SP.Ai.AiBrain>(); if (br != null) br.enabled = true;
                    float ang = (-30f + i * 20f) * Mathf.Deg2Rad;
                    var d = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f) * yo.transform.forward;
                    Teletransportar(e, yo.transform.position + d * (13f + i));
                    e.Health.Heal(e.Health.MaxHealth);
                    objetivos.Add(e);
                }
                float t0 = Time.realtimeSinceStartup, proximoTiro = 0f; int tiros = 0;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    if (Time.realtimeSinceStartup >= proximoTiro && objetivos.Count > 0)
                    {
                        proximoTiro = Time.realtimeSinceStartup + 0.3f;
                        var obj = objetivos[tiros % objetivos.Count];
                        if (obj != null && obj.Health.IsAlive && yo.Weapon != null)
                        {
                            yo.Weapon.ReponerMunicion();
                            yo.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(obj.transform.position - yo.transform.position, Vector3.up));
                            drv.Brain.Fire(obj.transform.position + Vector3.up * 1.2f);
                            tiros++;
                        }
                    }
                    muestras++;
                    foreach (var a in FuentesSonando())
                    {
                        string go = a.gameObject.name, clip = a.clip.name;
                        if (a.spatialBlend >= 0.99f) { fuentes3D++; continue; }
                        fuentes2D++;
                        string clave = go + "/" + clip;
                        vistas2D[clave] = vistas2D.TryGetValue(clave, out var c0) ? c0 + 1 : 1;
                        bool permitida;
                        if (Blanca2D.Contains(go)) permitida = true;
                        else if (go.StartsWith("UiVoice_", StringComparison.Ordinal)) permitida = !EsClipDeMundo(mundo, clip) || (clip == "Logro" && a.volume <= 0.2f);
                        else permitida = false;
                        if (!permitida) malas[clave] = malas.TryGetValue(clave, out var c1) ? c1 + 1 : 1;
                    }
                    yield return null;
                }
                sb.Append($"combate 20 s: tiros del jugador={tiros}, muestras={muestras}, fuentes 3D sonando (acum.)={fuentes3D}, 2D={fuentes2D}, reproducciones del director +{AudioDirector.TotalReproducidos - hist0}; ");
            }
            finally { ModoDios.Poner(diosAntes); }
            var l2d = new List<string>(); foreach (var kv in vistas2D) l2d.Add(kv.Key + "x" + kv.Value);
            sb.Append("2D vistas=[" + string.Join(", ", l2d) + "]; ");
            if (malas.Count > 0)
            {
                ok = false;
                var lm = new List<string>(); foreach (var kv in malas) lm.Add(kv.Key + "x" + kv.Value);
                sb.Append("2D NO PERMITIDAS=[" + string.Join(", ", lm) + "] (MAL); ");
            }
            if (AudioDirector.TotalReproducidos - hist0 < 15 || fuentes3D == 0) { ok = false; sb.Append("(MAL: casi no hubo audio que auditar); "); }
            sb.Append("(no pude ESCUCHAR: distorsion verificada por nombre/pico del clip horneado)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #072 graves y chapa
        static IEnumerator Bug072a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var sb = new StringBuilder(); bool ok = true;

            // 1) Los clips horneados tienen graves de verdad: banda 30-150 Hz (relativa al pico) >= 1.25x (+2 dB) el promedio de los crudos.
            foreach (var par in new[] { (SfxKind.Explosion, "Explosion"), (SfxKind.CannonBody, "CannonBody") })
            {
                var conSub = GenericSfx.Get(par.Item1);
                bool leido = MuestrasMono(conSub, out var m);
                float sumaCrudo = 0f; int cuantos = 0;
                foreach (var crudo in Resources.LoadAll<AudioClip>("Audio/Sfx/" + par.Item2))
                    if (MuestrasMono(crudo, out var mc)) { sumaCrudo += BandaGrave(mc, crudo.frequency); cuantos++; }
                float grave = leido ? BandaGrave(m, conSub.frequency) : 0f;
                float crudoMedia = cuantos > 0 ? sumaCrudo / cuantos : 0f;
                bool bien = leido && conSub.name == par.Item2 + "+Sub" && grave >= 1.25f * crudoMedia && grave > 0.005f && PicoDe(m) <= 0.96f;
                if (!bien) ok = false;
                sb.Append($"{par.Item2}: clip '{conSub.name}', banda 30-150 Hz / pico {grave:0.0000} vs crudos {crudoMedia:0.0000} (x{(crudoMedia > 0 ? grave / crudoMedia : 0):0.0}){(bien ? "" : " (MAL)")}; ");
                if (leido)
                {
                    var crudo0 = Resources.LoadAll<AudioClip>("Audio/Sfx/" + par.Item2);
                    float[] mc0 = null; if (crudo0.Length > 0) MuestrasMono(crudo0[0], out mc0);
                    GuardarOnda("v2_072_" + par.Item2.ToLowerInvariant() + "_graves.png", m, conSub.frequency, 1200, 260, mc0);
                }
            }
            var dest = GenericSfx.Get(SfxKind.DestruccionVehiculo);
            bool destOk = dest != null && dest.name == "DestruccionVehiculo" && dest.length > 3f && MuestrasMono(dest, out var md) && RmsDe(md) > 0.02f;
            if (!destOk) ok = false;
            sb.Append($"DestruccionVehiculo: {(dest != null ? dest.length.ToString("0.0") + " s" : "null")}{(destOk ? "" : " (MAL)")}; ");
            if (dest != null && MuestrasMono(dest, out var md2)) GuardarOnda("v2_072_destruccion_chapa.png", md2, dest.frequency);

            // 2) Perfiles espaciales: una explosion llega mas lejos que un grito y no se corta a los 90 m.
            bool perfiles = AudioDirector.Attenuation(100f, PerfilEspacial.Explosion) > 0f && AudioDirector.Attenuation(100f, PerfilEspacial.Voz) == 0f
                            && AudioDirector.Attenuation(100f, PerfilEspacial.Base) == 0f && AudioDirector.Attenuation(150f, PerfilEspacial.Disparo) == 0f
                            && PerfilEspacial.Para(SfxKind.Explosion).max == 220f && PerfilEspacial.Para(SfxKind.Death).rolloff == AudioRolloffMode.Logarithmic;
            if (!perfiles) ok = false;
            sb.Append($"perfiles OK={perfiles}; ");

            // 3) En juego: el cañon del tanque dispara de verdad y suena "CannonBody+Sub"; el duck propio es leve.
            OperacionPrueba.Arrancar(4);
            foreach (var x in Esperar(2f)) yield return x;
            TurretWeapon canon = null;
            foreach (var t in UnityEngine.Object.FindObjectsByType<TurretWeapon>(FindObjectsSortMode.None))
                if (t != null && t.transform.parent != null && t.transform.parent.name != "MetralletaMount" && t.isActiveAndEnabled && t.Muzzle != null) { canon = t; break; }
            if (canon == null) { Fin("FALLO no encontre el cañon del tanque (TurretWeapon)"); yield break; }
            bool dispara = false;
            for (int i = 0; i < 40 && !dispara; i++) { dispara = canon.TryFire(); if (!dispara) yield return null; }
            yield return null; yield return null;
            bool subSono = AudioDirector.SonoRecien("CannonBody+Sub", 20);
            float ceiling = AudioDucking.UserVolumeCeiling;
            float duckMin = AudioListener.volume;
            for (int i = 0; i < 6; i++) { duckMin = Mathf.Min(duckMin, AudioListener.volume); yield return null; }
            bool duckLeve = duckMin >= ceiling * 0.9f;
            if (!dispara || !subSono || !duckLeve) ok = false;
            sb.Append($"cañon dispara={dispara}, sono 'CannonBody+Sub'={subSono}, volumen del oyente tras el tiro min {duckMin:0.00} de {ceiling:0.00} (leve={duckLeve}); ");

            // 4) Camioneta destruida: suena DestruccionVehiculo (y su Logro 3D). Las camionetas salen al subir al tanque.
            OperacionDirector.Instancia.Embarcar();
            OperacionAuto auto = null;
            float t0 = Time.realtimeSinceStartup;
            while (auto == null && Time.realtimeSinceStartup - t0 < 30f)
            {
                foreach (var v in Vehicle.Todos) { if (v != null && !v.IsDestroyed && v.GetComponent<OperacionAuto>() != null) { auto = v.GetComponent<OperacionAuto>(); break; } }
                if (auto == null) yield return null;
            }
            bool autoOk = false;
            if (auto != null)
            {
                auto.Vehiculo.TakeDamage(9999999, -1);
                for (int i = 0; i < 20; i++) yield return null;
                autoOk = AudioDirector.SonoRecien("DestruccionVehiculo", 30);
            }
            if (!autoOk) ok = false;
            sb.Append($"camioneta {(auto != null ? "destruida" : "NO HALLADA")}: sono DestruccionVehiculo={autoOk}{(autoOk ? "" : " (MAL)")}; ");

            // 5) Vehicle.FinalExplosion (hoy muda): el tanque, forzado. Corromple el estado del tanque: reiniciar Play despues.
            Vehicle tanque = null;
            foreach (var v in Vehicle.Todos) if (v != null && v.GetComponent<OperacionAuto>() == null && v.GetComponent<VehicleMotor>() != null) { tanque = v; break; }
            bool finalOk = false;
            if (tanque != null)
            {
                int antes = AudioDirector.TotalReproducidos;
                tanque.FinalExplosion();
                yield return null; yield return null;
                finalOk = AudioDirector.SonoRecien("DestruccionVehiculo", 10) && AudioDirector.SonoRecien("Explosion+Sub", 10) && AudioDirector.TotalReproducidos - antes >= 2;
                if (tanque.FinalExplosionDone && !finalOk) sb.Append($"[reproducciones tras FinalExplosion: +{AudioDirector.TotalReproducidos - antes}] ");
            }
            if (!finalOk) ok = false;
            sb.Append($"Vehicle.FinalExplosion {(tanque != null ? "ejecutada" : "SIN TANQUE")}: reproduce Explosion+Sub y DestruccionVehiculo={finalOk}{(finalOk ? "" : " (MAL)")}; ");
            sb.Append("(no pude ESCUCHAR: verificado por energia grave del clip horneado, historial del director y volumen del oyente; graficos en Assets/Validacion/v2_072_*.png)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #093 musica de victoria
        static IEnumerator Bug093()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }

            // 1) El himno: generado en un hilo, 26,5 s, sin silencios ni NaN, pico <= 0.9.
            float tGen = Time.realtimeSinceStartup;
            MusicDirector.PrecargarVictoria();   // el camino real (hilo de fondo); abajo se calcula tambien en sincronico para medirlo
            var datos = SfxSintetico.HimnoDeVictoriaDatos();
            float genSeg = Time.realtimeSinceStartup - tGen;
            float seg = datos.Length / (float)SfxSintetico.HimnoSR;
            bool nan = false; foreach (var v in datos) if (float.IsNaN(v) || float.IsInfinity(v)) { nan = true; break; }
            int compasesMudos = 0;
            int porCompas = (int)(60.0 / SfxSintetico.HimnoBpm * 4 * SfxSintetico.HimnoSR);
            for (int c = 0; c < SfxSintetico.HimnoCompases; c++)
            {
                double e = 0; int n = 0;
                for (int i = c * porCompas; i < Mathf.Min(datos.Length, (c + 1) * porCompas); i++) { e += datos[i] * datos[i]; n++; }
                if (n > 0 && Math.Sqrt(e / n) < 0.06) compasesMudos++;
            }
            float pico = PicoDe(datos), rms = RmsDe(datos);
            bool himnoOk = !nan && seg > 25f && seg < 28f && pico <= 0.9f && pico > 0.25f && rms > 0.1f && rms < 0.2f && compasesMudos == 0;
            if (!himnoOk) ok = false;
            sb.Append($"himno: {seg:0.0} s, pico {pico:0.00}, rms {rms:0.00}, compases mudos {compasesMudos}, NaN={nan}{(himnoOk ? "" : " (MAL)")}; ");
            GuardarOnda("v2_093_himno_onda.png", datos, SfxSintetico.HimnoSR, 1400, 240);

            // 2) Flujo real: ultimo objetivo, subir al helicoptero y muestrear el fade con tiempo no escalado.
            OperacionPrueba.Arrancar(6);
            foreach (var x in Esperar(2f)) yield return x;
            MusicDirector.DetenerVictoria();
            float ceilingAmb = AudioDirector.GainFor(SfxChannel.Ambient);
            // Hay que haber escuchado algo de musica de calma/combate antes (los loops existen y suenan).
            float loopsAntes = MusicDirector.VolumenDeLoops;
            d.SubirAlHeli();
            float tInicio = Time.realtimeSinceStartup;
            while (!MusicDirector.VictoriaSonando && Time.realtimeSinceStartup - tInicio < 20f) yield return null;
            if (!MusicDirector.VictoriaSonando) { Fin("FALLO el despegue no disparo MusicDirector.TocarVictoria (VictoriaSonando=false tras 20 s)"); yield break; }
            float tDespegue = Time.realtimeSinceStartup;
            float segDelDespegue = tDespegue - tInicio;
            var instantes = new[] { 0.3f, 1.5f, 3f, 4.5f, 6.3f };
            var vols = new float[instantes.Length]; var loops = new float[instantes.Length]; var timeScale = new float[instantes.Length];
            bool suena = true;
            for (int k = 0; k < instantes.Length; k++)
            {
                while (Time.realtimeSinceStartup - tDespegue < instantes[k]) yield return null;
                vols[k] = MusicDirector.VolumenVictoria / Mathf.Max(0.0001f, ceilingAmb);
                loops[k] = MusicDirector.VolumenDeLoops;
                timeScale[k] = Time.timeScale;
                suena &= MusicDirector.VictoriaReproduciendo || !MusicDirector.VictoriaLista;
            }
            bool monotono = true;
            for (int k = 1; k < vols.Length; k++) if (vols[k] + 0.001f < vols[k - 1]) monotono = false;
            bool sube = vols[0] < 0.15f && vols[vols.Length - 1] >= 0.9f;
            bool loopsOk = loops[3] <= 0.05f && loops[4] <= 0.05f;
            sb.Append($"despegue a los {segDelDespegue:0.0} s de SubirAlHeli; volumen del himno (normalizado) a +0.3/1.5/3/4.5/6.3 s = {vols[0]:0.00}/{vols[1]:0.00}/{vols[2]:0.00}/{vols[3]:0.00}/{vols[4]:0.00} (monotono={monotono}, sube 0->>=0.9={sube}); loops de combate {loops[0]:0.00}/{loops[1]:0.00}/{loops[2]:0.00}/{loops[3]:0.00}/{loops[4]:0.00} (antes {loopsAntes:0.00}, <=0.05 desde +4.5 s={loopsOk}); ");
            if (!monotono || !sube || !loopsOk || !suena) ok = false;

            // 3) La victoria pone timeScale 0: el himno sigue sonando a pleno y los loops siguen en cero.
            float tv = Time.realtimeSinceStartup;
            while (Time.timeScale >= 1f && Time.realtimeSinceStartup - tv < 20f) yield return null;
            bool en0 = Time.timeScale < 1f;   // #077: la victoria de la Operacion ya no congela (camara lenta x0,35); la del resto de las misiones si (0)
            foreach (var x in Esperar(1.5f)) yield return x;
            float volFinal = MusicDirector.VolumenVictoria / Mathf.Max(0.0001f, ceilingAmb);
            bool sigue = en0 && MusicDirector.VictoriaSonando && MusicDirector.VictoriaReproduciendo && volFinal >= 0.9f && MusicDirector.VolumenDeLoops <= 0.05f;
            if (!sigue) ok = false;
            sb.Append($"con timeScale {Time.timeScale:0}: VictoriaSonando={MusicDirector.VictoriaSonando}, reproduciendo={MusicDirector.VictoriaReproduciendo}, volumen {volFinal:0.00}, loops {MusicDirector.VolumenDeLoops:0.00} (sigue={sigue}); ");
            // Se deja el juego como estaba: tiempo normal y sin himno.
            Time.timeScale = 1f;
            MusicDirector.DetenerVictoria();
            sb.Append($"(no pude ESCUCHAR: verificado por volumen de la AudioSource, ignoreListenerPause y reloj no escalado; generacion del himno {genSeg:0.0} s)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
