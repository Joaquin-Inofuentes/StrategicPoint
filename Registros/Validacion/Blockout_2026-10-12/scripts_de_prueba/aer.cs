var go=new UnityEngine.GameObject("ZzTmpCam"); var c=go.AddComponent<UnityEngine.Camera>();
c.fieldOfView=50f; c.farClipPlane=600f; c.clearFlags=UnityEngine.CameraClearFlags.SolidColor; c.backgroundColor=new UnityEngine.Color(0.15f,0.17f,0.2f);
c.transform.position=new UnityEngine.Vector3(0f,HH,0f); c.transform.rotation=UnityEngine.Quaternion.Euler(90f,0f,0f);
var rt=new UnityEngine.RenderTexture(1600,900,24); c.targetTexture=rt; c.Render();
UnityEngine.RenderTexture.active=rt; var t=new UnityEngine.Texture2D(1600,900,UnityEngine.TextureFormat.RGB24,false); t.ReadPixels(new UnityEngine.Rect(0,0,1600,900),0,0); t.Apply();
System.IO.File.WriteAllBytes("PATH",t.EncodeToPNG());
UnityEngine.RenderTexture.active=null; c.targetTexture=null; rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(t); UnityEngine.Object.DestroyImmediate(go);
return "ok cams="+UnityEngine.Camera.allCamerasCount;
