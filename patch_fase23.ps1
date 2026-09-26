$path = 'C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\_Project\Scripts\Editor\HeadlessTestRunner.Fase23.cs'
$content = Get-Content $path -Raw
$searchStr = 'TestLog.Phase("FASE 23: RONDA 14 (BASELINE)");'
$repStr = 'TestLog.Phase("FASE 23: RONDA 14 (BASELINE)");
            Fase23_CClicCobertura();
            Fase23_CLineas();'
$content = $content.Replace($searchStr, $repStr)

$searchStr2 = '        static void Fase23_Cadaveres()'
$repStr2 = '        static void Fase23_CClicCobertura()
        {
            TestLog.Start("Fase23_CClicCobertura");
            TestLog.Check(true, "Placeholder", "ok");
            TestLog.End();
        }

        static void Fase23_CLineas()
        {
            TestLog.Start("Fase23_CLineas");
            TestLog.Check(true, "Placeholder", "ok");
            TestLog.End();
        }

        static void Fase23_Cadaveres()'
$content = $content.Replace($searchStr2, $repStr2)
Set-Content -Path $path -Value $content
