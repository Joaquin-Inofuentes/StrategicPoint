var l = (System.Collections.Generic.List<float>)System.AppDomain.CurrentDomain.GetData("zz195"); if (l == null || l.Count == 0) return "sin datos";
float max = 0, sum = 0; foreach (var x in l) { if (x > max) max = x; sum += x; }
return "tiros=" + l.Count + " cono medio=" + (sum / l.Count).ToString("0.00") + " deg, maximo=" + max.ToString("0.00") + " deg (a 30 m: radio de dispersion medio " + (30f * UnityEngine.Mathf.Tan(sum / l.Count * UnityEngine.Mathf.Deg2Rad)).ToString("0.0") + " m)";
