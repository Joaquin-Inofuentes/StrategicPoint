using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // Bug #056 / #058: la camioneta enemiga se tiene que SENTIR.
    //   #058 "que los giros de las curvas no sean tan bruscos, que tengan cierta curva": el giro ya no salta a la velocidad maxima
    //        en un frame; la velocidad de giro acelera y frena (AceleracionDeGiro) y crece con el angulo que falta, asi entra y sale
    //        de cada curva redondeando.
    //   #058 "escuchar el sonido de las ruedas, sus particulas y que dejen un camino de huella": lazo de rodado (mas fuerte al
    //        doblar), polvo que levantan las ruedas traseras y dos huellas oscuras en el piso que se borran solas.
    //   #058 "escuchar sus metralletas y ver las particulas de los disparos": fogonazo de boca y estampido de ametralladora real.
    //   #056 "faltan las particulas de explosion cuando es destruido, y grandes explosiones": al morir, tres estallidos
    //        encadenados con bola de fuego, escombros, sacudida de camara, y la carcasa queda ardiendo con humo hasta desaparecer.
    public partial class OperacionAuto
    {
        public const float AceleracionDeGiro = 150f;      // grados/s²
        public float GiroActual { get; private set; }     // grados/s
        public bool EstallidoHecho { get; private set; }
        public int HuellasActivas => (huellaIzq != null && huellaIzq.emitting ? 1 : 0) + (huellaDer != null && huellaDer.emitting ? 1 : 0);
        public float VolumenRuedas => ruedas != null ? ruedas.volume : 0f;

        AudioSource ruedas;
        TrailRenderer huellaIzq, huellaDer;
        Vector3 ruedaTraseraIzq, ruedaTraseraDer;
        float proximoPolvo, proximoHumoCarcasa;
        static Material materialHuella;

        // Gira hacia rot con una velocidad de giro que acelera/frena en vez de saltar (curva redondeada).
        void Girar(Quaternion rot, float giroMaximo, float dt)
        {
            float falta = Vector3.SignedAngle(transform.forward, rot * Vector3.forward, Vector3.up);
            float quiere = Mathf.Clamp(falta * 2.4f, -giroMaximo, giroMaximo);
            GiroActual = Mathf.MoveTowards(GiroActual, quiere, AceleracionDeGiro * dt);
            transform.Rotate(0f, GiroActual * dt, 0f, Space.World);
        }

        void PrepararEfectos()
        {
            // Ruedas traseras: esquinas de la caja de los renderers, en coordenadas locales.
            // Caja en coordenadas locales: las 8 esquinas de cada renderer pasadas al espacio de la camioneta.
            var b = new Bounds(); bool hay = false;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var esquina = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = transform.InverseTransformPoint(r.transform.TransformPoint(esquina));
                    if (!hay) { b = new Bounds(local, Vector3.zero); hay = true; } else b.Encapsulate(local);
                }
            }
            if (!hay) b = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2.2f, 2f, 5.6f));
            ruedaTraseraIzq = new Vector3(b.center.x - b.extents.x * 0.72f, b.min.y + 0.05f, b.center.z - b.extents.z * 0.62f);
            ruedaTraseraDer = new Vector3(b.center.x + b.extents.x * 0.72f, b.min.y + 0.05f, b.center.z - b.extents.z * 0.62f);

            var go = new GameObject("SonidoRuedas");
            go.transform.SetParent(transform, false);
            ruedas = go.AddComponent<AudioSource>();
            ruedas.clip = GenericSfx.Get(SfxKind.RuedasCamioneta);
            ruedas.loop = true; ruedas.playOnAwake = false; ruedas.spatialBlend = 1f;
            ruedas.rolloffMode = AudioRolloffMode.Logarithmic; ruedas.minDistance = 5f; ruedas.maxDistance = 140f; ruedas.dopplerLevel = 0.6f;
            ruedas.volume = 0f;
            if (ruedas.clip != null) { ruedas.time = Random.value * ruedas.clip.length; ruedas.Play(); }

            huellaIzq = CrearHuella("HuellaIzq", ruedaTraseraIzq);
            huellaDer = CrearHuella("HuellaDer", ruedaTraseraDer);
        }

        TrailRenderer CrearHuella(string nombre, Vector3 local)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            // TransformZ: la cinta queda acostada en el piso (su Z mira hacia arriba) en vez de mirar a la camara.
            go.transform.rotation = Quaternion.LookRotation(Vector3.up, transform.forward);
            var t = go.AddComponent<TrailRenderer>();
            t.alignment = LineAlignment.TransformZ;
            t.time = 7f;
            t.minVertexDistance = 0.35f;
            t.widthMultiplier = 0.32f * Mathf.Clamp(transform.lossyScale.x * 2f, 0.6f, 1.4f);
            t.numCapVertices = 0;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            if (materialHuella == null)
            {
                var s = Shader.Find("Sprites/Default");
                materialHuella = new Material(s) { name = "M_HuellaCamioneta", hideFlags = HideFlags.DontSave };
            }
            t.sharedMaterial = materialHuella;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.05f, 0.045f, 0.04f), 0f), new GradientColorKey(new Color(0.08f, 0.07f, 0.06f), 1f) },
                      new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = g;
            t.emitting = false;
            return t;
        }

        // Rodado, polvo y huellas segun la velocidad y el giro.
        void TickRastro(float dt)
        {
            if (ruedas == null) return;
            float f = Mathf.Clamp01(Mathf.Abs(VelocidadActual) / VelocidadMaxima);
            float derrape = Mathf.Clamp01(Mathf.Abs(GiroActual) / 70f) * f;
            ruedas.volume = Mathf.Clamp01(f * 0.85f + derrape * 0.35f);
            ruedas.pitch = Mathf.Lerp(0.7f, 1.3f, f) + derrape * 0.15f;
            bool marca = Mathf.Abs(VelocidadActual) > 1.2f;
            if (huellaIzq != null) { huellaIzq.emitting = marca; huellaIzq.transform.rotation = Quaternion.LookRotation(Vector3.up, transform.forward); }
            if (huellaDer != null) { huellaDer.emitting = marca; huellaDer.transform.rotation = Quaternion.LookRotation(Vector3.up, transform.forward); }
            if (f > 0.12f && Time.time >= proximoPolvo)
            {
                proximoPolvo = Time.time + Mathf.Lerp(0.2f, 0.07f, f) / (1f + derrape);
                var atras = -transform.forward * Mathf.Abs(VelocidadActual) * 0.12f + Vector3.up * 0.5f;
                var polvo = new Color(0.62f, 0.6f, 0.56f, 0.55f + 0.25f * derrape);
                SpriteFx.Lanzar("smoke_01", transform.TransformPoint(ruedaTraseraIzq), polvo, 0.35f, 1.4f + f + derrape, 1.2f, Random.Range(-30f, 30f), atras, 1.4f, 1);
                SpriteFx.Lanzar("smoke_02", transform.TransformPoint(ruedaTraseraDer), polvo, 0.35f, 1.4f + f + derrape, 1.2f, Random.Range(-30f, 30f), atras, 1.4f, 1);
            }
        }

        // Fogonazo y estampido de la metralleta de la camioneta.
        void EfectoDeDisparo(Vector3 origen, Vector3 dir)
        {
            SpriteFx.Lanzar("muzzle_01", origen + dir * 0.25f, new Color(1f, 0.82f, 0.45f, 1f), 0.55f, 0.85f, 0.06f, Random.Range(0f, 360f), default, 0f, 6);
            SpriteFx.Lanzar("smoke_02", origen + dir * 0.4f, new Color(0.6f, 0.58f, 0.55f, 0.35f), 0.25f, 0.9f, 0.45f, Random.Range(-40f, 40f), dir * 1.5f + Vector3.up * 0.4f, 2.5f, 1);
            var clip = GenericSfx.GetWeaponShot(SP.Combat.WeaponKind.Heavy);
            if (clip != null) AudioDirector.PlayClipAt(clip, origen, 0.75f, 0.9f);
        }

        // Muerte: estallidos encadenados + escombros + sacudida; la carcasa queda ardiendo.
        void ExplotarGrande()
        {
            if (EstallidoHecho) return;
            EstallidoHecho = true;
            if (ruedas != null) ruedas.Stop();
            // Las huellas se sueltan del vehiculo: se siguen borrando solas aunque la carcasa desaparezca.
            foreach (var h in new[] { huellaIzq, huellaDer })
                if (h != null) { h.emitting = false; h.transform.SetParent(null, true); h.autodestruct = true; }
            var centro = transform.position + Vector3.up * 0.8f;
            StartCoroutine(Estallidos(centro));
        }

        System.Collections.IEnumerator Estallidos(Vector3 centro)
        {
            float[] radios = { 4.2f, 3.0f, 3.6f };
            float[] esperas = { 0f, 0.16f, 0.22f };
            for (int i = 0; i < radios.Length; i++)
            {
                if (esperas[i] > 0f) yield return new WaitForSeconds(esperas[i]);
                var p = centro + (i == 0 ? Vector3.zero : new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.2f, 1f), Random.Range(-1.5f, 1.5f)));
                ImpactFx.SpawnExplosion(p, radios[i]);
                AudioDirector.PlayAt(SfxKind.Explosion, p, 1f, 1f);
                if (i == 0) AudioDirector.PlayAt(SfxKind.ImpactoPesado, p, 1f, 1f);
                // Escombros de chapa oscura que vuelan y caen.
                for (int k = 0; k < 10; k++)
                {
                    var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.6f;
                    DebrisPool.Spawn(p, dir.normalized * Random.Range(5f, 11f), Color.Lerp(new Color(0.12f, 0.1f, 0.09f), new Color(0.5f, 0.12f, 0.08f), Random.value), Random.Range(0.12f, 0.3f), Random.Range(1.6f, 2.6f));
                }
                var rig = SP.CameraSystem.CameraRig.Instance;
                if (rig != null)
                {
                    float d = Vector3.Distance(rig.transform.position, p);
                    float k2 = 1f - Mathf.Clamp01(d / 45f);
                    if (k2 > 0f) rig.KickDirectional((rig.transform.position - p).normalized, k2 * 0.4f);
                }
            }
            // Columna de fuego grande al final.
            SpriteFx.Lanzar("fire_01", centro + Vector3.up * 0.6f, new Color(1f, 0.6f, 0.2f, 1f), 2.2f, 5.5f, 0.8f, Random.Range(-30f, 30f), Vector3.up * 1.5f, 0.5f, 4);
        }

        // Carcasa: llamas bajas y humo negro que sube mientras quedan los 6 s antes de desaparecer.
        void TickCarcasa()
        {
            if (Time.time < proximoHumoCarcasa) return;
            proximoHumoCarcasa = Time.time + 0.22f;
            var p = transform.position + Vector3.up * 0.9f + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.8f, 0.8f));
            SpriteFx.Lanzar("smoke_04", p, new Color(0.3f, 0.29f, 0.28f, 0.75f), 0.9f, 3.6f, 2.6f, Random.Range(-25f, 25f), Vector3.up * Random.Range(1.6f, 2.6f), 0.3f, 1);
            if (Random.value < 0.85f) SpriteFx.Lanzar("fire_02", p, new Color(1f, 0.55f, 0.18f, 0.95f), 0.6f, 1.3f, 0.5f, Random.Range(-50f, 50f), Vector3.up * 0.8f, 0.5f, 3);
        }
    }
}
