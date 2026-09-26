$path = 'C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\_Project\Scripts\Player\OrdenesDeEscuadra.cs'
$content = Get-Content $path -Raw
$searchStr = 'public static int Quietos(IEnumerable<Soldier> soldados)'
$repStr = 'public static bool CoberturaManual(Soldier elegido, Vector3 punto, Collider dueno)
        {
            if (elegido == null) return false;
            return OrderService.IssueCoverOrder(elegido, punto, dueno);
        }

        public static int Quietos(IEnumerable<Soldier> soldados)'
$content = $content.Replace($searchStr, $repStr)
Set-Content -Path $path -Value $content
