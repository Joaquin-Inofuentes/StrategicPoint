$path = 'C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\_Project\Scripts\Presentation\AttackLineManager.cs'
$content = Get-Content $path -Raw
$searchStr = 'bool visible = false; // Could check visibility'
$repStr = 'bool visible = false;
                if (CameraRig.Instance != null && CameraRig.Instance.Cam != null) {
                    var vp = CameraRig.Instance.Cam.WorldToViewportPoint(s.transform.position + Vector3.up);
                    visible = vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1;
                }'
$content = $content.Replace($searchStr, $repStr)
Set-Content -Path $path -Value $content
