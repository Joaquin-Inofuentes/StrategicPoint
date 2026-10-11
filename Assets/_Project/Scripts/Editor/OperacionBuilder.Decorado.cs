using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido ("agrega arte en todo lado, que no haya nada de blocking"): ademas de las texturas, el nivel se viste con
    //  1) detalle en cada edificio (ventanas con marco y alfeizar, puerta, parapeto y equipos en el techo),
    //  2) props del kit de arte del proyecto (Assets/_Project/Prefabs/ArteMundo: barricadas, cajas, barriles, autos quemados, escombros, cercos,
    //     arboles...) repartidos junto a los edificios, en el cuartel, en la muralla, en el centro de datos y a los costados de la ruta del tanque.
    public static partial class OperacionBuilder
    {
        const string DirArte = "Assets/_Project/Prefabs/ArteMundo/";
        static readonly List<Transform> edificiosCreados = new List<Transform>();
        static readonly Dictionary<string, GameObject> prefabsDeArte = new Dictionary<string, GameObject>();
        public static string ResumenDecorado { get; private set; } = "";

        static GameObject PrefabDeArte(string nombre)
        {
            if (prefabsDeArte.TryGetValue(nombre, out var p) && p != null) return p;
            p = AssetDatabase.LoadAssetAtPath<GameObject>(DirArte + nombre + ".prefab");
            prefabsDeArte[nombre] = p;
            return p;
        }

        static GameObject PonerArte(Transform padre, string nombre, Vector3 pos, float yaw, float escala = 1f, bool sinNavMesh = false)
        {
            var pf = PrefabDeArte(nombre);
            if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, padre);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * escala;
            if (sinNavMesh) { var m = go.AddComponent<NavMeshModifier>(); m.ignoreFromBuild = true; }
            return go;
        }

        // Caja libre de colisionadores en el piso: sirve para no apoyar un prop sobre un edificio, un soldado u otro prop.
        static bool LugarLibre(Vector3 c, float mitadX, float mitadZ, float yaw, float alto = 1.2f)
            => !Physics.CheckBox(new Vector3(c.x, alto * 0.5f + 0.25f, c.z), new Vector3(mitadX, alto * 0.5f, mitadZ), Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore);

        static float DistanciaAPolilinea2(Vector2[] pts, Vector2 p)
        {
            float mejor = 1e9f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var ab = pts[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                mejor = Mathf.Min(mejor, (p - (a + ab * t)).magnitude);
            }
            return mejor;
        }

        static void Decorado(Transform op, Transform[] ruta)
        {
            var g = Grupo(op, "7_Arte");
            Montanas(op);
            Physics.SyncTransforms();
            var rnd = new System.Random(20261010);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var pts = new Vector2[ruta.Length];
            for (int i = 0; i < ruta.Length; i++) pts[i] = new Vector2(ruta[i].position.x, ruta[i].position.z);

            // 1) detalle de cada edificio
            int detallados = 0;
            var gDet = Grupo(g, "DetalleDeEdificios");
            foreach (var b in edificiosCreados.ToArray())
                if (b != null && DetalleDeEdificio(gDet, b, rnd)) detallados++;

            // 2) props junto a las paredes de los edificios
            var gJunto = Grupo(g, "PropsJuntoAEdificios");
            string[] juntoA = { "P_Env_Caja_Madera", "P_Env_Barril", "P_Env_Palet", "P_Env_Neumaticos", "P_Env_Barricada_Sacos", "P_Env_Caja_Madera", "P_Env_Escombro_Pila_A", "P_Env_Barricada_Concreto", "P_Env_Barricada_Sacos", "P_Env_Barricada_Sacos", "P_Env_Barricada_Sacos", "P_Env_Farol" };
            float[] escJunto = { 1.1f, 0.5f, 1f, 1f, 1.1f, 1.3f, 0.8f, 1f, 1.1f, 1.1f, 1.1f, 1f };
            int nJunto = 0;
            foreach (var b in edificiosCreados.ToArray())
            {
                if (b == null || b.lossyScale.y < 2f) continue;
                if (b.name.StartsWith("Silo") || b.name.StartsWith("Contenedor") || b.name.StartsWith("Edificio_Borde")) continue;
                int cuantos = 1 + rnd.Next(3);
                for (int k = 0; k < cuantos; k++)
                {
                    int lado = rnd.Next(4);
                    var ejeLocal = lado % 2 == 0 ? b.forward : b.right;
                    float semi = (lado % 2 == 0 ? b.lossyScale.z : b.lossyScale.x) * 0.5f + 1.1f;
                    float largo = (lado % 2 == 0 ? b.lossyScale.x : b.lossyScale.z) * 0.5f - 1.2f;
                    var normal = ejeLocal * (lado < 2 ? 1f : -1f);
                    var paralelo = lado % 2 == 0 ? b.right : b.forward;
                    var pos = b.position + normal * semi + paralelo * R(-largo, largo); pos.y = 0f;
                    int ix = rnd.Next(juntoA.Length);
                    float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg + R(-25f, 25f);
                    if (!LugarLibre(pos, 0.9f * escJunto[ix], 0.9f * escJunto[ix], yaw)) continue;
                    var go = PonerArte(gJunto, juntoA[ix], pos, yaw, escJunto[ix]);
                    if (go != null) { nJunto++; Physics.SyncTransforms(); }
                }
            }

            // 3) relleno por zonas del cuartel, la muralla, el centro de datos y el patio
            var gZonas = Grupo(g, "PropsPorZonas");
            string[] militar = { "P_Env_Barricada_Sacos", "P_Env_Caja_Madera", "P_Env_Barril", "P_Env_Palet", "P_Env_Neumaticos", "P_Env_Emplazamiento_Cajon", "P_Env_Barricada_Concreto", "P_Env_Cartel", "P_Env_Barricada_Erizo", "P_Env_Crater", "P_Env_Barricada_Sacos", "P_Env_Barricada_Sacos", "P_Env_Barricada_Sacos", "P_Env_Emplazamiento_MG", "P_Env_Farol" };
            float[] escMil = { 1.2f, 1.2f, 0.5f, 1f, 1f, 1.4f, 1.1f, 1f, 1f, 1f, 1.2f, 1.2f, 1.2f, 1.1f, 1f };
            int nZonas = 0;
            void Zona(float x0, float x1, float z0, float z1, int cuantos, bool conCamino)
            {
                for (int i = 0, intentos = 0; i < cuantos && intentos < cuantos * 12; intentos++)
                {
                    var pos = new Vector3(R(x0, x1), 0f, R(z0, z1));
                    if (conCamino && Mathf.Abs(pos.x) < 4.5f) continue;     // el eje central queda libre para avanzar
                    int ix = rnd.Next(militar.Length);
                    float yaw = R(0f, 360f);
                    if (!LugarLibre(pos, 1.3f * escMil[ix], 1.3f * escMil[ix], yaw)) continue;
                    if (PonerArte(gZonas, militar[ix], pos, yaw, escMil[ix]) != null) { i++; nZonas++; Physics.SyncTransforms(); }
                }
            }
            Zona(-58f, 58f, -378f, -226f, 96, true);    // cuartel
            Zona(-58f, 58f, -214f, -112f, 44, true);    // muralla y puestos
            Zona(-58f, 58f, -104f, -2f, 30, true);      // centro de datos
            Zona(-58f, 58f, 6f, 46f, 14, true);         // patio del tanque

            // 4) desierto: a los costados de la ruta del tanque (entre 13 y 48 m del eje)
            var gRuta = Grupo(g, "PropsDeLaRuta");
            string[] desierto = { "P_Env_Auto_Quemado", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B", "P_Env_Neumaticos", "P_Env_Poste_Caido", "P_Env_Muro_Caido", "P_Env_Crater", "P_Env_Cartel", "P_Env_Barricada_Erizo", "P_Env_Caja_Madera", "P_Env_Barril", "P_Mod_Valla_Alambrado", "P_Mod_Valla_Chapa", "P_Env_Barricada_Concreto" };
            float[] escDes = { 1.1f, 1.3f, 1.2f, 1.2f, 1.2f, 1.2f, 1.4f, 1.2f, 1.2f, 1.3f, 0.5f, 1.2f, 1.2f, 1.1f };
            int nRuta = 0;
            for (int i = 0, intentos = 0; i < 120 && intentos < 2400; intentos++)
            {
                var c = pts[rnd.Next(pts.Length)] + new Vector2(R(-48f, 48f), R(-48f, 48f));
                if (c.x < -380f || c.x > 380f || c.y < 40f || c.y > 380f) continue;
                if (c.x > 195f && c.y > 120f) continue;                 // la ciudad
                if (c.x > -160f && c.x < 30f && c.y > 300f) continue;   // el rio y su cauce (bajo el puente)
                float dR = DistanciaAPolilinea2(pts, c);
                if (dR < 13f || dR > 48f) continue;
                var pos = new Vector3(c.x, 0f, c.y);
                int ix = rnd.Next(desierto.Length);
                float yaw = R(0f, 360f);
                if (!LugarLibre(pos, 1.6f * escDes[ix], 1.6f * escDes[ix], yaw, 2f)) continue;
                if (PonerArte(gRuta, desierto[ix], pos, yaw, escDes[ix], true) != null) { i++; nRuta++; Physics.SyncTransforms(); }
            }
            // Oasis junto al puente: arboles y arbustos (el rio corre por x -130..10, z ~342).
            string[] verde = { "P_Env_ArbolA", "P_Env_ArbolB", "P_Env_Arbusto_Grande", "P_Env_Arbusto_Medio", "P_Env_Arbusto_Chico" };
            int nVerde = 0;
            for (int i = 0, intentos = 0; i < 26 && intentos < 400; intentos++)
            {
                var pos = new Vector3(R(-150f, 20f), 0f, R(280f, 311f));   // orilla sur del rio (al norte de la ruta hay agua)
                if (DistanciaAPolilinea2(pts, new Vector2(pos.x, pos.z)) < 13f) continue;
                int ix = rnd.Next(verde.Length);
                float esc = ix < 2 ? R(1.2f, 1.9f) : R(1f, 1.5f);
                if (!LugarLibre(pos, 1.2f * esc, 1.2f * esc, 0f, 3f)) continue;
                if (PonerArte(gRuta, verde[ix], pos, R(0f, 360f), esc, true) != null) { i++; nVerde++; Physics.SyncTransforms(); }
            }

            // 5) Mas arte en la ciudad y en la ruta: faroles y nidos de sacos al borde de la calzada, y la ciudad vestida calle por calle.
            var gBorde = Grupo(g, "BordeDeLaRuta");
            int nBorde = 0, nCiudad = 0;
            bool EnElRio(Vector3 q) => q.x > -160f && q.x < 30f && q.z > 296f;
            float acum = 0f, proximo = 14f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var ab = pts[i + 1] - a; float len = ab.magnitude; if (len < 0.1f) continue;
                var dirR = ab / len; var nrm = new Vector2(-dirR.y, dirR.x);
                while (acum + len >= proximo)
                {
                    float t = proximo - acum; var c = a + dirR * t; proximo += R(30f, 46f);
                    float lado = rnd.Next(2) == 0 ? 1f : -1f;
                    var q = new Vector3(c.x + nrm.x * lado * R(7.5f, 9.5f), 0f, c.y + nrm.y * lado * R(7.5f, 9.5f));
                    if (EnElRio(q) || Mathf.Abs(q.x) > 380f || q.z < 30f) continue;
                    float yawR = Mathf.Atan2(dirR.x, dirR.y) * Mathf.Rad2Deg;
                    int tipo = rnd.Next(5);
                    if (tipo <= 1)
                    {   // farol
                        if (LugarLibre(q, 0.5f, 0.5f, 0f, 3f) && PonerArte(gBorde, "P_Env_Farol", q, yawR, 1.15f, true) != null) { nBorde++; Physics.SyncTransforms(); }
                    }
                    else if (tipo == 2)
                    {   // nido de sacos: tres tramos en U (el tanque pasa por la calzada, a mas de 7 m)
                        for (int k = -1; k <= 1; k++)
                        {
                            var qq = q + new Vector3(dirR.x, 0f, dirR.y) * (k * 3.4f); float yawQ = yawR + (k == 0 ? 90f : 0f); if (k == 0) qq += new Vector3(nrm.x, 0f, nrm.y) * lado * 1.2f;
                            if (LugarLibre(qq, 1.8f, 0.5f, yawQ, 1f) && PonerArte(gBorde, "P_Env_Barricada_Sacos", qq, yawQ, 1.1f, true) != null) { nBorde++; Physics.SyncTransforms(); }
                        }
                    }
                    else if (tipo == 3)
                    {
                        string[] varios = { "P_Env_Auto_Quemado", "P_Env_Escombro_Pila_A", "P_Env_Neumaticos", "P_Env_Barricada_Erizo", "P_Mod_Valla_Alambrado", "P_Env_Cartel" };
                        string pr = varios[rnd.Next(varios.Length)];
                        if (LugarLibre(q, 1.8f, 1.8f, yawR, 2f) && PonerArte(gBorde, pr, q, yawR + R(-20f, 20f), pr == "P_Env_Auto_Quemado" ? 1.1f : 1.2f, true) != null) { nBorde++; Physics.SyncTransforms(); }
                    }
                    else
                    {   // cajas y barriles agrupados
                        for (int k = 0; k < 3; k++)
                        {
                            string pr = k == 1 ? "P_Env_Barril" : "P_Env_Caja_Madera";
                            var qq = q + new Vector3(R(-2f, 2f), 0f, R(-2f, 2f));
                            if (LugarLibre(qq, 0.7f, 0.7f, 0f, 1.2f) && PonerArte(gBorde, pr, qq, R(0f, 360f), pr == "P_Env_Barril" ? 0.5f : 1.2f, true) != null) { nBorde++; Physics.SyncTransforms(); }
                        }
                    }
                }
                acum += len;
            }
            // Ciudad (x 195..380, z 120..380): escombros, autos, sacos en las esquinas, faroles, vallas y cajas entre los edificios.
            var gCiudad = Grupo(g, "PropsDeLaCiudad");
            string[] urbano = { "P_Env_Barricada_Sacos", "P_Env_Auto_Quemado", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B", "P_Env_Farol", "P_Env_Caja_Madera", "P_Env_Barril", "P_Env_Neumaticos", "P_Env_Barricada_Concreto", "P_Env_Barricada_Sacos", "P_Mod_Valla_Chapa", "P_Env_Poste_Caido", "P_Env_Muro_Caido", "P_Env_Palet", "P_Env_Barricada_Sacos", "P_Env_Cartel", "P_Env_Emplazamiento_Cajon", "P_Env_Barricada_Tablones" };
            float[] escUrb = { 1.2f, 1.1f, 1.3f, 1.2f, 1.1f, 1.2f, 0.5f, 1.2f, 1.1f, 1.2f, 1.2f, 1.2f, 1.2f, 1f, 1.2f, 1.1f, 1.3f, 1.2f };
            for (int i = 0, intentos = 0; i < 190 && intentos < 3200; intentos++)
            {
                var pos = new Vector3(R(196f, 378f), 0f, R(122f, 378f));
                int ix = rnd.Next(urbano.Length);
                float yaw = rnd.Next(2) == 0 ? rnd.Next(4) * 90f + R(-8f, 8f) : R(0f, 360f);
                if (!LugarLibre(pos, 1.3f * escUrb[ix], 1.3f * escUrb[ix], yaw, 1.4f)) continue;
                if (PonerArte(gCiudad, urbano[ix], pos, yaw, escUrb[ix]) != null) { i++; nCiudad++; Physics.SyncTransforms(); }
            }

            Decals(g, ruta, pts, rnd);   // OperacionBuilder.Decals.cs: manchas, grietas, huellas, humedad... (sin juntas de tile)
            ResumenDecorado = $"[Operacion] Arte: {detallados} edificios detallados, {nJunto} props junto a edificios, {nZonas} en cuartel/muralla/centro/patio, {nRuta} en la ruta, {nVerde} del oasis, {nBorde} al borde de la ruta y {nCiudad} en la ciudad.";
            Debug.Log(ResumenDecorado);
            edificiosCreados.Clear();
        }

        // Cordillera baja que rodea el mapa (fuera de las murallas, a 640-1000 m del centro): el horizonte deja de ser un plano verde oscuro.
        static void Montanas(Transform op)
        {
            var g = Grupo(op, "8_Horizonte");
            const int segs = 120;
            float[] radios = { 640f, 690f, 760f, 860f, 960f, 1060f };
            float[] perfil = { 0f, 0.5f, 1f, 0.75f, 0.35f, 0f };
            var rnd = new System.Random(7);
            var ruidoAng = new float[segs];
            for (int i = 0; i < segs; i++) ruidoAng[i] = (float)rnd.NextDouble();
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int k = 0; k < radios.Length; k++)
                for (int i = 0; i <= segs; i++)
                {
                    int ii = i % segs;
                    float ang = (float)i / segs * Mathf.PI * 2f;
                    float pico = 55f + 85f * Mathf.Abs(Mathf.Sin(ang * 3f + 0.7f)) * (0.5f + 0.5f * ruidoAng[ii]) + 30f * ruidoAng[(ii * 7) % segs];
                    float h = pico * perfil[k] * (0.8f + 0.4f * ruidoAng[(ii * 3 + k * 5) % segs]);
                    float r = radios[k] + (ruidoAng[(ii * 5 + k) % segs] - 0.5f) * 40f;
                    var p = new Vector3(Mathf.Cos(ang) * r, h - (k == 0 || k == radios.Length - 1 ? 1.5f : 0f), Mathf.Sin(ang) * r);
                    v.Add(p); uv.Add(new Vector2(p.x / 45f, p.z / 45f));
                }
            int fila = segs + 1;
            for (int k = 0; k < radios.Length - 1; k++)
                for (int i = 0; i < segs; i++)
                {
                    int a = k * fila + i, b = a + 1, c = a + fila, d = c + 1;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
                }
            var m = new Mesh { name = "Op_Montanas", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            // normales hacia arriba/afuera: el orden de vertices ya las deja mirando al cielo
            var go = new GameObject("Montanas", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(g, false);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = Mat("Montana", new Color(0.46f, 0.37f, 0.28f));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mod = go.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true;
        }

        // Ventanas con marco, puerta, parapeto y equipos de techo. Devuelve false si el edificio no se detalla.
        static bool DetalleDeEdificio(Transform grupo, Transform e, System.Random rnd)
        {
            string n = e.name;
            bool militar = n.StartsWith("Barraca_") || n.StartsWith("Comando") || n.StartsWith("Comedor") || n.StartsWith("Parque_Motor") || n.StartsWith("Garita_") || n.StartsWith("Cochera_") || n.StartsWith("Edificio_") || n.StartsWith("Bunker_Cuerpo");
            if (!militar || n.StartsWith("Edificio_Borde")) return false;
            var s = e.lossyScale;
            if (s.y < 2.8f) return false;
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var marco = Hormigon;
            VentanaApagada.SetFloat("_Smoothness", 0.9f);   // vidrio: refleja la luna y las luces
            var rotor = e.rotation;
            var cuerpo = Grupo(grupo, n);
            bool bunker = n.StartsWith("Bunker_Cuerpo");
            // cuatro caras: (eje a lo largo, normal, largo de la cara)
            var caras = new[] { (e.right, e.forward, s.x, s.z), (e.right, -e.forward, s.x, s.z), (e.forward, e.right, s.z, s.x), (e.forward, -e.right, s.z, s.x) };
            int ci = 0;
            foreach (var (eje, normal, largo, prof) in caras)
            {
                ci++;
                var centro = e.position; centro.y = 0f; centro += normal * (prof * 0.5f);
                float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
                int porFila = Mathf.Max(0, Mathf.FloorToInt((largo - 2f) / 3.4f));
                int pisos = Mathf.Max(1, Mathf.FloorToInt((s.y - 1.2f) / 3.1f));
                if (bunker) { porFila = Mathf.Min(porFila, 2); pisos = 1; }
                for (int f = 0; f < pisos; f++)
                {
                    float y0 = (bunker ? 1.7f : 1.5f) + f * 3.1f;
                    if (y0 + 1.2f > s.y - 0.5f) break;
                    for (int w = 0; w < porFila; w++)
                    {
                        float off = (w - (porFila - 1) * 0.5f) * 3.4f;
                        var p = centro + eje * off + normal * 0.03f;
                        bool prendida = rnd.NextDouble() < 0.16;
                        float aW = bunker ? 1.4f : 1.2f, aH = bunker ? 0.35f : 1.15f;
                        Decor(cuerpo, "Marco", p.x, p.z, aW + 0.28f, aH + 0.26f, 0.07f, marco, yaw, y0 - 0.13f);
                        var v = p + normal * 0.045f;
                        Decor(cuerpo, prendida ? "Ventana" : "Ventana_Apagada", v.x, v.z, aW, aH, 0.06f, prendida ? Ventana : VentanaApagada, yaw, y0);
                        if (!bunker)
                        {   // montantes en cruz: la ventana deja de ser un panel liso
                            var mv = p + normal * 0.09f;
                            Decor(cuerpo, "Montante_V", mv.x, mv.z, 0.07f, aH, 0.04f, marco, yaw, y0);
                            Decor(cuerpo, "Montante_H", mv.x, mv.z, aW, 0.07f, 0.04f, marco, yaw, y0 + aH * 0.5f);
                        }
                        var al = p + normal * 0.1f;
                        if (!bunker) Decor(cuerpo, "Alfeizar", al.x, al.z, aW + 0.5f, 0.1f, 0.2f, marco, yaw, y0 - 0.2f);
                    }
                }
                // una puerta en la cara mas ancha (la primera con largo >= 6)
                if (ci == 2 && largo >= 6f && !bunker && !n.StartsWith("Garita_"))
                {
                    var p = centro + normal * 0.05f;
                    Decor(cuerpo, "Puerta_Marco", p.x, p.z, 1.9f, 2.55f, 0.1f, marco, yaw, 0f);
                    var pp = centro + normal * 0.1f;
                    Decor(cuerpo, "Puerta", pp.x, pp.z, 1.5f, 2.35f, 0.08f, Puerta, yaw, 0f);
                    var vis = centro + normal * 0.13f;
                    Decor(cuerpo, "Puerta_Manija", vis.x + eje.x * 0.45f, vis.z + eje.z * 0.45f, 0.1f, 0.1f, 0.1f, MetalClaro, yaw, 1.1f);
                    var luz = centro + normal * 0.35f;
                    Decor(cuerpo, "Luz_Puerta", luz.x, luz.z, 0.35f, 0.2f, 0.25f, Lampara, yaw, 2.8f);
                }
            }
            // techo: parapeto y equipos
            if (s.y >= 4f && s.x >= 6f && s.z >= 6f && !bunker)
            {
                float yT = s.y;
                var c = e.position; c.y = 0f;
                Decor(cuerpo, "Parapeto_N", c.x, c.z, 0f + s.x, 0.4f, 0.3f, Hormigon, e.eulerAngles.y, yT).transform.position = c + e.forward * (s.z * 0.5f - 0.15f) + Vector3.up * (yT + 0.2f);
                Decor(cuerpo, "Parapeto_S", c.x, c.z, s.x, 0.4f, 0.3f, Hormigon, e.eulerAngles.y, yT).transform.position = c - e.forward * (s.z * 0.5f - 0.15f) + Vector3.up * (yT + 0.2f);
                Decor(cuerpo, "Parapeto_E", c.x, c.z, 0.3f, 0.4f, s.z, Hormigon, e.eulerAngles.y, yT).transform.position = c + e.right * (s.x * 0.5f - 0.15f) + Vector3.up * (yT + 0.2f);
                Decor(cuerpo, "Parapeto_O", c.x, c.z, 0.3f, 0.4f, s.z, Hormigon, e.eulerAngles.y, yT).transform.position = c - e.right * (s.x * 0.5f - 0.15f) + Vector3.up * (yT + 0.2f);
                var p1 = c + e.right * R(-s.x * 0.25f, s.x * 0.25f) + e.forward * R(-s.z * 0.25f, s.z * 0.25f);
                Decor(cuerpo, "Equipo_Aire", p1.x, p1.z, 1.7f, 0.9f, 1.2f, MetalClaro, e.eulerAngles.y, yT);
                Decor(cuerpo, "Equipo_Aire_Ventilador", p1.x, p1.z, 1.0f, 0.1f, 1.0f, Negro, e.eulerAngles.y, yT + 0.9f);
                var p2 = c + e.right * R(-s.x * 0.3f, s.x * 0.3f) - e.forward * R(0f, s.z * 0.3f);
                Decor(cuerpo, "Tanque_Agua", p2.x, p2.z, 1.4f, 1.6f, 1.4f, MetalOsc, 0f, yT);
                Decor(cuerpo, "Tanque_Agua_Tapa", p2.x, p2.z, 1.5f, 0.12f, 1.5f, MetalClaro, 0f, yT + 1.6f);
                if (rnd.NextDouble() < 0.7)
                {
                    var p3 = c + e.right * (s.x * 0.35f) + e.forward * (s.z * 0.3f);
                    Decor(cuerpo, "Antena", p3.x, p3.z, 0.08f, 5f, 0.08f, MetalOsc, 0f, yT);
                    Decor(cuerpo, "Antena_Travesaño", p3.x, p3.z, 1.2f, 0.06f, 0.06f, MetalOsc, 0f, yT + 4.2f);
                }
            }
            return true;
        }
    }
}
